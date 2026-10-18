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
using osu.Game.Rulesets.Osu.EzOsu.StableClassic;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Session
{
    /// <summary>
    /// Slider 影子状态：移植 SliderInputManager head/nested/tracking 逻辑（OSL-011 抽 helper 中）。
    /// </summary>
    internal sealed class OsuSessionSliderState
    {
        internal delegate void JudgementApplier(HitObject hitObject, HitResult result, double judgementClockTime, Vector2? cursorPositionAtHit);

        private readonly Slider slider;
        private readonly SliderHeadCircle head;
        private readonly List<HitObject> nestedInOrder;
        private readonly double headMissWindow;
        private readonly HashSet<HitObject> judged = new HashSet<HitObject>();
        private readonly Dictionary<HitObject, HitResult> results = new Dictionary<HitObject, HitResult>();

        private bool tracking;
        private double? trackingStartTime;
        private OsuAction? headHitAction;
        private double? timeToAcceptAnyKeyAfter;
        private readonly List<OsuAction> lastPressedActions = new List<OsuAction>();
        private bool sliderBodyJudged;

        private OsuSessionSliderState(Slider slider, SliderHeadCircle head, List<HitObject> nestedInOrder, double headMissWindow)
        {
            this.slider = slider;
            this.head = head;
            this.nestedInOrder = nestedInOrder;
            this.headMissWindow = headMissWindow;

            double last = Math.Max(slider.EndTime, head.StartTime + headMissWindow);

            foreach (var nested in nestedInOrder)
            {
                double nestedLast = OsuSliderJudgement.NestedDueTime(nested, slider.StartTime, slider.Duration);

                if (nestedLast > last)
                    last = nestedLast;
            }

            LastActiveTime = last;
        }

        public double StartTime => slider.StartTime;

        public double EndTime => slider.EndTime;

        /// <summary>
        /// 本滑条最后一个仍需参与模拟的时刻：主体结束、滑条头判定窗结束、各嵌套最晚开判时刻三者取大
        /// （尾部额外加 <see cref="SliderEventGenerator.TAIL_LENIENCY"/>）。
        /// 缺了它，短滑条的头或尾永远不会自动判定，整条滑条就永远停留在未判状态。
        /// </summary>
        public double LastActiveTime { get; }

        /// <summary>滑条头起始时刻；按键仲裁按它排序。</summary>
        public double HeadStartTime => head.StartTime;

        /// <summary>note lock 定位用：滑条头是嵌套物件，以其父滑条定位容器序。</summary>
        public HitObject ContainerObject => slider;

        public static IReadOnlyList<OsuSessionSliderState> CreateAll(IBeatmap beatmap, CancellationToken cancellationToken)
        {
            var list = new List<OsuSessionSliderState>();

            foreach (var hitObject in beatmap.HitObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (hitObject is not Slider slider)
                    continue;

                var head = slider.NestedHitObjects.OfType<SliderHeadCircle>().Single();
                double missWindow = head.HitWindows != null ? OsuCircleJudgement.AutoMissWindow(head.HitWindows) : 0;

                var nested = slider.NestedHitObjects
                                   .Where(o => o is not SliderHeadCircle)
                                   .Where(o => o.Judgement.MaxResult != HitResult.IgnoreHit)
                                   .ToList();

                list.Add(new OsuSessionSliderState(slider, head, nested, missWindow));
            }

            return list;
        }

        public IEnumerable<double> CollectSimulationTimes()
        {
            if (headMissWindow > 0)
                yield return head.StartTime + headMissWindow;

            foreach (var nested in nestedInOrder)
            {
                // 每个评分点的到期时刻都要有一拍：命中与否取决于「到期那一刻」的跟踪状态，
                // 用之后任意一拍的跟踪状态补判会多给 tick（stable 是逐帧按序消耗评分点）。
                yield return OsuSliderJudgement.NestedDueTime(nested, slider.StartTime, slider.Duration);

                // 默认轨的尾点宽限要到截止时刻才结算，缺这一拍尾点会悬空。
                double deadline = OsuSliderJudgement.NestedDeadline(nested, slider.ClassicSliderBehaviour, slider.StartTime, slider.Duration);

                if (deadline > OsuSliderJudgement.NestedDueTime(nested, slider.StartTime, slider.Duration))
                    yield return deadline;
            }

            yield return slider.EndTime;
            yield return LastActiveTime;
        }

        /// <summary>按键是否落在本滑条头上并可判；仅查询，不落判定。用于按键仲裁。</summary>
        public bool CanAcceptHeadPress(double time, Vector2 position) => resolveHeadPressResult(time, position).HasValue;

        public bool ProcessHeadPress(double time, Vector2 position, OsuAction action, IReadOnlyList<OsuAction> pressedActions, JudgementApplier apply)
        {
            HitResult? result = resolveHeadPressResult(time, position);

            if (result == null)
                return false;

            if (result.Value.IsHit())
                headHitAction = action;

            applyJudgement(head, result.Value, time, position, apply);
            markJudged(head, result.Value);
            postProcessHeadJudgement(time, position, pressedActions, apply);
            return true;
        }

        /// <summary>命中窗口 + 半径 + 判定窗三重前置校验；任一不成立返回 null。</summary>
        private HitResult? resolveHeadPressResult(double time, Vector2 position)
        {
            if (isJudged(head))
                return null;

            if (time < head.StartTime - headMissWindow || time > head.StartTime + headMissWindow)
                return null;

            if (!OsuCircleJudgement.IsInHitRadius(position, head.StackedPosition, head.Radius))
                return null;

            HitResult result = OsuCircleJudgement.JudgeSliderHeadPress(head.HitWindows!, time - head.StartTime, head.ClassicSliderBehaviour);

            return result == HitResult.None ? null : result;
        }

        /// <summary>
        /// note lock 抢拍：把尚未判定的头部立刻按过期结算（结果与自然过期一致，只有时刻提前）。
        /// 之后再走正常 <see cref="ProcessTime"/>，嵌套物件仍按滑条状态机判。
        /// </summary>
        public bool ForceMissHead(double judgementTime, JudgementApplier apply)
        {
            if (isJudged(head))
                return false;

            applyHeadMiss(judgementTime, apply);
            return true;
        }

        public void ProcessTime(double time, Vector2 cursorPosition, IReadOnlyList<OsuAction> pressedActions, JudgementApplier apply)
        {
            updateTracking(time, cursorPosition, pressedActions);

            // 头部超时与嵌套判定在同一时刻发生时，必须先结算头部，否则嵌套（含尾）会被整轮跳过；
            // 若该时刻正好是本滑条最后一个活跃时刻，嵌套就永远留在未判状态。
            if (!isJudged(head) && time >= head.StartTime + headMissWindow)
                applyHeadMiss(time, apply);

            // stable 逐帧按序消耗评分点：滑条头判没判不影响 tick / tail 的归属，故这里不以头部已判为前提。
            foreach (var nested in nestedInOrder)
            {
                if (isJudged(nested))
                    continue;

                if (!OsuSliderJudgement.IsNestedDue(nested, time, slider.StartTime, slider.Duration))
                    continue;

                tryJudgeNestedObject(nested, time, apply);
            }

            // 兜底：采样缺失时尾部可能一直没落判，此时按未命中结算尾部再算主体，避免整条滑条悬空。
            if (!sliderBodyJudged && time >= slider.EndTime)
            {
                foreach (var nested in nestedInOrder)
                {
                    if (isJudged(nested))
                        continue;

                    tryJudgeNestedObject(nested, time, apply);
                }

                if (!sliderBodyJudged)
                    applySliderBodyJudgement(time, apply);
            }
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

            // classic 档按「评分点到期那一刻的跟踪状态」逐点消费：晚判的滑条头不能回头补判
            // 已经到期的 tick / repeat / tail。
            if (slider.ClassicSliderBehaviour)
            {
                updateTracking(time, cursorPosition, pressedActions);
                return;
            }

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

        private void tryJudgeNestedObject(HitObject nestedObject, double judgementTime, JudgementApplier apply)
        {
            if (!OsuSliderJudgement.ArePrerequisitesJudged(nestedObject, isJudged, nestedInOrder))
                return;

            // 默认轨沿用 lazer 的「头部已判才给嵌套」语义；classic 档按 stable 逐帧消费评分点，不看头部。
            if (!slider.ClassicSliderBehaviour && !isJudged(head))
                return;

            bool hit;

            if (slider.ClassicSliderBehaviour)
            {
                hit = StableClassicSliderRules.WasTrackingAtDueTime(
                    trackingStartTime,
                    OsuSliderJudgement.NestedDueTime(nestedObject, slider.StartTime, slider.Duration));
            }
            else
                hit = tracking;

            // 默认轨保留 lazer 的尾点宽限：到期时没跟踪也先不落判，等跟踪或等到截止时刻。
            if (!hit && !OsuSliderJudgement.IsNestedSettled(nestedObject, judgementTime, slider.ClassicSliderBehaviour, slider.StartTime, slider.Duration))
                return;

            applyNestedForcefully(nestedObject, hit, judgementTime, apply);
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
            // Align SliderInputManager.Update: once tracking, use expanded follow area; otherwise ball radius only.
            bool isValidTrackingPosition = forceValidPosition ?? isMouseInFollowArea(cursorPosition, time, expanded: tracking);

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

            bool wasTracking = tracking;

            tracking = (!allJudged || time <= slider.EndTime)
                       && isValidTrackingPosition
                       && validTrackingAction;

            if (tracking && !wasTracking)
                trackingStartTime = time;
            else if (!tracking)
                trackingStartTime = null;
        }

        private bool isValidTrackingAction(OsuAction action, double time)
        {
            if (action != OsuAction.LeftButton && action != OsuAction.RightButton)
                return false;

            if (slider.ClassicSliderBehaviour)
                return StableClassicSliderRules.AcceptsAnyTrackingKey(action);

            if (headHitAction.HasValue && (!timeToAcceptAnyKeyAfter.HasValue || time <= timeToAcceptAnyKeyAfter.Value))
                return action == headHitAction;

            return true;
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
