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
    public class EzPatternRatingsTest
    {
        [Test]
        public void Aggregate_requires_min_plays_and_uses_overall()
        {
            var plays = new List<(double Overall, IReadOnlyList<string> Patterns)>();

            for (int i = 0; i < 3; i++)
                plays.Add((20, new[] { "tech" }));

            plays.Add((30, new[] { "tech", "jack" })); // jack only 1 play → dropped
            plays.Add((10, new[] { "delay" }));
            plays.Add((12, new[] { "delay" })); // delay only 2 → dropped

            var ratings = EzPatternRatings.AggregateModePatternRatings(plays);

            Assert.That(ratings.Count, Is.EqualTo(1));
            Assert.That(ratings[0].Id, Is.EqualTo("tech"));
            Assert.That(ratings[0].Plays, Is.EqualTo(4));
            Assert.That(ratings[0].Rating, Is.GreaterThan(10));
        }

        [Test]
        public void SkillModeEntries_prefers_pattern_axes_on_6k()
        {
            var ssr = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzMinaSkillAxis.Overall.ToSsrSkillId()] = 30,
                [EzMinaSkillAxis.Technical.ToSsrSkillId()] = 0.15,
                [EzMinaSkillAxis.Stream.ToSsrSkillId()] = 20,
            };

            var patterns = new[]
            {
                new EzPatternRating("tech", 22, 10),
                new EzPatternRating("jack", 18, 8),
                new EzPatternRating("delay", 16, 5),
            };

            var entries = EzPatternRatings.SkillModeEntries(6, ssr, patterns);

            Assert.That(entries.Count, Is.EqualTo(3));
            Assert.That(entries.All(e => EzPatternRatings.TryParseSkillId(e.SkillId, out _)));
            Assert.That(entries[0].SkillId, Is.EqualTo(EzPatternRatings.ToSkillId("tech")));
            Assert.That(entries.Any(e => e.SkillId.Contains("tech", StringComparison.Ordinal)));
        }

        [Test]
        public void SkillModeEntries_filters_sub_one_mina_on_4k()
        {
            var ssr = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzMinaSkillAxis.Stream.ToSsrSkillId()] = 20,
                [EzMinaSkillAxis.Technical.ToSsrSkillId()] = 0.2,
            };

            var entries = EzPatternRatings.SkillModeEntries(4, ssr, Array.Empty<EzPatternRating>());

            Assert.That(entries.Any(e => e.SkillId == EzMinaSkillAxis.Stream.ToSsrSkillId()));
            Assert.That(entries.All(e => e.Value >= EzPatternRatings.DISPLAY_MIN));
        }
    }
}
