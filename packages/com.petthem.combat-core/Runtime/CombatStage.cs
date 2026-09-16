using System;

namespace PetThem.Combat
{
    public sealed partial class CombatWorld
    {
        private bool bossSpawned;

        /// <summary>The boss while it is alive, or null. Unity draws its health bar from this.</summary>
        public Enemy Boss { get; private set; }

        /// <summary>True once the boss has been defeated.</summary>
        public bool BossDefeated { get; private set; }

        /// <summary>Seconds until the boss arrives, or 0 once it has.</summary>
        public float SecondsToBoss => Math.Max(0, config.bossSpawnTime - Time);

        /// <summary>
        /// Coins earned so far. Meant to be spent between runs in a shop that does not exist yet,
        /// so nothing reads this except the result screen and the run log.
        /// </summary>
        public int Coins => (int)(Kills * config.coinsPerKill
            + (BossDefeated ? config.coinsPerBossKill : 0)
            + Time * config.coinsPerSecondSurvived);

        public float HealthOf(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Runner: return config.runnerHealth;
                case EnemyKind.Brute: return config.bruteHealth;
                case EnemyKind.Boss: return config.bossHealth;
                default: return config.gruntHealth;
            }
        }

        public float SpeedOf(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Runner: return config.runnerSpeed;
                case EnemyKind.Brute: return config.bruteSpeed;
                case EnemyKind.Boss: return config.bossSpeed;
                default: return config.gruntSpeed;
            }
        }

        public float ContactDamageOf(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Brute: return config.bruteContactDamage;
                case EnemyKind.Boss: return config.bossContactDamage;
                default: return config.contactDamage;
            }
        }

        public float RadiusOf(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Runner: return 0.31f;
                case EnemyKind.Brute: return 0.62f;
                case EnemyKind.Boss: return config.bossRadius;
                default: return 0.45f;
            }
        }

        /// <summary>
        /// The boss arrives once, at its scheduled time, and ignores the enemy cap so it cannot be
        /// starved out by a crowd of ordinary enemies.
        /// </summary>
        private void SpawnBossWhenDue()
        {
            if (bossSpawned || Time < config.bossSpawnTime) return;
            bossSpawned = true;

            float angle = Next() * (float)Math.PI * 2;
            var direction = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle));
            float distance = Math.Min(config.arenaHalfWidth, config.arenaHalfHeight) * 0.9f;
            Vec2 position = Clamp(Position + direction * distance, config.bossRadius + 0.2f);
            if ((position - Position).Length < 5)
                position = Clamp(Position - direction * distance, config.bossRadius + 0.2f);

            Boss = new Enemy
            {
                Id = nextId++,
                Kind = EnemyKind.Boss,
                Health = config.bossHealth,
                MaxHealth = config.bossHealth,
                Position = position,
                Radius = config.bossRadius,
            };
            enemies.Add(Boss);
            Emit("boss_spawn", EnemyKind.Boss.ToString(), Boss.Id, config.bossHealth, position);
        }

        /// <summary>Called when an enemy dies, so the run can end the moment the boss falls.</summary>
        private void OnEnemyKilled(Enemy enemy)
        {
            if (enemy != Boss) return;
            Boss = null;
            BossDefeated = true;
            State = RunState.Won;
            Emit("run_end", "boss_down", enemy.Id, Kills);
        }
    }
}
