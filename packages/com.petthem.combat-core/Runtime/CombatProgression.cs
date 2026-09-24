using System;
using System.Collections.Generic;

namespace PetThem.Combat
{
    /// <summary>
    /// What kind of card an upgrade is, so the level-up screen can group and colour them.
    /// </summary>
    public enum UpgradeKind
    {
        /// <summary>Makes the chosen weapon better.</summary>
        Weapon,
        /// <summary>Makes the chosen pet better.</summary>
        Pet,
        /// <summary>Adds or grows something that fights alongside you.</summary>
        Friend,
        /// <summary>Fires off something when you hit, kill, or get hit.</summary>
        Trigger,
        /// <summary>Keeps you alive or moving.</summary>
        Body,
    }

    public enum UpgradeId
    {
        PunchPower, PunchReach,
        ArrowPower, ArrowPierce,
        LaserPower, LaserCooling,
        PetPower, PetHaste,
        PetReach, PetChill, PetGuard,
        MoveSpeed, Vitality, Heal,
        // Things that fight with you.
        Drone, Orbit,
        // Things that go off on an event.
        Crit, Blast, Lifesteal, Thorns, Repel,
        // Everything else.
        Regen, Greed,
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

        /// <summary>Re-rolls the player still has this run.</summary>
        public int RerollsLeft { get; private set; }

        /// <summary>True when this upgrade cannot be taken any further.</summary>
        /// <remarks>
        /// The screen reads this to mark a card as the last of its kind, which is the one time a
        /// choice is worth making for the rank rather than for the effect.
        /// </remarks>
        public bool IsAtCap(UpgradeId id) => IsRankCapped(id) && UpgradeRank(id) >= 5;

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
            id == UpgradeId.PetReach || id == UpgradeId.PetChill || id == UpgradeId.PetGuard ||
            id == UpgradeId.Drone || id == UpgradeId.Orbit ||
            id == UpgradeId.Crit || id == UpgradeId.Blast || id == UpgradeId.Lifesteal ||
            id == UpgradeId.Thorns || id == UpgradeId.Repel ||
            id == UpgradeId.Regen || id == UpgradeId.Greed;

        /// <summary>
        /// Which family a card belongs to. The level-up screen groups by this, so a hand of three
        /// reads as a set of choices rather than a list of numbers.
        /// </summary>
        public static UpgradeKind KindOf(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.PunchPower:
                case UpgradeId.PunchReach:
                case UpgradeId.ArrowPower:
                case UpgradeId.ArrowPierce:
                case UpgradeId.LaserPower:
                case UpgradeId.LaserCooling: return UpgradeKind.Weapon;

                case UpgradeId.PetPower:
                case UpgradeId.PetHaste:
                case UpgradeId.PetReach:
                case UpgradeId.PetChill:
                case UpgradeId.PetGuard: return UpgradeKind.Pet;

                case UpgradeId.Drone:
                case UpgradeId.Orbit: return UpgradeKind.Friend;

                case UpgradeId.Crit:
                case UpgradeId.Blast:
                case UpgradeId.Lifesteal:
                case UpgradeId.Thorns:
                case UpgradeId.Repel: return UpgradeKind.Trigger;

                default: return UpgradeKind.Body;
            }
        }

        /// <summary>Every upgrade taken this run, with its rank. What the pause screen lists.</summary>
        public IReadOnlyList<UpgradeChoice> TakenUpgrades()
        {
            var taken = new List<UpgradeChoice>();
            foreach (UpgradeId id in Enum.GetValues(typeof(UpgradeId)))
            {
                int rank = UpgradeRank(id);
                if (rank > 0) taken.Add(new UpgradeChoice(id, rank));
            }
            taken.Sort((a, b) => b.Rank != a.Rank ? b.Rank - a.Rank : ((int)a.Id - (int)b.Id));
            return taken;
        }

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
            OfferUpgradeChoices();
        }

        /// <summary>
        /// Throws this level's three cards away and deals three more.
        /// </summary>
        /// <remarks>
        /// Costs a re-roll from a budget the run starts with, and nothing else: no experience, no
        /// level, no time -- combat is already stopped while the cards are up. A player who is
        /// offered three cards for a build they are not playing has been handed a wasted level,
        /// and that is a bad feeling to have designed in on purpose.
        ///
        /// It draws from the same stream the first deal used, because a re-roll is an upgrade roll.
        /// Giving it a stream of its own would make the spawns and the boss depend on whether
        /// somebody pressed a button, which is exactly what the separate streams exist to prevent.
        /// </remarks>
        public bool RerollUpgrades()
        {
            if (State != RunState.Playing || !HasUpgradeChoice || RerollsLeft <= 0) return false;
            events.Clear();
            RerollsLeft--;
            upgradeChoices.Clear();
            Emit("reroll", "player", 0, RerollsLeft);
            OfferUpgradeChoices();
            return true;
        }

        /// <summary>
        /// Adds a re-roll mid-run, for whatever the game decides is worth one.
        /// </summary>
        /// <remarks>
        /// Separate from the run's budget so the rule stays here rather than in the screen: the
        /// core owns how many re-rolls exist, and the caller owns what earns one. Nothing calls
        /// this yet; a rewarded advert is the intended earner and no advert exists.
        /// </remarks>
        public bool GrantReroll()
        {
            if (State != RunState.Playing) return false;
            RerollsLeft++;
            return true;
        }

        private void OfferUpgradeChoices()
        {
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
            return Apply(id);
        }

        /// <summary>
        /// Applies an upgrade without it having been offered. Only the checks use this, to reach a
        /// given upgrade at a given rank without playing until the cards happen to come up.
        /// </summary>
        internal bool Apply(UpgradeId id)
        {
            if (State != RunState.Playing) return false;
            Level++;
            upgradeRanks[id] = UpgradeRank(id) + 1;
            switch (id)
            {
                // Repel and the other event effects read their own rank where they fire, so
                // taking one changes nothing here.
                case UpgradeId.Repel: break;
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
                case UpgradeId.Greed:
                    config.coinsPerKill = baseConfig.coinsPerKill * (1 + config.greedBonus * UpgradeRank(id));
                    break;
                // Drone, Orbit, Crit, Blast, Lifesteal, Thorns and Regen read their own rank, so
                // taking one only has to rebuild the ring of friends around the player.
                case UpgradeId.Drone:
                case UpgradeId.Orbit: RefreshCompanions(); break;
            }
            Emit("level_up", id.ToString(), 0, Level);
            Emit("upgrade", id.ToString(), 0, UpgradeRank(id));
            upgradeChoices.Clear();
            PrepareUpgradeChoices(); // Keep XP earned by several kills in the same tick.
            return true;
        }

    }
}
