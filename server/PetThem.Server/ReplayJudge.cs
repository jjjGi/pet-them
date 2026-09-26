using PetThem.Combat;

namespace PetThem.Server;

/// <summary>What replaying a submitted run produced, or why it could not be replayed.</summary>
public sealed record ReplayVerdict(
    bool Replayed, string Refusal, int Kills, float Seconds, bool BossDefeated, int Steps);

/// <summary>
/// Plays an uploaded run again on the server and reports what actually happened.
/// </summary>
/// <remarks>
/// The client sends the seed and what it did, never what it scored. The server plays the same
/// fight with the same combat core and works out the result itself, so a claim is no longer
/// something to be trusted or refused -- it is just a claim, next to an answer.
///
/// **It is not compared bit for bit, because that was measured and it does not hold.** The same
/// seed and inputs in the editor and in .NET produce transcripts differing on 977 of 3,202 lines;
/// over forty runs the kills matched every time and the coins twice differed by one. So the payout
/// comes from the server's own replay rather than from agreeing with the client, and the claim is
/// kept only to notice a run that is nowhere near. Demanding an exact match would reject honest
/// players, which is the one failure worth designing against. See docs/verification.md, 0.19.1.
///
/// Treasure chests need no decisions of their own: they are picked up by walking into one, so they
/// fall out of the inputs like everything else. What does need sending is the level-up screen --
/// which card was taken, and how many re-rolls were spent first, because a re-roll draws from the
/// same random stream and moves the cards that follow.
/// </remarks>
public static class ReplayJudge
{
    /// <summary>
    /// How far the claim may be from the replay before the run is called unverified.
    /// </summary>
    /// <remarks>
    /// Provisional. On the two runtimes that have been measured the kills matched exactly in forty
    /// runs out of forty, so nought would have done. A phone has not been measured -- IL2CPP
    /// compiles to C++ and ARM64 is not x64 -- so this leaves a little room rather than banning
    /// players on an assumption. Tighten it once a device has been measured.
    /// </remarks>
    public const int KillTolerance = 2;

    /// <summary>
    /// The most input segments one submission may carry.
    /// </summary>
    /// <remarks>
    /// A thumb holds still for many steps at a time, so a real three-minute run is a few hundred
    /// segments. This is room to spare, and a bound: without one, a submission is an invitation to
    /// hand the server as much work as anybody likes.
    /// </remarks>
    public const int MaxSegments = 4000;

    /// <summary>Replays the run, or says why it will not.</summary>
    public static ReplayVerdict Judge(Contracts.RunSubmission run, BalanceConfig defaults)
    {
        if (run.Inputs == null || run.Inputs.Length == 0)
            return Refuse("no_inputs");
        if (run.Inputs.Length > MaxSegments)
            return Refuse("too_many_segments");

        int steps = 0;
        foreach (Contracts.InputSegment segment in run.Inputs)
        {
            if (segment.Steps <= 0) return Refuse("bad_segment_length");
            if (!Finite(segment.MoveX) || !Finite(segment.MoveY) ||
                !Finite(segment.AimX) || !Finite(segment.AimY) || !Finite(segment.Draw))
                return Refuse("bad_input_value");
            steps += segment.Steps;
            if (steps > MaxSteps(defaults)) return Refuse("too_many_steps");
        }

        var world = new CombatWorld(defaults, run.Seed, true,
            Enum.Parse<WeaponId>(run.Weapon), Enum.Parse<PetId>(run.Pet), enableTwists: true);

        Contracts.Pick[] picks = run.Picks ?? Array.Empty<Contracts.Pick>();
        int nextPick = 0, played = 0;

        foreach (Contracts.InputSegment segment in run.Inputs)
        {
            var input = new PlayerInput(
                new Vec2(segment.MoveX, segment.MoveY), new Vec2(segment.AimX, segment.AimY),
                segment.Punch, segment.Hold, segment.Draw);

            for (int i = 0; i < segment.Steps; i++)
            {
                if (world.State != RunState.Playing) break;

                // Step does nothing at all while cards are on the table, so a replay that cannot
                // answer the level-up screen stops there and would report a fight that ended at
                // the first level. The picks are what let it carry on.
                if (world.HasUpgradeChoice)
                {
                    if (nextPick >= picks.Length) return Refuse("missing_pick");
                    Contracts.Pick pick = picks[nextPick++];
                    if (pick.Rerolls < 0 || pick.Rerolls > 64) return Refuse("bad_reroll_count");
                    for (int r = 0; r < pick.Rerolls; r++)
                        if (!world.RerollUpgrades()) return Refuse("reroll_not_available");
                    if (!Enum.TryParse(pick.Upgrade, false, out UpgradeId chosen))
                        return Refuse("unknown_upgrade");
                    // Refused rather than ignored: a card that was never dealt means the upload
                    // does not describe a run this world could have had.
                    if (!world.ChooseUpgrade(chosen)) return Refuse("upgrade_not_offered");
                }

                world.Step(input);
                played++;
            }
            if (world.State != RunState.Playing) break;
        }

        return new ReplayVerdict(true, "", world.Kills, world.Time, world.BossDefeated, played);
    }

    /// <summary>True when the claim is close enough to the replay to call the run verified.</summary>
    public static bool Agrees(Contracts.RunSubmission run, ReplayVerdict verdict) =>
        verdict.Replayed && Math.Abs(run.Kills - verdict.Kills) <= KillTolerance;

    private static int MaxSteps(BalanceConfig defaults) =>
        (int)((defaults.duration + ServerHost.SecondsSlack) / CombatWorld.StepSeconds);

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static ReplayVerdict Refuse(string reason) =>
        new ReplayVerdict(false, reason, 0, 0, false, 0);
}
