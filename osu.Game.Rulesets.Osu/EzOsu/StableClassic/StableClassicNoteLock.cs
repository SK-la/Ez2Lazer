// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Osu.UI;

namespace osu.Game.Rulesets.Osu.EzOsu.StableClassic
{
    /// <summary>
    /// classic note lock 求解（对应上游 <c>LegacyHitPolicy</c>）：先忽略落在叠堆前一个物件上的输入，
    /// 再要求更早的未判物件都已结束并落在可命中半径内。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="OsuNoteLockSelector"/> 的 lazer 分支并列：两者只回答顺序/可达性，
    /// 「本次结果本身为 <c>HitResult.None</c>」由调用方按判定窗先行处理。
    /// </remarks>
    public static class StableClassicNoteLock
    {
        /// <summary>结束时刻比较的额外宽容，抵消未对齐物件。</summary>
        private const double END_TIME_TOLERANCE = 3;

        /// <summary>
        /// classic note lock 的可命中半径：以 miss 窗为界，Autopilot 下收窄 200 ms。
        /// </summary>
        public static double HittableRange(bool autopilot) => OsuHitWindows.MISS_WINDOW - (autopilot ? 200 : 0);

        /// <summary>
        /// note lock 的 Drawable 侧：已判中的滑条头遮挡其下方物件，直到整条滑条判完。
        /// 让重叠物件在滑条期间拿不到输入，与叠堆/顺序检查互补。
        /// </summary>
        public static void BlockInputUnderSliderHead(DrawableSliderHead head)
            => head.HitArea.CanBeHit = () => !head.DrawableSlider.AllJudged;

        /// <summary>紧邻的前一个物件仍在叠堆且未全部判定时忽略输入（点到别的高度，不是抢拍）。</summary>
        /// <param name="previousObject">目标在容器中的紧邻前一个物件；目标为嵌套物件（滑条头）时传 <c>null</c>。</param>
        public static ClickAction CheckPrevious(OsuNoteLockEntry? previousObject)
        {
            if (previousObject is { } previous && previous.StackHeight > 0 && !previous.AllJudged)
                return ClickAction.Ignore;

            return ClickAction.Hit;
        }

        /// <summary>任何更早的未判物件若已结束（3ms 容差之外）则挡下，否则按可命中半径判时间可达性。</summary>
        /// <param name="precedingObjects">参与顺序判定的存活物件，按容器顺序；目标定位不到时（嵌套物件）传整张存活列表。</param>
        /// <param name="targetStartTime">目标物件的起始时刻。</param>
        /// <param name="time">本次输入时刻。</param>
        /// <param name="hittableRange">允许命中的时间半径。</param>
        public static ClickAction CheckOrder(IReadOnlyList<OsuNoteLockEntry> precedingObjects, double targetStartTime, double time, double hittableRange)
        {
            for (int i = 0; i < precedingObjects.Count; i++)
            {
                var entry = precedingObjects[i];

                if (entry.AllJudged)
                    continue;

                if (entry.EndTime + END_TIME_TOLERANCE < targetStartTime)
                    return ClickAction.Shake;
            }

            return Math.Abs(targetStartTime - time) < hittableRange ? ClickAction.Hit : ClickAction.Shake;
        }
    }
}
