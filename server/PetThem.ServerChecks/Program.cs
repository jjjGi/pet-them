using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PetThem.Combat;
using PetThem.Server;

// Starts the real server on a port the OS picks and talks to it over real HTTP. Calling the
// handlers directly would prove the handlers work; this proves the server does -- routing, model
// binding, status codes and the Authorization header included.
//
// No test framework, for the same reason tools/CoreChecks has none: one fewer thing to install
// before the checks can be run, and the output is a list anyone can read.

int passed = 0;
var failures = new List<string>();

// Quiet: a request log line per call would bury the list of checks under a few hundred lines.
WebApplication app = ServerHost.Build([], services =>
    services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning)));
app.Urls.Add("http://127.0.0.1:0");
await app.StartAsync();

string baseUrl = app.Services.GetRequiredService<IServer>()
    .Features.Get<IServerAddressesFeature>()!.Addresses.First();
using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

await Check("the server answers before anyone has signed in", async () =>
{
    HttpResponseMessage health = await http.GetAsync("/health");
    True(health.StatusCode == HttpStatusCode.OK, $"health returned {health.StatusCode}");
});

await Check("a profile needs a token, and only a real one", async () =>
{
    HttpResponseMessage none = await http.GetAsync("/v1/profile");
    True(none.StatusCode == HttpStatusCode.Unauthorized, $"no header returned {none.StatusCode}");

    using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/profile");
    request.Headers.Add("Authorization", "Bearer not-a-real-token");
    HttpResponseMessage made_up = await http.SendAsync(request);
    True(made_up.StatusCode == HttpStatusCode.Unauthorized, $"invented token returned {made_up.StatusCode}");
});

await Check("a new player starts with no coins and only the starter pet", async () =>
{
    string token = await NewSession();
    Contracts.ProfileResponse profile = await Profile(token);
    True(profile.Coins == 0, $"coins were {profile.Coins}");
    True(profile.RunsFinished == 0, $"runsFinished was {profile.RunsFinished}");
    True(profile.UnlockedPets.Length == 1, $"had {profile.UnlockedPets.Length} pets");
    True(profile.UnlockedPets[0] == PlayerProfile.StarterPet.ToString(), "starter pet missing");
});

await Check("two sessions are two different players", async () =>
{
    string first = await NewSession();
    string second = await NewSession();
    True(first != second, "two sessions shared a token");

    await SubmitRun(first, Run("only-mine", kills: 40));
    True((await Profile(first)).Coins > 0, "the run did not pay the player who sent it");
    True((await Profile(second)).Coins == 0, "a run paid a player who did not send it");
});

await Check("the server prices the run, not the client", async () =>
{
    string token = await NewSession();
    var config = new BalanceConfig();
    Contracts.RunSubmission run = Run("priced", kills: 50, seconds: 90, bossDefeated: true);

    Contracts.RunReceipt receipt = await SubmitRun(token, run);
    // The same function the game called to show the player their coins, on the run's own twisted
    // numbers -- which the server got from the seed rather than from the client.
    var world = new CombatWorld(config, run.Seed, true,
        Enum.Parse<WeaponId>(run.Weapon), Enum.Parse<PetId>(run.Pet), enableTwists: true);
    int expected = CombatWorld.CoinsFor(world.GetConfig(), run.Kills, run.Seconds, run.BossDefeated);

    True(receipt.CoinsAwarded == expected, $"awarded {receipt.CoinsAwarded}, expected {expected}");
    True(receipt.Twist == world.Twist.ToString(), $"twist was {receipt.Twist}, expected {world.Twist}");
    True((await Profile(token)).Coins == expected, "the profile did not match the receipt");
});

await Check("the same run is never paid twice", async () =>
{
    string token = await NewSession();
    Contracts.RunSubmission run = Run("sent-three-times", kills: 30);

    Contracts.RunReceipt first = await SubmitRun(token, run);
    True(!first.AlreadyCounted, "the first upload was treated as a repeat");
    True(first.CoinsAwarded > 0, "the first upload paid nothing");

    // A phone that loses signal mid-upload sends the same run again, and may do it more than once.
    for (int i = 0; i < 2; i++)
    {
        Contracts.RunReceipt again = await SubmitRun(token, run);
        True(again.AlreadyCounted, "a repeat was not reported as already counted");
        True(again.CoinsAwarded == 0, $"a repeat paid {again.CoinsAwarded} coins");
    }

    Contracts.ProfileResponse profile = await Profile(token);
    True(profile.Coins == first.CoinsAwarded, $"coins ended at {profile.Coins}, not {first.CoinsAwarded}");
    True(profile.RunsFinished == 1, $"runsFinished ended at {profile.RunsFinished}");
});

await Check("nonsense runs are refused before anything is paid", async () =>
{
    string token = await NewSession();
    var duration = new BalanceConfig().duration;

    await Refused(token, Run("neg", kills: -5), "bad_kills");
    await Refused(token, Run("slow", seconds: duration + ServerHost.SecondsSlack + 1), "bad_seconds");
    await Refused(token, Run("huge", seconds: 1e30f), "bad_seconds");
    await Refused(token, Run("gun") with { Weapon = "Shotgun" }, "unknown_weapon");
    await Refused(token, Run("dog") with { Pet = "Rex" }, "unknown_pet");
    await Refused(token, Run("") with { RunId = "" }, "bad_run_id");

    // NaN and infinity have no JSON spelling, so a client using a normal serializer cannot send
    // them at all -- this check could not even build the request through the typed path. Sent as
    // raw text instead, which is the only way it could ever arrive. The server's own guard against
    // them stays: it costs a line and does not depend on the binder refusing first.
    string nan = $$"""
        {"runId":"nan","seed":1,"weapon":"Punch","pet":"Mochi","kills":1,"seconds":NaN,"bossDefeated":false}
        """;
    using (var raw = new HttpRequestMessage(HttpMethod.Post, "/v1/runs"))
    {
        raw.Headers.Add("Authorization", "Bearer " + token);
        raw.Content = new StringContent(nan, System.Text.Encoding.UTF8, "application/json");
        HttpResponseMessage refused = await http.SendAsync(raw);
        True(refused.StatusCode == HttpStatusCode.BadRequest,
            $"a body containing NaN returned {refused.StatusCode}");
    }

    True((await Profile(token)).Coins == 0, "a refused run still paid out");
});

await Check("a pet costs exactly its price and cannot be bought twice", async () =>
{
    var config = new BalanceConfig();
    int price = PlayerProfile.PriceOf(PetId.Bori, config);
    string token = await NewSession();

    HttpResponseMessage broke = await Unlock(token, "Bori");
    True(broke.StatusCode == HttpStatusCode.PaymentRequired, $"broke player got {broke.StatusCode}");

    // Enough runs to afford it, each one its own upload.
    for (int i = 0; (await Profile(token)).Coins < price; i++)
    {
        await SubmitRun(token, Run("earning-" + i, kills: 120, seconds: 150));
        True(i < 40, "could not afford the pet after 40 runs");
    }

    int before = (await Profile(token)).Coins;
    HttpResponseMessage bought = await Unlock(token, "Bori");
    True(bought.StatusCode == HttpStatusCode.OK, $"buying returned {bought.StatusCode}");

    Contracts.ProfileResponse after = await Profile(token);
    True(after.Coins == before - price, $"paid {before - after.Coins}, expected {price}");
    True(after.UnlockedPets.Contains("Bori"), "Bori was paid for but not unlocked");

    HttpResponseMessage twice = await Unlock(token, "Bori");
    True(twice.StatusCode == HttpStatusCode.Conflict, $"buying twice returned {twice.StatusCode}");
    True((await Profile(token)).Coins == after.Coins, "buying twice charged the player again");
});

await Check("the starter pet is not for sale and unknown pets are refused", async () =>
{
    string token = await NewSession();
    HttpResponseMessage starter = await Unlock(token, PlayerProfile.StarterPet.ToString());
    True(starter.StatusCode == HttpStatusCode.Conflict, $"starter returned {starter.StatusCode}");
    HttpResponseMessage nonsense = await Unlock(token, "Rex");
    True(nonsense.StatusCode == HttpStatusCode.BadRequest, $"unknown pet returned {nonsense.StatusCode}");
});

await app.StopAsync();

if (failures.Count > 0)
{
    Console.WriteLine();
    foreach (string failure in failures) Console.WriteLine("FAIL " + failure);
    Console.WriteLine($"{failures.Count} server checks failed.");
    return 1;
}
Console.WriteLine($"{passed} server checks passed.");
return 0;

async Task Check(string name, Func<Task> run)
{
    int before = failures.Count;
    try { await run(); }
    catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); }
    if (failures.Count == before) { passed++; Console.WriteLine("PASS " + name); }
    else Console.WriteLine("FAIL " + name);
}

void True(bool condition, string detail)
{
    if (!condition) throw new Exception(detail);
}

async Task<string> NewSession()
{
    HttpResponseMessage response = await http.PostAsync("/v1/session", null);
    response.EnsureSuccessStatusCode();
    return (await Read<Contracts.SessionResponse>(response)).Token;
}

async Task<Contracts.ProfileResponse> Profile(string token)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/profile");
    request.Headers.Add("Authorization", "Bearer " + token);
    HttpResponseMessage response = await http.SendAsync(request);
    response.EnsureSuccessStatusCode();
    return await Read<Contracts.ProfileResponse>(response);
}

async Task<HttpResponseMessage> Post(string token, string path, object body)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, path);
    request.Headers.Add("Authorization", "Bearer " + token);
    request.Content = JsonContent.Create(body);
    return await http.SendAsync(request);
}

async Task<Contracts.RunReceipt> SubmitRun(string token, Contracts.RunSubmission run)
{
    HttpResponseMessage response = await Post(token, "/v1/runs", run);
    response.EnsureSuccessStatusCode();
    return await Read<Contracts.RunReceipt>(response);
}

async Task Refused(string token, Contracts.RunSubmission run, string error)
{
    HttpResponseMessage response = await Post(token, "/v1/runs", run);
    True(response.StatusCode == HttpStatusCode.BadRequest,
        $"{error}: expected 400, got {response.StatusCode}");
    Contracts.Problem problem = await Read<Contracts.Problem>(response);
    True(problem.Error == error, $"expected error '{error}', got '{problem.Error}'");
}

Task<HttpResponseMessage> Unlock(string token, string pet) =>
    Post(token, "/v1/shop/unlock", new Contracts.UnlockRequest(pet));

static Contracts.RunSubmission Run(string id, int kills = 20, float seconds = 60,
    bool bossDefeated = false, int seed = 4242) =>
    new(id, seed, WeaponId.Punch.ToString(), PetId.Mochi.ToString(), kills, seconds, bossDefeated);

static async Task<T> Read<T>(HttpResponseMessage response)
{
    string body = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<T>(body,
               new JsonSerializerOptions(JsonSerializerDefaults.Web))
           ?? throw new Exception($"could not read {typeof(T).Name} from: {body}");
}
