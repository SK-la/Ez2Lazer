// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzPlayerSsrHistorySamplesTest
    {
        [Test]
        public void SampleIndicesIncludeEndpointsAndCap()
        {
            var indices = EzPlayerSsrAggregator.SampleIndices(100, 5);
            Assert.That(indices.First(), Is.EqualTo(0));
            Assert.That(indices.Last(), Is.EqualTo(99));
            Assert.That(indices.Count, Is.LessThanOrEqualTo(5));
            Assert.That(indices, Is.Ordered.Ascending);
        }

        [Test]
        public void ChronologicalSamplesUsePlayDatesNotNow()
        {
            var start = new DateTimeOffset(2019, 1, 15, 12, 0, 0, TimeSpan.Zero);
            var plays = new List<(DateTimeOffset, EzSkillsetVector)>();

            for (int i = 0; i < 10; i++)
            {
                plays.Add((
                    start.AddYears(i),
                    new EzSkillsetVector(10 + i, 1, 1, 1, 1, 1, 1, 1)));
            }

            var samples = EzPlayerSsrAggregator.BuildChronologicalHistorySamples(plays, maxPoints: 4);

            Assert.That(samples.Count, Is.EqualTo(4));
            Assert.That(samples[0].RecordedAt.Year, Is.EqualTo(2019));
            Assert.That(samples[^1].RecordedAt.Year, Is.EqualTo(2028));
            Assert.That(samples[^1].Vector.Overall, Is.GreaterThan(samples[0].Vector.Overall));
        }

        [Test]
        public void Rolling_window_allows_rating_to_fall()
        {
            // Strong early plays then a long stretch of weak ones; after the
            // strong prefix leaves the rolling window the aggregate must drop.
            var plays = new List<(DateTimeOffset, EzSkillsetVector)>();
            var t0 = DateTimeOffset.UtcNow;

            for (int i = 0; i < 5; i++)
                plays.Add((t0.AddMinutes(i), vec(20)));

            for (int i = 5; i < 60; i++)
                plays.Add((t0.AddMinutes(i), vec(5)));

            var samples = EzPlayerSsrAggregator.BuildChronologicalHistorySamples(
                plays,
                maxPoints: plays.Count,
                rollingPlays: 10);

            Assert.That(samples.Count, Is.EqualTo(plays.Count));
            double early = samples[4].Vector.Overall;
            double late = samples[^1].Vector.Overall;
            Assert.That(late, Is.LessThan(early));
        }

        private static EzSkillsetVector vec(double overall) => new EzSkillsetVector(overall, overall, overall, overall, overall, overall, overall, overall);
    }
}
