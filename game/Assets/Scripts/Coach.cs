using PetThem.Combat;

namespace PetThem.Game
{
    /// <summary>What the coach is telling the player right now, or None when it is quiet.</summary>
    public enum CoachLesson { None, Move, Attack, Draw, Upgrade, Boss }

    /// <summary>
    /// The first thirty seconds. Teaches the controls by watching what the player actually does
    /// rather than by explaining them on a screen nobody reads.
    /// </summary>
    /// <remarks>
    /// Free of UnityEngine on purpose, like <see cref="Texts"/> and PlayerProfile: the whole point
    /// of a tutorial is that it fires at the right moment, and that is only checkable if the rules
    /// can be run without the editor. The screen layer feeds it observations and draws one line.
    ///
    /// It teaches nothing the combat core does not already do. There is no scripted room, no safe
    /// first wave, no paused hand-holding. That keeps the bots and the balance checks measuring the
    /// same game the player gets, and it means a lesson can never desync from the rules it
    /// describes: if the player somehow learns it early, the lesson simply never shows.
    ///
    /// Each lesson is finished for good once cleared, and the mask survives between runs, so
    /// quitting ten seconds in resumes at the lesson that was still open rather than starting over.
    /// </remarks>
    public sealed class Coach
    {
        /// <summary>Metres walked before we accept that moving has been understood.</summary>
        public const float MoveDistance = 3f;

        /// <summary>Kills before we accept that attacking has been understood.</summary>
        public const int AttackKills = 3;

        /// <summary>
        /// Pulls too short to fire before the bow lesson appears. One is a slip; two in a row is a
        /// player who thinks the bow is broken.
        /// </summary>
        public const int ShortDrawsBeforeNudge = 2;

        /// <summary>How long an announcement stays up. Long enough to read, short enough to lose.</summary>
        public const float UpgradeSeconds = 6f, BossSeconds = 8f;

        /// <summary>How far ahead of the boss the warning goes up.</summary>
        public const float BossLeadSeconds = 12f;

        private const int MoveLearned = 1, AttackLearned = 2, UpgradeLearned = 4, BossLearned = 8;
        private const int Everything = MoveLearned | AttackLearned | UpgradeLearned | BossLearned;

        private int learned;
        private WeaponId weapon;
        private float travelled, upgradeAt = -1, bossAt = -1;
        private int shortDraws;
        private bool arrowFired;

        /// <param name="learned">
        /// What earlier runs already taught, as returned by <see cref="Learned"/>. Unknown bits are
        /// dropped rather than trusted, so a save written by a later version cannot silence a lesson
        /// this version still has.
        /// </param>
        public Coach(int learned = 0) => this.learned = learned & Everything;

        /// <summary>The lessons already cleared, to hand back to the next <see cref="Coach"/>.</summary>
        public int Learned => learned;

        /// <summary>True once there is nothing left to teach. The screen layer stops drawing.</summary>
        public bool Finished => learned == Everything;

        /// <summary>Forgets everything, for the player who wants the lessons again.</summary>
        public void Reset() { learned = 0; Begin(weapon); }

        /// <summary>Starts a run. Clears what was counted, keeps what was learned.</summary>
        public void Begin(WeaponId runWeapon)
        {
            weapon = runWeapon;
            travelled = 0;
            shortDraws = 0;
            arrowFired = false;
            upgradeAt = bossAt = -1;
        }

        /// <summary>
        /// One combat step's worth of the world, taken from the same values the HUD prints.
        /// </summary>
        /// <param name="runTime">Seconds into the run.</param>
        /// <param name="stepDistance">Metres the player moved since the previous step.</param>
        /// <param name="runKills">Kills so far this run.</param>
        /// <param name="secondsToBoss">Seconds until the boss, from CombatWorld.</param>
        /// <param name="bossArrived">
        /// The boss is on the field, or has already been and gone. CombatWorld's countdown floors
        /// at zero and stays there for the rest of the run, so on its own it cannot tell "twelve
        /// seconds out" from "killed a minute ago". Without this the warning would go up over the
        /// corpse.
        /// </param>
        public void Observe(float runTime, float stepDistance, int runKills, float secondsToBoss, bool bossArrived)
        {
            if (stepDistance > 0) travelled += stepDistance;
            if (travelled >= MoveDistance) learned |= MoveLearned;
            if (runKills >= AttackKills) learned |= AttackLearned;
            // The warning goes up before the boss walks in, because the lesson is what to watch for
            // when it does. Once it is standing there it is too late to be told.
            //
            // Strictly above zero as well as below the lead: CombatWorld's countdown floors at zero
            // and stays there for the rest of the run, so zero means the boss is here or was here,
            // never that it is due. That holds even if a caller forgets to report its arrival.
            if (bossAt < 0 && (learned & BossLearned) == 0 && !bossArrived &&
                secondsToBoss > 0 && secondsToBoss <= BossLeadSeconds)
                bossAt = runTime;
            if (bossAt >= 0 && runTime >= bossAt + BossSeconds) learned |= BossLearned;
            if (upgradeAt >= 0 && runTime >= upgradeAt + UpgradeSeconds) learned |= UpgradeLearned;
        }

        /// <summary>A bow released without enough pull to fire. The shot that never happened.</summary>
        public void ShortDraw() => shortDraws++;

        /// <summary>An arrow actually left the bow, so the gesture landed.</summary>
        public void ArrowFired() => arrowFired = true;

        /// <summary>An upgrade was picked. The lesson is what happens to it when the run ends.</summary>
        public void UpgradeTaken(float runTime)
        {
            if (upgradeAt < 0 && (learned & UpgradeLearned) == 0) upgradeAt = runTime;
        }

        /// <summary>
        /// The one line to show, or None. Announcements outrank the standing lessons: a boss on the
        /// way matters more this second than a movement hint that has been up for a minute.
        /// </summary>
        public CoachLesson Lesson
        {
            get
            {
                if (bossAt >= 0 && (learned & BossLearned) == 0) return CoachLesson.Boss;
                if (upgradeAt >= 0 && (learned & UpgradeLearned) == 0) return CoachLesson.Upgrade;
                // Ahead of the movement lesson deliberately. A player stabbing at a bow that will
                // not fire is failing right now; walking can wait until the bow answers.
                if ((learned & AttackLearned) == 0 && weapon == WeaponId.Arrow && !arrowFired &&
                    shortDraws >= ShortDrawsBeforeNudge) return CoachLesson.Draw;
                if ((learned & MoveLearned) == 0) return CoachLesson.Move;
                if ((learned & AttackLearned) == 0) return CoachLesson.Attack;
                return CoachLesson.None;
            }
        }
    }
}
