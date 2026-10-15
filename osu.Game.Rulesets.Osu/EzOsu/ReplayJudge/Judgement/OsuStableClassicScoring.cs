// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.EzOsu.StableClassic;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement
{
    /// <summary>
    /// StableClassic 计分 / 窗口挂钩（OSL-013）。
    /// 窗口已对齐 stable OD 半窗；总分走 <see cref="StableStdScoring"/> 自管的 peppy-stars 累加。
    /// </summary>
    public static class OsuStableClassicScoring
    {
        public static bool ShouldUseLegacyScoreAlgorithm(EzEnumJudgeMode track)
            => track == EzEnumJudgeMode.StableClassic;

        /// <summary>
        /// 就地绑定 classic 判定语义：替换谱面物件 HitWindows，并在 stable 轨下把滑条切到 classic 判定
        /// （主体按 tick 命中比例给 300/100/50、滑条头与尾按 tick 记）。
        /// 与 Mania BindJudgements 同纪律：Drawable 与 Session 共享同一 beatmap 实例，且必须在全谱模拟前完成。
        /// </summary>
        public static void BindClassicSemanticsToBeatmap(IBeatmap beatmap)
        {
            double od = beatmap.Difficulty.OverallDifficulty;

            foreach (var hitObject in beatmap.HitObjects)
                applyRecursive(hitObject, od);
        }

        private static void applyRecursive(HitObject hitObject, double overallDifficulty)
        {
            if (hitObject.HitWindows != null && !ReferenceEquals(hitObject.HitWindows, HitWindows.Empty))
            {
                HitWindows classic = new StableStdHitWindows();
                classic.SetDifficulty(overallDifficulty);
                hitObject.HitWindows = classic;
            }

            if (hitObject is Slider slider)
                slider.ClassicSliderBehaviour = true;

            foreach (var nested in hitObject.NestedHitObjects)
                applyRecursive(nested, overallDifficulty);
        }
    }
}
