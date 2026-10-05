// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement
{
    /// <summary>
    /// ClassicNative 计分挂钩点（OSL-013）。当前仅标记意图；总分公式待与 stable 对齐后写入 <see cref="ScoreProcessor"/>。
    /// </summary>
    public static class OsuClassicNativeScoring
    {
        public static bool ShouldUseLegacyScoreAlgorithm(EzEnumOsuJudgementTrack track)
            => track == EzEnumOsuJudgementTrack.ClassicNative;

        // TODO(EZ-SR-OSL-013): classic 总分 / Accuracy 与 stable 对齐；禁止继续依赖 GetDisplayScore 假换算。
    }
}
