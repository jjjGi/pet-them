using System;
using System.Collections.Generic;

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
        // Laser: continuous damage limited by heat rather than by a cooldown.
        public float laserDamagePerSecond = 52, laserRange = 8.5f, laserWidth = 0.75f;
        public float laserHeatPerSecond = 36, laserCoolPerSecond = 28, laserOverheatPenalty = 1.5f;
        // Brute: slow, heavy, hits hard. Punishes standing still more than the chaser does.
        public float bruteHealth = 80, bruteSpeed = 0.85f, bruteContactDamage = 18, bruteShare = 0.16f;
        // Boss: one per run, arrives late. Killing it ends the run early as a win.
        public float bossSpawnTime = 120, bossHealth = 1800, bossSpeed = 1.05f;
        public float bossContactDamage = 26, bossRadius = 1.9f;
        // Run reward. Spent in the shop between runs, which is not built yet.
        public float coinsPerKill = 1, coinsPerBossKill = 150, coinsPerSecondSurvived = 0.5f;

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
                bossSpawnTime, bossHealth, bossSpeed, bossContactDamage, bossRadius })
                if (!Vec2.Finite(value) || value <= 0) throw new ArgumentException("Balance values must be finite and positive.");
            if (!Vec2.Finite(punchKnockback) || punchKnockback < 0) throw new ArgumentException("Invalid knockback.");
            foreach (float reward in new[] { coinsPerKill, coinsPerBossKill, coinsPerSecondSurvived })
                if (!Vec2.Finite(reward) || reward < 0) throw new ArgumentException("Reward values must be finite and not negative.");
            if (!Vec2.Finite(bruteShare) || bruteShare < 0 || bruteShare > 0.9f)
                throw new ArgumentException("bruteShare must be between 0 and 0.9.");
            if (maxEnemies < 1 || maxEnemies > 1000 || minSpawnInterval > spawnInterval ||
                arenaHalfWidth <= 1 || arenaHalfHeight <= 1 || string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Invalid limits or version.");
            if (arrowPierce < 1 || arrowPierce > 20) throw new ArgumentException("arrowPierce must be between 1 and 20.");
        }
    }

    public struct PlayerInput
    {
        public Vec2 move, aim;
        /// <summary>Fire once: a punch, or the release of a drawn arrow.</summary>
        public bool punch;
        /// <summary>The attack control is held down: the laser beam is on, or an arrow is being aimed.</summary>
        public bool hold;
        public PlayerInput(Vec2 move, Vec2 aim, bool punch)
        { this.move = move; this.aim = aim; this.punch = punch; this.hold = false; }
        public PlayerInput(Vec2 move, Vec2 aim, bool punch, bool hold)
        { this.move = move; this.aim = aim; this.punch = punch; this.hold = hold; }
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
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Validate();
            this.config = config.Copy();
            baseConfig = config.Copy();
            Weapon = weapon;
            projectileView = projectiles.AsReadOnly();
            ProgressionEnabled = enableProgression;
            progressionRandom = unchecked((uint)seed) ^ 0xa341316c;
            if (progressionRandom == 0) progressionRandom = 1;
            Seed = seed;
            random = unchecked((uint)seed);
            if (random == 0) random = 0x9e3779b9;
            Health = config.playerHealth;
            enemyView = enemies.AsReadOnly();
            eventView = events.AsReadOnly();
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
            if (State == RunState.Playing && Tick >= nextPet)
            {
                Enemy target = Nearest(config.petRange);
                if (target != null)
                {
                    nextPet = Tick + Frames(config.petCooldown);
                    Emit("attack", "pet", target.Id, 0, target.Position);
                    Damage(target, config.petDamage, "pet");
                }
            }

            for (int i = 0; i < enemies.Count && State == RunState.Playing; i++)
            {
                Enemy enemy = enemies[i];
                Vec2 delta = Position - enemy.Position;
                float travel = Math.Min(delta.Length, SpeedOf(enemy.Kind) * StepSeconds);
                enemy.Position += delta.Normalized * travel;
                // A bigger body reaches the player from further out.
                if ((enemy.Position - Position).Length <= 0.35f + enemy.Radius && Tick >= nextHurt)
                {
                    nextHurt = Tick + Frames(config.hurtCooldown);
                    float actual = Math.Min(Health, ContactDamageOf(enemy.Kind));
                    Health -= actual;
                    Emit("hurt", enemy.Kind.ToString(), enemy.Id, actual, Position);
                    if (Health <= 0) { State = RunState.Lost; Emit("run_end", "death", enemy.Id, Kills); break; }
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
        private Vec2 Clamp(Vec2 p, float margin) => new Vec2(
            Math.Max(-config.arenaHalfWidth + margin, Math.Min(config.arenaHalfWidth - margin, p.x)),
            Math.Max(-config.arenaHalfHeight + margin, Math.Min(config.arenaHalfHeight - margin, p.y)));
        private void Spawn()
        {
            float angle = Next() * (float)Math.PI * 2;
            Vec2 direction = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle));
            float distance = Math.Min(config.arenaHalfWidth, config.arenaHalfHeight) * 0.92f;
            Vec2 position = Clamp(Position + direction * distance, 0.4f);
            // Avoid spawning on top of the player at an arena edge.
            if ((position - Position).Length < 4) position = Clamp(Position - direction * distance, 0.4f);
            float roll = Next();
            EnemyKind kind = roll < 0.28f ? EnemyKind.Runner
                : roll < 0.28f + config.bruteShare ? EnemyKind.Brute
                : EnemyKind.Grunt;
            float health = HealthOf(kind) * (1 + (wave - 1) * 0.12f);
            var enemy = new Enemy { Id = nextId++, Kind = kind, Health = health, MaxHealth = health,
                Position = position, Radius = RadiusOf(kind) };
            enemies.Add(enemy);
            Emit("spawn", kind.ToString(), enemy.Id, health, position);
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
            float actual = Math.Min(amount, enemy.Health);
            enemy.Health -= actual;
            Emit("damage", source, enemy.Id, actual, enemy.Position);
            if (enemy.Health <= 0)
            {
                enemies.Remove(enemy); Kills++; Emit("kill", source, enemy.Id, 1, enemy.Position);
                AwardExperience();
                OnEnemyKilled(enemy);
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
