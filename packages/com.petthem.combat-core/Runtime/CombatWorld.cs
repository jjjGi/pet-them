using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// The rule checks reach one internal seam: applying an upgrade without waiting for it to be
// offered. Nothing else is shared, and the public API stays the same for the game.
[assembly: InternalsVisibleTo("CoreChecks")]

namespace PetThem.Combat
{
    [Serializable]
    public struct Vec2
    {
        public float x, y;
        public Vec2(float x, float y) { this.x = x; this.y = y; }
        public float Length => (float)Math.Sqrt(x * x + y * y);
        public Vec2 Normalized => Length > 0.00001f ? this / Length : new Vec2();
        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.x + b.x, a.y + b.y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.x - b.x, a.y - b.y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.x * s, a.y * s);
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.x / s, a.y / s);
        public static float Dot(Vec2 a, Vec2 b) => a.x * b.x + a.y * b.y;
        public bool IsFinite => Finite(x) && Finite(y);
        internal static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    }

    [Serializable]
    public sealed class BalanceConfig
    {
        public string version = "prototype-0.3";
        public float duration = 180, arenaHalfWidth = 14, arenaHalfHeight = 8;
        // Opt-in so old recordings and arena simulations keep their original rules.
        public bool endlessWorld;
        public bool adventureEnabled;
        public float playerHealth = 100, playerSpeed = 4.5f;
        public float punchDamage = 26, punchRange = 2.6f, punchCooldown = 0.38f, punchKnockback = 1.5f;
        public float petDamage = 12, petRange = 7, petCooldown = 0.9f;
        public float gruntHealth = 32, gruntSpeed = 1.25f, runnerHealth = 18, runnerSpeed = 2.15f;
        public float contactDamage = 10, hurtCooldown = 0.65f;
        public float spawnInterval = 1.3f, minSpawnInterval = 0.35f, waveDuration = 30;
        public int maxEnemies = 100;
        // Arrow: slow, aimed, pierces a line of enemies. Rewards picking the right target.
        public float arrowDamage = 40, arrowCooldown = 0.72f, arrowSpeed = 17, arrowRadius = 0.45f;
        public int arrowPierce = 2;
        // The bow has to be drawn. A pull shorter than arrowMinDraw is not a shot at all, and a
        // pull that only just clears it leaves at arrowMinPower of full speed and damage.
        public float arrowMinDraw = 0.25f, arrowMinPower = 0.55f;
        // Laser: continuous damage limited by heat rather than by a cooldown.
        public float laserDamagePerSecond = 52, laserRange = 8.5f, laserWidth = 0.75f;
        public float laserHeatPerSecond = 36, laserCoolPerSecond = 28, laserOverheatPenalty = 1.5f;
        // Brute: slow, heavy, hits hard. Punishes standing still more than the chaser does.
        public float bruteHealth = 80, bruteSpeed = 0.85f, bruteContactDamage = 18, bruteShare = 0.16f;
        // The shares above are what the mix reaches, not what it starts at. It opens gentler and
        // takes mixWaves waves to get there, so a run has a shape rather than one flat crowd.
        public float runnerShare = 0.34f, mixWaves = 5;
        // Boss: one per run, arrives late. Killing it ends the run early as a win.
        public float bossSpawnTime = 120, bossHealth = 1800, bossSpeed = 1.05f;
        public float bossContactDamage = 26, bossRadius = 1.9f;
        // Boss moves. Every one is announced first: bossTelegraph seconds of winding up, standing
        // still, so the player is told what is coming and has time to answer it.
        public float bossMoveInterval = 4.5f, bossTelegraph = 0.85f;
        public float bossChargeSpeed = 11, bossChargeSeconds = 0.8f, bossChargeDamage = 38;
        public float bossSlamRadius = 4.2f, bossSlamDamage = 32;
        public int bossSummonCount = 4;
        // Below this share of its health the boss stops pausing as long between moves.
        public float bossEnrageHealth = 0.35f, bossEnrageHaste = 0.62f;
        // Run reward. Spent in the shop between runs, which is not built yet.
        public float coinsPerKill = 1, coinsPerBossKill = 150, coinsPerSecondSurvived = 0.5f;
        // Pets. Mochi uses petDamage as it is; the other two trade damage for their own effect.
        public float petControlDamageShare = 0.45f, petSupportDamageShare = 0.3f;
        public float petSlowFactor = 0.45f, petSlowSeconds = 1.6f, petControlKnockback = 1.2f;
        public float petHealInterval = 6.5f, petHealAmount = 7, petGuardReduction = 0.22f;
        // Shop prices, in coins. Mochi is the starter pet and is never for sale.
        public float boriPrice = 320, cocoPrice = 520;
        // Drones copy the weapon you picked. Damage is a share of that weapon's own damage.
        public float droneInterval = 1.15f, droneShare = 0.55f, droneRange = 8, droneOrbit = 1.9f;
        // Orbiting guards. Each one hurts what it brushes past, then waits before hurting it again.
        public float orbitRadius = 2.2f, orbitSpeed = 2.4f, orbitDamage = 14, orbitHitRadius = 0.6f;
        public float orbitRecovery = 0.55f;
        // Effects that fire on an event rather than on a timer.
        public float critChance = 0.12f, critMultiplier = 2.4f;
        public float blastDamage = 18, blastRadius = 2.2f;
        public float lifestealPerKill = 0.6f, thornsDamage = 12, regenPerSecond = 0.8f;
        public float greedBonus = 0.5f;
        // Repel: being hit shoves the crowd off you. The answer to being surrounded, which is the
        // shape most deaths have.
        public float repelRadius = 3.4f, repelForce = 2.6f;
        // Re-rolls of the level-up cards, per run. Spent by choice, so nought is a valid setting.
        public int rerollsPerRun = 2;

        public BalanceConfig Copy() => (BalanceConfig)MemberwiseClone();
        public void Validate()
        {
            foreach (float value in new[] { duration, arenaHalfWidth, arenaHalfHeight, playerHealth,
                playerSpeed, punchDamage, punchRange, punchCooldown, petDamage, petRange, petCooldown,
                gruntHealth, gruntSpeed, runnerHealth, runnerSpeed, contactDamage, hurtCooldown,
                spawnInterval, minSpawnInterval, waveDuration,
                arrowDamage, arrowCooldown, arrowSpeed, arrowRadius,
                laserDamagePerSecond, laserRange, laserWidth,
                laserHeatPerSecond, laserCoolPerSecond, laserOverheatPenalty,
                bruteHealth, bruteSpeed, bruteContactDamage,
                bossSpawnTime, bossHealth, bossSpeed, bossContactDamage, bossRadius,
                bossMoveInterval, bossTelegraph, bossChargeSpeed, bossChargeSeconds,
                bossChargeDamage, bossSlamRadius, bossSlamDamage, bossEnrageHaste,
                petSlowSeconds, petControlKnockback, petHealInterval, petHealAmount })
                if (!Vec2.Finite(value) || value <= 0) throw new ArgumentException("Balance values must be finite and positive.");
            foreach (float share in new[] { petControlDamageShare, petSupportDamageShare, petSlowFactor })
                if (!Vec2.Finite(share) || share <= 0 || share > 1)
                    throw new ArgumentException("Pet shares and the slow factor must be above 0 and at most 1.");
            if (!Vec2.Finite(petGuardReduction) || petGuardReduction < 0 || petGuardReduction > 0.9f)
                throw new ArgumentException("petGuardReduction must be between 0 and 0.9.");
            if (!Vec2.Finite(punchKnockback) || punchKnockback < 0) throw new ArgumentException("Invalid knockback.");
            foreach (float reward in new[] { coinsPerKill, coinsPerBossKill, coinsPerSecondSurvived })
                if (!Vec2.Finite(reward) || reward < 0) throw new ArgumentException("Reward values must be finite and not negative.");
            foreach (float price in new[] { boriPrice, cocoPrice })
                if (!Vec2.Finite(price) || price < 1) throw new ArgumentException("Shop prices must be at least 1 coin.");
            foreach (float value in new[] { droneInterval, droneShare, droneRange, droneOrbit,
                orbitRadius, orbitSpeed, orbitDamage, orbitHitRadius, orbitRecovery,
                critMultiplier, blastDamage, blastRadius, lifestealPerKill, thornsDamage,
                regenPerSecond, greedBonus, repelRadius, repelForce })
                if (!Vec2.Finite(value) || value <= 0)
                    throw new ArgumentException("Upgrade effect values must be finite and positive.");
            if (!Vec2.Finite(critChance) || critChance <= 0 || critChance > 1)
                throw new ArgumentException("critChance must be above 0 and at most 1.");
            if (critMultiplier < 1) throw new ArgumentException("critMultiplier must be at least 1.");
            if (bossSummonCount < 0 || bossSummonCount > 20)
                throw new ArgumentException("bossSummonCount must be between 0 and 20.");
            if (!Vec2.Finite(bossEnrageHealth) || bossEnrageHealth < 0 || bossEnrageHealth >= 1)
                throw new ArgumentException("bossEnrageHealth must be at least 0 and below 1.");
            if (bossTelegraph >= bossMoveInterval)
                throw new ArgumentException("bossTelegraph must be shorter than bossMoveInterval.");
            if (!Vec2.Finite(bruteShare) || bruteShare < 0 || bruteShare > 0.9f)
                throw new ArgumentException("bruteShare must be between 0 and 0.9.");
            if (!Vec2.Finite(runnerShare) || runnerShare < 0 || runnerShare > 0.9f)
                throw new ArgumentException("runnerShare must be between 0 and 0.9.");
            // The two shares are rolled against the same number, so together they must leave room
            // for the walkers. Without this a bad config would silently stop spawning grunts.
            if (runnerShare + bruteShare > 0.95f)
                throw new ArgumentException("runnerShare and bruteShare must leave room for grunts.");
            if (!Vec2.Finite(mixWaves) || mixWaves < 1 || mixWaves > 100)
                throw new ArgumentException("mixWaves must be between 1 and 100.");
            if (maxEnemies < 1 || maxEnemies > 1000 || minSpawnInterval > spawnInterval ||
                arenaHalfWidth <= 1 || arenaHalfHeight <= 1 || string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Invalid limits or version.");
            if (arrowPierce < 1 || arrowPierce > 20) throw new ArgumentException("arrowPierce must be between 1 and 20.");
            // Not in the list above: a run with no re-rolls is a legitimate setting, and the list
            // there insists on values above zero.
            if (rerollsPerRun < 0 || rerollsPerRun > 20)
                throw new ArgumentException("rerollsPerRun must be between 0 and 20.");
            if (!Vec2.Finite(arrowMinDraw) || arrowMinDraw < 0 || arrowMinDraw >= 1)
                throw new ArgumentException("arrowMinDraw must be at least 0 and below 1.");
            if (!Vec2.Finite(arrowMinPower) || arrowMinPower <= 0 || arrowMinPower > 1)
                throw new ArgumentException("arrowMinPower must be above 0 and at most 1.");
        }
    }

    public struct PlayerInput
    {
        public Vec2 move, aim;
        /// <summary>Fire once: a punch, or the release of a drawn arrow.</summary>
        public bool punch;
        /// <summary>The attack control is held down: the laser beam is on, or an arrow is being drawn.</summary>
        public bool hold;
        /// <summary>
        /// How far back the attack control was pulled, from 0 to 1. Only the arrow reads it.
        /// Callers that do not aim by dragging get a full draw.
        /// </summary>
        public float draw;

        public PlayerInput(Vec2 move, Vec2 aim, bool punch)
        { this.move = move; this.aim = aim; this.punch = punch; this.hold = false; this.draw = 1; }
        public PlayerInput(Vec2 move, Vec2 aim, bool punch, bool hold)
        { this.move = move; this.aim = aim; this.punch = punch; this.hold = hold; this.draw = 1; }
        public PlayerInput(Vec2 move, Vec2 aim, bool punch, bool hold, float draw)
        { this.move = move; this.aim = aim; this.punch = punch; this.hold = hold; this.draw = draw; }
    }

    public enum RunState { Playing, Won, Lost, Abandoned }
    public enum EnemyKind { Grunt, Runner, Brute, Boss }

    public sealed class Enemy
    {
        public int Id { get; internal set; }
        public EnemyKind Kind { get; internal set; }
        public Vec2 Position { get; internal set; }
        public float Health { get; internal set; }
        public float MaxHealth { get; internal set; }
        /// <summary>Body size. A bigger enemy is easier to hit and reaches the player sooner.</summary>
        public float Radius { get; internal set; } = 0.4f;
        /// <summary>Tick this enemy stops being slowed. Set by the control pet.</summary>
        internal int SlowUntilTick;
        /// <summary>Drawn differently while true, so the player can see the pet working.</summary>
        public bool Slowed { get; internal set; }
    }

    [Serializable]
    public sealed class CombatEvent
    {
        public int tick, wave, targetId, alive;
        public float time, value, x, y, health;
        public string type, source;
    }

    // No Unity physics or wall-clock time: the player and CLI advance the same 60 Hz rules.
    public sealed partial class CombatWorld
    {
        public const float StepSeconds = 1f / 60;
        private readonly BalanceConfig config;
        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<CombatEvent> events = new List<CombatEvent>();
        private readonly IReadOnlyList<Enemy> enemyView;
        private readonly IReadOnlyList<CombatEvent> eventView;
        private uint random;
        private int nextId = 1, nextSpawn, nextPunch, nextPet, nextHurt, wave;
        public int Seed { get; }
        public int Tick { get; private set; }
        public float Time => Tick * StepSeconds;
        public int Wave => wave;
        public int Kills { get; private set; }
        public float Health { get; private set; }
        public Vec2 Position { get; private set; }
        public Vec2 Facing { get; private set; } = new Vec2(1, 0);
        public RunState State { get; private set; } = RunState.Playing;
        public IReadOnlyList<Enemy> Enemies => enemyView;
        // Events are replaced on each step; consumers must drain them before the next step.
        public IReadOnlyList<CombatEvent> Events => eventView;
        public BalanceConfig GetConfig() => config.Copy();

        public CombatWorld(BalanceConfig config, int seed, bool enableProgression = false)
            : this(config, seed, enableProgression, WeaponId.Punch) { }

        public CombatWorld(BalanceConfig config, int seed, bool enableProgression, WeaponId weapon)
            : this(config, seed, enableProgression, weapon, PetId.Mochi) { }

        /// <param name="enableTwists">
        /// Off by default so that every existing caller -- the checks and the bots -- keeps the
        /// numbers it was written against. The game turns it on; anything comparing runs across
        /// versions should leave it off.
        /// </param>
        public CombatWorld(BalanceConfig config, int seed, bool enableProgression, WeaponId weapon, PetId pet,
            bool enableTwists = false)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Validate();
            Twist = RollTwist(seed, enableTwists);
            // Folded in before anything reads a number, so the twist is invisible from here on:
            // nothing downstream needs to know a run is twisted, it just plays the numbers it has.
            BalanceConfig twisted = config.Copy();
            ApplyTwist(Twist, twisted);
            twisted.Validate();
            this.config = twisted;
            // The starting values that upgrades are a percentage of are the twisted ones, so a
            // "+20% of starting power" card is worth the same share of the run it is played in.
            baseConfig = twisted.Copy();
            Weapon = weapon;
            Pet = pet;
            projectileView = projectiles.AsReadOnly();
            ProgressionEnabled = enableProgression;
            progressionRandom = unchecked((uint)seed) ^ 0xa341316c;
            if (progressionRandom == 0) progressionRandom = 1;
            effectRandom = unchecked((uint)seed) ^ 0x2545f491;
            if (effectRandom == 0) effectRandom = 0x6c078965;
            bossRandom = unchecked((uint)seed) ^ 0x7f4a7c15;
            if (bossRandom == 0) bossRandom = 0x1b873593;
            droneView = drones.AsReadOnly();
            orbView = orbs.AsReadOnly();
            Seed = seed;
            random = unchecked((uint)seed);
            if (random == 0) random = 0x9e3779b9;
            Health = config.playerHealth;
            RerollsLeft = twisted.rerollsPerRun;
            enemyView = enemies.AsReadOnly();
            eventView = events.AsReadOnly();
            InitializeAdventure();
        }

        public void Step(PlayerInput input)
        {
            if (!input.move.IsFinite || !input.aim.IsFinite) throw new ArgumentException("Non-finite input.");
            events.Clear();
            if (State != RunState.Playing || HasUpgradeChoice) return;
            Tick++;
            int currentWave = 1 + (Tick - 1) / Frames(config.waveDuration);
            if (currentWave != wave) { wave = currentWave; Emit("wave", "world", 0, wave); }

            Vec2 movement = input.move.Length > 1 ? input.move.Normalized : input.move;
            Position = Clamp(Position + movement * (config.playerSpeed * StepSeconds), 0.45f);
            if (config.endlessWorld)
            {
                // Reuse distant enemies without awarding kills/XP or consuming the population cap.
                foreach (Enemy enemy in enemies)
                    if (enemy != Boss && (enemy.Position - Position).Length > SpawnRadius * 3)
                    {
                        float angle = Next() * (float)Math.PI * 2;
                        enemy.Position = Position + new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle)) * SpawnRadius;
                        Emit("relocate", "world", enemy.Id, 0, enemy.Position);
                    }
            }
            if (input.aim.Length > 0.05f) Facing = input.aim.Normalized;

            SpawnBossWhenDue();
            if (Tick >= nextSpawn && enemies.Count < config.maxEnemies)
            {
                Spawn();
                float interval = Math.Max(config.minSpawnInterval, config.spawnInterval / (1 + (wave - 1) * 0.32f));
                nextSpawn = Tick + Frames(interval);
            }

            StepWeapon(input);
            // Killing the boss ends the run inside the attack above, so nothing after it may act.
            if (State == RunState.Playing) StepPet();
            if (State == RunState.Playing) StepCompanions();
            if (State == RunState.Playing) StepAdventure();
            if (State == RunState.Playing) StepBoss();

            // Walked as a copy: thorns can kill the enemy being handled, and its blast can take
            // several more with it.
            foreach (Enemy enemy in ScanEnemies(contactScan))
            {
                if (State != RunState.Playing) break;
                if (enemy.Health <= 0) continue;
                // A winding-up or charging boss moves on its own terms, not towards the player.
                bool selfDriven = enemy == Boss && BossState != BossAction.Stalk;
                if (!selfDriven)
                {
                    Vec2 delta = Position - enemy.Position;
                    float travel = Math.Min(delta.Length, SpeedOf(enemy) * StepSeconds);
                    enemy.Position += delta.Normalized * travel;
                }
                // A bigger body reaches the player from further out.
                if ((enemy.Position - Position).Length <= 0.35f + enemy.Radius && Tick >= nextHurt)
                {
                    nextHurt = Tick + Frames(config.hurtCooldown);
                    float incoming = ContactDamageOf(enemy.Kind) * (1 - GuardReduction);
                    float actual = Math.Min(Health, incoming);
                    Health -= actual;
                    Emit("hurt", enemy.Kind.ToString(), enemy.Id, actual, Position);
                    if (Health <= 0) { State = RunState.Lost; Emit("run_end", "death", enemy.Id, Kills); break; }
                    ApplyThorns(enemy);
                    ApplyRepel();
                }
            }
            if (State == RunState.Playing && Tick >= Frames(config.duration))
            { State = RunState.Won; Emit("run_end", "survived", 0, Kills); }
            if (Tick % 60 == 0 || State != RunState.Playing) Emit("snapshot", "world", 0, Kills, Position);
            PrepareUpgradeChoices();
        }

        public void Abandon()
        {
            events.Clear();
            if (State != RunState.Playing) return;
            State = RunState.Abandoned;
            Emit("run_end", "abandoned", 0, Kills);
        }

        private static int Frames(float seconds) => Math.Max(1, (int)Math.Ceiling(seconds / StepSeconds - 0.00001));
        private float Next()
        {
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            return (random & 0x00ffffff) / 16777216f;
        }
        private float SpawnRadius => Math.Max(config.arenaHalfWidth, config.arenaHalfHeight);
        private Vec2 Clamp(Vec2 p, float margin) => config.endlessWorld ? p : new Vec2(
            Math.Max(-config.arenaHalfWidth + margin, Math.Min(config.arenaHalfWidth - margin, p.x)),
            Math.Max(-config.arenaHalfHeight + margin, Math.Min(config.arenaHalfHeight - margin, p.y)));
        private void Spawn()
        {
            float angle = Next() * (float)Math.PI * 2;
            Vec2 direction = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle));
            float distance = config.endlessWorld ? SpawnRadius : Math.Min(config.arenaHalfWidth, config.arenaHalfHeight) * 0.92f;
            Vec2 position = Clamp(Position + direction * distance, 0.4f);
            // Avoid spawning on top of the player at an arena edge.
            if ((position - Position).Length < 4) position = Clamp(Position - direction * distance, 0.4f);
            // The mix moves with the wave instead of staying fixed for the whole run. Wave one is
            // almost all walkers and holds no brutes at all; by mixWaves it is the full mix. Before
            // this, spawning faster and giving each enemy more health was the only thing that
            // changed, so minute three fought the same fight as minute one, only more of it.
            float ramp = config.mixWaves <= 1 ? 1
                : Math.Min(1, (wave - 1) / (config.mixWaves - 1));
            float runners = config.runnerShare * (0.3f + 0.7f * ramp);
            float brutes = config.bruteShare * ramp;
            float roll = Next();
            EnemyKind kind = roll < runners ? EnemyKind.Runner
                : roll < runners + brutes ? EnemyKind.Brute
                : EnemyKind.Grunt;
            SpawnAt(kind, position, "wave");
        }

        /// <summary>Places one enemy. Shared by the wave timer and by the boss's summon.</summary>
        private Enemy SpawnAt(EnemyKind kind, Vec2 position, string source)
        {
            float health = HealthOf(kind) * (1 + (wave - 1) * 0.12f);
            var enemy = new Enemy { Id = nextId++, Kind = kind, Health = health, MaxHealth = health,
                Position = position, Radius = RadiusOf(kind) };
            enemies.Add(enemy);
            // The source says where it came from: a summoned enemy is not a wave spawn.
            Emit("spawn", kind.ToString(), enemy.Id, health, position);
            if (source != "wave") Emit("summon", kind.ToString(), enemy.Id, health, position);
            return enemy;
        }
        private Enemy Nearest(float range)
        {
            Enemy result = null;
            foreach (Enemy enemy in enemies)
            {
                float distance = (enemy.Position - Position).Length;
                if (distance <= range) { range = distance; result = enemy; }
            }
            return result;
        }
        private void Damage(Enemy enemy, float amount, string source)
        {
            // Thorns and the blast are consequences of a hit, not hits themselves, so they do not
            // roll for a critical of their own.
            bool rolls = source != "thorns" && source != "blast";
            float rolled = rolls ? ApplyCrit(amount) : amount;
            float actual = Math.Min(rolled, enemy.Health);
            enemy.Health -= actual;
            Emit("damage", rolls && LastHitWasCritical ? source + "_crit" : source,
                enemy.Id, actual, enemy.Position);
            if (enemy.Health <= 0)
            {
                enemies.Remove(enemy); Kills++; Emit("kill", source, enemy.Id, 1, enemy.Position);
                AwardExperience();
                OnEnemyKilled(enemy);
                OnKillEffects(enemy);
            }
        }
        private void Emit(string type, string source, int targetId, float value, Vec2 position = default)
        {
            events.Add(new CombatEvent { tick = Tick, time = Time, wave = wave, type = type,
                source = source, targetId = targetId, value = value, x = position.x, y = position.y,
                health = Health, alive = enemies.Count });
        }
    }
}
