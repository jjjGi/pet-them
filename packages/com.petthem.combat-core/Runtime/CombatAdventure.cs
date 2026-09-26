using System;
using System.Collections.Generic;

namespace PetThem.Combat
{
    public sealed partial class CombatWorld
    {
        public const int EvolutionRank = 2;
        public const int TreasureLimit = 3;
        public const float EvolutionRadius = 3.4f;
        public const float EvolutionRange = 8;
        public bool AdventureEnabled => config.adventureEnabled && config.endlessWorld && ProgressionEnabled;
        public bool TreasureAvailable { get; private set; }
        public Vec2 TreasurePosition { get; private set; }
        public int TreasuresOpened { get; private set; }
        public bool Evolved { get; private set; }
        public UpgradeId EvolutionWeaponUpgrade => Weapon == WeaponId.Punch ? UpgradeId.PunchPower :
            Weapon == WeaponId.Arrow ? UpgradeId.ArrowPower : UpgradeId.LaserPower;
        private int nextTreasure, nextEvolution;
        private readonly List<Enemy> evolutionScan = new List<Enemy>();

        private void InitializeAdventure()
        {
            if (AdventureEnabled) PlaceTreasure();
        }

        private void PlaceTreasure()
        {
            // A separate coordinate rule never consumes combat or upgrade randomness.
            int direction = (int)(((uint)Seed + (uint)TreasuresOpened) % 4);
            Vec2 offset = direction == 0 ? new Vec2(12, 0) : direction == 1 ? new Vec2(0, 12) :
                direction == 2 ? new Vec2(-12, 0) : new Vec2(0, -12);
            TreasurePosition = Position + offset;
            TreasureAvailable = true;
        }

        private void StepAdventure()
        {
            if (!AdventureEnabled) return;
            if (!TreasureAvailable && TreasuresOpened < TreasureLimit && Tick >= nextTreasure) PlaceTreasure();
            if (TreasureAvailable && (Position - TreasurePosition).Length <= 1.1f)
            {
                TreasureAvailable = false;
                TreasuresOpened++;
                nextTreasure = Tick + Frames(20);
                RerollsLeft++;
                float healed = Math.Min(MaxHealth - Health, MaxHealth * .15f);
                Health += healed;
                if (Level < MaxLevel) Experience = Math.Max(Experience, ExperienceToNextLevel);
                Emit("treasure", "chest", 0, TreasuresOpened, TreasurePosition);
                if (healed > 0) Emit("heal", "chest", 0, healed, Position);
            }
            if (!Evolved || Tick < nextEvolution) return;
            Enemy target = Nearest(Weapon == WeaponId.Punch ? EvolutionRadius : EvolutionRange);
            if (target == null) return;
            Vec2 direction = (target.Position - Position).Normalized;
            if (direction.Length < .5f) direction = Facing;
            nextEvolution = Tick + Frames(3);
            Emit("evolution_attack", Weapon.ToString(), 0, 0, direction);
            float damage = PetDamage * (Pet == PetId.Mochi ? 2 : 1.5f);
            foreach (Enemy enemy in ScanEnemies(evolutionScan))
            {
                if (State != RunState.Playing) break;
                if (!Alive(enemy)) continue;
                Vec2 delta = enemy.Position - Position;
                bool hit = Weapon == WeaponId.Punch ? delta.Length <= EvolutionRadius + enemy.Radius :
                    EvolutionRay(delta, direction, enemy.Radius, Weapon == WeaponId.Laser ? .65f : .22f);
                if (Weapon == WeaponId.Arrow)
                {
                    var side = new Vec2(-direction.y, direction.x);
                    hit |= EvolutionRay(delta, (direction + side * .4f).Normalized, enemy.Radius, .22f) ||
                           EvolutionRay(delta, (direction - side * .4f).Normalized, enemy.Radius, .22f);
                }
                if (!hit) continue;
                if (Pet == PetId.Bori)
                {
                    enemy.SlowUntilTick = Tick + Frames(config.petSlowSeconds);
                    enemy.Slowed = true;
                    Emit("slow", "evolution", enemy.Id, config.petSlowSeconds, enemy.Position);
                }
                Damage(enemy, damage, "evolution");
            }
            if (State == RunState.Playing && Pet == PetId.Coco)
            {
                float healed = Math.Min(MaxHealth - Health, config.petHealAmount);
                Health += healed;
                if (healed > 0) Emit("heal", "evolution", 0, healed, Position);
            }
        }

        private static bool EvolutionRay(Vec2 delta, Vec2 direction, float radius, float halfWidth)
        {
            float along = Vec2.Dot(delta, direction);
            return along >= 0 && (delta - direction * Math.Min(along, EvolutionRange)).Length <= radius + halfWidth;
        }

        private void TryEvolve()
        {
            if (!AdventureEnabled || Evolved || UpgradeRank(EvolutionWeaponUpgrade) < EvolutionRank ||
                UpgradeRank(UpgradeId.PetPower) < EvolutionRank) return;
            Evolved = true;
            Emit("evolution", Weapon + "_" + Pet, 0, 1, Position);
        }
    }
}
