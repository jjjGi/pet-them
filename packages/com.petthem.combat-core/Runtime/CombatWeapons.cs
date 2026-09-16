using System;
using System.Collections.Generic;

namespace PetThem.Combat
{
    public enum WeaponId { Punch, Arrow, Laser }

    /// <summary>An arrow in flight. Owned by CombatWorld; Unity only reads it to draw.</summary>
    public sealed class Projectile
    {
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
                case WeaponId.Arrow: StepArrow(input.punch || released); break;
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
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = enemies[i];
                Vec2 delta = enemy.Position - Position;
                if (delta.Length <= config.punchRange + 0.35f &&
                    (delta.Length < 0.01f || Vec2.Dot(Facing, delta.Normalized) >= 0.15f))
                {
                    Damage(enemy, config.punchDamage, "punch");
                    if (enemy.Health > 0)
                        enemy.Position = Clamp(enemy.Position + Facing * config.punchKnockback, 0.35f);
                }
            }
        }

        /// <summary>
        /// Fires on release of the attack control, or on a tap for players who do not drag.
        /// Holding only aims, so the arrow cannot be spammed faster than its cooldown either way.
        /// </summary>
        private void StepArrow(bool firing)
        {
            if (!firing || Tick < nextArrow) return;
            nextArrow = Tick + Frames(config.arrowCooldown);
            var arrow = new Projectile
            {
                Id = nextProjectileId++,
                Position = Position,
                Velocity = Facing * config.arrowSpeed,
                Damage = config.arrowDamage,
                PierceLeft = config.arrowPierce,
            };
            projectiles.Add(arrow);
            // targetId stays 0: an arrow has no target yet, and the field means an enemy id.
            Emit("attack", "arrow", 0, config.arrowDamage, Facing);
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
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = enemies[i];
                if (!WithinBeam(enemy.Position)) continue;
                if (!reported) { Emit("attack", "laser", 0, damage, Facing); reported = true; }
                Damage(enemy, damage, "laser");
            }
        }

        /// <summary>Distance from the enemy to the beam segment, within half the beam width.</summary>
        private bool WithinBeam(Vec2 point)
        {
            Vec2 delta = point - Position;
            float along = Vec2.Dot(delta, Facing);
            if (along < 0 || along > config.laserRange) return false;
            Vec2 closest = Position + Facing * along;
            return (point - closest).Length <= config.laserWidth * 0.5f + 0.35f;
        }

        private void MoveProjectiles()
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile arrow = projectiles[i];
                arrow.Position += arrow.Velocity * StepSeconds;
                if (Math.Abs(arrow.Position.x) > config.arenaHalfWidth ||
                    Math.Abs(arrow.Position.y) > config.arenaHalfHeight)
                {
                    projectiles.RemoveAt(i);
                    continue;
                }
                for (int e = enemies.Count - 1; e >= 0 && arrow.PierceLeft > 0; e--)
                {
                    Enemy enemy = enemies[e];
                    // An arrow may only count once per enemy, or a slow arrow would tick repeatedly.
                    if (arrow.Hit.Contains(enemy.Id)) continue;
                    if ((enemy.Position - arrow.Position).Length > config.arrowRadius + 0.4f) continue;
                    arrow.Hit.Add(enemy.Id);
                    arrow.PierceLeft--;
                    Damage(enemy, arrow.Damage, "arrow");
                }
                if (arrow.PierceLeft <= 0) projectiles.RemoveAt(i);
            }
        }
    }
}
