// Compiled twice, under two different project settings, like RunOutbox.
#nullable disable

using System;
using System.Text;
using PetThem.Combat;

namespace PetThem.Game
{
    /// <summary>
    /// Runs one fixed fight and writes down everything that happened, exactly.
    /// </summary>
    /// <remarks>
    /// Replay validation rests on one claim: the same seed and the same inputs produce the same
    /// run everywhere it is played. The server would re-simulate an uploaded run and compare, and
    /// if the phone and the server disagree by a single bit the whole scheme rejects honest
    /// players. That claim has never been measured, so this measures it.
    ///
    /// The combat core calls Math.Sin and Math.Cos in eight places, and every one of them decides
    /// where something spawns or orbits rather than how it looks. IEEE-754 requires square root to
    /// be correctly rounded and does not require that of the trigonometric functions, so two
    /// conforming runtimes may legitimately differ in the last bit -- and one bit in a spawn angle
    /// is a different fight a minute later. That is the specific thing under suspicion here.
    ///
    /// Floats go into the transcript as their raw bits. Printing them as decimals would let a
    /// formatting difference look like a divergence, or worse, hide one.
    ///
    /// Free of UnityEngine so the same code runs in the checks, in the editor and on a device,
    /// and the three transcripts can be compared byte for byte.
    /// </remarks>
    public static class DeterminismProbe
    {
        /// <summary>The seed every transcript is taken with. Any fixed number would do.</summary>
        public const int Seed = 20260922;

        /// <summary>Three minutes at the fixed step: a whole run, boss included.</summary>
        public const int Steps = 10800;

        /// <summary>
        /// Eight directions as exact literals rather than anything computed.
        /// </summary>
        /// <remarks>
        /// The diagonals are the single-precision value nearest 1/sqrt(2), written out. Computing
        /// them would put the thing being measured into the measurement.
        /// </remarks>
        private static readonly Vec2[] Directions =
        {
            new Vec2(1f, 0f), new Vec2(0.70710677f, 0.70710677f),
            new Vec2(0f, 1f), new Vec2(-0.70710677f, 0.70710677f),
            new Vec2(-1f, 0f), new Vec2(-0.70710677f, -0.70710677f),
            new Vec2(0f, -1f), new Vec2(0.70710677f, -0.70710677f),
        };

        /// <summary>
        /// The input for a given step. Integer arithmetic only, so the inputs themselves are
        /// identical everywhere and any difference in the transcript comes from the simulation.
        /// </summary>
        public static PlayerInput InputAt(int tick) => new PlayerInput(
            Directions[(tick / 37) % Directions.Length],
            Directions[(tick / 23) % Directions.Length],
            tick % 23 == 0,
            (tick / 53) % 2 == 0,
            (tick % 100) / 100f);

        /// <summary>
        /// Builds the world the transcript is taken from, from the balance the game ships.
        /// </summary>
        /// <remarks>
        /// Here rather than in each runner so the two cannot build it differently. A measurement
        /// where the runtimes disagree because one of them was set up differently proves nothing,
        /// and that mistake would look exactly like the one being hunted.
        ///
        /// Health is raised so the run lasts the full three minutes and reaches the boss. The
        /// first attempt used the shipped value, died at forty-five seconds, and measured nothing
        /// about the boss, its summons or the orbiting companions -- which is where most of the
        /// trigonometry is.
        /// </remarks>
        public static CombatWorld NewWorld(BalanceConfig shipped) => NewWorld(shipped, Seed);

        /// <param name="seed">The run to build. Only the outcome sweep uses anything but <see cref="Seed"/>.</param>
        public static CombatWorld NewWorld(BalanceConfig shipped, int seed)
        {
            if (shipped == null) throw new ArgumentNullException(nameof(shipped));
            BalanceConfig config = shipped.Copy();
            config.playerHealth = 100000;
            config.Validate();
            return new CombatWorld(config, seed, true, WeaponId.Punch, PetId.Mochi, enableTwists: true);
        }

        /// <summary>
        /// Plays many fixed fights and reports only how each one ended.
        /// </summary>
        /// <remarks>
        /// The full transcript answers "do the two runtimes agree exactly", and the answer is no.
        /// This answers the question that follows, which is the one the design actually turns on:
        /// how often a disagreement in the last bit grows into a different result. One run
        /// matching is an anecdote; a count over many seeds is something to decide on.
        ///
        /// Only the ending is compared, because that is what a server would be paying out on.
        /// </remarks>
        public static string Outcomes(BalanceConfig shipped, int seeds)
        {
            var report = new StringBuilder();
            for (int i = 0; i < seeds; i++)
            {
                CombatWorld world = NewWorld(shipped, Seed + i * 7919);
                for (int tick = 0; tick < Steps && world.State == RunState.Playing; tick++)
                {
                    if (world.HasUpgradeChoice)
                        world.ChooseUpgrade(world.UpgradeChoices[tick % world.UpgradeChoices.Count].Id);
                    world.Step(InputAt(tick));
                }
                report.Append(world.Seed).Append(' ').Append(world.State)
                    .Append(' ').Append(world.Kills)
                    .Append(' ').Append(world.Coins)
                    .Append(' ').Append(world.Twist)
                    .Append('\n');
            }
            return report.ToString();
        }

        /// <summary>
        /// Plays the fixed fight and returns the transcript, one line per thing that happened.
        /// </summary>
        public static string Transcribe(CombatWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var transcript = new StringBuilder();
            transcript.Append("seed=").Append(world.Seed)
                .Append(" twist=").Append(world.Twist)
                .Append(" weapon=").Append(world.Weapon)
                .Append(" pet=").Append(world.Pet)
                .Append('\n');

            for (int tick = 0; tick < Steps && world.State == RunState.Playing; tick++)
            {
                // Cards have to be taken or the world stops: Step returns immediately while a
                // choice is pending. The first transcript ran the full three minutes and recorded
                // seven spawns and five kills, because it stalled at the first level-up and never
                // noticed. Picking by tick keeps it deterministic and spreads the choices around,
                // so the run grows a build and reaches the boss -- which is where the companions,
                // the criticals and the summons are, and those are the parts worth measuring.
                if (world.HasUpgradeChoice)
                {
                    UpgradeChoice taken = world.UpgradeChoices[tick % world.UpgradeChoices.Count];
                    world.ChooseUpgrade(taken.Id);
                    transcript.Append(tick).Append(" took ").Append(taken.Id)
                        .Append(' ').Append(taken.Rank).Append('\n');
                }
                world.Step(InputAt(tick));
                foreach (CombatEvent e in world.Events)
                    transcript.Append(tick).Append(' ').Append(e.type).Append(' ').Append(e.source)
                        .Append(' ').Append(e.targetId)
                        .Append(' ').Append(Bits(e.value))
                        .Append(' ').Append(Bits(e.x)).Append(' ').Append(Bits(e.y))
                        .Append('\n');

                // A snapshot every second as well, so a divergence that has not yet produced an
                // event still shows up, and shows up near where it started.
                if (tick % 60 == 0)
                    transcript.Append(tick).Append(" state ")
                        .Append(Bits(world.Position.x)).Append(' ').Append(Bits(world.Position.y))
                        .Append(' ').Append(Bits(world.Health))
                        .Append(' ').Append(world.Kills)
                        .Append(' ').Append(world.Enemies.Count)
                        .Append('\n');
            }
            transcript.Append("end state=").Append(world.State)
                .Append(" kills=").Append(world.Kills)
                .Append(" coins=").Append(world.Coins)
                .Append('\n');
            return transcript.ToString();
        }

        /// <summary>
        /// A float as its exact bits.
        /// </summary>
        /// <remarks>
        /// Decimal text would round, so two genuinely different numbers could print the same and a
        /// divergence would go unseen. Bits cannot do that.
        /// </remarks>
        private static string Bits(float value) =>
            BitConverter.ToInt32(BitConverter.GetBytes(value), 0).ToString("x8");
    }
}
