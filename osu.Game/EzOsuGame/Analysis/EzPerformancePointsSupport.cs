// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Analysis
{
    public static class EzPerformancePointsSupport
    {
        /// <summary>
        /// 官方 perfect PP 管线版本：各 ruleset 的 <see cref="Rulesets.Difficulty.DifficultyCalculator.Version"/>。
        /// PP 为 ppy 全模式指标，与 Ez xxy SR 无关。
        /// </summary>
        public static bool TryGetPerformancePointsVersion(IRulesetInfo rulesetInfo, IWorkingBeatmap difficultyCalculatorBeatmap, out int version)
        {
            version = 0;

            if (rulesetInfo is not RulesetInfo localRulesetInfo || !localRulesetInfo.Available)
                return false;

            try
            {
                var ruleset = localRulesetInfo.CreateInstance();

                if (ruleset.CreatePerformanceCalculator() == null)
                    return false;

                version = ruleset.CreateDifficultyCalculator(difficultyCalculatorBeatmap).Version;
                return version >= 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
