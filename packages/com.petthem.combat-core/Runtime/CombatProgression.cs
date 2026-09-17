using System;
using System.Collections.Generic;

namespace PetThem.Combat
{
    public enum UpgradeId
    {
        PunchPower, PunchReach,
        ArrowPower, ArrowPierce,
        LaserPower, LaserCooling,
        PetPower, PetHaste,
        PetReach, PetChill, PetGuard,
        MoveSpeed, Vitality, Heal,
    }

    /// <summary>
    /// One card on the level-up screen: which upgrade, and which rank it would become.
    /// </summary>
    /// <remarks>
    /// Carries no display text. The wording belongs to whatever is showing it, so the shared
    /// combat rules stay free of a user-facing language and the simulator does not link against
    /// strings it will never print.
    /// </remarks>
    public sealed class UpgradeChoice
    {
        public UpgradeId Id { get; }
        public int Rank { get; }
        internal UpgradeChoice(UpgradeId id, int rank) { Id = id; Rank = rank; }
    }

    public sealed partial class CombatWorld
    {
        public const int MaxLevel = 30;
        private readonly BalanceConfig baseConfig;
        private readonly Dictionary<UpgradeId, int> upgradeRanks = new Dictionary<UpgradeId, int>();
        private readonly List<UpgradeChoice> upgradeChoices = new List<UpgradeChoice>();
        private uint progressionRandom;
        public bool ProgressionEnabled { get; }
        public int Level { get; private set; } = 1;
        public int Experience { get; private set; }
        public int ExperienceToNextLevel => 5 + (Level - 1) * 3;
        public bool HasUpgradeChoice => upgradeChoices.Count > 0;
        public IReadOnlyList<UpgradeChoice> UpgradeChoices => upgradeChoices.AsReadOnly();
        public float MaxHealth => config.playerHealth;
        public float PunchRange => config.punchRange;
        public int UpgradeRank(UpgradeId id) => upgradeRanks.TryGetValue(id, out int rank) ? rank : 0;

        /// <summary>Weapon upgrades belong to the weapon this run started with; the rest are shared.</summary>
        public bool AppliesToWeapon(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.PunchPower:
                case UpgradeId.PunchReach: return Weapon == WeaponId.Punch;
                case UpgradeId.ArrowPower:
                case UpgradeId.ArrowPierce: return Weapon == WeaponId.Arrow;
                case UpgradeId.LaserPower:
                case UpgradeId.LaserCooling: return Weapon == WeaponId.Laser;
                // One upgrade belongs to each pet's own role.
                case UpgradeId.PetReach: return Pet == PetId.Mochi;
                case UpgradeId.PetChill: return Pet == PetId.Bori;
                case UpgradeId.PetGuard: return Pet == PetId.Coco;
                default: return true;
            }
        }

        /// <summary>Upgrades that would break the feel of the run if stacked without limit.</summary>
        public static bool IsRankCapped(UpgradeId id) =>
            id == UpgradeId.PunchReach || id == UpgradeId.ArrowPierce || id == UpgradeId.LaserCooling ||
            id == UpgradeId.PetHaste || id == UpgradeId.MoveSpeed ||
            id == UpgradeId.PetReach || id == UpgradeId.PetChill || id == UpgradeId.PetGuard;

        private void AwardExperience()
        {
            if (!ProgressionEnabled || Level >= MaxLevel) return;
            Experience++;
            Emit("xp", "kill", 0, 1, Position);
        }

        private void PrepareUpgradeChoices()
        {
            if (!ProgressionEnabled || State != RunState.Playing || HasUpgradeChoice || Level >= MaxLevel ||
                Experience < ExperienceToNextLevel) return;
            var pool = new List<UpgradeId>();
            foreach (UpgradeId id in Enum.GetValues(typeof(UpgradeId)))
            {
                // Only the weapon this run started with can be upgraded.
                if (!AppliesToWeapon(id)) continue;
                if (IsRankCapped(id) && UpgradeRank(id) >= 5) continue;
                if (id == UpgradeId.Heal && Health >= MaxHealth) continue;
                pool.Add(id);
            }
            for (int i = 0; i < 3; i++)
            {
                // The pool is built to hold at least three, but never divide by zero if that changes.
                if (pool.Count == 0) break;
                // A separate stream keeps upgrade rolls from changing enemy spawn randomness.
                progressionRandom ^= progressionRandom << 13;
                progressionRandom ^= progressionRandom >> 17;
                progressionRandom ^= progressionRandom << 5;
                int index = (int)(progressionRandom % (uint)pool.Count);
                UpgradeId id = pool[index]; pool.RemoveAt(index);
                upgradeChoices.Add(new UpgradeChoice(id, UpgradeRank(id) + 1));
                Emit("upgrade_offer", id.ToString(), 0, UpgradeRank(id) + 1);
            }
        }

        // Like Step, this replaces Events. Callers must record each command's events once.
        public bool ChooseUpgrade(UpgradeId id)
        {
            if (State != RunState.Playing || !upgradeChoices.Exists(choice => choice.Id == id)) return false;
            events.Clear();
            Experience -= ExperienceToNextLevel;
            Level++;
            upgradeRanks[id] = UpgradeRank(id) + 1;
            switch (id)
            {
                case UpgradeId.PunchPower: config.punchDamage += baseConfig.punchDamage * .2f; break;
                case UpgradeId.PunchReach: config.punchRange += baseConfig.punchRange * .1f; break;
                case UpgradeId.ArrowPower: config.arrowDamage += baseConfig.arrowDamage * .22f; break;
                case UpgradeId.ArrowPierce: config.arrowPierce += 1; break;
                case UpgradeId.LaserPower:
                    config.laserDamagePerSecond += baseConfig.laserDamagePerSecond * .18f; break;
                case UpgradeId.LaserCooling:
                    config.laserHeatPerSecond = baseConfig.laserHeatPerSecond * (1 - .1f * UpgradeRank(id));
                    config.laserCoolPerSecond += baseConfig.laserCoolPerSecond * .12f;
                    break;
                case UpgradeId.PetPower: config.petDamage += baseConfig.petDamage * .25f; break;
                case UpgradeId.PetHaste: config.petCooldown = baseConfig.petCooldown * (1 - .1f * UpgradeRank(id)); break;
                case UpgradeId.PetReach: config.petRange += baseConfig.petRange * .12f; break;
                case UpgradeId.PetChill:
                    config.petSlowFactor = Math.Max(.15f, baseConfig.petSlowFactor - .06f * UpgradeRank(id));
                    config.petSlowSeconds += baseConfig.petSlowSeconds * .15f;
                    break;
                case UpgradeId.PetGuard:
                    config.petGuardReduction = Math.Min(.7f, baseConfig.petGuardReduction + .06f * UpgradeRank(id));
                    config.petHealAmount += baseConfig.petHealAmount * .25f;
                    break;
                case UpgradeId.MoveSpeed: config.playerSpeed += baseConfig.playerSpeed * .08f; break;
                case UpgradeId.Vitality:
                    config.playerHealth += baseConfig.playerHealth * .2f;
                    Health = Math.Min(MaxHealth, Health + baseConfig.playerHealth * .2f);
                    break;
                case UpgradeId.Heal: Health = Math.Min(MaxHealth, Health + MaxHealth * .4f); break;
            }
            Emit("level_up", id.ToString(), 0, Level);
            Emit("upgrade", id.ToString(), 0, UpgradeRank(id));
            upgradeChoices.Clear();
            PrepareUpgradeChoices(); // Keep XP earned by several kills in the same tick.
            return true;
        }

    }
}
