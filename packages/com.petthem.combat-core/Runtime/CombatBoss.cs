using System;

namespace PetThem.Combat
{
    /// <summary>What the boss is doing right now.</summary>
    public enum BossAction
    {
        /// <summary>Walking at the player, like any other enemy.</summary>
        Stalk,
        /// <summary>Standing still, winding up. The player can see which move is coming.</summary>
        Telegraph,
        /// <summary>Rushing in a straight line, hitting much harder than a walk into you.</summary>
        Charge,
        /// <summary>Slamming the ground: everything close by is hit at once.</summary>
        Slam,
        /// <summary>Calling in a ring of ordinary enemies.</summary>
        Summon,
    }

    public sealed partial class CombatWorld
    {
        private uint bossRandom;
        private int bossActionUntil, nextBossMove;
        private Vec2 bossHeading;

        /// <summary>What the boss is doing. Stalking when there is no boss.</summary>
        public BossAction BossState { get; private set; } = BossAction.Stalk;

        /// <summary>The move being wound up, or the one running. Unity draws the warning from this.</summary>
        public BossAction BossMove { get; private set; } = BossAction.Stalk;

        /// <summary>How far through the wind-up, from 0 to 1. Zero when not winding up.</summary>
        public float BossTelegraph { get; private set; }

        /// <summary>The direction a charge is aimed at, fixed when the wind-up started.</summary>
        public Vec2 BossHeading => bossHeading;

        /// <summary>True once the boss is hurt enough to stop pausing as long between moves.</summary>
        public bool BossEnraged =>
            Boss != null && Boss.Health <= Boss.MaxHealth * config.bossEnrageHealth;

        /// <summary>
        /// Runs the boss's own behaviour. Ordinary enemies only walk at the player; this is what
        /// makes the last minute a fight rather than more of the same.
        /// </summary>
        private void StepBoss()
        {
            if (Boss == null)
            {
                BossState = BossAction.Stalk;
                BossTelegraph = 0;
                return;
            }

            if (Tick < bossActionUntil)
            {
                if (BossState == BossAction.Telegraph)
                {
                    int frames = Frames(config.bossTelegraph);
                    BossTelegraph = Math.Min(1, 1 - (bossActionUntil - Tick) / (float)frames);
                }
                if (BossState == BossAction.Charge) ChargeStep();
                return;
            }

            // A move just ended, or none is running.
            if (BossState == BossAction.Telegraph) { Unleash(); return; }
            BossState = BossAction.Stalk;
            BossTelegraph = 0;

            if (Tick < nextBossMove) return;
            BeginTelegraph();
        }

        private void BeginTelegraph()
        {
            BossMove = PickMove();
            BossState = BossAction.Telegraph;
            BossTelegraph = 0;
            bossActionUntil = Tick + Frames(config.bossTelegraph);
            // Aimed when the wind-up starts, not when it lands, so stepping aside works.
            bossHeading = (Position - Boss.Position).Normalized;
            if (bossHeading.Length < 0.5f) bossHeading = new Vec2(1, 0);
            Emit("boss_telegraph", BossMove.ToString(), Boss.Id, config.bossTelegraph, Boss.Position);
        }

        private BossAction PickMove()
        {
            bossRandom ^= bossRandom << 13;
            bossRandom ^= bossRandom >> 17;
            bossRandom ^= bossRandom << 5;
            float roll = (bossRandom & 0x00ffffff) / 16777216f;

            // Summoning is skipped when the arena is already full, or the move would do nothing.
            bool canSummon = config.bossSummonCount > 0 && enemies.Count < config.maxEnemies;
            if (roll < 0.42f) return BossAction.Charge;
            if (roll < 0.75f) return BossAction.Slam;
            return canSummon ? BossAction.Summon : BossAction.Charge;
        }

        private void Unleash()
        {
            float interval = config.bossMoveInterval * (BossEnraged ? config.bossEnrageHaste : 1);
            nextBossMove = Tick + Frames(interval);
            BossTelegraph = 1;

            switch (BossMove)
            {
                case BossAction.Charge:
                    BossState = BossAction.Charge;
                    bossActionUntil = Tick + Frames(config.bossChargeSeconds);
                    Emit("boss_move", nameof(BossAction.Charge), Boss.Id, config.bossChargeDamage, bossHeading);
                    break;

                case BossAction.Slam:
                    BossState = BossAction.Stalk;
                    bossActionUntil = Tick;
                    Slam();
                    break;

                default:
                    BossState = BossAction.Stalk;
                    bossActionUntil = Tick;
                    Summon();
                    break;
            }
        }

        private void ChargeStep()
        {
            Boss.Position = Clamp(Boss.Position + bossHeading * (config.bossChargeSpeed * StepSeconds),
                config.bossRadius * 0.5f);
            if ((Boss.Position - Position).Length > config.bossRadius + 0.35f) return;

            // A charge that connects hurts far more than being walked into, and it ends there.
            bossActionUntil = Tick;
            BossState = BossAction.Stalk;
            HurtPlayer(config.bossChargeDamage, nameof(BossAction.Charge));
        }

        private void Slam()
        {
            Emit("boss_move", nameof(BossAction.Slam), Boss.Id, config.bossSlamDamage, Boss.Position);
            if ((Position - Boss.Position).Length <= config.bossSlamRadius)
                HurtPlayer(config.bossSlamDamage, nameof(BossAction.Slam));
        }

        private void Summon()
        {
            Emit("boss_move", nameof(BossAction.Summon), Boss.Id, config.bossSummonCount, Boss.Position);
            for (int i = 0; i < config.bossSummonCount && enemies.Count < config.maxEnemies; i++)
            {
                float angle = (float)(Math.PI * 2 * i / Math.Max(1, config.bossSummonCount));
                Vec2 at = Clamp(Boss.Position + new Vec2(
                    (float)Math.Cos(angle), (float)Math.Sin(angle)) * (config.bossRadius + 1.1f), 0.4f);
                SpawnAt(EnemyKind.Runner, at, "boss");
            }
        }

        /// <summary>
        /// Damage from a boss move. Goes through the support pet's guard like contact damage does,
        /// and pushes the contact timer out so a slam is not instantly followed by a free bite.
        /// </summary>
        private void HurtPlayer(float amount, string source)
        {
            if (State != RunState.Playing) return;
            float incoming = amount * (1 - GuardReduction);
            float actual = Math.Min(Health, incoming);
            Health -= actual;
            nextHurt = Tick + Frames(config.hurtCooldown);
            Emit("hurt", source, Boss?.Id ?? 0, actual, Position);
            if (Health <= 0)
            {
                State = RunState.Lost;
                Emit("run_end", "death", Boss?.Id ?? 0, Kills);
            }
        }
    }
}
