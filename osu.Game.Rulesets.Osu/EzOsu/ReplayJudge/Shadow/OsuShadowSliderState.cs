// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Shadow
{
    /// <summary>
    /// Slider 影子状态：移植 SliderInputManager head/nested/tracking 逻辑（OSL-011 抽 helper 中）。
    /// </summary>
    internal sealed class OsuShadowSliderState
    {
        internal delegate void JudgementApplier(HitObject hitObject, HitResult result, double judgementClockTime, Vector2? cursorPositionAtHit);

        private readonly Slider slider;
        private readonly SliderHeadCircle head;
        private readonly List<HitObject> nestedInOrder;
        private readonly double headMissWindow;
        private readonly HashSet<HitObject> judged = new HashSet<HitObject>();
        private readonly Dictionary<HitObject, HitResult> results = new Dictionary<HitObject, HitResult>();

        private bool tracking;
        private OsuAction? headHitAction;
        private double? timeToAcceptAnyKeyAfter;
        private readonly List<OsuAction> lastPressedActions = new List<OsuAction>();
        private bool sliderBodyJudged;

        private OsuShadowSliderState(Slider slider, SliderHeadCircle head, List<HitObject> nestedInOrder, double headMissWindow)
        {
            this.slider = slider;
            this.head = head;
            this.nestedInOrder = nestedInOrder;
            this.headMissWindow = headMissWindow;
        }

        public static IReadOnlyList<OsuShadowSliderState> CreateAll(IBeatmap beatmap, CancellationToken cancellationToken)
        {
            var list = new List<OsuShadowSliderState>();

            foreach (var hitObject in beatmap.HitObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (hitObject is not Slider slider)
                    continue;

                var head = slider.NestedHitObjects.OfType<SliderHeadCircle>().Single();
                double missWindow = head.HitWindows?.WindowFor(HitResult.Miss) ?? 0;

                var nested = slider.NestedHitObjects
                                   .Where(o => o is not SliderHeadCircle)
                                   .Where(o => o.Judgement.MaxResult != HitResult.IgnoreHit)
                                   .ToList();

                list.Add(new OsuShadowSliderState(slider, head, nested, missWindow));
            }

            return list;
        }

        public IEnumerable<double> CollectSimulationTimes()
        {
            if (headMissWindow > 0)
                yield return head.StartTime + headMissWindow;

            foreach (var nested in nestedInOrder)
                yield return nested.StartTime;

            const double step = 1;
            for (double t = slider.StartTime; t <= slider.EndTime; t += step)
                yield return t;

            yield return slider.EndTime;
        }

        public void ProcessHeadPress(double time, Vector2 position, OsuAction action, IReadOnlyList<OsuAction> pressedActions, JudgementApplier apply)
        {
            if (isJudged(head))
                return;

            if (time < head.StartTime - headMissWindow || time > head.StartTime + headMissWindow)
                return;

            if (!OsuCircleJudgement.IsInHitRadius(position, head.StackedPosition, head.Radius))
                return;

            double timeOffset = time - head.StartTime;
            HitResult result = OsuCircleJudgement.MapClassicSliderHeadIfNeeded(
                OsuCircleJudgement.ResultForPress(head.HitWindows!, timeOffset),
                head.ClassicSliderBehaviour);

            if (result.IsHit())
                headHitAction = action;

            applyJudgement(head, result, time, position, apply);
            markJudged(head, result);
            postProcessHeadJudgement(time, position, pressedActions, apply);
        }

        public void ProcessTime(double time, Vector2 cursorPosition, IReadOnlyList<OsuAction> pressedActions, JudgementApplier apply)
        {
            updateTracking(time, cursorPosition, pressedActions);

            if (isJudged(head))
            {
                foreach (var nested in nestedInOrder)
                {
                    if (isJudged(nested))
                        continue;

                    if (nested is SliderTailCircle)
                    {
                        if (time < nested.StartTime + SliderEventGenerator.TAIL_LENIENCY)
                            continue;
                    }
                    else if (time < nested.StartTime)
                    {
                        continue;
                    }

                    tryJudgeNestedObject(nested, time - nested.StartTime, time, apply);
                }
            }

            if (!isJudged(head) && time >= head.StartTime + headMissWindow)
                applyHeadMiss(time, apply);

            if (!sliderBodyJudged && isTailJudged() && time >= slider.EndTime)
                applySliderBodyJudgement(time, apply);
        }

        private void applyHeadMiss(double judgementTime, JudgementApplier apply)
        {
            HitResult result = OsuSliderJudgement.HeadMissResult(head.ClassicSliderBehaviour);
            applyJudgement(head, result, judgementTime, null, apply);
            markJudged(head, result);
        }

        private void postProcessHeadJudgement(double time, Vector2 cursorPosition, IReadOnlyList<OsuAction> pressedActions, JudgementApplier apply)
        {
            if (!isJudged(head) || !wasHit(head))
                return;

            if (!isMouseInFollowArea(cursorPosition, time, expanded: true))
                return;

            bool allTicksInRange = true;

            foreach (var nested in nestedInOrder)
            {
                if (isJudged(nested))
                    continue;

                if (nested.StartTime > time)
                    break;

                if (!isNestedInExpandedFollowArea(nested, cursorPosition))
                {
                    allTicksInRange = false;
                    break;
                }
            }

            foreach (var nested in nestedInOrder)
            {
                if (isJudged(nested))
                    continue;

                if (nested.StartTime > time)
                    break;

                applyNestedForcefully(nested, allTicksInRange, time, apply);
            }

            updateTracking(time, cursorPosition, pressedActions, forceValidPosition: allTicksInRange || isMouseInFollowArea(cursorPosition, time, expanded: false));
        }

        private void tryJudgeNestedObject(HitObject nestedObject, double startOffset, double judgementTime, JudgementApplier apply)
        {
            if (!OsuSliderJudgement.CanJudgeNestedAtOffset(nestedObject, startOffset, isJudged, nestedInOrder))
                return;

            if (!isJudged(head))
                return;

            if (tracking)
                applyNestedForcefully(nestedObject, hit: true, judgementTime, apply);
            else if (startOffset >= 0)
                applyNestedForcefully(nestedObject, hit: false, judgementTime, apply);
        }

        private void applyNestedForcefully(HitObject nestedObject, bool hit, double judgementTime, JudgementApplier apply)
        {
            if (isJudged(nestedObject))
                return;

            HitResult result = hit ? nestedObject.Judgement.MaxResult : nestedObject.Judgement.MinResult;
            applyJudgement(nestedObject, result, judgementTime, null, apply);
            markJudged(nestedObject, result);
        }

        private void applySliderBodyJudgement(double judgementTime, JudgementApplier apply)
        {
            sliderBodyJudged = true;
            HitResult result = OsuSliderJudgement.BodyResultFromNestedObjects(slider, wasHit);
            apply(slider, result, judgementTime, null);
        }

        private void updateTracking(double time, Vector2 cursorPosition, IReadOnlyList<OsuAction> pressedActions, bool? forceValidPosition = null)
        {
            bool isValidTrackingPosition = forceValidPosition ?? isMouseInFollowArea(cursorPosition, time, expanded: false);

            if (headHitAction == null)
                timeToAcceptAnyKeyAfter = null;
            else if (timeToAcceptAnyKeyAfter == null)
            {
                var otherKey = headHitAction == OsuAction.RightButton ? OsuAction.LeftButton : OsuAction.RightButton;

                if (!lastPressedActions.Contains(otherKey))
                    timeToAcceptAnyKeyAfter = time;
            }

            lastPressedActions.Clear();
            bool validTrackingAction = false;

            foreach (var action in pressedActions)
            {
                if (isValidTrackingAction(action, time))
                    validTrackingAction = true;

                lastPressedActions.Add(action);
            }

            bool allJudged = nestedInOrder.All(isJudged) && sliderBodyJudged;

            tracking = (!allJudged || time <= slider.EndTime)
                       && isValidTrackingPosition
                       && validTrackingAction;
        }

        private bool isValidTrackingAction(OsuAction action, double time)
        {
            if (headHitAction.HasValue && (!timeToAcceptAnyKeyAfter.HasValue || time <= timeToAcceptAnyKeyAfter.Value))
                return action == headHitAction;

            return action is OsuAction.LeftButton or OsuAction.RightButton;
        }

        private bool isMouseInFollowArea(Vector2 cursorPosition, double time, bool expanded)
            => OsuSliderJudgement.IsInFollowArea(
                cursorPosition,
                slider.StackedPosition,
                slider.CurvePositionAt,
                slider.StartTime,
                slider.Duration,
                time,
                slider.Radius,
                expanded);

        private bool isNestedInExpandedFollowArea(HitObject nested, Vector2 cursorPosition)
            => OsuSliderJudgement.IsNestedInExpandedFollowArea(
                nested,
                cursorPosition,
                slider.StackedPosition,
                slider.CurvePositionAt,
                slider.StartTime,
                slider.Duration,
                slider.Radius);

        private bool isTailJudged()
        {
            var tail = nestedInOrder.LastOrDefault(o => o is SliderTailCircle);
            return tail == null || isJudged(tail);
        }

        private bool isJudged(HitObject hitObject) => judged.Contains(hitObject);

        private bool wasHit(HitObject hitObject) => results.TryGetValue(hitObject, out HitResult result) && result.IsHit();

        private void markJudged(HitObject hitObject, HitResult result)
        {
            judged.Add(hitObject);
            results[hitObject] = result;
        }

        private static void applyJudgement(HitObject hitObject, HitResult result, double judgementClockTime, Vector2? cursorPosition, JudgementApplier apply)
            => apply(hitObject, result, judgementClockTime, cursorPosition);
    }
}
