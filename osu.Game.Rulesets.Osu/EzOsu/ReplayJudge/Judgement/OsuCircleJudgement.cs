// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement
{
    /// <summary>
    /// HitCircle / SliderHead 共用判定纯函数（OSL-011）：Drawable 与 Session 同调入口。
    /// </summary>
    public static class OsuCircleJudgement
    {
        /// <summary>与 <see cref="HitWindows.ResultFor"/> 一致；未命中窗口时为 <see cref="HitResult.None"/>。</summary>
        public static HitResult ResultFor(HitWindows hitWindows, double timeOffset)
            => hitWindows.ResultFor(timeOffset);

        /// <summary>
        /// Session 按键：与 Drawable 同调——<see cref="HitResult.None"/> 表示尚未可判，不落 Miss。
        /// </summary>
        public static HitResult ResultForPress(HitWindows hitWindows, double timeOffset)
            => hitWindows.ResultFor(timeOffset);

        /// <summary>对齐 Drawable <c>!CanBeHit</c> 自动 Miss 的半窗（LowestSuccessful，osu 为 Meh）。</summary>
        public static double AutoMissWindow(HitWindows hitWindows)
            => hitWindows.WindowFor(HitResult.Meh);

        public static bool IsInHitRadius(Vector2 cursorPosition, Vector2 stackedPosition, double radius)
            => Vector2.Distance(cursorPosition, stackedPosition) <= radius;

        /// <summary>Classic slider head：命中窗口内任意有效 hit → LargeTickHit，否则 LargeTickMiss。</summary>
        public static HitResult MapClassicSliderHead(HitResult windowResult)
            => windowResult.IsHit() ? HitResult.LargeTickHit : HitResult.LargeTickMiss;

        // Drawable / Session 共用：命中窗内任意有效 hit → LargeTickHit，窗外的 None 原样透出（尚未可判）。
        private static HitResult mapClassicSliderHeadIfNeeded(HitResult windowResult, bool classicSliderBehaviour)
        {
            if (!classicSliderBehaviour || windowResult == HitResult.None)
                return windowResult;

            return MapClassicSliderHead(windowResult);
        }

        /// <summary>
        /// 滑条头按下的唯一判定入口：Drawable 与 Session 都必须从这里取类型，避免两条路径的窗口映射分叉。
        /// </summary>
        /// <param name="hitWindows">滑条头自身的判定窗；classic 档为 stable 窗，经典 mod 为普通窗。</param>
        /// <param name="timeOffset">按下时刻相对滑条头起始时刻的裸帧偏移。</param>
        /// <param name="classicSliderBehaviour">滑条头是否按 tick 入账；未命中窗口时返回 <see cref="HitResult.None"/> 表示尚不可判。</param>
        public static HitResult JudgeSliderHeadPress(HitWindows hitWindows, double timeOffset, bool classicSliderBehaviour)
            => mapClassicSliderHeadIfNeeded(hitWindows.ResultFor(timeOffset), classicSliderBehaviour);
    }
}
