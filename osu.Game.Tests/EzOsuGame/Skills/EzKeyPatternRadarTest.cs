// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzKeyPatternRadarTest
    {
        [Test]
        public void Stored_complete_rows_rebuild_the_live_axis_ids()
        {
            var skills = new Dictionary<string, double>
            {
                ["Bracket"] = 1.25,
                ["Chord"] = 2.5,
                ["LN%"] = 12.5,
                ["Jack"] = 0.4,
                ["Delay"] = 0.8,
                ["Dump"] = 3.1,
            };

            var radar = EzKeyPatternRadar.TryCreateFromStored(skills);

            Assert.That(radar, Is.Not.Null);
            Assert.That(radar!.Value.Count, Is.EqualTo(6));
            Assert.That(radar.Value[0].Axis, Is.EqualTo("Bracket"));
            Assert.That(radar.Value[2].Axis, Is.EqualTo("LN%"));
            Assert.That(radar.Value[2].Value, Is.EqualTo(12.5).Within(1e-9));
        }

        [Test]
        public void Incomplete_rows_do_not_build_a_radar()
        {
            var skills = new Dictionary<string, double> { ["Bracket"] = 1 };

            Assert.That(EzKeyPatternRadar.TryCreateFromStored(skills), Is.Null);
        }
    }
}
