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

        /// <summary>Drawable 路径保留 <see cref="HitResult.None"/>（尚未可判）；Session 请先 <see cref="ResultForPress"/>。</summary>
        public static HitResult MapClassicSliderHeadIfNeeded(HitResult windowResult, bool classicSliderBehaviour)
        {
            if (!classicSliderBehaviour || windowResult == HitResult.None)
                return windowResult;

            return MapClassicSliderHead(windowResult);
        }
    }
}
