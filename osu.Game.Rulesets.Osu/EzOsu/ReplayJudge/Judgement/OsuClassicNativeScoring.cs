// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement
{
    /// <summary>
    /// ClassicNative 计分 / 窗口挂钩（OSL-013）。
    /// 窗口已对齐 stable OD 半窗；总分走 <see cref="ScoreProcessor.IsLegacyScore"/> classic 算法。
    /// </summary>
    public static class OsuClassicNativeScoring
    {
        public static bool ShouldUseLegacyScoreAlgorithm(EzEnumOsuJudgementTrack track)
            => track == EzEnumOsuJudgementTrack.ClassicNative;

        /// <summary>
        /// 就地替换谱面物件 HitWindows（与 Mania BindJudgements 同纪律：Drawable 与 Session 共享同一 beatmap 实例）。
        /// </summary>
        public static void ApplyHitWindowsToBeatmap(IBeatmap beatmap)
        {
            double od = beatmap.Difficulty.OverallDifficulty;

            foreach (var hitObject in beatmap.HitObjects)
                applyRecursive(hitObject, od);
        }

        private static void applyRecursive(HitObject hitObject, double overallDifficulty)
        {
            if (hitObject.HitWindows != null && !ReferenceEquals(hitObject.HitWindows, HitWindows.Empty))
            {
                var classic = new OsuClassicNativeHitWindows();
                classic.SetDifficulty(overallDifficulty);
                hitObject.HitWindows = classic;
            }

            foreach (var nested in hitObject.NestedHitObjects)
                applyRecursive(nested, overallDifficulty);
        }
    }
}
