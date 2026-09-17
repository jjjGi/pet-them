using System;
using System.Collections.Generic;

namespace PetThem.Combat
{
    /// <summary>A drone circling the player. It copies the weapon the run started with.</summary>
    public sealed class Drone
    {
        public int Index { get; internal set; }
        public Vec2 Position { get; internal set; }
        /// <summary>Where it last shot, so Unity can draw the beam or the muzzle flash.</summary>
        public Vec2 LastTarget { get; internal set; }
        /// <summary>True on the step it fired, for one frame of effect.</summary>
        public bool Fired { get; internal set; }
    }

    /// <summary>An orb circling the player that hurts what it brushes past.</summary>
    public sealed class Orb
    {
        public int Index { get; internal set; }
        public Vec2 Position { get; internal set; }
    }

    public sealed partial class CombatWorld
    {
        private readonly List<Drone> drones = new List<Drone>();
        private readonly List<Orb> orbs = new List<Orb>();
        private readonly IReadOnlyList<Drone> droneView;
        private readonly IReadOnlyList<Orb> orbView;
        private readonly Dictionary<int, int> orbRecovery = new Dictionary<int, int>();
        private uint effectRandom;
        private int nextDrone, nextRegen;
        private bool resolvingBlast;

        public IReadOnlyList<Drone> Drones => droneView;
        public IReadOnlyList<Orb> Orbs => orbView;

        /// <summary>True while the last damage dealt was a critical hit, for the effect layer.</summary>
        public bool LastHitWasCritical { get; private set; }

        private int DroneCount => UpgradeRank(UpgradeId.Drone);
        private int OrbCount => UpgradeRank(UpgradeId.Orbit);

        // One buffer per place that walks the enemies while dealing damage. They cannot share a
        // buffer: a blast happens inside another loop's Damage call and would overwrite it.
        private readonly List<Enemy> weaponScan = new List<Enemy>();
        private readonly List<Enemy> orbScan = new List<Enemy>();
        private readonly List<Enemy> blastScan = new List<Enemy>();
        private readonly List<Enemy> contactScan = new List<Enemy>();

        /// <summary>
        /// Fills a buffer with the enemies to walk while dealing damage.
        /// </summary>
        /// <remarks>
        /// Damaging one enemy used to remove at most that one, so walking the live list backwards
        /// was safe. A blast kills several at once, which invalidates the index. Callers walk a
        /// copy instead and skip anything already dead, since a corpse is gone from the world but
        /// still sitting in the copy.
        /// </remarks>
        private List<Enemy> ScanEnemies(List<Enemy> buffer)
        {
            buffer.Clear();
            buffer.AddRange(enemies);
            return buffer;
        }

        private static bool Alive(Enemy enemy) => enemy.Health > 0;

        /// <summary>Rebuilds the ring of drones and orbs after one of them is upgraded.</summary>
        private void RefreshCompanions()
        {
            while (drones.Count < DroneCount) drones.Add(new Drone { Index = drones.Count });
            while (orbs.Count < OrbCount) orbs.Add(new Orb { Index = orbs.Count });
        }

        private void StepCompanions()
        {
            MoveCompanions();
            FireDrones();
            SweepOrbs();
            Regenerate();
        }

        private void MoveCompanions()
        {
            for (int i = 0; i < drones.Count; i++)
            {
                // Spread evenly around the player and drift the opposite way to the orbs, so the
                // two never sit on top of each other.
                float angle = -Time * 1.1f + (float)(Math.PI * 2 * i / Math.Max(1, drones.Count));
                drones[i].Position = Position + new Vec2(
                    (float)Math.Cos(angle) * config.droneOrbit,
                    (float)Math.Sin(angle) * config.droneOrbit * 0.72f);
                drones[i].Fired = false;
            }
            for (int i = 0; i < orbs.Count; i++)
            {
                float angle = Time * config.orbitSpeed + (float)(Math.PI * 2 * i / Math.Max(1, orbs.Count));
                orbs[i].Position = Position + new Vec2(
                    (float)Math.Cos(angle) * config.orbitRadius,
                    (float)Math.Sin(angle) * config.orbitRadius * 0.78f);
            }
        }

        /// <summary>
        /// Drones fire the weapon the run started with, at a share of its damage. More drones fire
        /// more often rather than all at once, so the rhythm stays readable.
        /// </summary>
        private void FireDrones()
        {
            if (drones.Count == 0 || Tick < nextDrone) return;
            Enemy target = Nearest(config.droneRange);
            if (target == null) return;

            nextDrone = Tick + Math.Max(1, Frames(config.droneInterval) / drones.Count);
            Drone drone = drones[Tick % drones.Count];
            drone.Fired = true;
            drone.LastTarget = target.Position;

            float damage = WeaponBaseDamage() * config.droneShare;
            Emit("attack", "drone", target.Id, damage, drone.Position);

            if (Weapon == WeaponId.Arrow)
            {
                // The arrow drone launches a real arrow, so it pierces like the player's does.
                var arrow = new Projectile
                {
                    Id = nextProjectileId++,
                    Position = drone.Position,
                    Velocity = (target.Position - drone.Position).Normalized * config.arrowSpeed,
                    Damage = damage,
                    PierceLeft = config.arrowPierce,
                };
                projectiles.Add(arrow);
                return;
            }
            Damage(target, damage, "drone");
        }

        /// <summary>The weapon's own damage, so a drone scales with the weapon upgrades taken.</summary>
        private float WeaponBaseDamage()
        {
            switch (Weapon)
            {
                case WeaponId.Arrow: return config.arrowDamage;
                // Per shot rather than per second, so a beam drone is not worth ten of the others.
                case WeaponId.Laser: return config.laserDamagePerSecond * config.droneInterval * 0.5f;
                default: return config.punchDamage;
            }
        }

        private void SweepOrbs()
        {
            if (orbs.Count == 0) return;
            float damage = config.orbitDamage * (1 + (OrbCount - 1) * 0.15f);
            foreach (Orb orb in orbs)
            {
                foreach (Enemy enemy in ScanEnemies(orbScan))
                {
                    if (!Alive(enemy)) continue;
                    if ((enemy.Position - orb.Position).Length > config.orbitHitRadius + enemy.Radius) continue;
                    // One enemy cannot be ground down by the same orbs every single step.
                    if (orbRecovery.TryGetValue(enemy.Id, out int until) && Tick < until) continue;
                    orbRecovery[enemy.Id] = Tick + Frames(config.orbitRecovery);
                    Emit("attack", "orbit", enemy.Id, damage, orb.Position);
                    Damage(enemy, damage, "orbit");
                }
            }
        }

        private void Regenerate()
        {
            if (UpgradeRank(UpgradeId.Regen) == 0 || Tick < nextRegen || Health >= MaxHealth) return;
            nextRegen = Tick + Frames(1);
            float amount = config.regenPerSecond * UpgradeRank(UpgradeId.Regen);
            float healed = Math.Min(amount, MaxHealth - Health);
            if (healed <= 0) return;
            Health += healed;
            Emit("heal", "regen", 0, healed, Position);
        }

        /// <summary>
        /// Rolls a critical hit. Uses its own random stream so taking the upgrade cannot shift
        /// where enemies spawn or which cards come up.
        /// </summary>
        private float ApplyCrit(float amount)
        {
            LastHitWasCritical = false;
            int rank = UpgradeRank(UpgradeId.Crit);
            if (rank == 0) return amount;

            effectRandom ^= effectRandom << 13;
            effectRandom ^= effectRandom >> 17;
            effectRandom ^= effectRandom << 5;
            float roll = (effectRandom & 0x00ffffff) / 16777216f;
            if (roll >= Math.Min(0.75f, config.critChance * rank)) return amount;

            LastHitWasCritical = true;
            return amount * config.critMultiplier;
        }

        /// <summary>
        /// Everything that goes off because an enemy died. Blast cannot start another blast, so a
        /// packed arena cannot chain into clearing itself.
        /// </summary>
        private void OnKillEffects(Enemy dead)
        {
            int lifesteal = UpgradeRank(UpgradeId.Lifesteal);
            if (lifesteal > 0 && Health < MaxHealth)
            {
                float healed = Math.Min(config.lifestealPerKill * lifesteal, MaxHealth - Health);
                if (healed > 0) { Health += healed; Emit("heal", "lifesteal", dead.Id, healed, Position); }
            }

            int blast = UpgradeRank(UpgradeId.Blast);
            if (blast == 0 || resolvingBlast) return;

            resolvingBlast = true;
            float damage = config.blastDamage * blast;
            float radius = config.blastRadius;
            Emit("attack", "blast", dead.Id, damage, dead.Position);
            foreach (Enemy enemy in ScanEnemies(blastScan))
            {
                if (!Alive(enemy)) continue;
                if ((enemy.Position - dead.Position).Length <= radius + enemy.Radius)
                    Damage(enemy, damage, "blast");
            }
            resolvingBlast = false;
        }

        /// <summary>Hurts whatever just touched the player, if thorns were taken.</summary>
        private void ApplyThorns(Enemy attacker)
        {
            int rank = UpgradeRank(UpgradeId.Thorns);
            if (rank == 0) return;
            Emit("attack", "thorns", attacker.Id, config.thornsDamage * rank, Position);
            Damage(attacker, config.thornsDamage * rank, "thorns");
        }
    }
}
