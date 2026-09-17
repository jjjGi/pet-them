using System;
using System.Collections.Generic;

namespace PetThem.Combat
{
    /// <summary>
    /// What the player keeps between runs: coins and the pets they have unlocked.
    /// </summary>
    /// <remarks>
    /// Deliberately free of UnityEngine so the rules here can be checked without the editor.
    /// Unity owns only the reading and writing of the file.
    ///
    /// A save file is edited by hand or damaged more often than anyone plans for, so nothing here
    /// throws on bad data. <see cref="Normalize"/> repairs it and reports what it changed, because
    /// losing a save is worse than losing whatever the corrupt value claimed.
    /// </remarks>
    [Serializable]
    public sealed class PlayerProfile
    {
        public const int CurrentSchemaVersion = 1;

        /// <summary>The pet every player starts with. It can never be locked.</summary>
        public const PetId StarterPet = PetId.Mochi;

        public int schemaVersion = CurrentSchemaVersion;
        public int coins;
        public int runsFinished;
        public string[] unlockedPets = { StarterPet.ToString() };

        public static int PriceOf(PetId pet, BalanceConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            switch (pet)
            {
                case PetId.Bori: return (int)config.boriPrice;
                case PetId.Coco: return (int)config.cocoPrice;
                default: return 0;
            }
        }

        public bool IsUnlocked(PetId pet)
        {
            if (pet == StarterPet) return true;
            if (unlockedPets == null) return false;
            foreach (string name in unlockedPets)
                if (string.Equals(name, pet.ToString(), StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Adds a run's coins. Ignores a negative amount rather than taking coins away.</summary>
        public void AddRunReward(int amount)
        {
            if (amount <= 0) return;
            coins = (int)Math.Min(int.MaxValue, (long)coins + amount);
            runsFinished++;
        }

        /// <summary>
        /// Buys a pet. Returns false and changes nothing when it is already owned or unaffordable,
        /// so the caller never has to undo a partial purchase.
        /// </summary>
        public bool Unlock(PetId pet, BalanceConfig config)
        {
            if (IsUnlocked(pet)) return false;
            int price = PriceOf(pet, config);
            if (price <= 0 || coins < price) return false;

            var owned = new List<string>(unlockedPets ?? Array.Empty<string>()) { pet.ToString() };
            unlockedPets = owned.ToArray();
            coins -= price;
            return true;
        }

        /// <summary>
        /// Repairs anything a damaged or hand-edited file could contain and lists what it changed.
        /// An empty list means the file was already sound.
        /// </summary>
        public IReadOnlyList<string> Normalize()
        {
            var repairs = new List<string>();

            if (coins < 0) { repairs.Add($"coins was {coins}, reset to 0"); coins = 0; }
            if (runsFinished < 0) { repairs.Add($"runsFinished was {runsFinished}, reset to 0"); runsFinished = 0; }
            if (schemaVersion < 1 || schemaVersion > CurrentSchemaVersion)
            {
                repairs.Add($"schemaVersion was {schemaVersion}, treated as {CurrentSchemaVersion}");
                schemaVersion = CurrentSchemaVersion;
            }

            var kept = new List<string>();
            foreach (string name in unlockedPets ?? Array.Empty<string>())
            {
                if (!Enum.TryParse(name, false, out PetId parsed))
                {
                    repairs.Add($"dropped unknown pet '{name}'");
                    continue;
                }
                string canonical = parsed.ToString();
                if (kept.Contains(canonical)) { repairs.Add($"dropped duplicate pet '{canonical}'"); continue; }
                kept.Add(canonical);
            }
            if (!kept.Contains(StarterPet.ToString()))
            {
                repairs.Add($"{StarterPet} was missing and was restored");
                kept.Insert(0, StarterPet.ToString());
            }
            if (unlockedPets == null || kept.Count != unlockedPets.Length) unlockedPets = kept.ToArray();
            else unlockedPets = kept.ToArray();

            return repairs;
        }
    }
}
