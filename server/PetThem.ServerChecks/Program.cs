using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PetThem.Combat;
using PetThem.Game;
using PetThem.Server;

// Starts the real server on a port the OS picks and talks to it over real HTTP. Calling the
// handlers directly would prove the handlers work; this proves the server does -- routing, model
// binding, status codes and the Authorization header included.
//
// No test framework, for the same reason tools/CoreChecks has none: one fewer thing to install
// before the checks can be run, and the output is a list anyone can read.

// BalanceConfig is public fields, so reading it needs saying so. Kept here rather than borrowed
// from the server, because the server getting this wrong is one of the things being checked.
var ReadsFields = new JsonSerializerOptions { IncludeFields = true };

int passed = 0;
var failures = new List<string>();

// A database of its own, thrown away at the end. The checks run against the store the server
// actually ships with, not a stand-in: transactions and the primary key that makes a run pay once
// are the things worth checking, and a fake would have neither.
string databaseFile = Path.Combine(Path.GetTempPath(), $"petthem-checks-{Guid.NewGuid():N}.db");

// Quiet: a request log line per call would bury the list of checks under a few hundred lines.
WebApplication app = ServerHost.Build(["--Database=" + databaseFile], services =>
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
await Check("the server is running the balance the game ships, not the code defaults", async () =>
{
    // This is the check that was missing, and its absence hid a real one. LoadBalance read the
    // shipped file with System.Text.Json, which ignores public fields unless told otherwise, so
    // it quietly returned every default and the file might as well not have existed. Every check
    // below built its expectation with new BalanceConfig() too, so both sides were wrong the same
    // way and agreed with each other.
    //
    // The fix is not enough on its own: what stops it coming back is asking the running server
    // which numbers it has and holding that against the file on disk.
    // Its own options, deliberately not ServerHost.BalanceJson: a check that shares the thing it
    // is testing fails for the wrong reason and stops describing the fault.
    string shipped = File.ReadAllText(Path.Combine(RepoRoot(), "game/Assets/Resources/balance-default.json"));
    var onDisk = JsonSerializer.Deserialize<BalanceConfig>(shipped, ReadsFields)!;
    True(onDisk.version != new BalanceConfig().version,
        "the shipped file matches the code defaults, so this check proves nothing");

    HttpResponseMessage health = await http.GetAsync("/health");
    string body = await health.Content.ReadAsStringAsync();
    True(body.Contains(onDisk.version),
        $"the server reports {body}, and the shipped balance is {onDisk.version}");
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
    // Read from the shipped file rather than constructed from the defaults. Building the
    // expectation the same way the server builds its own is how a shared mistake goes unseen.
    var config = JsonSerializer.Deserialize<BalanceConfig>(
        File.ReadAllText(Path.Combine(RepoRoot(), "game/Assets/Resources/balance-default.json")),
        ReadsFields)!;
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

await Check("what the client queues is what the server accepts", async () =>
{
    // The game cannot reference this server project, so the submission shape exists on both sides.
    // This is the only thing standing between them and a silent drift -- a renamed field would
    // deserialize to a default here and pay the wrong amount, with nothing failing to say so.
    var queued = new PendingRun
    {
        runId = "contract-" + Guid.NewGuid().ToString("N")[..8],
        seed = 909, weapon = WeaponId.Arrow.ToString(), pet = PetId.Coco.ToString(),
        kills = 61, seconds = 123.5f, bossDefeated = true,
        // Bookkeeping the client keeps for itself. The server has no business seeing it.
        attempts = 3, nextAttemptAt = 1_700_000_000,
    };

    // Unity writes this with JsonUtility, which serializes public *fields* under their own
    // names. System.Text.Json ignores fields unless told to, so IncludeFields is what makes this
    // produce the body the game will actually send rather than an empty object -- which is the
    // first thing this check caught.
    string body = JsonSerializer.Serialize(queued,
        new JsonSerializerOptions(JsonSerializerDefaults.Web) { IncludeFields = true });
    True(body.Contains("\"runId\""), $"the client body has no runId: {body}");
    var parsed = JsonSerializer.Deserialize<Contracts.RunSubmission>(body,
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    True(parsed.RunId == queued.runId, $"runId came over as '{parsed.RunId}'");
    True(parsed.Seed == queued.seed, $"seed came over as {parsed.Seed}");
    True(parsed.Weapon == queued.weapon, $"weapon came over as '{parsed.Weapon}'");
    True(parsed.Pet == queued.pet, $"pet came over as '{parsed.Pet}'");
    True(parsed.Kills == queued.kills, $"kills came over as {parsed.Kills}");
    True(Math.Abs(parsed.Seconds - queued.seconds) < 0.001f, $"seconds came over as {parsed.Seconds}");
    True(parsed.BossDefeated == queued.bossDefeated, "bossDefeated did not survive");

    // And the real server takes that same body over the wire.
    string token = await NewSession();
    using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/runs");
    request.Headers.Add("Authorization", "Bearer " + token);
    request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
    HttpResponseMessage response = await http.SendAsync(request);
    True(response.StatusCode == HttpStatusCode.OK, $"the server answered {response.StatusCode}");

    Contracts.RunReceipt receipt = await Read<Contracts.RunReceipt>(response);
    True(receipt.CoinsAwarded > 0, "a real run paid nothing");
    True(!receipt.AlreadyCounted, "a first upload was treated as a repeat");
});

await Check("a profile outlives the process that wrote it", async () =>
{
    // The reason this store exists. Everything above would pass against a dictionary.
    string token = await NewSession();
    await SubmitRun(token, Run("survives-a-restart", kills: 44, seconds: 100));
    Contracts.ProfileResponse before = await Profile(token);
    True(before.Coins > 0, "the run paid nothing");

    // A separate store over the same file, which is what a restarted server is.
    var reopened = new SqliteProfileStore(databaseFile);
    string? playerId = reopened.PlayerFor(token);
    True(playerId != null, "the session did not survive");
    PlayerProfile after = reopened.Load(playerId!);
    True(after.coins == before.Coins, $"coins came back as {after.coins}, not {before.Coins}");
    True(after.runsFinished == before.RunsFinished, "runsFinished did not survive");

    // And the run stays counted, so a client still holding it in its outbox is not paid twice
    // just because the server was restarted between attempts.
    RunOutcome repeat = reopened.ApplyRun(playerId!, "survives-a-restart", 999);
    True(repeat.AlreadyCounted, "a restarted server paid a run it had already paid");
    True(repeat.CoinsAwarded == 0, $"a restarted server paid {repeat.CoinsAwarded} again");
});
await Check("the same run arriving at once is still paid once", async () =>
{
    // A lock made this true before. Now a primary key does, and the difference matters: the code
    // no longer asks "has this been counted?" and then counts it, with a gap in between.
    string token = await NewSession();
    Contracts.RunSubmission run = Run("all-at-once", kills: 35, seconds: 80);

    Contracts.RunReceipt[] receipts = await Task.WhenAll(
        Enumerable.Range(0, 8).Select(_ => SubmitRun(token, run)));

    int paid = receipts.Count(r => !r.AlreadyCounted);
    True(paid == 1, $"{paid} of 8 simultaneous uploads were treated as the first");
    int total = receipts.Sum(r => r.CoinsAwarded);
    Contracts.ProfileResponse profile = await Profile(token);
    True(profile.Coins == total, $"coins ended at {profile.Coins}, receipts totalled {total}");
    True(profile.RunsFinished == 1, $"runsFinished ended at {profile.RunsFinished}");
});
await Check("a database from a newer server is refused rather than misread", async () =>
{
    // Running an old server against a new file would read columns it does not understand and
    // write back whatever it made of them. Better to stop.
    string file = Path.Combine(Path.GetTempPath(), $"petthem-future-{Guid.NewGuid():N}.db");
    _ = new SqliteProfileStore(file);
    using (var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + file))
    {
        await connection.OpenAsync();
        using Microsoft.Data.Sqlite.SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE schema_version SET version = $v";
        command.Parameters.AddWithValue("$v", SqliteProfileStore.SchemaVersion + 1);
        await command.ExecuteNonQueryAsync();
    }

    bool refused = false;
    try { _ = new SqliteProfileStore(file); }
    catch (InvalidOperationException) { refused = true; }
    True(refused, "an older server opened a database written by a newer one");

    // And opening the same file twice in a row is fine, which is every restart.
    string ordinary = Path.Combine(Path.GetTempPath(), $"petthem-twice-{Guid.NewGuid():N}.db");
    _ = new SqliteProfileStore(ordinary);
    _ = new SqliteProfileStore(ordinary);
});

await Check("a run that sends its inputs is paid for what the replay did, not what it claimed", async () =>
{
    // The point of the whole thing. The client sends what it did; the server plays it and works
    // out the score itself, so the claim stops being something to trust or refuse.
    string token = await NewSession();
    Contracts.RunSubmission honest = Replayable("honest-" + Guid.NewGuid().ToString("N")[..6]);

    Contracts.RunReceipt receipt = await SubmitRun(token, honest);
    True(receipt.Replayed, $"the run was not replayed: {receipt.ReplayRefusal}");
    True(receipt.CoinsAwarded > 0, "a replayed run paid nothing");
    True(receipt.Verified, $"an honest run was not verified (replay said {receipt.ReplayedKills})");

    // The same inputs, with the kill count inflated. The payout must not move.
    string token2 = await NewSession();
    Contracts.RunSubmission liar = honest with
    {
        RunId = "liar-" + Guid.NewGuid().ToString("N")[..6],
        Kills = honest.Kills + 5000,
    };
    Contracts.RunReceipt lied = await SubmitRun(token2, liar);
    True(lied.Replayed, "the inflated run was not replayed");
    True(lied.CoinsAwarded == receipt.CoinsAwarded,
        $"inflating the kills moved the payout from {receipt.CoinsAwarded} to {lied.CoinsAwarded}");
    True(!lied.Verified, "a claim five thousand kills out was reported as verified");
    True(lied.ReplayedKills == receipt.ReplayedKills, "the replay itself changed");
});
await Check("a replay the world could not have had is refused", async () =>
{
    string token = await NewSession();
    Contracts.RunSubmission good = Replayable("shapes");

    // An empty stream is not a broken replay, it is a client that did not send one -- Unity
    // JsonUtility cannot write a null array, so [] is what an old client sends. It is paid on its
    // word, exactly as before, and the receipt says it was not replayed.
    Contracts.RunReceipt none = await SubmitRun(token,
        good with { RunId = "s0", Inputs = Array.Empty<Contracts.InputSegment>() });
    True(!none.Replayed, "an empty stream was treated as a replay");
    True(none.CoinsAwarded > 0, "a client that sent no replay was not paid");

    // An empty segment, a segment claiming more steps than a run has, and a card that was never
    // dealt. Each is a different way of not describing a real run.
    await RefusedRun(token, good with { RunId = "s2",
        Inputs = new[] { new Contracts.InputSegment(0, 0, 0, 0, 0, false, false, 0) } }, "bad_replay");
    await RefusedRun(token, good with { RunId = "s3",
        Inputs = new[] { new Contracts.InputSegment(999_999, 0, 0, 0, 0, false, false, 0) } }, "bad_replay");
    await RefusedRun(token, good with { RunId = "s4",
        Picks = new[] { new Contracts.Pick(0, "NoSuchUpgrade") } }, "bad_replay");

    // s0 was paid on purpose; nothing after it was.
    True((await Profile(token)).Coins == none.CoinsAwarded, "a refused replay paid out");
});
await Check("a tape the game recorded is one the server can replay", async () =>
{
    // The end of the whole chain, and the part neither side can prove alone. The game records
    // with RunTape, serialises with Unity's field-based shape, and the server has to be able to
    // play that back and arrive where the player did. Both halves are compiled in here, so a
    // rename on either side fails this rather than quietly paying the wrong amount.
    var config = JsonSerializer.Deserialize<BalanceConfig>(
        File.ReadAllText(Path.Combine(RepoRoot(), "game/Assets/Resources/balance-default.json")),
        ReadsFields)!;
    const int seed = 606061;
    var world = new CombatWorld(config, seed, true, WeaponId.Punch, PetId.Mochi, enableTwists: true);
    var tape = new RunTape();
    tape.Begin();

    for (int tick = 0; tick < 3600 && world.State == RunState.Playing; tick++)
    {
        if (world.HasUpgradeChoice)
        {
            UpgradeId chosen = world.UpgradeChoices[0].Id;
            world.ChooseUpgrade(chosen);
            tape.Chose(chosen);
        }
        var input = new PlayerInput(new Vec2(1, 0), new Vec2(1, 0), tick % 19 == 0, false, 0);
        tape.Step(input);
        world.Step(input);
    }
    True(tape.Usable, "the tape gave up on an ordinary run");
    True(tape.SegmentCount < RunTape.MaxSegments,
        $"a 3,600 step run used {tape.SegmentCount} segments");

    // Serialised the way Unity writes it -- public fields, their own names -- and read by the
    // server's own contract. This is where a mismatch between the two shapes shows up.
    var queued = new PendingRun
    {
        runId = "taped-" + Guid.NewGuid().ToString("N")[..6],
        seed = seed, weapon = WeaponId.Punch.ToString(), pet = PetId.Mochi.ToString(),
        kills = world.Kills, seconds = world.Time, bossDefeated = world.BossDefeated,
        inputs = tape.Inputs(), picks = tape.Picks(),
    };
    string body = JsonSerializer.Serialize(queued,
        new JsonSerializerOptions(JsonSerializerDefaults.Web) { IncludeFields = true });
    var parsed = JsonSerializer.Deserialize<Contracts.RunSubmission>(body,
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    True(parsed.Inputs != null && parsed.Inputs.Length == tape.SegmentCount,
        "the inputs did not survive the wire");
    True(parsed.Picks != null && parsed.Picks.Length == tape.Picks().Length,
        "the picks did not survive the wire");

    string token = await NewSession();
    using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/runs");
    request.Headers.Add("Authorization", "Bearer " + token);
    request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
    HttpResponseMessage response = await http.SendAsync(request);
    True(response.StatusCode == HttpStatusCode.OK,
        $"the server answered {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    Contracts.RunReceipt receipt = await Read<Contracts.RunReceipt>(response);
    True(receipt.Replayed, $"a recorded tape was not replayed: {receipt.ReplayRefusal}");
    True(receipt.Verified, $"the game's own run did not verify: {world.Kills} vs {receipt.ReplayedKills}");
    True(receipt.CoinsAwarded > 0, "a replayed run paid nothing");
});
await Check("a run that levels up needs its cards, and the re-rolls that came first", async () =>
{
    // Step does nothing while cards are on the table, so a replay with no answer for the level-up
    // screen stops there. Without the picks it would report the fight it managed before the first
    // level and be paid for that.
    string token = await NewSession();
    Contracts.RunSubmission levelling = Replayable("levels", steps: 5400);

    await RefusedRun(token, levelling with { RunId = "no-picks", Picks = Array.Empty<Contracts.Pick>() },
        "bad_replay");

    // A re-roll moves the cards that follow, so replaying the choice without it deals a hand the
    // chosen card is not in.
    Contracts.RunReceipt withPicks = await SubmitRun(token, levelling);
    True(withPicks.Replayed, $"the levelling run was not replayed: {withPicks.ReplayRefusal}");
    True(withPicks.ReplayedKills > 0, "a run long enough to level up killed nothing");
});

await app.StopAsync();
// Pooled connections keep the files open, and Windows will not delete a file that is.
Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
foreach (string leftover in Directory.GetFiles(Path.GetTempPath(), "petthem-*.db*"))
    try { File.Delete(leftover); } catch (IOException) { /* temp files; not worth failing over. */ }

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
    // The body, not just the status. EnsureSuccessStatusCode throws away the one thing the server
    // sent to explain itself, and a failing check that does not say why is most of a wasted run.
    if (!response.IsSuccessStatusCode)
        throw new Exception($"{run.RunId}: {(int)response.StatusCode} " +
            await response.Content.ReadAsStringAsync());
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

// A run with its inputs attached, built by playing it here so the segments describe a fight the
// server can actually reproduce. The picks are taken as they are offered, which is what a player
// pressing the first card would produce.
Contracts.RunSubmission Replayable(string runId, int steps = 1800)
{
    var config = JsonSerializer.Deserialize<BalanceConfig>(
        File.ReadAllText(Path.Combine(RepoRoot(), "game/Assets/Resources/balance-default.json")),
        ReadsFields)!;
    const int seed = 515151;
    var world = new CombatWorld(config, seed, true, WeaponId.Punch, PetId.Mochi, enableTwists: true);
    var picks = new List<Contracts.Pick>();
    // One segment per step is wasteful on the wire and exactly right here: it keeps the check
    // about replaying rather than about run-length encoding.
    var segments = new List<Contracts.InputSegment>();
    for (int tick = 0; tick < steps && world.State == RunState.Playing; tick++)
    {
        if (world.HasUpgradeChoice)
        {
            picks.Add(new Contracts.Pick(0, world.UpgradeChoices[0].Id.ToString()));
            world.ChooseUpgrade(world.UpgradeChoices[0].Id);
        }
        bool punch = tick % 19 == 0;
        // Merged the way a client would, rather than one segment per step: the first attempt sent
        // 5,400 of them and was refused for it, which is the bound doing its job.
        if (segments.Count > 0 && segments[^1].Punch == punch)
            segments[^1] = segments[^1] with { Steps = segments[^1].Steps + 1 };
        else
            segments.Add(new Contracts.InputSegment(1, 1, 0, 1, 0, punch, false, 0));
        world.Step(new PlayerInput(new Vec2(1, 0), new Vec2(1, 0), punch, false, 0));
    }
    return new Contracts.RunSubmission(runId, seed, WeaponId.Punch.ToString(), PetId.Mochi.ToString(),
        world.Kills, world.Time, world.BossDefeated, segments.ToArray(), picks.ToArray());
}

async Task RefusedRun(string token, Contracts.RunSubmission run, string error)
{
    HttpResponseMessage response = await Post(token, "/v1/runs", run);
    True(response.StatusCode == HttpStatusCode.BadRequest,
        $"{run.RunId}: expected 400, got {response.StatusCode}");
    Contracts.Problem problem = await Read<Contracts.Problem>(response);
    True(problem.Error == error, $"{run.RunId}: expected '{error}', got '{problem.Error}'");
}

// Walks up to the folder holding both the game and the server.
static string RepoRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "game/Assets")))
        directory = directory.Parent;
    if (directory == null) throw new Exception("Could not find the repository root.");
    return directory.FullName;
}

static async Task<T> Read<T>(HttpResponseMessage response)
{
    string body = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<T>(body,
               new JsonSerializerOptions(JsonSerializerDefaults.Web))
           ?? throw new Exception($"could not read {typeof(T).Name} from: {body}");
}
