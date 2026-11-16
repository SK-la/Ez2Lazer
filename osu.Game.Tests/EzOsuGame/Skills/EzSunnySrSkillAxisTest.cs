// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
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
    }
}