using System.Text.Json;
using PetThem.Combat;

int passed = 0;
Check("reject invalid config and non-finite input", () =>
{
    Throws(() => new CombatWorld(new BalanceConfig { punchCooldown = 0 }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { punchKnockback = float.NaN }, 1));
    var world = new CombatWorld(new BalanceConfig(), 1);
    Throws(() => world.Step(new PlayerInput(new Vec2(float.NaN, 0), default, false)));
});
Check("movement normalizes diagonal input and respects arena bounds", () =>
{
    var world = Safe();
    world.Step(new PlayerInput(new Vec2(1,1), default, false));
    Near(world.Position.Length, world.GetConfig().playerSpeed * CombatWorld.StepSeconds);
    for (int i = 0; i < 3000; i++) world.Step(new PlayerInput(new Vec2(1,1), default, false));
    True(world.Position.x <= world.GetConfig().arenaHalfWidth - .45f + .001f);
    True(world.Position.y <= world.GetConfig().arenaHalfHeight - .45f + .001f);
});
Check("config is snapshotted and accessor cannot mutate world", () =>
{
    var config = new BalanceConfig { playerHealth = 100000, duration = 1 };
    var world = new CombatWorld(config,1);
    config.duration = 0.01f;
    var copy = world.GetConfig(); copy.duration = .01f;
    world.Step(default);
    True(world.State == RunState.Playing);
    Near(world.GetConfig().duration,1);
});
Check("same seed and inputs reproduce events and state", () =>
{
    var a = Safe(); var b = Safe();
    var options = new JsonSerializerOptions { IncludeFields = true };
    for (int i = 0; i < 10800; i++)
    {
        var input = new PlayerInput(new Vec2((float)Math.Sin(i * .01), (float)Math.Cos(i * .01)),default,i % 5 == 0);
        a.Step(input); b.Step(input);
        True(JsonSerializer.Serialize(a.Events, options) == JsonSerializer.Serialize(b.Events, options));
    }
    Near(a.Health,b.Health); True(a.Kills == b.Kills && a.State == b.State);
});
Check("different seeds vary spawn positions", () =>
{
    var a = new CombatWorld(new BalanceConfig(),1);
    var b = new CombatWorld(new BalanceConfig(),2);
    a.Step(default); b.Step(default);
    True((a.Enemies[0].Position - b.Enemies[0].Position).Length > .001f);
});
Check("punch cooldown limits rapid inputs", () =>
{
    var config = new BalanceConfig { duration = 2, playerHealth = 100000, punchCooldown = .5f };
    var world = new CombatWorld(config,4);
    var ticks = new List<int>();
    for (int i = 0; i < 120; i++)
    {
        world.Step(new PlayerInput(default,default,true));
        foreach (var e in world.Events) if (e.type == "attack" && e.source == "punch") ticks.Add(e.tick);
    }
    True(ticks.SequenceEqual(new[] {1,31,61,91}));
});
Check("pet attacks automatically and records only effective damage", () =>
{
    var world = new CombatWorld(new BalanceConfig { petRange = 100, petDamage = 1000 }, 1);
    world.Step(default);
    True(world.Kills == 1 && world.Enemies.Count == 0);
    var spawn = world.Events.Single(e => e.type == "spawn");
    var damage = world.Events.Single(e => e.type == "damage");
    Near(spawn.value,damage.value);
    True(damage.source == "pet");
});
Check("punch direction excludes targets behind the player", () =>
{
    var config = new BalanceConfig { punchRange = 100, punchDamage = 1000, petRange = .001f };
    var world = new CombatWorld(config,1);
    world.Step(default);
    var direction = world.Enemies[0].Position.Normalized;
    world.Step(new PlayerInput(default,direction * -1,true));
    True(world.Kills == 0);
    for (int i = 0; i < 30; i++) world.Step(default);
    world.Step(new PlayerInput(default,direction,true));
    True(world.Kills == 1);
});
Check("victory occurs after exactly 180 seconds and terminal events do not repeat", () =>
{
    var world = Safe();
    for (int i = 0; i < 10799; i++) world.Step(default);
    True(world.State == RunState.Playing);
    world.Step(default);
    True(world.State == RunState.Won && world.Tick == 10800);
    True(world.Events.Count(e => e.type == "run_end") == 1);
    world.Step(default);
    True(world.Tick == 10800 && world.Events.Count == 0);
});
Check("contact damage causes one loss event and health never goes negative", () =>
{
    var world = new CombatWorld(new BalanceConfig { playerHealth = 1, petRange = .001f }, 1);
    while (world.State == RunState.Playing) world.Step(default);
    True(world.State == RunState.Lost && world.Health == 0);
    True(world.Events.Count(e => e.type == "run_end") == 1);
});
Check("abandon is distinct from death", () =>
{
    var world = Safe(); world.Step(default); world.Abandon();
    True(world.State == RunState.Abandoned && world.Events.Single().source == "abandoned");
    world.Abandon(); True(world.Events.Count == 0);
});
Check("enemy count stays within cap", () =>
{
    var world = new CombatWorld(new BalanceConfig { maxEnemies = 3, playerHealth = 100000,
        petRange = .001f, spawnInterval = .01f, minSpawnInterval = .01f },1);
    for (int i = 0; i < 1000; i++) { world.Step(default); True(world.Enemies.Count <= 3); }
});
Check("XP from kills offers three unique upgrades and freezes the combat clock", () =>
{
    var world = ProgressionWorld();
    UntilChoice(world);
    True(world.Kills == 5 && world.Experience == 5 && world.Level == 1);
    True(world.UpgradeChoices.Count == 3 && world.UpgradeChoices.Select(c => c.Id).Distinct().Count() == 3);
    int tick = world.Tick; float hp = world.Health; int kills = world.Kills;
    world.Step(new PlayerInput(new Vec2(1,0), default, true));
    True(world.Tick == tick && world.Kills == kills); Near(world.Health, hp);
    True(!world.ChooseUpgrade((UpgradeId)999));
    True(world.HasUpgradeChoice && world.Level == 1);
    var selected = world.UpgradeChoices[0].Id;
    True(world.ChooseUpgrade(selected));
    True(world.Level == 2 && world.Experience == 0 && world.UpgradeRank(selected) == 1);
    True(world.Events.Count(e => e.type == "upgrade") == 1 && world.Events.Count(e => e.type == "level_up") == 1);
    True(!world.ChooseUpgrade(selected));
    world.Step(default); True(world.Tick == tick + 1);
});
Check("progression rolls and chosen upgrades reproduce with the same seed", () =>
{
    var a = ProgressionWorld(); var b = ProgressionWorld();
    var json = new JsonSerializerOptions { IncludeFields = true };
    for (int i = 0; i < 1400; i++)
    {
        a.Step(default); b.Step(default);
        True(JsonSerializer.Serialize(a.Events, json) == JsonSerializer.Serialize(b.Events, json));
        if (a.HasUpgradeChoice)
        {
            True(a.UpgradeChoices.Select(c => c.Id).SequenceEqual(b.UpgradeChoices.Select(c => c.Id)));
            a.ChooseUpgrade(a.UpgradeChoices[0].Id); b.ChooseUpgrade(b.UpgradeChoices[0].Id);
            True(JsonSerializer.Serialize(a.Events, json) == JsonSerializer.Serialize(b.Events, json));
        }
    }
    True(a.Level > 2); Near(a.GetConfig().punchDamage, b.GetConfig().punchDamage);
});
Check("capped upgrades stay bounded and a fresh run resets growth", () =>
{
    var world = ProgressionWorld();
    for (int i = 0; i < 10000 && world.Level < CombatWorld.MaxLevel; i++)
    {
        world.Step(default);
        if (!world.HasUpgradeChoice) continue;
        var choice = world.UpgradeChoices.FirstOrDefault(c => c.Id == UpgradeId.PetHaste)
            ?? world.UpgradeChoices.FirstOrDefault(c => c.Id == UpgradeId.PunchReach)
            ?? world.UpgradeChoices[0];
        world.ChooseUpgrade(choice.Id);
        True(world.GetConfig().petCooldown >= .005f - .00001f);
        True(world.UpgradeRank(UpgradeId.PetHaste) <= 5 && world.UpgradeRank(UpgradeId.PunchReach) <= 5);
    }
    True(world.Level == CombatWorld.MaxLevel && !world.HasUpgradeChoice);
    True(world.UpgradeRank(UpgradeId.PetHaste) == 5);
    var fresh = ProgressionWorld();
    True(fresh.Level == 1 && fresh.Experience == 0 && !fresh.HasUpgradeChoice);
    Near(fresh.GetConfig().petCooldown, .01f);
    foreach (UpgradeId id in Enum.GetValues<UpgradeId>()) True(fresh.UpgradeRank(id) == 0);
});
Check("growth modifies this run only and health upgrades respect maximum health", () =>
{
    var config = new BalanceConfig { petRange = 100, petDamage = 1000, petCooldown = .01f,
        spawnInterval = .01f, minSpawnInterval = .01f };
    var world = new CombatWorld(config, 6, true);
    while (world.Level < 8)
    {
        UntilChoice(world);
        var choice = world.UpgradeChoices.FirstOrDefault(c => c.Id == UpgradeId.Vitality) ?? world.UpgradeChoices[0];
        world.ChooseUpgrade(choice.Id);
        True(world.Health <= world.MaxHealth);
    }
    True(world.UpgradeRank(UpgradeId.Vitality) > 0);
    Near(world.MaxHealth, config.playerHealth * (1 + .2f * world.UpgradeRank(UpgradeId.Vitality)));
    Near(config.playerHealth, 100); Near(config.petDamage, 1000);
    world.Abandon();
    True(!world.ChooseUpgrade(UpgradeId.Vitality));
});
Check("multi-kill XP overflow is kept for successive upgrade choices", () =>
{
    var config = new BalanceConfig { playerHealth = 100000, petRange = .001f,
        punchRange = 100, punchDamage = 1000, spawnInterval = .01f, minSpawnInterval = .01f };
    var world = new CombatWorld(config, 6, true);
    for (int i = 0; i < 180; i++) world.Step(default);
    for (int i = 0; i < 200 && !world.HasUpgradeChoice; i++) world.Step(new PlayerInput(default, default, true));
    True(world.HasUpgradeChoice && world.Experience > 13);
    int earned = world.Experience;
    world.ChooseUpgrade(world.UpgradeChoices[0].Id);
    True(world.Experience == earned - 5 && world.HasUpgradeChoice);
    world.ChooseUpgrade(world.UpgradeChoices[0].Id);
    True(world.Experience == earned - 13 && world.Level == 3);
});
Check("the arrow flies, pierces a limited number of enemies, and respects its cooldown", () =>
{
    var config = new BalanceConfig { playerHealth = 100000, petRange = .001f, arrowPierce = 2,
        arrowDamage = 1000, spawnInterval = .01f, minSpawnInterval = .01f };
    var world = new CombatWorld(config, 42, false, WeaponId.Arrow);
    for (int i = 0; i < 600; i++) world.Step(default);
    True(world.Enemies.Count > 4);

    world.Step(new PlayerInput(default, default, true));
    True(Count(world, "attack", "arrow") == 1);
    True(world.WeaponBusy);
    // A dense crowd means the arrow can already have struck on the step it was fired.
    int damaged = Count(world, "damage", "arrow");

    // The cooldown holds even when the fire button is held down every step.
    for (int i = 0; i < 40; i++)
    {
        world.Step(new PlayerInput(default, default, true));
        True(Count(world, "attack", "arrow") == 0);
        damaged += Count(world, "damage", "arrow");
    }
    // One arrow, so it may damage at most arrowPierce enemies before it is spent.
    True(damaged >= 1 && damaged <= config.arrowPierce);
    True(world.Projectiles.Count == 0);
});
Check("an arrow fires once on release and damages each enemy only once", () =>
{
    // Few enemies and a very slow arrow, so it sits inside one enemy for many steps.
    var config = new BalanceConfig { playerHealth = 100000, petRange = .001f, arrowPierce = 20,
        arrowDamage = 1, arrowSpeed = .5f, arrowCooldown = 60,
        spawnInterval = 2, minSpawnInterval = 2 };
    var world = new CombatWorld(config, 7, false, WeaponId.Arrow);
    for (int i = 0; i < 600; i++) world.Step(default);

    // Holding only aims. The shot leaves on release.
    world.Step(new PlayerInput(default, default, false, true));
    True(Count(world, "attack", "arrow") == 0);
    world.Step(new PlayerInput(default, default, false, false));
    True(Count(world, "attack", "arrow") == 1);
    True(world.Projectiles.Count == 1);

    var hits = new Dictionary<int, int>();
    for (int i = 0; i < 900 && world.Projectiles.Count > 0; i++)
    {
        world.Step(default);
        foreach (CombatEvent e in world.Events)
            if (e.type == "damage" && e.source == "arrow")
                hits[e.targetId] = hits.TryGetValue(e.targetId, out int n) ? n + 1 : 1;
    }
    True(hits.Count >= 1);
    foreach (int count in hits.Values) True(count == 1);
});
Check("the laser burns only what is in front of it and locks out when it overheats", () =>
{
    var config = new BalanceConfig { playerHealth = 100000, petRange = .001f,
        laserDamagePerSecond = 600, laserRange = 6, laserWidth = .6f,
        laserHeatPerSecond = 50, laserCoolPerSecond = 50,
        spawnInterval = .01f, minSpawnInterval = .01f };
    var world = new CombatWorld(config, 11, false, WeaponId.Laser);
    for (int i = 0; i < 900; i++) world.Step(default);
    True(!world.Overheated && world.Heat == 0);

    var beam = new Vec2(1, 0);
    int steps = 0;
    while (!world.Overheated && steps++ < 600) world.Step(new PlayerInput(default, beam, false, true));
    True(world.Overheated);
    Near(world.Heat, 100);

    // Everything the beam damaged was ahead of the player, never behind.
    var world2 = new CombatWorld(config, 11, false, WeaponId.Laser);
    for (int i = 0; i < 900; i++) world2.Step(default);
    for (int i = 0; i < 60; i++)
    {
        world2.Step(new PlayerInput(default, beam, false, true));
        foreach (CombatEvent e in world2.Events)
            if (e.type == "damage" && e.source == "laser")
                True(e.x >= world2.Position.x - .001f && (e.x - world2.Position.x) <= config.laserRange + 1);
    }

    // While overheated the beam does nothing, however hard the button is held.
    int killsAtOverheat = world.Kills;
    for (int i = 0; i < 30; i++) world.Step(new PlayerInput(default, beam, false, true));
    True(world.Kills == killsAtOverheat && world.LaserActive == false);

    // It only returns once the heat is fully gone, not the moment the button is let go.
    for (int i = 0; i < 600 && world.Overheated; i++) world.Step(default);
    True(!world.Overheated && world.Heat <= 0);
    world.Step(new PlayerInput(default, beam, false, true));
    True(world.LaserActive);
});
Check("upgrades offered match the weapon the run started with", () =>
{
    foreach (WeaponId weapon in new[] { WeaponId.Punch, WeaponId.Arrow, WeaponId.Laser })
    {
        var world = new CombatWorld(new BalanceConfig { playerHealth = 100000, petDamage = 1000,
            petRange = 100, petCooldown = .01f, spawnInterval = .01f, minSpawnInterval = .01f },
            42, true, weapon);
        True(world.Weapon == weapon);
        for (int level = 0; level < 6; level++)
        {
            UntilChoice(world);
            True(world.UpgradeChoices.Count == 3);
            foreach (UpgradeChoice choice in world.UpgradeChoices)
            {
                True(world.AppliesToWeapon(choice.Id));
                True(!OtherWeaponUpgrade(choice.Id, weapon));
            }
            world.ChooseUpgrade(world.UpgradeChoices[0].Id);
        }
    }
});
Check("weapon damage scales with its own upgrade and the run's config is not shared", () =>
{
    var config = new BalanceConfig();
    var arrow = new CombatWorld(config, 42, true, WeaponId.Arrow);
    var laser = new CombatWorld(config, 42, true, WeaponId.Laser);
    Near(arrow.GetConfig().arrowDamage, config.arrowDamage);
    Near(laser.GetConfig().laserDamagePerSecond, config.laserDamagePerSecond);

    Throws(() => new CombatWorld(new BalanceConfig { arrowPierce = 0 }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { laserRange = 0 }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { arrowSpeed = float.NaN }, 1));
});
Console.WriteLine(passed + " checks passed.");
return;

int Count(CombatWorld world, string type, string source)
{
    int total = 0;
    foreach (CombatEvent e in world.Events) if (e.type == type && e.source == source) total++;
    return total;
}

bool OtherWeaponUpgrade(UpgradeId id, WeaponId weapon)
{
    bool punch = id == UpgradeId.PunchPower || id == UpgradeId.PunchReach;
    bool arrow = id == UpgradeId.ArrowPower || id == UpgradeId.ArrowPierce;
    bool laser = id == UpgradeId.LaserPower || id == UpgradeId.LaserCooling;
    return (punch && weapon != WeaponId.Punch)
        || (arrow && weapon != WeaponId.Arrow)
        || (laser && weapon != WeaponId.Laser);
}

CombatWorld Safe() => new CombatWorld(new BalanceConfig { playerHealth = 100000 },42);
CombatWorld ProgressionWorld() => new CombatWorld(new BalanceConfig { playerHealth = 100000,
    petDamage = 1000, petRange = 100, petCooldown = .01f, spawnInterval = .01f, minSpawnInterval = .01f }, 42, true);
void UntilChoice(CombatWorld world)
{
    for (int i = 0; i < 1200 && !world.HasUpgradeChoice && world.State == RunState.Playing; i++) world.Step(default);
    True(world.HasUpgradeChoice);
}
void Check(string name, Action run) { run(); passed++; Console.WriteLine("PASS " + name); }
void True(bool condition) { if (!condition) throw new Exception("Assertion failed."); }
void Near(float a,float b) { if (Math.Abs(a-b) > .001f) throw new Exception(a + " != " + b); }
void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected argument failure."); }
