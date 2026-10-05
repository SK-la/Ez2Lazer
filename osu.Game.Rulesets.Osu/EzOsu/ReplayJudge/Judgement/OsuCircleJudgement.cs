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
        /// Session / Auto 按键路径：窗口内按键若 <see cref="HitResult.None"/> 则落为 Miss
        /// （对齐 Shadow 桥既有语义，与 Drawable「None 则等待」在边界上仍可能不同——Drawable 另有 CheckHittable）。
        /// </summary>
        public static HitResult ResultForPress(HitWindows hitWindows, double timeOffset)
        {
            HitResult result = hitWindows.ResultFor(timeOffset);
            return result == HitResult.None ? HitResult.Miss : result;
        }

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
