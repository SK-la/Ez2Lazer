// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Skills;

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

        private static BeatmapInfo beatmap(float circleSize)
            => new BeatmapInfo { Difficulty = new BeatmapDifficulty { CircleSize = circleSize } };
    }
}
