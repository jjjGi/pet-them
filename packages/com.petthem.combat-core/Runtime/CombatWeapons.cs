using System;
using System.Collections.Generic;

namespace PetThem.Combat
{
    public enum WeaponId { Punch, Arrow, Laser }

    /// <summary>An arrow in flight. Owned by CombatWorld; Unity only reads it to draw.</summary>
    public sealed class Projectile
    {
        internal float Travelled;
        public int Id { get; internal set; }
        public Vec2 Position { get; internal set; }
        public Vec2 Velocity { get; internal set; }
        public float Damage { get; internal set; }
        public int PierceLeft { get; internal set; }
        internal readonly List<int> Hit = new List<int>();
    }

    public sealed partial class CombatWorld
    {
        private readonly List<Projectile> projectiles = new List<Projectile>();
        private readonly IReadOnlyList<Projectile> projectileView;
        private int nextProjectileId = 1, nextArrow;
        private bool heldLastStep;

        public WeaponId Weapon { get; }
        public IReadOnlyList<Projectile> Projectiles => projectileView;

        /// <summary>Laser heat from 0 to 100. Only meaningful for the laser.</summary>
        public float Heat { get; private set; }
        /// <summary>True after the laser hit 100 heat. It cannot fire again until the heat is gone.</summary>
        public bool Overheated { get; private set; }
        /// <summary>The beam damaged something this step. Unity draws the beam when this is true.</summary>
        public bool LaserActive { get; private set; }
        /// <summary>Where the beam ends this step, so the drawn beam matches the damaged area.</summary>
        public Vec2 LaserEnd { get; private set; }

        /// <summary>True while the weapon cannot attack: on cooldown, or the laser is overheated.</summary>
        public bool WeaponBusy => Weapon switch
        {
            WeaponId.Punch => Tick < nextPunch,
            WeaponId.Arrow => Tick < nextArrow,
            _ => Overheated,
        };

        /// <summary>
        /// Forgets that the attack control was held, so resuming after a pause or an upgrade screen
        /// does not read the gap as a release and fire an arrow the player never let go of.
        /// </summary>
        public void CancelHeldAttack()
        {
            heldLastStep = false;
            LaserActive = false;
        }

        private void StepWeapon(PlayerInput input)
        {
            LaserActive = false;
            // An arrow also leaves when a held aim is released, so aiming must know about that too.
            bool released = heldLastStep && !input.hold;
            bool attacking = Weapon == WeaponId.Arrow ? input.punch || released : input.punch;
            AimFromInput(input, attacking);
            switch (Weapon)
            {
                case WeaponId.Arrow: StepArrow(input.punch || released, input.draw); break;
                case WeaponId.Laser: StepLaser(input); break;
                default: StepPunch(input); break;
            }
            MoveProjectiles();
            heldLastStep = input.hold;
        }

        /// <summary>
        /// An explicit aim wins. With no aim, the punch and the arrow snap to the nearest enemy so a
        /// tap on a phone still connects; the laser keeps its last direction instead, because a beam
        /// that jumps between targets on its own is not something the player is steering.
        /// </summary>
        private void AimFromInput(PlayerInput input, bool attacking)
        {
            if (input.aim.Length > 0.05f) { Facing = input.aim.Normalized; return; }
            if (Weapon == WeaponId.Laser) return;
            if (!attacking) return;
            // An arrow reaches across the arena, so a tap may snap to anything on screen.
            float range = Weapon == WeaponId.Arrow ? config.arenaHalfWidth : config.punchRange + 1;
            Enemy nearest = Nearest(range);
            if (nearest != null) Facing = (nearest.Position - Position).Normalized;
        }

        private void StepPunch(PlayerInput input)
        {
            if (!input.punch || Tick < nextPunch) return;
            nextPunch = Tick + Frames(config.punchCooldown);
            Emit("attack", "punch", 0, 0, Facing);
            foreach (Enemy enemy in ScanEnemies(weaponScan))
            {
                if (!Alive(enemy)) continue;
                Vec2 delta = enemy.Position - Position;
                if (delta.Length <= config.punchRange + enemy.Radius &&
                    (delta.Length < 0.01f || Vec2.Dot(Facing, delta.Normalized) >= 0.15f))
                {
                    Damage(enemy, config.punchDamage, "punch");
                    if (enemy.Health > 0)
                        enemy.Position = Clamp(enemy.Position + Facing * config.punchKnockback, 0.35f);
                }
            }
        }

        /// <summary>
        /// Loosed when the attack control is released, with the shot's strength taken from how far
        /// it was pulled. A pull shorter than arrowMinDraw does nothing, so a stray tap is not a
        /// wasted arrow and the bow has to be drawn on purpose.
        /// </summary>
        private void StepArrow(bool firing, float draw)
        {
            if (!firing || Tick < nextArrow) return;
            float power = DrawPower(draw);
            if (power <= 0) return;

            nextArrow = Tick + Frames(config.arrowCooldown);
            float damage = config.arrowDamage * power;
            var arrow = new Projectile
            {
                Id = nextProjectileId++,
                Position = Position,
                Velocity = Facing * (config.arrowSpeed * power),
                Damage = damage,
                PierceLeft = config.arrowPierce,
            };
            projectiles.Add(arrow);
            // targetId stays 0: an arrow has no target yet, and the field means an enemy id.
            Emit("attack", "arrow", 0, damage, Facing);
        }

        /// <summary>
        /// Turns a pull into a share of full strength: 0 when the bow was not drawn far enough,
        /// otherwise between arrowMinPower and 1. Unity uses this to draw the power gauge, so what
        /// the player sees is what the shot will be.
        /// </summary>
        public float DrawPower(float draw)
        {
            float pull = Math.Min(1, Math.Max(0, draw));
            if (pull < config.arrowMinDraw) return 0;
            float span = 1 - config.arrowMinDraw;
            float past = span <= 0.00001f ? 1 : (pull - config.arrowMinDraw) / span;
            return config.arrowMinPower + (1 - config.arrowMinPower) * past;
        }

        /// <summary>
        /// Damage per second applied per step, limited by heat instead of a cooldown. Holding the
        /// beam to 100 heat locks it out until the heat is fully gone, so the cost of overheating is
        /// a real gap in damage rather than a brief stutter.
        /// </summary>
        private void StepLaser(PlayerInput input)
        {
            bool firing = input.hold && !Overheated;
            if (firing)
            {
                Heat = Math.Min(100, Heat + config.laserHeatPerSecond * StepSeconds);
                if (Heat >= 100) { Overheated = true; Emit("overheat", "laser", 0, Heat, Position); }
                BurnBeam();
            }
            else
            {
                float cooling = config.laserCoolPerSecond * (Overheated ? 1f / config.laserOverheatPenalty : 1f);
                Heat = Math.Max(0, Heat - cooling * StepSeconds);
                if (Overheated && Heat <= 0) Overheated = false;
            }
        }

        private void BurnBeam()
        {
            LaserActive = true;
            LaserEnd = Position + Facing * config.laserRange;
            float damage = config.laserDamagePerSecond * StepSeconds;
            bool reported = false;
            foreach (Enemy enemy in ScanEnemies(weaponScan))
            {
                if (!Alive(enemy)) continue;
                if (!WithinBeam(enemy.Position, enemy.Radius)) continue;
                if (!reported) { Emit("attack", "laser", 0, damage, Facing); reported = true; }
                Damage(enemy, damage, "laser");
            }
        }

        /// <summary>
        /// Distance from the enemy's body to the beam segment. The enemy's centre must be in front
        /// of the player: a beam that reached behind would not match what the player sees.
        /// </summary>
        private bool WithinBeam(Vec2 point, float radius)
        {
            Vec2 delta = point - Position;
            float along = Vec2.Dot(delta, Facing);
            if (along < 0) return false;
            Vec2 closest = Position + Facing * Math.Min(along, config.laserRange);
            return (point - closest).Length <= config.laserWidth * 0.5f + radius;
        }

        private void MoveProjectiles()
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile arrow = projectiles[i];
                arrow.Position += arrow.Velocity * StepSeconds;
                arrow.Travelled += arrow.Velocity.Length * StepSeconds;
                if (config.endlessWorld ? arrow.Travelled > SpawnRadius * 3 :
                    Math.Abs(arrow.Position.x) > config.arenaHalfWidth ||
                    Math.Abs(arrow.Position.y) > config.arenaHalfHeight)
                {
                    projectiles.RemoveAt(i);
                    continue;
                }
                foreach (Enemy enemy in ScanEnemies(weaponScan))
                {
                    if (arrow.PierceLeft <= 0) break;
                    if (!Alive(enemy)) continue;
                    // An arrow may only count once per enemy, or a slow arrow would tick repeatedly.
                    if (arrow.Hit.Contains(enemy.Id)) continue;
                    if ((enemy.Position - arrow.Position).Length > config.arrowRadius + enemy.Radius) continue;
                    arrow.Hit.Add(enemy.Id);
                    arrow.PierceLeft--;
                    Damage(enemy, arrow.Damage, "arrow");
                }
                if (arrow.PierceLeft <= 0) projectiles.RemoveAt(i);
            }
        }
    }
}
