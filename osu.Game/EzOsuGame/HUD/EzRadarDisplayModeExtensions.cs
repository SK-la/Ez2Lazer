// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.EzOsuGame.HUD
{
    public static class EzRadarDisplayModeExtensions
    {
        /// <summary>
        /// Analysis system whose skill axes match the given wedge radar mode, or <see cref="EzAnalysisSystem.None"/> when the mode is not skill-based.
        /// KeyPattern maps to <see cref="EzAnalysisSystem.KeyPattern"/> (chart structure), not player skill tags.
        /// </summary>
        public static EzAnalysisSystem SkillAxisSystem(this EzRadarDisplayMode mode) => mode switch
        {
            EzRadarDisplayMode.KeyPattern => EzAnalysisSystem.KeyPattern,
            EzRadarDisplayMode.SunnySRSkill => EzAnalysisSystem.SSR,
            EzRadarDisplayMode.MSDSkillDual or EzRadarDisplayMode.MSDSkillChart => EzAnalysisSystem.Mina,
            EzRadarDisplayMode.PPPSkillDual or EzRadarDisplayMode.PPPSkillChart => EzAnalysisSystem.PPP,
            _ => EzAnalysisSystem.None,
        };
    }
}
