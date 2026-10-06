// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Objects;
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
            => classicSliderBehaviour ? HitResult.LargeTickMiss : HitResult.Miss;

        public static HitResult BodyResultFromNestedHits(bool classicSliderBehaviour, int totalNested, int hitNested, HitResult maxResult, HitResult minResult)
        {
            if (classicSliderBehaviour)
            {
                if (hitNested == totalNested)
                    return HitResult.Great;
                if (hitNested == 0)
                    return HitResult.Miss;

                double hitFraction = (double)hitNested / totalNested;
                return hitFraction >= 0.5 ? HitResult.Ok : HitResult.Meh;
            }

            return hitNested > 0 ? maxResult : minResult;
        }

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
        /// 嵌套物件在 <paramref name="startOffset"/> 下可否开判。
        /// 返回 false 表示尚早；tail 还要求前序 tick/repeat 已判。
        /// </summary>
        public static bool CanJudgeNestedAtOffset(HitObject nestedObject, double startOffset, Func<HitObject, bool> isJudged, IEnumerable<HitObject> nestedInOrder)
        {
            switch (nestedObject)
            {
                case SliderRepeat:
                case SliderTick:
                    return startOffset >= 0;

                case SliderTailCircle:
                    if (startOffset < SliderEventGenerator.TAIL_LENIENCY)
                        return false;

                    var lastTick = nestedInOrder.LastOrDefault(o => o is SliderTick or SliderRepeat);
                    return lastTick == null || isJudged(lastTick);

                default:
                    return false;
            }
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
