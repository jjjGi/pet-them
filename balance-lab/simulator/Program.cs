using System.Globalization;
using System.Text.Json;
using PetThem.Combat;

var json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = false };
try
{
    string configPath = Value("--config") ?? "game/Assets/Resources/balance-default.json";
    string output = Path.GetFullPath(Value("--output") ?? "experiments/smoke");
    int count = int.Parse(Value("--runs") ?? "5", CultureInfo.InvariantCulture);
    int seed = int.Parse(Value("--seed") ?? "42", CultureInfo.InvariantCulture);
    if (count < 1 || count > 1000) throw new ArgumentException("--runs must be between 1 and 1000.");
    var config = JsonSerializer.Deserialize<BalanceConfig>(File.ReadAllText(configPath), json)
        ?? throw new ArgumentException("Empty config.");
    config.Validate();
    if (config.duration > 3600) throw new ArgumentException("Simulation duration limit is 3600 seconds.");
    Directory.CreateDirectory(output);
    var results = new List<object>();
    for (int run = 0; run < count; run++)
    {
        int runSeed = checked(seed + run);
        var world = new CombatWorld(config, runSeed);
        string id = "orbit-bot-" + runSeed + "-" + Guid.NewGuid().ToString("N")[..8];
        string file = Path.Combine(output, id + ".jsonl");
        using var writer = new StreamWriter(file, false, new System.Text.UTF8Encoding(false));
        writer.WriteLine(JsonSerializer.Serialize(new { type = "run_start", schemaVersion = "1",
            source = "simulation", policy = "orbit-auto-punch-v1", runId = id, seed = runSeed,
            fixedStep = CombatWorld.StepSeconds, buildVersion = "0.1.0",
            configJson = JsonSerializer.Serialize(config, json) }, json));
        while (world.State == RunState.Playing)
        {
            // A reproducible smoke policy, not an estimate of human skill.
            float angle = world.Time * 0.32f;
            var target = new Vec2((float)Math.Cos(angle) * config.arenaHalfWidth * .58f,
                (float)Math.Sin(angle) * config.arenaHalfHeight * .58f);
            world.Step(new PlayerInput((target - world.Position).Normalized, new Vec2(), true));
            foreach (CombatEvent e in world.Events) writer.WriteLine(JsonSerializer.Serialize(e, json));
        }
        results.Add(new { seed = runSeed, state = world.State.ToString(), seconds = world.Time,
            kills = world.Kills, health = world.Health, log = Path.GetFileName(file) });
    }
    string summary = Path.Combine(output, "summary-" + Guid.NewGuid().ToString("N")[..8] + ".json");
    File.WriteAllText(summary, JsonSerializer.Serialize(new {
        schemaVersion = "1", source = "simulation", policy = "orbit-auto-punch-v1",
        limitation = "Synthetic movement and auto-punch. Does not measure mobile input, fatigue, or fun.",
        config = config, results
    }, new JsonSerializerOptions(json) { WriteIndented = true }));
    Console.WriteLine(summary);
}
catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is JsonException ||
                           ex is OverflowException || ex is FormatException)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

string? Value(string flag)
{
    int index = Array.IndexOf(args, flag);
    if (index < 0) return null;
    if (index + 1 >= args.Length) throw new ArgumentException("Missing value for " + flag);
    return args[index + 1];
}
