// Compiled twice, under two different project settings, like RunOutbox.
#nullable disable

using System;
using System.Collections.Generic;
using PetThem.Combat;

namespace PetThem.Game
{
    /// <summary>
    /// Writes down what the player did, so the server can play the run again.
    /// </summary>
    /// <remarks>
    /// The server pays for what a replay produces rather than for what the client claims, and this
    /// is the half that makes a replay possible. Without it a run is only its own word.
    ///
    /// Inputs are merged while they are recorded rather than compressed afterwards: a thumb holds
    /// still for many steps at a time, so a three-minute run is a few hundred segments instead of
    /// 10,800. That matters twice over -- on the wire, and in the queue file, which holds up to
    /// two hundred runs on a phone that may have been offline for a week.
    ///
    /// Inputs alone are not enough. CombatWorld.Step does nothing at all while a level-up card is
    /// on the table, so a replay with no answer for that screen stops at the first level and
    /// reports the fight it managed before it. The cards have to be recorded too -- and the
    /// re-rolls that came before each one, because a re-roll draws from the same random stream the
    /// cards do, and replaying a choice without it deals a hand that choice is not in.
    ///
    /// **It gives up rather than growing without limit.** Past the cap the tape is dropped and the
    /// run uploads unreplayed: it is still counted and still paid, on its word, exactly as runs
    /// were before any of this existed. Losing a verification is a smaller harm than losing a run.
    ///
    /// Free of UnityEngine so the merging and the giving-up can be checked without the editor.
    /// </remarks>
    public sealed class RunTape
    {
        /// <summary>
        /// The most segments to keep. Past this the tape is abandoned.
        /// </summary>
        /// <remarks>
        /// Below the server's own limit on purpose, so a tape that would be refused is never sent
        /// in the first place. A real run is a few hundred; reaching this means something is
        /// producing an input that changes every step, and the honest answer is to stop recording
        /// rather than to send something that will bounce.
        /// </remarks>
        public const int MaxSegments = 3600;

        private readonly List<InputSegment> segments = new List<InputSegment>();
        private readonly List<Pick> picks = new List<Pick>();
        private int pendingRerolls;

        /// <summary>True while the tape is still worth sending.</summary>
        public bool Usable { get; private set; } = true;

        /// <summary>Segments recorded so far. For the settings screen and the checks.</summary>
        public int SegmentCount => segments.Count;

        /// <summary>Starts a new run's tape.</summary>
        public void Begin()
        {
            segments.Clear();
            picks.Clear();
            pendingRerolls = 0;
            Usable = true;
        }

        /// <summary>Records one step's input, merging it into the last segment when it matches.</summary>
        public void Step(PlayerInput input)
        {
            if (!Usable) return;
            if (segments.Count > 0 && Same(segments[segments.Count - 1], input))
            {
                segments[segments.Count - 1].steps++;
                return;
            }
            if (segments.Count >= MaxSegments) { Abandon(); return; }
            segments.Add(new InputSegment
            {
                steps = 1,
                moveX = input.move.x, moveY = input.move.y,
                aimX = input.aim.x, aimY = input.aim.y,
                punch = input.punch, hold = input.hold, draw = input.draw,
            });
        }

        /// <summary>Records a re-roll, which belongs to the card that is chosen after it.</summary>
        public void Rerolled()
        {
            if (Usable) pendingRerolls++;
        }

        /// <summary>Records a card being taken, together with the re-rolls spent first.</summary>
        public void Chose(UpgradeId id)
        {
            if (!Usable) return;
            picks.Add(new Pick { rerolls = pendingRerolls, upgrade = id.ToString() });
            pendingRerolls = 0;
        }

        /// <summary>Stops recording and throws away what there is.</summary>
        /// <remarks>
        /// Everything is dropped rather than truncated. Half a tape is not a shorter run, it is a
        /// different one, and the server replaying it would arrive somewhere the player never was.
        /// </remarks>
        public void Abandon()
        {
            Usable = false;
            segments.Clear();
            picks.Clear();
            pendingRerolls = 0;
        }

        /// <summary>The recorded inputs, or nothing when the tape was abandoned.</summary>
        public InputSegment[] Inputs() =>
            Usable ? segments.ToArray() : Array.Empty<InputSegment>();

        /// <summary>The recorded cards, or nothing when the tape was abandoned.</summary>
        public Pick[] Picks() =>
            Usable ? picks.ToArray() : Array.Empty<Pick>();

        private static bool Same(InputSegment segment, PlayerInput input) =>
            segment.moveX == input.move.x && segment.moveY == input.move.y &&
            segment.aimX == input.aim.x && segment.aimY == input.aim.y &&
            segment.punch == input.punch && segment.hold == input.hold &&
            segment.draw == input.draw;
    }
}
