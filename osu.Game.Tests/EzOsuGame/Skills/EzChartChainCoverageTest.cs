// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Rulesets;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    /// <summary>
    /// The player pass uses this to tell "the chain will rate this chart, wait for it" apart from "nothing will
    /// ever come of this chart", so it has to mirror the chain's own candidate gate exactly.
    /// </summary>
    [TestFixture]
    public class EzChartChainCoverageTest
    {
        [TestCase(4f, true)]
        [TestCase(7f, true)]
        [TestCase(18f, true)]
        [TestCase(3.6f, true)] // rounds to 4, which the chain rates
        [TestCase(2f, false)]
        [TestCase(3f, false)]
        [TestCase(19f, false)]
        [TestCase(20f, false)]
        public void Keymode_range_matches_the_engine(float circleSize, bool expected)
        {
            Assert.That(EzChartChainCoverage.IsRateableChart(beatmap(circleSize)), Is.EqualTo(expected));
        }

        /// <summary>
        /// A non-positive column count is a candidate for the chain (it fails later, and settles as unrateable),
        /// so it must not be silently dropped as unrateable-by-keymode here.
        /// </summary>
        [TestCase(0f)]
        [TestCase(0.4f)]
        public void Non_positive_column_count_is_still_a_candidate(float circleSize)
        {
            Assert.That(EzChartChainCoverage.IsRateableChart(beatmap(circleSize)), Is.True);
        }

        /// <summary>
        /// A mania score on an osu!std beatmap is a convert. The chain's candidate list is mania-only, so no stage
        /// ever writes a row for it and a play on one must not read as missing.
        /// </summary>
        [TestCase(4f)]
        [TestCase(7f)]
        public void Convert_beatmap_is_settled(float circleSize)
        {
            Assert.That(EzChartChainCoverage.IsRateableChart(beatmap(circleSize, rulesetId: 0)), Is.False);
        }

        /// <summary>
        /// The chain walks beatmaps through their set, so a chart without one never enters the candidate list.
        /// </summary>
        [TestCase(4f)]
        [TestCase(7f)]
        public void Chart_without_a_set_is_settled(float circleSize)
        {
            Assert.That(EzChartChainCoverage.IsRateableChart(beatmap(circleSize, hasSet: false)), Is.False);
        }

        private static BeatmapInfo beatmap(float circleSize, int rulesetId = 3, bool hasSet = true)
            => new BeatmapInfo(new RulesetInfo { OnlineID = rulesetId }, new BeatmapDifficulty { CircleSize = circleSize })
            {
                BeatmapSet = hasSet ? new BeatmapSetInfo() : null
            };
    }
}
