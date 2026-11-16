// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.SkillTag.KeyPattern;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class EzAnalysisSystemStorageTest
    {
        [Test]
        public void Pattern_round_trips_as_chart_key_pattern()
        {
            Assert.That(EzAnalysisSystem.KeyPattern.ToStorageId(), Is.EqualTo("ezpattern"));
            Assert.That(EzAnalysisSystemStorage.FromStorageId("ezpattern"), Is.EqualTo(EzAnalysisSystem.KeyPattern));
        }

        [Test]
        public void Player_skill_tag_round_trips_without_occupying_pattern()
        {
            Assert.That(EzAnalysisSystem.PlayerSkillTag.ToStorageId(), Is.EqualTo("player_skill_tag"));
            Assert.That(EzAnalysisSystemStorage.FromStorageId("player_skill_tag"), Is.EqualTo(EzAnalysisSystem.PlayerSkillTag));
            Assert.That(EzAnalysisSystem.KeyPattern, Is.Not.EqualTo(EzAnalysisSystem.PlayerSkillTag));
        }

        [Test]
        public void Pattern_version_is_the_keypattern_unit()
        {
            Assert.That(EzAnalysisSystem.KeyPattern.CurrentVersion(), Is.EqualTo(EzKeyPatternUnit.VERSION));
            Assert.That(EzKeyPatternUnit.VERSION, Is.GreaterThan(0));
        }
    }
}
