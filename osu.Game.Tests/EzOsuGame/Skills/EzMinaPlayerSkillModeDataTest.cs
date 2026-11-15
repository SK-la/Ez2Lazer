// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills.Mina;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzMinaPlayerSkillModeDataTest
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

            var ratings = EzMinaPlayerSkillModeData.AggregateModeSkillRatings(plays);

            Assert.That(ratings.Count, Is.EqualTo(1));
            Assert.That(ratings[0].SkillId, Is.EqualTo("tech"));
            Assert.That(ratings[0].PlayCount, Is.EqualTo(4));
            Assert.That(ratings[0].SkillRating, Is.GreaterThan(10));
        }

        [Test]
        public void SkillModeEntries_prefers_pattern_axes_on_6k()
        {
            var ssr = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzMinaSkillAxis.Overall.ToMetaId()] = 30,
                [EzMinaSkillAxis.Technical.ToMetaId()] = 0.15,
                [EzMinaSkillAxis.Stream.ToMetaId()] = 20,
            };

            var patterns = new[]
            {
                new EzMinaPlayerSkillTagPlayData("tech", 22, 10),
                new EzMinaPlayerSkillTagPlayData("jack", 18, 8),
                new EzMinaPlayerSkillTagPlayData("delay", 16, 5),
            };

            var entries = EzMinaPlayerSkillModeData.SkillModeEntries(6, ssr, patterns);

            Assert.That(entries.Count, Is.EqualTo(3));
            Assert.That(entries.All(e => EzMinaPlayerSkillModeData.TryParseSkillId(e.SkillId, out _)));
            Assert.That(entries[0].SkillId, Is.EqualTo(EzMinaPlayerSkillModeData.ToSkillId("tech")));
            Assert.That(entries.Any(e => e.SkillId.Contains("tech", StringComparison.Ordinal)));
        }

        [Test]
        public void SkillModeEntries_filters_sub_one_mina_on_4k()
        {
            var ssr = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzMinaSkillAxis.Stream.ToMetaId()] = 20,
                [EzMinaSkillAxis.Technical.ToMetaId()] = 0.2,
            };

            var entries = EzMinaPlayerSkillModeData.SkillModeEntries(4, ssr, Array.Empty<EzMinaPlayerSkillTagPlayData>());

            Assert.That(entries.Any(e => e.SkillId == EzMinaSkillAxis.Stream.ToMetaId()));
            Assert.That(entries.All(e => e.Value >= EzMinaPlayerSkillModeData.DISPLAY_MIN));
        }
    }
}