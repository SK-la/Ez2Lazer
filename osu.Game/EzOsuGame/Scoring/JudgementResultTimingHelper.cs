// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Judgements;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Headless Session 写入 <see cref="JudgementResult"/> 时间与 rate（与 Drawable ApplyResult 边界一致）。
    /// </summary>
    public static class JudgementResultTimingHelper
    {
        public static void ApplyTiming(JudgementResult result, double timeOffset, double gameplayRate)
        {
            result.TimeOffset = timeOffset;
            result.GameplayRate = gameplayRate;
        }
    }
}
