// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.SSR;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzSunnySrSkillAxisTest
    {
        [Test]
        public void Chart_axis_ids_match_contract()
        {
            var ids = EzSunnySrSkillAxisExtensions.ChartAxisIds;
            Assert.That(ids, Is.EquivalentTo(new[] { "bracket", "jack", "burst", "ln_hold", "ln_release", "anchor" }));
        }

        [Test]
        public void Stored_radar_requires_full_axis_set()
        {
            var partial = new Dictionary<string, double> { ["bracket"] = 1 };
            Assert.That(EzSunnySrSkillRadar.TryCreateFromStored(partial), Is.Null);

            var complete = new Dictionary<string, double>();

            foreach (string id in EzSunnySrSkillAxisExtensions.ChartAxisIds)
                complete[id] = 1.5;

            Assert.That(EzSunnySrSkillRadar.TryCreateFromStored(complete)?.Count, Is.EqualTo(6));

            var slice = new EzAnalysisResult(new KpsSummary(1, 2, null));
            var overlaid = slice.WithPersistedSsrRadar(EzSunnySrSkillRadar.TryCreateFromStored(complete)!.Value);

            Assert.That(overlaid.SsrPatternRadar?.Count, Is.EqualTo(6));
            Assert.That(overlaid.WithPersistedSsrRadar(default).SsrPatternRadar?.Count, Is.EqualTo(6));
        }
    }
}