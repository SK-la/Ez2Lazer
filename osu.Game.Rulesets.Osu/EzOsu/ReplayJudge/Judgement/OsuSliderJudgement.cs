// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.EzOsu.StableClassic;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement
{
    /// <summary>
    /// Slider nested / body / follow-area 判定纯函数（OSL-011）：Drawable 与 Session 同调。
    /// </summary>
    public static class OsuSliderJudgement
    {
        public static HitResult HeadMissResult(bool classicSliderBehaviour)
            => classicSliderBehaviour ? StableClassicSliderRules.HeadMissResult : HitResult.Miss;

        /// <summary>
        /// 滑条主体判定：classic 按命中比例给档（见 <see cref="StableClassicSliderRules.BodyResultFromNestedHits"/>），
        /// 默认轨任一评分点命中即满档。
        /// </summary>
        public static HitResult BodyResultFromNestedHits(bool classicSliderBehaviour, int totalNested, int hitNested, HitResult maxResult, HitResult minResult)
            => classicSliderBehaviour
                ? StableClassicSliderRules.BodyResultFromNestedHits(totalNested, hitNested)
                : (hitNested > 0 ? maxResult : minResult);

        public static HitResult BodyResultFromNestedObjects(Slider slider, Func<HitObject, bool> wasHit)
        {
            int total = slider.NestedHitObjects.Count;
            int hit = slider.NestedHitObjects.Count(wasHit);
            return BodyResultFromNestedHits(
                slider.ClassicSliderBehaviour,
                total,
                hit,
                slider.Judgement.MaxResult,
                slider.Judgement.MinResult);
        }

        /// <summary>
        /// 滑条最后一个评分点的到期时刻：常规长度下是结束前 36ms；短于 72ms 的滑条提前到半程
        /// （stable 会把最后一个评分点重排到「半程」与「结束前 36ms」中较晚者）。
        /// </summary>
        public static double TailDueTime(double sliderStartTime, double sliderDuration)
            => Math.Max(sliderStartTime + sliderDuration / 2, sliderStartTime + sliderDuration + SliderEventGenerator.TAIL_LENIENCY);

        /// <summary>嵌套评分点的到期时刻：tick / repeat 取自身时刻，tail 取 <see cref="TailDueTime"/>。</summary>
        public static double NestedDueTime(HitObject nestedObject, double sliderStartTime, double sliderDuration)
            => nestedObject is SliderTailCircle
                ? TailDueTime(sliderStartTime, sliderDuration)
                : nestedObject.StartTime;

        /// <summary>
        /// 结算截止时刻：到期时还没跟踪，到这个时刻就必须落判。
        /// </summary>
        /// <remarks>
        /// classic 到期即截止（stable 逐帧按序消费评分点，错过就是错过）；
        /// 默认轨保留 lazer 的尾点宽限：尾点可以从到期一直补到滑条结束。
        /// </remarks>
        public static double NestedDeadline(HitObject nestedObject, bool classicSliderBehaviour, double sliderStartTime, double sliderDuration)
        {
            double dueTime = NestedDueTime(nestedObject, sliderStartTime, sliderDuration);
            return classicSliderBehaviour ? StableClassicSliderRules.NestedDeadline(dueTime) : Math.Max(nestedObject.StartTime, dueTime);
        }

        /// <summary>该嵌套物件是否已到期。到期后才允许结算（命中或丢失）。</summary>
        public static bool IsNestedDue(HitObject nestedObject, double time, double sliderStartTime, double sliderDuration)
            => time >= NestedDueTime(nestedObject, sliderStartTime, sliderDuration);

        /// <summary>到期仍未跟踪时是否必须立刻落判（否则继续等跟踪）。</summary>
        public static bool IsNestedSettled(HitObject nestedObject, double time, bool classicSliderBehaviour, double sliderStartTime, double sliderDuration)
            => time >= NestedDeadline(nestedObject, classicSliderBehaviour, sliderStartTime, sliderDuration);

        /// <summary>
        /// 尾部还要求前序 tick / repeat 已判：评分点按序消耗，尾点排在它们之后。
        /// </summary>
        public static bool ArePrerequisitesJudged(HitObject nestedObject, Func<HitObject, bool> isJudged, IEnumerable<HitObject> nestedInOrder)
        {
            if (nestedObject is not SliderTailCircle)
                return true;

            var lastTick = nestedInOrder.LastOrDefault(o => o is SliderTick or SliderRepeat);
            return lastTick == null || isJudged(lastTick);
        }

        public static float FollowRadius(double sliderRadius, bool expanded)
        {
            float radius = (float)sliderRadius;
            if (expanded)
                radius *= DrawableSliderBall.FOLLOW_AREA;
            return radius;
        }

        public static bool IsInFollowArea(
            Vector2 cursorPosition,
            Vector2 sliderStackedPosition,
            Func<double, Vector2> curvePositionAt,
            double sliderStartTime,
            double sliderDuration,
            double time,
            double sliderRadius,
            bool expanded)
        {
            float radius = FollowRadius(sliderRadius, expanded);
            double followProgress = Math.Clamp((time - sliderStartTime) / sliderDuration, 0, 1);
            Vector2 followCirclePosition = sliderStackedPosition + curvePositionAt(followProgress);
            return (cursorPosition - followCirclePosition).LengthSquared <= radius * radius;
        }

        public static bool IsNestedInExpandedFollowArea(
            HitObject nested,
            Vector2 cursorPosition,
            Vector2 sliderStackedPosition,
            Func<double, Vector2> curvePositionAt,
            double sliderStartTime,
            double sliderDuration,
            double sliderRadius)
        {
            float radius = FollowRadius(sliderRadius, expanded: true);
            double objectProgress = Math.Clamp((nested.StartTime - sliderStartTime) / sliderDuration, 0, 1);
            Vector2 objectPosition = sliderStackedPosition + curvePositionAt(objectProgress);
            return (cursorPosition - objectPosition).LengthSquared <= radius * radius;
        }
    }
}
