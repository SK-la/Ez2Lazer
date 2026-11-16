// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.HUD;

namespace osu.Game.Tests.EzOsuGame.HUD
{
    [TestFixture]
    public class EzRadarDisplayModeExtensionsTest
    {
        [TestCase(EzRadarDisplayMode.SunnySRSkill, EzAnalysisSystem.SSR)]
        [TestCase(EzRadarDisplayMode.MSDSkillDual, EzAnalysisSystem.Mina)]
        [TestCase(EzRadarDisplayMode.KeyPattern, EzAnalysisSystem.Pattern)]
        public void Skill_axis_system_maps_radar_mode(EzRadarDisplayMode mode, EzAnalysisSystem expected)
            => Assert.That(mode.SkillAxisSystem(), Is.EqualTo(expected));
    }
}
