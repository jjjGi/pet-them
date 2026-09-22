using PetThem.Combat;

namespace PetThem.Server;

/// <summary>
/// Builds the server. Separate from Program.cs so the checks can start the real thing on a
/// random port and talk to it over real HTTP, rather than calling the handlers directly and
/// proving only that the handlers work.
/// </summary>
public static class ServerHost
{
    /// <summary>
    /// The clock is three minutes; a run claiming much more than that did not happen. A little
    /// slack because the last step of a run lands after the duration, not on it.
    /// </summary>
    public const float SecondsSlack = 2f;

    public static WebApplication Build(string[] args, Action<IServiceCollection>? services = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        // A file by default, so restarting the server does not cost every player their coins.
        // Overridable so the checks can point at a database of their own.
        builder.Services.AddSingleton<IProfileStore>(_ => new SqliteProfileStore(
            builder.Configuration["Database"] ?? Path.Combine(AppContext.BaseDirectory, "petthem.db")));
        builder.Services.AddSingleton(LoadBalance);
        services?.Invoke(builder.Services);

        WebApplication app = builder.Build();
        MapRoutes(app);
        return app;
    }

    /// <summary>
    /// The defaults the game ships with, read from the game's own file.
    /// </summary>
    /// <remarks>
    /// The same balance-default.json Unity loads from Resources, so the server prices a run with
    /// the numbers the player actually played. Serving this file to the client instead of the
    /// other way round is the next step; reading it is what makes that possible.
    /// </remarks>
    private static BalanceConfig LoadBalance(IServiceProvider _)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "balance-default.json");
        BalanceConfig config = File.Exists(path)
            ? System.Text.Json.JsonSerializer.Deserialize<BalanceConfig>(File.ReadAllText(path))
              ?? throw new InvalidOperationException("balance-default.json did not parse.")
            : new BalanceConfig();
        config.Validate();
        return config;
    }

    private static void MapRoutes(WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapPost("/v1/session", (IProfileStore store) => Results.Ok(store.CreateGuest()));

        app.MapGet("/v1/profile", (HttpContext http, IProfileStore store) =>
            WithPlayer(http, store, playerId =>
                Results.Ok(Contracts.ProfileResponse.From(store.Load(playerId)))));

        app.MapPost("/v1/runs", (HttpContext http, IProfileStore store, BalanceConfig defaults,
            Contracts.RunSubmission run) => WithPlayer(http, store, playerId =>
        {
            if (Reject(run, defaults) is { } problem) return problem;

            // The twist rewrites the very numbers a payout is made of, and it is rolled from the
            // seed. Rebuilding the run's world from that seed gets the server the same twisted
            // config the player played, without trusting the client to report which twist it was.
            var world = new CombatWorld(defaults, run.Seed, true,
                Enum.Parse<WeaponId>(run.Weapon), Enum.Parse<PetId>(run.Pet), enableTwists: true);
            int coins = CombatWorld.CoinsFor(world.GetConfig(), run.Kills, run.Seconds, run.BossDefeated);

            RunOutcome outcome = store.ApplyRun(playerId, run.RunId, coins);
            return Results.Ok(new Contracts.RunReceipt(run.RunId, outcome.CoinsAwarded,
                outcome.AlreadyCounted, world.Twist.ToString(),
                Contracts.ProfileResponse.From(outcome.Profile)));
        }));

        app.MapPost("/v1/shop/unlock", (HttpContext http, IProfileStore store, BalanceConfig defaults,
            Contracts.UnlockRequest request) => WithPlayer(http, store, playerId =>
        {
            if (!Enum.TryParse(request.Pet, false, out PetId pet))
                return Fail(StatusCodes.Status400BadRequest, "unknown_pet", $"No such pet: {request.Pet}");

            // Priced from the defaults, not from a run's twisted copy: the shop is between runs
            // and has nothing to do with whatever condition the last fight was played under.
            UnlockOutcome outcome = store.ApplyUnlock(playerId, pet, defaults);
            return outcome.Result switch
            {
                UnlockResult.Bought => Results.Ok(Contracts.ProfileResponse.From(outcome.Profile)),
                UnlockResult.AlreadyOwned => Fail(StatusCodes.Status409Conflict, "already_owned",
                    $"{pet} is already unlocked."),
                UnlockResult.NotEnoughCoins => Fail(StatusCodes.Status402PaymentRequired, "not_enough_coins",
                    $"{pet} costs {outcome.Price}; the player has {outcome.Profile.coins}."),
                _ => Fail(StatusCodes.Status409Conflict, "not_for_sale", $"{pet} is not for sale."),
            };
        }));
    }

    /// <summary>
    /// What the server refuses to even price.
    /// </summary>
    /// <remarks>
    /// These are shape checks, not proof: a run that passes them is still only the client's word
    /// about how the fight went. They exist so that nonsense -- a negative kill count, an hour-long
    /// three-minute run, a weapon that does not exist -- never reaches the payout at all.
    /// The kill count is the one that is still taken on trust, and replay validation is what
    /// would close it.
    /// </remarks>
    private static IResult? Reject(Contracts.RunSubmission run, BalanceConfig defaults)
    {
        if (string.IsNullOrWhiteSpace(run.RunId) || run.RunId.Length > 96)
            return Fail(StatusCodes.Status400BadRequest, "bad_run_id", "runId must be 1 to 96 characters.");
        if (!Enum.TryParse(run.Weapon, false, out WeaponId _))
            return Fail(StatusCodes.Status400BadRequest, "unknown_weapon", $"No such weapon: {run.Weapon}");
        if (!Enum.TryParse(run.Pet, false, out PetId _))
            return Fail(StatusCodes.Status400BadRequest, "unknown_pet", $"No such pet: {run.Pet}");
        if (run.Kills < 0)
            return Fail(StatusCodes.Status400BadRequest, "bad_kills", "kills cannot be negative.");
        if (float.IsNaN(run.Seconds) || float.IsInfinity(run.Seconds) || run.Seconds < 0)
            return Fail(StatusCodes.Status400BadRequest, "bad_seconds", "seconds must be finite and not negative.");
        if (run.Seconds > defaults.duration + SecondsSlack)
            return Fail(StatusCodes.Status400BadRequest, "bad_seconds",
                $"a run cannot last longer than {defaults.duration + SecondsSlack:0} seconds.");
        return null;
    }

    private static IResult WithPlayer(HttpContext http, IProfileStore store, Func<string, IResult> run)
    {
        string? token = Bearer(http);
        if (token == null)
            return Fail(StatusCodes.Status401Unauthorized, "no_token", "Send an Authorization: Bearer header.");
        string? playerId = store.PlayerFor(token);
        if (playerId == null)
            return Fail(StatusCodes.Status401Unauthorized, "unknown_token", "That token is not a session.");
        return run(playerId);
    }

    private static string? Bearer(HttpContext http)
    {
        string? header = http.Request.Headers.Authorization;
        const string scheme = "Bearer ";
        if (header == null || !header.StartsWith(scheme, StringComparison.Ordinal)) return null;
        string token = header[scheme.Length..].Trim();
        return token.Length == 0 ? null : token;
    }

    private static IResult Fail(int status, string error, string detail) =>
        Results.Json(new Contracts.Problem(error, detail), statusCode: status);
}
