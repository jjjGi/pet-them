using System.Text.Json;
using PetThem.Combat;
using PetThem.Game;

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

    // Reaching the cap through the cards alone depends on the draw, so the cap itself is checked
    // directly: once an upgrade is at five it stops being offered.
    var capped = ProgressionWorld();
    for (int i = 0; i < 5; i++) True(capped.Apply(UpgradeId.PetHaste));
    True(capped.UpgradeRank(UpgradeId.PetHaste) == 5);
    for (int i = 0; i < 4000 && capped.Level < CombatWorld.MaxLevel; i++)
    {
        capped.Step(default);
        if (!capped.HasUpgradeChoice) continue;
        foreach (UpgradeChoice offer in capped.UpgradeChoices) True(offer.Id != UpgradeId.PetHaste);
        capped.ChooseUpgrade(capped.UpgradeChoices[0].Id);
    }
    True(capped.UpgradeRank(UpgradeId.PetHaste) == 5);

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
        world.ChooseUpgrade(world.UpgradeChoices[0].Id);
        True(world.Health <= world.MaxHealth);
    }
    // Applied directly: whether Vitality is ever offered depends on the draw, and the point here
    // is what it does to maximum health, not how often it comes up.
    True(world.Apply(UpgradeId.Vitality));
    True(world.Apply(UpgradeId.Vitality));
    True(world.Health <= world.MaxHealth);
    True(world.UpgradeRank(UpgradeId.Vitality) == 2);
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
Check("the boss arrives once, on time, even when the enemy cap is full", () =>
{
    var config = new BalanceConfig { playerHealth = 100000, petRange = .001f, maxEnemies = 1,
        bossSpawnTime = 2, spawnInterval = .01f, minSpawnInterval = .01f };
    var world = new CombatWorld(config, 42);
    for (int i = 0; i < 60; i++) world.Step(default);
    True(world.Boss == null && world.SecondsToBoss > 0);
    True(world.Enemies.Count == config.maxEnemies);

    int spawns = 0;
    for (int i = 0; i < 600; i++)
    {
        world.Step(default);
        spawns += Count(world, "boss_spawn", nameof(EnemyKind.Boss));
        // The cap applies to ordinary enemies. The boss is never starved out by a crowd.
        int ordinary = 0;
        foreach (Enemy e in world.Enemies) if (e.Kind != EnemyKind.Boss) ordinary++;
        True(ordinary <= config.maxEnemies);
        if (spawns > 0) True(world.Enemies.Contains(world.Boss));
    }
    True(spawns == 1);
    True(world.Boss != null && !world.BossDefeated);
    Near(world.SecondsToBoss, 0);
    Enemy boss = world.Boss!;
    True(boss.MaxHealth == config.bossHealth);
    True(boss.Radius == config.bossRadius);
    True(world.RadiusOf(EnemyKind.Boss) > world.RadiusOf(EnemyKind.Grunt));
    True(world.HealthOf(EnemyKind.Boss) > world.HealthOf(EnemyKind.Brute));
});
Check("killing the boss wins the run at once and nothing acts after it", () =>
{
    var config = new BalanceConfig { playerHealth = 100000, petRange = .001f,
        bossSpawnTime = 1, bossHealth = 50, punchDamage = 10000, punchRange = 30,
        punchCooldown = .02f, spawnInterval = .01f, minSpawnInterval = .01f };
    var world = new CombatWorld(config, 42);
    for (int i = 0; i < 90; i++) world.Step(default);
    True(world.Boss != null);

    int ends = 0;
    string reason = "";
    for (int i = 0; i < 300 && world.State == RunState.Playing; i++)
    {
        world.Step(new PlayerInput(default, default, true));
        foreach (CombatEvent e in world.Events)
            if (e.type == "run_end") { ends++; reason = e.source; }
    }
    True(world.State == RunState.Won);
    True(world.BossDefeated && world.Boss == null);
    True(ends == 1 && reason == "boss_down");
    True(world.Time < config.duration);

    // A finished run stays finished and emits nothing further.
    world.Step(new PlayerInput(default, default, true));
    True(world.Events.Count == 0 && world.State == RunState.Won);
});
Check("surviving the clock with the boss still up is a survival win, not a boss kill", () =>
{
    var world = new CombatWorld(new BalanceConfig { playerHealth = 100000, duration = 3,
        bossSpawnTime = 1, petRange = .001f }, 42);
    string reason = "";
    for (int i = 0; i < 600 && world.State == RunState.Playing; i++)
    {
        world.Step(default);
        foreach (CombatEvent e in world.Events) if (e.type == "run_end") reason = e.source;
    }
    True(world.State == RunState.Won);
    True(reason == "survived");
    True(!world.BossDefeated && world.Boss != null);
});
Check("brutes are slower, tougher and hit harder than chasers", () =>
{
    var config = new BalanceConfig();
    var world = new CombatWorld(config, 42);
    True(world.SpeedOf(EnemyKind.Brute) < world.SpeedOf(EnemyKind.Grunt));
    True(world.HealthOf(EnemyKind.Brute) > world.HealthOf(EnemyKind.Grunt));
    True(world.ContactDamageOf(EnemyKind.Brute) > world.ContactDamageOf(EnemyKind.Grunt));
    True(world.ContactDamageOf(EnemyKind.Boss) > world.ContactDamageOf(EnemyKind.Brute));

    // With a share configured, brutes actually turn up in a long run. The pet has to be lethal
    // here: with nothing killing the crowd the arena fills, spawning stops, and the run never
    // reaches the later waves that brutes are drawn from at all.
    var busy = new CombatWorld(new BalanceConfig { playerHealth = 100000,
        petDamage = 1000, petRange = 100, petCooldown = .01f, waveDuration = 5,
        spawnInterval = .05f, minSpawnInterval = .05f, bossSpawnTime = 1000 }, 42);
    int brutes = 0, runners = 0, grunts = 0;
    for (int i = 0; i < 3600; i++)
    {
        busy.Step(default);
        brutes += Count(busy, "spawn", nameof(EnemyKind.Brute));
        runners += Count(busy, "spawn", nameof(EnemyKind.Runner));
        grunts += Count(busy, "spawn", nameof(EnemyKind.Grunt));
    }
    True(brutes > 0 && runners > 0 && grunts > 0);
    True(grunts > brutes);

    // Setting the share to zero removes them entirely, at every wave.
    var none = new CombatWorld(new BalanceConfig { playerHealth = 100000,
        petDamage = 1000, petRange = 100, petCooldown = .01f, waveDuration = 5, bruteShare = 0,
        spawnInterval = .05f, minSpawnInterval = .05f, bossSpawnTime = 1000 }, 42);
    for (int i = 0; i < 3600; i++)
    { none.Step(default); True(Count(none, "spawn", nameof(EnemyKind.Brute)) == 0); }
});
Check("coins come from kills, the boss and time, and reset with the run", () =>
{
    var config = new BalanceConfig { playerHealth = 100000, petRange = .001f,
        duration = 4, bossSpawnTime = 1000, coinsPerKill = 3, coinsPerSecondSurvived = 2,
        coinsPerBossKill = 500, punchDamage = 10000, punchRange = 30, punchCooldown = .02f,
        spawnInterval = .05f, minSpawnInterval = .05f };
    var world = new CombatWorld(config, 42);
    True(world.Coins == 0);
    for (int i = 0; i < 600 && world.State == RunState.Playing; i++)
        world.Step(new PlayerInput(default, default, true));
    True(world.Kills > 0);
    True(world.Coins == (int)(world.Kills * 3 + world.Time * 2));
    True(!world.BossDefeated);

    // A fresh run starts from zero, so coins are per run and not carried in the world.
    var again = new CombatWorld(config, 42);
    True(again.Coins == 0 && again.Kills == 0 && !again.BossDefeated);

    Throws(() => new CombatWorld(new BalanceConfig { coinsPerKill = -1 }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { bossSpawnTime = 0 }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { bruteShare = -0.1f }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { bruteShare = 1 }, 1));
});
Check("each pet trades damage for its own effect", () =>
{
    var config = new BalanceConfig();
    var mochi = new CombatWorld(config, 42, false, WeaponId.Punch, PetId.Mochi);
    var bori = new CombatWorld(config, 42, false, WeaponId.Punch, PetId.Bori);
    var coco = new CombatWorld(config, 42, false, WeaponId.Punch, PetId.Coco);

    True(mochi.PetDamage > bori.PetDamage && bori.PetDamage > coco.PetDamage);
    Near(mochi.PetDamage, config.petDamage);
    True(mochi.GuardReduction == 0 && bori.GuardReduction == 0);
    Near(coco.GuardReduction, config.petGuardReduction);
    True(mochi.Pet == PetId.Mochi && coco.Pet == PetId.Coco);
});
Check("the control pet slows and shoves what it hits, and the slow wears off", () =>
{
    var config = new BalanceConfig { playerHealth = 100000, petDamage = .0001f, petRange = 40,
        petCooldown = .05f, petSlowSeconds = .5f, petSlowFactor = .2f,
        spawnInterval = .3f, minSpawnInterval = .3f, bossSpawnTime = 1000 };
    var bori = new CombatWorld(config, 42, false, WeaponId.Punch, PetId.Bori);
    int slows = 0;
    for (int i = 0; i < 300; i++) { bori.Step(default); slows += Count(bori, "slow", "pet"); }
    True(slows > 0);
    bool anySlowed = false;
    foreach (Enemy e in bori.Enemies) if (e.Slowed) anySlowed = true;
    True(anySlowed);

    // Mochi does the same damage but never slows anything.
    var mochi = new CombatWorld(config, 42, false, WeaponId.Punch, PetId.Mochi);
    for (int i = 0; i < 300; i++)
    { mochi.Step(default); True(Count(mochi, "slow", "pet") == 0); }
    foreach (Enemy e in mochi.Enemies) True(!e.Slowed);

    // A slowed enemy closes on the player more slowly than the same enemy would otherwise.
    var slowConfig = new BalanceConfig { playerHealth = 100000, petDamage = .0001f, petRange = 40,
        petCooldown = .05f, petSlowSeconds = 30, petSlowFactor = .2f, petControlKnockback = .0001f,
        spawnInterval = .3f, minSpawnInterval = .3f, bossSpawnTime = 1000 };
    var slowed = new CombatWorld(slowConfig, 7, false, WeaponId.Punch, PetId.Bori);
    var normal = new CombatWorld(slowConfig, 7, false, WeaponId.Punch, PetId.Mochi);
    for (int i = 0; i < 600; i++) { slowed.Step(default); normal.Step(default); }
    True(Spread(slowed) > Spread(normal));
});
Check("the support pet heals on its interval and softens contact damage", () =>
{
    var config = new BalanceConfig { playerHealth = 100, petRange = .0001f,
        petHealInterval = 1, petHealAmount = 9, petGuardReduction = .5f, contactDamage = 20,
        hurtCooldown = .2f, spawnInterval = .1f, minSpawnInterval = .1f, bossSpawnTime = 1000 };
    var coco = new CombatWorld(config, 42, false, WeaponId.Punch, PetId.Coco);

    // Full health, so there is nothing to heal and no heal event is written.
    for (int i = 0; i < 120; i++) { coco.Step(default); True(Count(coco, "heal", "pet") == 0); }

    float firstHurt = 0;
    for (int i = 0; i < 600 && firstHurt == 0; i++)
    {
        coco.Step(default);
        foreach (CombatEvent e in coco.Events) if (e.type == "hurt") firstHurt = e.value;
    }
    // Half the contact damage is blocked, so the recorded hurt is the reduced number.
    Near(firstHurt, config.contactDamage * (1 - config.petGuardReduction));

    int heals = 0;
    for (int i = 0; i < 300; i++) { coco.Step(default); heals += Count(coco, "heal", "pet"); }
    True(heals > 0);

    // The same fight with Mochi takes full contact damage and never heals.
    var mochi = new CombatWorld(config, 42, false, WeaponId.Punch, PetId.Mochi);
    float mochiHurt = 0;
    for (int i = 0; i < 600 && mochiHurt == 0; i++)
    {
        mochi.Step(default);
        True(Count(mochi, "heal", "pet") == 0);
        foreach (CombatEvent e in mochi.Events) if (e.type == "hurt") mochiHurt = e.value;
    }
    Near(mochiHurt, config.contactDamage);
    True(mochiHurt > firstHurt);
});
Check("upgrades offered match the pet as well as the weapon", () =>
{
    foreach (PetId pet in new[] { PetId.Mochi, PetId.Bori, PetId.Coco })
    {
        var world = new CombatWorld(new BalanceConfig { playerHealth = 100000, petDamage = 1000,
            petRange = 100, petCooldown = .01f, spawnInterval = .01f, minSpawnInterval = .01f,
            bossSpawnTime = 1000 }, 42, true, WeaponId.Arrow, pet);
        for (int level = 0; level < 8; level++)
        {
            UntilChoice(world);
            True(world.UpgradeChoices.Count == 3);
            foreach (UpgradeChoice choice in world.UpgradeChoices)
            {
                True(world.AppliesToWeapon(choice.Id));
                True(!OtherPetUpgrade(choice.Id, pet));
                True(!OtherWeaponUpgrade(choice.Id, WeaponId.Arrow));
            }
            world.ChooseUpgrade(world.UpgradeChoices[0].Id);
        }
    }
});
Check("bad pet values are rejected", () =>
{
    Throws(() => new CombatWorld(new BalanceConfig { petSlowFactor = 0 }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { petSlowFactor = 1.5f }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { petGuardReduction = -0.1f }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { petGuardReduction = 1 }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { petHealInterval = 0 }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { petControlDamageShare = 0 }, 1));
});
Check("buying a pet costs exactly its price and cannot go wrong halfway", () =>
{
    var config = new BalanceConfig();
    var profile = new PlayerProfile();
    True(profile.IsUnlocked(PetId.Mochi));
    True(!profile.IsUnlocked(PetId.Bori) && !profile.IsUnlocked(PetId.Coco));
    True(PlayerProfile.PriceOf(PetId.Mochi, config) == 0);

    // Too poor: nothing changes at all.
    profile.AddRunReward(PlayerProfile.PriceOf(PetId.Bori, config) - 1);
    int before = profile.coins;
    True(!profile.Unlock(PetId.Bori, config));
    True(profile.coins == before && !profile.IsUnlocked(PetId.Bori));

    profile.AddRunReward(1);
    True(profile.Unlock(PetId.Bori, config));
    True(profile.IsUnlocked(PetId.Bori));
    True(profile.coins == 0);

    // Buying the same pet twice takes no more coins.
    profile.AddRunReward(10000);
    int rich = profile.coins;
    True(!profile.Unlock(PetId.Bori, config));
    True(profile.coins == rich);

    // The starter pet is never for sale.
    True(!profile.Unlock(PetId.Mochi, config));
    True(profile.coins == rich);

    True(profile.Unlock(PetId.Coco, config));
    True(profile.coins == rich - PlayerProfile.PriceOf(PetId.Coco, config));

    // A run's coins are added, and a negative reward never removes any.
    int held = profile.coins;
    int runs = profile.runsFinished;
    profile.AddRunReward(-50);
    True(profile.coins == held && profile.runsFinished == runs);
    profile.AddRunReward(7);
    True(profile.coins == held + 7 && profile.runsFinished == runs + 1);
});
Check("a damaged save is repaired instead of breaking the game", () =>
{
    var clean = new PlayerProfile { coins = 12 };
    True(clean.Normalize().Count == 0);

    var broken = new PlayerProfile
    {
        schemaVersion = 99,
        coins = -500,
        runsFinished = -3,
        unlockedPets = new[] { "Bori", "Bori", "Dragon", "" },
    };
    var repairs = broken.Normalize();
    True(repairs.Count >= 4);
    True(broken.coins == 0 && broken.runsFinished == 0);
    True(broken.schemaVersion == PlayerProfile.CurrentSchemaVersion);
    // The starter pet comes back, the duplicate and the unknown name are gone, Bori is kept.
    True(broken.IsUnlocked(PetId.Mochi) && broken.IsUnlocked(PetId.Bori));
    True(broken.unlockedPets.Length == 2);

    var empty = new PlayerProfile { unlockedPets = null! };
    empty.Normalize();
    True(empty.IsUnlocked(PetId.Mochi) && empty.unlockedPets!.Length == 1);

    // Normalizing twice changes nothing the second time.
    var again = broken.Normalize();
    True(again.Count == 0);

    Throws(() => new CombatWorld(new BalanceConfig { boriPrice = 0 }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { cocoPrice = float.NaN }, 1));
});
Check("the bow has to be drawn, and a longer pull makes a stronger shot", () =>
{
    var config = new BalanceConfig { playerHealth = 100000, petRange = .001f, arrowPierce = 1,
        arrowMinDraw = .25f, arrowMinPower = .5f, bossSpawnTime = 1000,
        spawnInterval = .01f, minSpawnInterval = .01f };
    var world = new CombatWorld(config, 42, false, WeaponId.Arrow);

    // The gauge and the shot read the same function, so what is shown is what is fired.
    True(world.DrawPower(0) == 0);
    True(world.DrawPower(.24f) == 0);
    Near(world.DrawPower(.25f), .5f);
    Near(world.DrawPower(1), 1);
    Near(world.DrawPower(5), 1);          // clamped, not extrapolated
    True(world.DrawPower(.6f) > world.DrawPower(.3f));

    for (int i = 0; i < 300; i++) world.Step(default);

    // A tap, or any pull below the minimum, is not a shot at all.
    world.Step(new PlayerInput(default, default, false, true, .1f));
    world.Step(new PlayerInput(default, default, false, false, .1f));
    True(Count(world, "attack", "arrow") == 0);
    True(world.Projectiles.Count == 0);
    True(!world.WeaponBusy);

    // Clearing the minimum fires, and the recorded damage is the reduced one.
    world.Step(new PlayerInput(default, default, false, true, .25f));
    world.Step(new PlayerInput(default, default, false, false, .25f));
    float weak = 0;
    foreach (CombatEvent e in world.Events) if (e.type == "attack" && e.source == "arrow") weak = e.value;
    Near(weak, config.arrowDamage * .5f);

    // A full pull is worth the configured damage.
    var full = new CombatWorld(config, 42, false, WeaponId.Arrow);
    for (int i = 0; i < 300; i++) full.Step(default);
    full.Step(new PlayerInput(default, default, false, true, 1));
    full.Step(new PlayerInput(default, default, false, false, 1));
    float strong = 0;
    foreach (CombatEvent e in full.Events) if (e.type == "attack" && e.source == "arrow") strong = e.value;
    Near(strong, config.arrowDamage);
    True(strong > weak);

    // And it flies faster. Shot into an empty stretch of the arena so nothing eats the arrow
    // before its speed can be read.
    True(ArrowSpeed(1) > ArrowSpeed(.3f));

    // Callers that do not pull, like the bots, still shoot at full strength.
    var bot = new CombatWorld(config, 42, false, WeaponId.Arrow);
    for (int i = 0; i < 300; i++) bot.Step(default);
    bot.Step(new PlayerInput(default, default, true));
    True(Count(bot, "attack", "arrow") == 1);

    Throws(() => new CombatWorld(new BalanceConfig { arrowMinDraw = 1 }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { arrowMinDraw = -.1f }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { arrowMinPower = 0 }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { arrowMinPower = 1.5f }, 1));
});
Check("drones copy the weapon the run started with", () =>
{
    foreach (WeaponId weapon in new[] { WeaponId.Punch, WeaponId.Arrow, WeaponId.Laser })
    {
        var world = Grown(weapon, UpgradeId.Drone, 2);
        True(world.Drones.Count == 2);
        True(world.Orbs.Count == 0);

        int shots = 0, arrows = 0;
        for (int i = 0; i < 600; i++)
        {
            world.Step(default);
            shots += Count(world, "attack", "drone");
            arrows += world.Projectiles.Count;
        }
        True(shots > 0);
        // The arrow drone launches a real arrow; the other two hit at once.
        if (weapon == WeaponId.Arrow) True(arrows > 0); else True(arrows == 0);
    }

    // No drone upgrade, no drones, and nothing fires.
    var plain = Grown(WeaponId.Punch, UpgradeId.Vitality, 1);
    for (int i = 0; i < 300; i++)
    { plain.Step(default); True(Count(plain, "attack", "drone") == 0); }
    True(plain.Drones.Count == 0);
});
Check("orbs circle the player and cannot grind one enemy every step", () =>
{
    var world = Grown(WeaponId.Punch, UpgradeId.Orbit, 3);
    True(world.Orbs.Count == 3);

    var hits = new Dictionary<int, List<int>>();
    for (int i = 0; i < 900; i++)
    {
        world.Step(default);
        foreach (CombatEvent e in world.Events)
            if (e.type == "attack" && e.source == "orbit")
            {
                if (!hits.TryGetValue(e.targetId, out var ticks)) hits[e.targetId] = ticks = new List<int>();
                ticks.Add(e.tick);
            }
    }
    True(hits.Count > 0);
    int recovery = (int)(world.GetConfig().orbitRecovery * 60) - 1;
    foreach (var ticks in hits.Values)
        for (int i = 1; i < ticks.Count; i++)
            True(ticks[i] - ticks[i - 1] >= recovery);

    // They keep station around the player rather than drifting off.
    foreach (Orb orb in world.Orbs)
        True((orb.Position - world.Position).Length <= world.GetConfig().orbitRadius + .01f);
});
Check("triggers fire on the event they belong to, and a blast cannot chain", () =>
{
    // Lifesteal tops the player back up on a kill, but never past the maximum. Compared against
    // the identical run without it, because the player is taking damage the whole time too.
    var steal = Grown(WeaponId.Punch, UpgradeId.Lifesteal, 3, hurt: true);
    var noSteal = Grown(WeaponId.Punch, UpgradeId.Greed, 3, hurt: true);
    int heals = 0;
    for (int i = 0; i < 600; i++)
    {
        steal.Step(new PlayerInput(default, default, true));
        noSteal.Step(new PlayerInput(default, default, true));
        heals += Count(steal, "heal", "lifesteal");
        True(steal.Health <= steal.MaxHealth + .001f);
    }
    True(heals > 0);
    True(steal.Health > noSteal.Health);

    // A blast goes off once per kill and does not set off another blast.
    var blast = Grown(WeaponId.Punch, UpgradeId.Blast, 2);
    for (int i = 0; i < 600; i++)
    {
        blast.Step(new PlayerInput(default, default, true));
        int kills = Count(blast, "kill", "punch") + Count(blast, "kill", "pet")
            + Count(blast, "kill", "blast") + Count(blast, "kill", "drone");
        True(Count(blast, "attack", "blast") <= kills);
    }

    // Thorns hurt whatever touched the player. Run with health to spare: a hit that kills the
    // player does not sting back, and that is the intended behaviour, not something to count.
    var thorns = Grown(WeaponId.Punch, UpgradeId.Thorns, 2);
    int stung = 0, touched = 0;
    for (int i = 0; i < 900; i++)
    {
        thorns.Step(default);
        stung += Count(thorns, "attack", "thorns");
        foreach (CombatEvent e in thorns.Events) if (e.type == "hurt") touched++;
    }
    True(touched > 0 && stung == touched);
    True(thorns.State == RunState.Playing);
});
Check("a critical hit is rolled on its own stream and never changes the run around it", () =>
{
    var crit = Grown(WeaponId.Punch, UpgradeId.Crit, 3);
    int crits = 0, hits = 0;
    for (int i = 0; i < 900; i++)
    {
        crit.Step(new PlayerInput(default, default, true));
        foreach (CombatEvent e in crit.Events)
        {
            if (e.type != "damage") continue;
            hits++;
            if (e.source.EndsWith("_crit")) crits++;
        }
    }
    True(hits > 0 && crits > 0 && crits < hits);

    // Without the upgrade nothing crits at all.
    var plain = Grown(WeaponId.Punch, UpgradeId.Vitality, 1);
    for (int i = 0; i < 600; i++)
    {
        plain.Step(new PlayerInput(default, default, true));
        foreach (CombatEvent e in plain.Events)
            if (e.type == "damage") True(!e.source.EndsWith("_crit"));
    }

    // Same seed, same crits.
    var a = Grown(WeaponId.Punch, UpgradeId.Crit, 3);
    var b = Grown(WeaponId.Punch, UpgradeId.Crit, 3);
    var json = new JsonSerializerOptions { IncludeFields = true };
    for (int i = 0; i < 600; i++)
    {
        a.Step(new PlayerInput(default, default, true));
        b.Step(new PlayerInput(default, default, true));
        True(JsonSerializer.Serialize(a.Events, json) == JsonSerializer.Serialize(b.Events, json));
    }
});
Check("an effect that kills several enemies at once does not corrupt the loop that caused it", () =>
{
    // A blast removes enemies other than the one being damaged. Every loop that deals damage
    // while walking the enemy list has to survive that, for each weapon and for contact damage.
    foreach (WeaponId weapon in new[] { WeaponId.Punch, WeaponId.Arrow, WeaponId.Laser })
    {
        var config = new BalanceConfig
        {
            playerHealth = 400, petRange = 100, petDamage = 200, petCooldown = .05f,
            punchRange = 30, punchDamage = 200, punchCooldown = .05f,
            arrowDamage = 200, arrowCooldown = .05f, arrowPierce = 12,
            laserDamagePerSecond = 900, laserRange = 30, laserWidth = 8,
            laserHeatPerSecond = .01f, laserCoolPerSecond = 100,
            blastDamage = 400, blastRadius = 30,
            orbitDamage = 200, orbitHitRadius = 6, orbitRecovery = .05f,
            thornsDamage = 400, bossSpawnTime = 1000,
            spawnInterval = .02f, minSpawnInterval = .02f,
        };
        var world = new CombatWorld(config, 3, true, weapon);
        for (int i = 0; i < 400; i++) world.Step(default);
        foreach (UpgradeId id in new[] { UpgradeId.Blast, UpgradeId.Orbit, UpgradeId.Thorns,
                                         UpgradeId.Drone, UpgradeId.Crit })
            for (int rank = 0; rank < 3; rank++) True(world.Apply(id));

        // A wide blast on a packed arena: this is what used to run off the end of the list.
        for (int i = 0; i < 1800 && world.State == RunState.Playing; i++)
            world.Step(new PlayerInput(new Vec2(1, 0), new Vec2(1, 0), true, true));
        True(world.Kills > 0);
        foreach (Enemy enemy in world.Enemies) True(enemy.Health > 0);
    }
});
Check("every boss move is announced before it lands", () =>
{
    var config = new BalanceConfig
    {
        playerHealth = 100000, petRange = .001f, duration = 600,
        bossSpawnTime = 1, bossHealth = 1000000, bossMoveInterval = 1.2f, bossTelegraph = .4f,
        spawnInterval = 3, minSpawnInterval = 3,
    };
    var world = new CombatWorld(config, 5);
    for (int i = 0; i < 120; i++) world.Step(default);
    True(world.Boss != null);

    var announced = new List<string>();
    var performed = new List<string>();
    var announcedAt = new Dictionary<string, int>();
    int hurtWithoutWarning = 0;
    // A charge keeps travelling after it is unleashed, so its damage arrives well after the
    // announcement. What matters is that the warning came first and lasted its full length.
    int windUp = (int)(config.bossTelegraph * 60) - 1;

    for (int i = 0; i < 60 * 90; i++)
    {
        world.Step(default);
        foreach (CombatEvent e in world.Events)
        {
            if (e.type == "boss_telegraph") { announced.Add(e.source); announcedAt[e.source] = e.tick; }
            if (e.type == "boss_move") performed.Add(e.source);
            // Contact damage needs no warning; a move does.
            if (e.type != "hurt") continue;
            if (e.source != nameof(BossAction.Charge) && e.source != nameof(BossAction.Slam)) continue;
            if (!announcedAt.TryGetValue(e.source, out int at) || e.tick - at < windUp)
                hurtWithoutWarning++;
        }
    }

    True(announced.Count > 0 && performed.Count > 0);
    True(hurtWithoutWarning == 0);
    // All three moves turn up over ninety seconds, so none of them is unreachable.
    foreach (string move in new[] { nameof(BossAction.Charge), nameof(BossAction.Slam),
                                    nameof(BossAction.Summon) })
        True(performed.Contains(move));
    // Every move that landed was announced first.
    True(performed.Count <= announced.Count);
});
Check("a charge is aimed when it winds up, so stepping aside works", () =>
{
    var config = new BalanceConfig
    {
        playerHealth = 100000, petRange = .001f, duration = 600,
        bossSpawnTime = 1, bossHealth = 1000000, bossMoveInterval = 1.2f, bossTelegraph = .5f,
        bossSummonCount = 0, spawnInterval = 100, minSpawnInterval = 100,
    };
    var world = new CombatWorld(config, 9);
    for (int i = 0; i < 120; i++) world.Step(default);

    // Wait for a charge to be announced, then note where it was aimed.
    Vec2 aimed = default;
    bool found = false;
    for (int i = 0; i < 60 * 60 && !found; i++)
    {
        world.Step(default);
        foreach (CombatEvent e in world.Events)
            if (e.type == "boss_telegraph" && e.source == nameof(BossAction.Charge))
            { aimed = world.BossHeading; found = true; }
    }
    True(found);
    True(Math.Abs(aimed.Length - 1) < .01f);

    // The wind-up is visible the whole way through, and the boss holds still for it.
    True(world.BossState == BossAction.Telegraph);
    Vec2 stoodAt = world.Boss.Position;
    float lastProgress = -1;
    while (world.BossState == BossAction.Telegraph)
    {
        True(world.BossTelegraph >= lastProgress - .001f);
        lastProgress = world.BossTelegraph;
        True((world.Boss.Position - stoodAt).Length < .01f);
        world.Step(default);
    }
    True(lastProgress > .5f);

    // The direction does not follow the player once the charge is under way.
    True(world.BossState == BossAction.Charge);
    for (int i = 0; i < 20 && world.BossState == BossAction.Charge; i++)
    {
        world.Step(new PlayerInput(new Vec2(0, 1), default, false));
        True((world.BossHeading - aimed).Length < .001f);
    }
});
Check("the boss speeds up when hurt and its summons are counted as summons", () =>
{
    var config = new BalanceConfig
    {
        playerHealth = 100000, petRange = .001f, duration = 600,
        bossSpawnTime = 1, bossHealth = 400, bossEnrageHealth = .5f,
        punchDamage = 20, punchRange = 40, punchCooldown = .4f,
        bossSummonCount = 3, spawnInterval = 100, minSpawnInterval = 100,
    };
    var world = new CombatWorld(config, 4);
    for (int i = 0; i < 120; i++) world.Step(default);
    True(world.Boss != null && !world.BossEnraged);

    int summoned = 0;
    bool sawEnraged = false;
    for (int i = 0; i < 60 * 60 && world.State == RunState.Playing; i++)
    {
        world.Step(new PlayerInput(default, default, true));
        summoned += Count(world, "summon", nameof(EnemyKind.Runner));
        if (world.BossEnraged) sawEnraged = true;
    }
    True(sawEnraged);
    True(summoned > 0 && summoned % config.bossSummonCount == 0);
    // Killing it still ends the run, patterns or not.
    True(world.BossDefeated && world.State == RunState.Won);

    Throws(() => new CombatWorld(new BalanceConfig { bossTelegraph = 9, bossMoveInterval = 2 }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { bossSummonCount = -1 }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { bossEnrageHealth = 1 }, 1));
});
Check("regen and greed change the numbers they claim to", () =>
{
    // The player is being hit at the same time, so this compares against the identical run
    // without the upgrade rather than watching the health bar go up.
    var regen = Grown(WeaponId.Punch, UpgradeId.Regen, 2, hurt: true);
    var without = Grown(WeaponId.Punch, UpgradeId.Greed, 2, hurt: true);
    int healed = 0;
    for (int i = 0; i < 600; i++)
    {
        regen.Step(default);
        without.Step(default);
        healed += Count(regen, "heal", "regen");
        True(regen.Health <= regen.MaxHealth + .001f);
    }
    True(healed > 0);
    True(regen.State == RunState.Playing && without.State == RunState.Playing);
    True(regen.Health > without.Health);

    var plain = Grown(WeaponId.Punch, UpgradeId.Greed, 2);
    True(plain.GetConfig().coinsPerKill > new BalanceConfig().coinsPerKill);
    int coinsBefore = plain.Coins;
    for (int i = 0; i < 300; i++) plain.Step(new PlayerInput(default, default, true));
    True(plain.Coins > coinsBefore);
});
Check("every upgrade has a family, a cap policy, and shows up in the taken list", () =>
{
    var world = ProgressionWorld();
    True(world.TakenUpgrades().Count == 0);

    var seen = new HashSet<UpgradeKind>();
    foreach (UpgradeId id in Enum.GetValues(typeof(UpgradeId))) seen.Add(CombatWorld.KindOf(id));
    // Every family is actually used, so the level-up screen never has an empty group.
    True(seen.Count == Enum.GetValues(typeof(UpgradeKind)).Length);

    int levels = 0;
    for (int i = 0; i < 20000 && levels < 10; i++)
    {
        world.Step(default);
        if (!world.HasUpgradeChoice) continue;
        world.ChooseUpgrade(world.UpgradeChoices[0].Id);
        levels++;
    }
    True(levels == 10);

    var taken = world.TakenUpgrades();
    True(taken.Count > 0);
    int total = 0;
    foreach (UpgradeChoice choice in taken)
    {
        True(choice.Rank == world.UpgradeRank(choice.Id));
        True(choice.Rank > 0);
        if (CombatWorld.IsRankCapped(choice.Id)) True(choice.Rank <= 5);
        total += choice.Rank;
    }
    True(total == levels);
    // Sorted strongest first, so the pause screen reads as a build.
    for (int i = 1; i < taken.Count; i++) True(taken[i - 1].Rank >= taken[i].Rank);
});
Check("a run's twist is fixed, rolled apart, and off unless asked for", () =>
{
    // Every existing caller -- the bots, and every check above this one -- was written against
    // untwisted numbers. If the default ever flips, their results silently stop meaning anything.
    for (int seed = 1; seed <= 40; seed++)
        True(new CombatWorld(new BalanceConfig(), seed, true, WeaponId.Punch, PetId.Mochi).Twist == TwistId.Calm);

    // The same seed must give the same twist, and the twist must not move with anything else.
    for (int seed = 1; seed <= 40; seed++)
    {
        var a = new CombatWorld(new BalanceConfig(), seed, true, WeaponId.Punch, PetId.Mochi, true);
        var b = new CombatWorld(new BalanceConfig(), seed, true, WeaponId.Laser, PetId.Coco, true);
        True(a.Twist == b.Twist);
    }

    // All of them have to be reachable, or a twist is dead content nobody will ever see.
    var seen = new HashSet<TwistId>();
    for (int seed = 1; seed <= 400; seed++)
        seen.Add(new CombatWorld(new BalanceConfig(), seed, true, WeaponId.Punch, PetId.Mochi, true).Twist);
    True(seen.Count == Enum.GetValues(typeof(TwistId)).Length);
});
Check("each twist changes the run it claims to change, and pays for it", () =>
{
    var plain = new CombatWorld(new BalanceConfig(), 7, true, WeaponId.Punch, PetId.Mochi).GetConfig();
    foreach (TwistId twist in Enum.GetValues(typeof(TwistId)))
    {
        // Reached through the constructor rather than by calling the private applier, so this
        // checks the path the game actually takes.
        int seed = 1;
        while (seed < 5000 &&
               new CombatWorld(new BalanceConfig(), seed, true, WeaponId.Punch, PetId.Mochi, true).Twist != twist)
            seed++;
        True(seed < 5000);
        var config = new CombatWorld(new BalanceConfig(), seed, true, WeaponId.Punch, PetId.Mochi, true).GetConfig();

        switch (twist)
        {
            case TwistId.Calm:
                Near(config.spawnInterval, plain.spawnInterval);
                Near(config.coinsPerKill, plain.coinsPerKill);
                break;
            case TwistId.Swarm:
                True(config.spawnInterval < plain.spawnInterval);
                True(config.gruntHealth < plain.gruntHealth);
                break;
            case TwistId.Swift:
                True(config.gruntSpeed > plain.gruntSpeed);
                True(config.runnerSpeed > plain.runnerSpeed);
                break;
            case TwistId.Armored:
                True(config.gruntHealth > plain.gruntHealth);
                True(config.spawnInterval > plain.spawnInterval);
                break;
            case TwistId.EarlyBoss:
                True(config.bossSpawnTime < plain.bossSpawnTime);
                break;
            case TwistId.Harsh:
                True(config.contactDamage > plain.contactDamage);
                True(config.bossSlamDamage > plain.bossSlamDamage);
                break;
        }
        // Anything that makes the run harder has to be worth more than the quiet one.
        if (twist != TwistId.Calm) True(config.coinsPerKill > plain.coinsPerKill);
    }
});
Check("the enemy mix opens gentler and builds up with the wave", () =>
{
    // Wave one holds no brutes at all; by mixWaves the full share is in play. Counted from real
    // spawn events rather than from the share maths, so the ramp has to survive the rounding.
    // The pet clears the crowd so spawning keeps up with the waves. Without it the arena hits the
    // cap in the first few seconds and every later wave has nothing to measure.
    var config = new BalanceConfig { playerHealth = 1000000,
        petDamage = 1000, petRange = 100, petCooldown = .01f, spawnInterval = .05f,
        minSpawnInterval = .05f, waveDuration = 5, mixWaves = 5, bossSpawnTime = 100000 };
    var world = new CombatWorld(config, 99);
    var byWave = new Dictionary<int, Dictionary<EnemyKind, int>>();
    while (world.Time < 30 && world.State == RunState.Playing)
    {
        world.Step(default);
        foreach (CombatEvent e in world.Events)
        {
            if (e.type != "spawn") continue;
            if (!byWave.TryGetValue(e.wave, out var counts))
                byWave[e.wave] = counts = new Dictionary<EnemyKind, int>();
            var kind = (EnemyKind)Enum.Parse(typeof(EnemyKind), e.source);
            counts[kind] = counts.TryGetValue(kind, out int n) ? n + 1 : 1;
        }
    }
    True(byWave.ContainsKey(1) && byWave.ContainsKey(6));
    True(!byWave[1].ContainsKey(EnemyKind.Brute));
    float Share(int wave, EnemyKind kind)
    {
        int total = 0, of = 0;
        foreach (var pair in byWave[wave]) { total += pair.Value; if (pair.Key == kind) of = pair.Value; }
        return total == 0 ? 0 : of / (float)total;
    }
    True(Share(6, EnemyKind.Brute) > 0);
    True(Share(6, EnemyKind.Runner) > Share(1, EnemyKind.Runner));
    True(Share(6, EnemyKind.Grunt) < Share(1, EnemyKind.Grunt));
    // A mix that leaves no room for walkers is a config mistake, not a hard run.
    Throws(() => new CombatWorld(new BalanceConfig { runnerShare = .8f, bruteShare = .5f }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { runnerShare = -.1f }, 1));
    Throws(() => new CombatWorld(new BalanceConfig { mixWaves = 0 }, 1));
});
Check("every id the player can see is translated in every language", () =>
{
    var languages = (Language[])Enum.GetValues(typeof(Language));
    True(languages.Length >= 2);

    foreach (Language language in languages)
    {
        Texts.Use(language);
        True(Texts.Current == language);

        foreach (UpgradeId id in Enum.GetValues(typeof(UpgradeId)))
        {
            Translated(Texts.UpgradeTitle(id), id.ToString());
            Translated(Texts.UpgradeDescription(id), id.ToString());
        }
        foreach (WeaponId id in Enum.GetValues(typeof(WeaponId)))
        {
            Translated(Texts.Weapon(id), id.ToString());
            Translated(Texts.WeaponHint(id), id.ToString());
        }
        foreach (PetId id in Enum.GetValues(typeof(PetId)))
        {
            Translated(Texts.Pet(id), id.ToString());
            Translated(Texts.PetRole(id), id.ToString());
        }
        // Only the moves the player is warned about. Stalk and Telegraph are never named on screen.
        foreach (BossAction move in new[] { BossAction.Charge, BossAction.Slam, BossAction.Summon })
            Translated(Texts.BossMove(move), move.ToString());
        foreach (TwistId twist in Enum.GetValues(typeof(TwistId)))
        {
            Translated(Texts.TwistTitle(twist), twist.ToString());
            Translated(Texts.TwistDescription(twist), twist.ToString());
        }

        // A sample of the screen text, so a language that is only half filled in is caught.
        foreach (string line in new[]
                 {
                     Texts.StartHeadline, Texts.StartBlurb, Texts.StartControls, Texts.EnemyLegend,
                     Texts.Play, Texts.TryAgain, Texts.Restart, Texts.Back, Texts.Build,
                     Texts.ShopHeadline, Texts.ShopHint, Texts.Owned, Texts.Starter,
                     Texts.LevelUpHeadline, Texts.LevelUpHint, Texts.MoveHint, Texts.Heat,
                     Texts.BossEnraged, Texts.BossDownHeadline,
                 })
            True(!string.IsNullOrWhiteSpace(line));
    }

    // Two upgrades must never share a card title, or a choice is impossible to tell apart.
    foreach (Language language in languages)
    {
        Texts.Use(language);
        var titles = new List<string>();
        foreach (UpgradeId id in Enum.GetValues(typeof(UpgradeId)))
        {
            string title = Texts.UpgradeTitle(id);
            True(!titles.Contains(title));
            titles.Add(title);
        }
    }

    // The languages are actually different, so nothing was pasted into both columns.
    Texts.Use(Language.Korean);
    string korean = Texts.StartBlurb;
    Texts.Use(Language.English);
    True(Texts.StartBlurb != korean);
    True(Texts.Next == Language.Korean);

    Texts.UseSystemLanguage(true);
    True(Texts.Current == Language.Korean);
    Texts.UseSystemLanguage(false);
    True(Texts.Current == Language.English);
});
Console.WriteLine(passed + " checks passed.");
return;

// A translation that fell through to the default case comes back as the raw enum name.
void Translated(string text, string idName)
{
    True(!string.IsNullOrWhiteSpace(text));
    True(text != idName);
}

// Mean distance from the player: a crowd that is slowed stays further out.
float Spread(CombatWorld world)
{
    if (world.Enemies.Count == 0) return 0;
    float total = 0;
    foreach (Enemy e in world.Enemies) total += (e.Position - world.Position).Length;
    return total / world.Enemies.Count;
}

bool OtherPetUpgrade(UpgradeId id, PetId pet) =>
    (id == UpgradeId.PetReach && pet != PetId.Mochi)
    || (id == UpgradeId.PetChill && pet != PetId.Bori)
    || (id == UpgradeId.PetGuard && pet != PetId.Coco);

// A run that already holds the given upgrade at the given rank, with enemies on the field.
// `hurt` starts the player low on health so healing and contact effects have something to show.
CombatWorld Grown(WeaponId weapon, UpgradeId id, int rank, bool hurt = false)
{
    var config = new BalanceConfig
    {
        playerHealth = hurt ? 400 : 100000,
        petRange = .001f,
        bossSpawnTime = 1000,
        spawnInterval = .25f,
        minSpawnInterval = .25f,
    };
    var world = new CombatWorld(config, 42, true, weapon);
    for (int i = 0; i < 240; i++) world.Step(default);
    for (int taken = 0; taken < rank; taken++) True(world.Apply(id));
    True(world.UpgradeRank(id) == rank);
    return world;
}

// Speed of an arrow loosed at the given pull, aimed away from the only enemy on the field.
float ArrowSpeed(float draw)
{
    var world = new CombatWorld(new BalanceConfig { playerHealth = 100000, petRange = .001f,
        arrowMinDraw = .25f, arrowMinPower = .5f, bossSpawnTime = 1000,
        spawnInterval = 900, minSpawnInterval = 900 }, 42, false, WeaponId.Arrow);
    world.Step(default);
    Vec2 away = world.Enemies[0].Position.Normalized * -1;
    world.Step(new PlayerInput(default, away, false, true, draw));
    world.Step(new PlayerInput(default, away, false, false, draw));
    True(world.Projectiles.Count == 1);
    return world.Projectiles[0].Velocity.Length;
}

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
