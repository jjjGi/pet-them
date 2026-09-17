using System;

namespace PetThem.Combat
{
    /// <summary>
    /// The pet fights on its own, so its role is what the player is choosing: more damage,
    /// crowd control, or staying alive.
    /// </summary>
    public enum PetId
    {
        /// <summary>Attacker. Full damage on the nearest enemy.</summary>
        Mochi,
        /// <summary>Controller. Less damage, but slows and shoves what it hits.</summary>
        Bori,
        /// <summary>Support. Least damage, heals over time and softens contact damage.</summary>
        Coco,
    }

    public sealed partial class CombatWorld
    {
        private int nextPetHeal;

        public PetId Pet { get; }

        /// <summary>How much of the contact damage the support pet removes, or 0 for the others.</summary>
        public float GuardReduction => Pet == PetId.Coco ? config.petGuardReduction : 0;

        /// <summary>Seconds until the support pet's next heal, or 0 for the others.</summary>
        public float SecondsToPetHeal =>
            Pet == PetId.Coco ? Math.Max(0, (nextPetHeal - Tick) * StepSeconds) : 0;

        public float PetDamage => config.petDamage * PetDamageShare(Pet);

        private float PetDamageShare(PetId pet)
        {
            switch (pet)
            {
                case PetId.Bori: return config.petControlDamageShare;
                case PetId.Coco: return config.petSupportDamageShare;
                default: return 1;
            }
        }

        private void StepPet()
        {
            HealFromSupportPet();
            if (Tick < nextPet) return;
            Enemy target = Nearest(config.petRange);
            if (target == null) return;

            nextPet = Tick + Frames(config.petCooldown);
            Emit("attack", "pet", target.Id, PetDamage, target.Position);

            if (Pet == PetId.Bori)
            {
                // Shove first: a dead enemy cannot be pushed, and Damage may remove it.
                Vec2 away = (target.Position - Position).Normalized;
                if (away.Length < 0.5f) away = new Vec2(1, 0);
                target.Position = Clamp(target.Position + away * config.petControlKnockback, 0.35f);
                target.SlowUntilTick = Tick + Frames(config.petSlowSeconds);
                target.Slowed = true;
                Emit("slow", "pet", target.Id, config.petSlowSeconds, target.Position);
            }

            Damage(target, PetDamage, "pet");
        }

        private void HealFromSupportPet()
        {
            if (Pet != PetId.Coco || Tick < nextPetHeal) return;
            nextPetHeal = Tick + Frames(config.petHealInterval);
            float healed = Math.Min(config.petHealAmount, MaxHealth - Health);
            if (healed <= 0) return;
            Health += healed;
            Emit("heal", "pet", 0, healed, Position);
        }

        /// <summary>Movement speed for this enemy right now, including the control pet's slow.</summary>
        private float SpeedOf(Enemy enemy)
        {
            if (Tick >= enemy.SlowUntilTick)
            {
                enemy.Slowed = false;
                return SpeedOf(enemy.Kind);
            }
            return SpeedOf(enemy.Kind) * config.petSlowFactor;
        }
    }
}
