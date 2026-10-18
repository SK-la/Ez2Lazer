// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.StableClassic
{
    /// <summary>
    /// stable classic 滑条判定规则：只描述与 lazer 默认轨不同的那部分语义。
    /// </summary>
    /// <remarks>
    /// 两档共用的到期时刻（<c>OsuSliderJudgement.NestedDueTime</c> / <c>TailDueTime</c>）不在此处；
    /// 本类只回答「classic 档怎么解释这个时刻」。
    /// </remarks>
    public static class StableClassicSliderRules
    {
        /// <summary>滑条头未命中落 tick 档：stable 把滑条头当作评分点，而非独立判定。</summary>
        public static HitResult HeadMissResult => HitResult.LargeTickMiss;

        /// <summary>
        /// 滑条主体按命中比例给档：全中 300、全失 Miss、过半 100、其余 50。
        /// 分母含滑条头自身与全部 tick / repeat / tail。
        /// </summary>
        public static HitResult BodyResultFromNestedHits(int totalNested, int hitNested)
        {
            if (hitNested == totalNested)
                return HitResult.Great;
            if (hitNested == 0)
                return HitResult.Miss;

            return (double)hitNested / totalNested >= 0.5 ? HitResult.Ok : HitResult.Meh;
        }

        /// <summary>
        /// 评分点到期即截止：到期那一刻没在跟踪就是没命中，之后重新接上也不能补判。
        /// </summary>
        public static double NestedDeadline(double dueTime) => dueTime;

        /// <summary>只有跟踪覆盖了评分点的到期时刻才算命中。</summary>
        public static bool WasTrackingAtDueTime(double? trackingStartTime, double dueTime)
            => trackingStartTime is { } started && started <= dueTime;

        /// <summary>任意主键都算跟踪：换手指、或另一键先按着，都不打断。</summary>
        public static bool AcceptsAnyTrackingKey(OsuAction action)
            => action is OsuAction.LeftButton or OsuAction.RightButton;
    }
}
