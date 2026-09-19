using System;

namespace PetThem.Combat
{
    /// <summary>
    /// The one condition a run is played under. Rolled when the run starts and fixed for its whole
    /// length.
    /// </summary>
    /// <remarks>
    /// Without this every run is the same three minutes: the same enemy mix arriving at the same
    /// rate, differing only in which upgrade cards happen to come up. A twist changes what the run
    /// asks of the player before they have chosen anything, and pays for the trouble in coins, so
    /// the harder conditions are the ones worth hoping for rather than the ones worth restarting.
    /// </remarks>
    public enum TwistId
    {
        /// <summary>Nothing changes. The run the game has always had.</summary>
        Calm,
        /// <summary>Many more enemies, each of them frailer.</summary>
        Swarm,
        /// <summary>The same crowd, moving much faster.</summary>
        Swift,
        /// <summary>Fewer enemies, each taking far longer to bring down.</summary>
        Armored,
        /// <summary>The boss arrives while the build is still half-finished.</summary>
        EarlyBoss,
        /// <summary>Everything hits harder. The run is survivable but unforgiving.</summary>
        Harsh,
    }

    public sealed partial class CombatWorld
    {
        private uint twistRandom;

        /// <summary>The condition this run is played under. Fixed for the life of the run.</summary>
        public TwistId Twist { get; private set; }

        /// <summary>
        /// Rolls the run's twist and folds it into the config, on its own random stream so that
        /// adding or reordering twists never shifts spawns, cards, crits or boss moves for a seed.
        /// </summary>
        private TwistId RollTwist(int seed, bool enabled)
        {
            twistRandom = unchecked((uint)seed) ^ 0x3c6ef372;
            if (twistRandom == 0) twistRandom = 0x85ebca6b;
            if (!enabled) return TwistId.Calm;
            twistRandom ^= twistRandom << 13;
            twistRandom ^= twistRandom >> 17;
            twistRandom ^= twistRandom << 5;
            var all = (TwistId[])Enum.GetValues(typeof(TwistId));
            return all[(int)((twistRandom & 0x00ffffff) % (uint)all.Length)];
        }

        /// <summary>
        /// Multiplies the run's numbers to match its twist. Coin rates move with the difficulty, so
        /// a harder run is worth more without needing a separate reward path to keep in step.
        /// </summary>
        private static void ApplyTwist(TwistId twist, BalanceConfig config)
        {
            switch (twist)
            {
                case TwistId.Swarm:
                    config.spawnInterval *= 0.62f;
                    config.minSpawnInterval *= 0.62f;
                    ScaleEnemyHealth(config, 0.82f);
                    ScaleCoins(config, 1.25f);
                    break;

                case TwistId.Swift:
                    config.gruntSpeed *= 1.28f;
                    config.runnerSpeed *= 1.28f;
                    config.bruteSpeed *= 1.28f;
                    ScaleCoins(config, 1.25f);
                    break;

                case TwistId.Armored:
                    ScaleEnemyHealth(config, 1.4f);
                    config.spawnInterval *= 1.18f;
                    config.minSpawnInterval *= 1.18f;
                    ScaleCoins(config, 1.3f);
                    break;

                case TwistId.EarlyBoss:
                    // Early enough to interrupt the build, not so early that there is no build.
                    config.bossSpawnTime *= 0.6f;
                    ScaleCoins(config, 1.35f);
                    break;

                case TwistId.Harsh:
                    config.contactDamage *= 1.35f;
                    config.bruteContactDamage *= 1.35f;
                    config.bossContactDamage *= 1.35f;
                    config.bossChargeDamage *= 1.35f;
                    config.bossSlamDamage *= 1.35f;
                    ScaleCoins(config, 1.4f);
                    break;
            }
        }

        private static void ScaleEnemyHealth(BalanceConfig config, float by)
        {
            config.gruntHealth *= by;
            config.runnerHealth *= by;
            config.bruteHealth *= by;
        }

        private static void ScaleCoins(BalanceConfig config, float by)
        {
            config.coinsPerKill *= by;
            config.coinsPerBossKill *= by;
            config.coinsPerSecondSurvived *= by;
        }
    }
}
