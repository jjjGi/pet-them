// Compiled twice, under two different project settings: Unity build it with nullable references
// off, and tools/CoreChecks builds it with them on and warnings as errors. Pinned off here so the
// file means one thing in both places rather than needing annotations that Unity would warn about.
#nullable disable

using System;
using System.Collections.Generic;

namespace PetThem.Game
{
    /// <summary>What to do with a run after an upload attempt came back.</summary>
    public enum UploadVerdict
    {
        /// <summary>The server has it. Stop carrying it.</summary>
        Done,

        /// <summary>Nothing was decided -- no network, or the server is having a bad day. Try later.</summary>
        Retry,

        /// <summary>The session is no good. Get a new one, then try again.</summary>
        Reauthenticate,

        /// <summary>The server will never accept this. Carrying it any longer is a loop.</summary>
        Rejected,
    }

    /// <summary>One input held for a number of steps.</summary>
    /// <remarks>
    /// Run-length encoded because a thumb does not move sixty times a second. A three-minute run
    /// is 10,800 steps and a few hundred of these, which is a few kilobytes on the wire and in
    /// the queue file rather than a quarter of a megabyte.
    ///
    /// Field names and order match the server's InputSegment. A check compiles both and compares
    /// them, because the game cannot reference the server project and the shape therefore exists
    /// twice.
    /// </remarks>
    [Serializable]
    public sealed class InputSegment
    {
        public int steps;
        public float moveX, moveY, aimX, aimY;
        public bool punch, hold;
        public float draw;
    }

    /// <summary>What the player did at one level-up screen.</summary>
    /// <remarks>
    /// The re-rolls come first because they draw from the same stream the cards do: replaying the
    /// choice without them deals a different hand, and the card that was taken is not in it.
    /// </remarks>
    [Serializable]
    public sealed class Pick
    {
        public int rerolls;
        public string upgrade = "";
    }

    /// <summary>A finished run waiting to reach the server.</summary>
    /// <remarks>
    /// Public fields and [Serializable] because Unity's JsonUtility writes it to disk, the same
    /// arrangement <see cref="PetThem.Combat.PlayerProfile"/> uses.
    /// </remarks>
    [Serializable]
    public sealed class PendingRun
    {
        public string runId = "";
        public int seed;
        public string weapon = "", pet = "";
        public int kills;
        public float seconds;
        public bool bossDefeated;

        /// <summary>
        /// Everything the player did, so the server can play the run again rather than take the
        /// numbers above on trust.
        /// </summary>
        /// <remarks>
        /// Empty is allowed and means "not recorded": the run is still uploaded and still paid,
        /// just on its word. The replay is best effort; being paid is not.
        /// </remarks>
        public InputSegment[] inputs = Array.Empty<InputSegment>();
        public Pick[] picks = Array.Empty<Pick>();

        /// <summary>How many times we have tried to send it.</summary>
        public int attempts;

        /// <summary>Unix seconds before which there is no point trying again.</summary>
        public double nextAttemptAt;
    }

    /// <summary>
    /// Finished runs that have not reached the server yet.
    /// </summary>
    /// <remarks>
    /// The game is playable with no network and with no server at all, so a finished run cannot
    /// depend on either. It is written here the moment the run ends and sent whenever sending
    /// becomes possible, which may be days later on a different network.
    ///
    /// **A run is never silently dropped.** Those coins are something the player earned. Anything
    /// that might succeed later is kept and retried with a widening gap; only a server saying the
    /// submission is permanently wrong removes one, and that removal is recorded rather than
    /// swallowed. Retrying is safe to do freely because the server pays a given runId once -- the
    /// idempotency on that side is what lets this side be stubborn.
    ///
    /// Free of UnityEngine, like PlayerProfile and Coach: when to retry and when to give up is
    /// exactly the sort of thing that is wrong for months if it cannot be run without the editor.
    /// The file itself is Unity's to write.
    /// </remarks>
    [Serializable]
    public sealed class RunOutbox
    {
        /// <summary>First gap after a failure. It doubles from here.</summary>
        public const double FirstDelaySeconds = 2;

        /// <summary>The gap stops widening here, so a queue left overnight still goes out promptly.</summary>
        public const double MaxDelaySeconds = 300;

        /// <summary>
        /// How many runs to carry. Reached only by a player who kept playing offline for a very
        /// long time; past it the oldest go, because the newest are the ones they remember earning.
        /// </summary>
        public const int Capacity = 200;

        public PendingRun[] pending = Array.Empty<PendingRun>();

        /// <summary>Runs that were thrown away, and why. Shown rather than hidden.</summary>
        public string[] discarded = Array.Empty<string>();

        public int Count => pending.Length;

        /// <summary>
        /// Adds a run. A runId already queued is not added again, so a double call after a run
        /// ends cannot queue it twice.
        /// </summary>
        public bool Enqueue(PendingRun run, double now)
        {
            if (run == null || string.IsNullOrEmpty(run.runId)) return false;
            foreach (PendingRun queued in pending)
                if (string.Equals(queued.runId, run.runId, StringComparison.Ordinal)) return false;

            run.attempts = 0;
            run.nextAttemptAt = now;
            var list = new List<PendingRun>(pending) { run };
            var dropped = new List<string>(discarded);
            while (list.Count > Capacity)
            {
                dropped.Add($"{list[0].runId}: queue was full ({Capacity})");
                list.RemoveAt(0);
            }
            pending = list.ToArray();
            discarded = dropped.ToArray();
            return true;
        }

        /// <summary>The run to try next, or null when nothing is due yet.</summary>
        /// <remarks>
        /// Oldest first. A player who was offline for a week gets paid in the order they played,
        /// which is the order they would expect if they are watching the number go up.
        /// </remarks>
        public PendingRun Due(double now)
        {
            foreach (PendingRun run in pending)
                if (run.nextAttemptAt <= now) return run;
            return null;
        }

        /// <summary>Files the result of an attempt.</summary>
        public void Record(string runId, UploadVerdict verdict, double now)
        {
            var list = new List<PendingRun>(pending);
            int index = list.FindIndex(r => string.Equals(r.runId, runId, StringComparison.Ordinal));
            if (index < 0) return;

            switch (verdict)
            {
                case UploadVerdict.Done:
                    list.RemoveAt(index);
                    break;
                case UploadVerdict.Rejected:
                    var dropped = new List<string>(discarded)
                        { $"{runId}: the server refused it" };
                    discarded = dropped.ToArray();
                    list.RemoveAt(index);
                    break;
                default:
                    // Reauthenticate waits the same as Retry. Getting a new session is the caller's
                    // job, and if it fails, hammering the server about it will not help.
                    list[index].attempts++;
                    list[index].nextAttemptAt = now + Backoff(list[index].attempts);
                    break;
            }
            pending = list.ToArray();
        }

        /// <summary>The gap before attempt number <paramref name="attempts"/> is retried.</summary>
        public static double Backoff(int attempts)
        {
            if (attempts <= 0) return 0;
            double delay = FirstDelaySeconds;
            // Doubling by multiplication rather than Math.Pow: attempts climbs without limit on a
            // phone that is offline for a week, and 2^400 is not a number we want to reason about.
            for (int i = 1; i < attempts && delay < MaxDelaySeconds; i++) delay *= 2;
            return Math.Min(delay, MaxDelaySeconds);
        }

        /// <summary>
        /// What an HTTP reply means for a queued run.
        /// </summary>
        /// <remarks>
        /// The split that matters is between "nothing was decided" and "this will never work".
        /// A timeout, a 429 or a 500 is the server not having answered yet, and the run stays.
        /// A 400 is the server having read it and said no -- sending it again gets the same no,
        /// so keeping it is an endless loop rather than persistence.
        ///
        /// 401 is neither: the run is fine and the session is not.
        /// </remarks>
        public static UploadVerdict VerdictFor(long status, bool networkError)
        {
            if (networkError || status <= 0) return UploadVerdict.Retry;
            if (status >= 200 && status < 300) return UploadVerdict.Done;
            if (status == 401) return UploadVerdict.Reauthenticate;
            if (status == 408 || status == 429) return UploadVerdict.Retry;
            if (status >= 500) return UploadVerdict.Retry;
            return UploadVerdict.Rejected;
        }

        /// <summary>
        /// Repairs anything a damaged or hand-edited file could hold and lists what it changed.
        /// An empty list means the file was already sound.
        /// </summary>
        public IReadOnlyList<string> Normalize()
        {
            var repairs = new List<string>();
            pending ??= Array.Empty<PendingRun>();
            discarded ??= Array.Empty<string>();

            var kept = new List<PendingRun>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (PendingRun run in pending)
            {
                if (run == null || string.IsNullOrEmpty(run.runId))
                { repairs.Add("dropped an entry with no run id"); continue; }
                if (!seen.Add(run.runId))
                { repairs.Add($"dropped duplicate {run.runId}"); continue; }
                if (run.attempts < 0) { repairs.Add($"{run.runId}: attempts was {run.attempts}"); run.attempts = 0; }
                if (run.kills < 0) { repairs.Add($"{run.runId}: kills was {run.kills}"); run.kills = 0; }
                if (double.IsNaN(run.nextAttemptAt) || double.IsInfinity(run.nextAttemptAt))
                { repairs.Add($"{run.runId}: next attempt was not a time"); run.nextAttemptAt = 0; }
                if (float.IsNaN(run.seconds) || float.IsInfinity(run.seconds) || run.seconds < 0)
                { repairs.Add($"{run.runId}: seconds was {run.seconds}"); run.seconds = 0; }
                kept.Add(run);
            }

            while (kept.Count > Capacity)
            {
                repairs.Add($"{kept[0].runId}: over capacity");
                kept.RemoveAt(0);
            }
            if (kept.Count != pending.Length) pending = kept.ToArray();
            return repairs;
        }
    }
}
