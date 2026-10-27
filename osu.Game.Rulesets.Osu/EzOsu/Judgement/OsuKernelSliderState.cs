// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement;
using osu.Game.Rulesets.Osu.EzOsu.StableClassic;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.Judgement
{
    /// <summary>
    /// 滑条判定状态：头、tick / repeat / tail、跟踪区间与主体聚合。
    /// 打分点按到期时刻结算，跟踪状态由 <see cref="IOsuJudgementSampleSource"/> 在到期时刻取值。
    /// </summary>
    internal sealed class OsuKernelSliderState : IOsuKernelDeadlineOwner
    {
        private readonly Slider slider;
        private readonly SliderHeadCircle head;
        private readonly List<HitObject> nestedInOrder;
        private readonly HitObject? lastScoredNested;
        private readonly double headMissWindow;
        private readonly HashSet<HitObject> judged = new HashSet<HitObject>();
        private readonly Dictionary<HitObject, HitResult> results = new Dictionary<HitObject, HitResult>();
        private readonly Dictionary<HitObject, double> retryDeadlines = new Dictionary<HitObject, double>();
        private readonly Dictionary<HitObject, double> judgementTimes = new Dictionary<HitObject, double>();
        private readonly List<HitObject> rewindScratch = new List<HitObject>();
        private readonly List<(double time, bool tracking)> trackingHistory = new List<(double time, bool tracking)>();

        private readonly IOsuJudgementSampleSource samples;
        private readonly Action<OsuJudgementEvent> emit;

        private bool tracking;
        private double? trackingStartTime;
        private OsuAction? headHitAction;
        private double? headJudgedTime;
        private double? timeToAcceptAnyKeyAfter;
        private bool sliderBodyJudged;
        private double sliderBodyJudgedTime;

        public OsuKernelSliderState(Slider slider, int deadlineOrder, IOsuJudgementSampleSource samples, Action<OsuJudgementEvent> emit)
        {
            this.slider = slider;
            this.samples = samples;
            this.emit = emit;
            DeadlineOrder = deadlineOrder;

            head = slider.NestedHitObjects.OfType<SliderHeadCircle>().Single();
            headMissWindow = head.HitWindows != null ? OsuCircleJudgement.AutoMissWindow(head.HitWindows) : 0;

            nestedInOrder = slider.NestedHitObjects
                                  .Where(o => o is not SliderHeadCircle)
                                  .Where(o => o.Judgement.MaxResult != HitResult.IgnoreHit)
                                  .ToList();

            lastScoredNested = nestedInOrder.LastOrDefault(o => o is SliderTick or SliderRepeat);

            double last = Math.Max(slider.EndTime, head.StartTime + headMissWindow);

            foreach (var nested in nestedInOrder)
            {
                double nestedLast = OsuSliderJudgement.NestedDueTime(nested, slider.StartTime, slider.Duration);

                if (nestedLast > last)
                    last = nestedLast;
            }

            // stable 的 int 结束已关掉身窗。若跟踪到真实结束，本滑条永不离开活跃范围，后续滑条会一直留在每帧遍历里。
            if (slider.RevealEndTimeWrapsUnderStableRules)
                last = slider.StableRevealEndTime;

            LastActiveTime = last;
        }

        public int DeadlineOrder { get; }

        public double StartTime => slider.StartTime;

        public double EndTime => slider.EndTime;

        public double LastActiveTime { get; }

        public double HeadStartTime => head.StartTime;

        /// <summary>滑条判定最早可能取样的时刻：滑条头按下窗口的起点（可早于滑条起始时刻）。</summary>
        public double EarliestActiveTime => head.StartTime - headMissWindow;

        public HitObject ContainerObject => slider;

        /// <summary>滑条头判定目标（策略查询与内核通知都以它为目标）。</summary>
        public HitObject HeadObject => head;

        public bool IsActiveAt(double time) => time >= EarliestActiveTime && time <= LastActiveTime + 1;

        /// <summary>滑条头是否已落判；已判后按下只做输入消费，不再询问策略。</summary>
        public bool HeadJudged => isJudged(head);

        /// <summary>滑条头在给定按下时刻的原始窗口结果（不因未命中而返回 null）。</summary>
        public HitResult HeadResultAt(double time, Vector2 position)
        {
            if (isJudged(head))
                return HitResult.None;

            if (!IsHeadPositionInRange(position))
                return HitResult.None;

            return OsuCircleJudgement.JudgeSliderHeadPress(head.HitWindows!, time - head.StartTime, head.ClassicSliderBehaviour);
        }

        private bool IsHeadPositionInRange(Vector2 position)
            => OsuCircleJudgement.IsInHitRadius(position, head.StackedPosition, head.Radius);

        /// <summary>滑条头的可按下区间（可早于滑条起始时刻）。候选仲裁不得用 <see cref="IsActiveAt"/> 过滤。</summary>
        public bool IsHeadPressWindowAt(double time) => time >= head.StartTime - headMissWindow && time <= head.StartTime + headMissWindow;

        public double? NextDeadline
        {
            get
            {
                double? next = null;

                if (!isJudged(head))
                {
                    double headDeadline = head.StartTime + headMissWindow;

                    // 默认轨的嵌套评分点必须等滑条头落下才能落判，提前入队只会让队首空转。
                    if (!slider.ClassicSliderBehaviour)
                        return headDeadline;

                    next = headDeadline;
                }

                for (int i = 0; i < nestedInOrder.Count; i++)
                {
                    HitObject nested = nestedInOrder[i];

                    if (isJudged(nested))
                        continue;

                    double due = effectiveNestedDue(nested);

                    // 默认轨尾点到期时若未跟踪，会宽限到截止时刻再落判；队列必须等到那个时刻。
                    if (retryDeadlines.TryGetValue(nested, out double retry) && retry > due)
                        due = retry;

                    if (next == null || due < next)
                        next = due;
                }

                if (!sliderBodyJudged && (next == null || slider.EndTime < next))
                    next = slider.EndTime;

                return next;
            }
        }

        /// <summary>嵌套评分点的队内到期时刻：默认轨不能早于滑条头落判的时刻，尾点不能早于其前序评分点。</summary>
        private double effectiveNestedDue(HitObject nested)
        {
            double due = OsuSliderJudgement.NestedDueTime(nested, slider.StartTime, slider.Duration);

            if (!slider.ClassicSliderBehaviour && headJudgedTime is double headTime && due < headTime)
                due = headTime;

            // 尾点要求前序 tick / repeat 已判；前序到期时刻更晚时，队首必须等前序，否则会空转到永远。
            if (nested is SliderTailCircle && lastScoredNested != null)
            {
                double prerequisiteDue = OsuSliderJudgement.NestedDueTime(lastScoredNested, slider.StartTime, slider.Duration);

                if (prerequisiteDue > due)
                    due = prerequisiteDue;
            }

            return due;
        }

        public void SettleAt(double deadline)
        {
            track(deadline);

            if (!isJudged(head) && deadline >= head.StartTime + headMissWindow)
                applyHeadMiss(deadline);

            for (int i = 0; i < nestedInOrder.Count; i++)
            {
                HitObject nested = nestedInOrder[i];

                if (isJudged(nested))
                    continue;

                if (deadline < effectiveNestedDue(nested))
                    continue;

                tryJudgeNestedObject(nested, deadline);
            }

            if (!sliderBodyJudged && deadline >= slider.EndTime)
            {
                for (int i = 0; i < nestedInOrder.Count; i++)
                {
                    HitObject nested = nestedInOrder[i];

                    if (!isJudged(nested))
                        tryJudgeNestedObject(nested, deadline);
                }

                if (!sliderBodyJudged)
                    applySliderBodyJudgement(deadline);
            }
        }

        /// <summary>只更新跟踪状态，并在跟踪恢复时补判已到期、仍在宽限期内的评分点。返回是否产生了判定。</summary>
        public bool Track(double time)
        {
            if (!IsActiveAt(time))
                return false;

            bool wasTracking = tracking;
            track(time);

            if (!tracking || wasTracking || slider.ClassicSliderBehaviour)
                return false;

            bool judged = false;

            for (int i = 0; i < nestedInOrder.Count; i++)
            {
                HitObject nested = nestedInOrder[i];

                if (isJudged(nested))
                    continue;

                if (time < effectiveNestedDue(nested))
                    break;

                judged |= tryJudgeNestedObject(nested, time);
            }

            return judged;
        }

        /// <summary>曲终兜底：把剩余目标一律结算，避免悬空。</summary>
        public void FinalizeAt(double judgementTime)
        {
            if (!isJudged(head))
                applyHeadMiss(judgementTime);

            for (int i = 0; i < nestedInOrder.Count; i++)
            {
                HitObject nested = nestedInOrder[i];

                if (!isJudged(nested))
                    applyNestedForcefully(nested, false, judgementTime);
            }

            if (!sliderBodyJudged)
                applySliderBodyJudgement(judgementTime);
        }

        /// <summary>倒带：撤销判定时刻晚于 <paramref name="time"/> 的落判，并把跟踪区间恢复到该时刻。</summary>
        public void RewindTo(double time)
        {
            rewindScratch.Clear();

            foreach (var entry in judgementTimes)
            {
                if (entry.Value > time)
                    rewindScratch.Add(entry.Key);
            }

            for (int i = 0; i < rewindScratch.Count; i++)
            {
                HitObject target = rewindScratch[i];

                judged.Remove(target);
                results.Remove(target);
                judgementTimes.Remove(target);
            }

            rewindScratch.Clear();
            retryDeadlines.Clear();

            if (!isJudged(head))
            {
                headJudgedTime = null;
                headHitAction = null;
                timeToAcceptAnyKeyAfter = null;
            }

            if (sliderBodyJudged && sliderBodyJudgedTime > time)
                sliderBodyJudged = false;

            while (trackingHistory.Count > 0 && trackingHistory[trackingHistory.Count - 1].time > time)
                trackingHistory.RemoveAt(trackingHistory.Count - 1);

            if (trackingHistory.Count == 0)
            {
                tracking = false;
                trackingStartTime = null;
            }
            else
            {
                var last = trackingHistory[trackingHistory.Count - 1];
                tracking = last.tracking;
                trackingStartTime = last.tracking ? last.time : null;
            }
        }

        public bool CanAcceptHeadPress(double time, Vector2 position) => resolveHeadPressResult(time, position).HasValue;

        public bool ProcessHeadPress(double time, Vector2 position, OsuAction action)
        {
            HitResult? result = resolveHeadPressResult(time, position);

            if (result == null)
                return false;

            if (result.Value.IsHit())
                headHitAction = action;

            applyJudgement(head, result.Value, time, position);
            markJudged(head, result.Value, time);
            headJudgedTime = time;
            postProcessHeadJudgement(time, position);
            return true;
        }

        public bool ForceMissHead(double judgementTime)
        {
            if (isJudged(head))
                return false;

            applyHeadMiss(judgementTime);
            return true;
        }

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

        private void applyHeadMiss(double judgementTime)
        {
            HitResult result = OsuSliderJudgement.HeadMissResult(head.ClassicSliderBehaviour);
            applyJudgement(head, result, judgementTime, null);
            markJudged(head, result, judgementTime);
            headJudgedTime = judgementTime;
        }

        private void postProcessHeadJudgement(double time, Vector2 cursorPosition)
        {
            if (!isJudged(head) || !wasHit(head))
                return;

            if (!isMouseInFollowArea(cursorPosition, time, expanded: true))
                return;

            if (slider.ClassicSliderBehaviour)
            {
                updateTracking(time, cursorPosition, forceValidPosition: null);
                return;
            }

            bool allTicksInRange = true;

            for (int i = 0; i < nestedInOrder.Count; i++)
            {
                HitObject nested = nestedInOrder[i];

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

            for (int i = 0; i < nestedInOrder.Count; i++)
            {
                HitObject nested = nestedInOrder[i];

                if (isJudged(nested))
                    continue;

                if (nested.StartTime > time)
                    break;

                applyNestedForcefully(nested, allTicksInRange, time);
            }

            updateTracking(time, cursorPosition, forceValidPosition: allTicksInRange || isMouseInFollowArea(cursorPosition, time, expanded: false));
        }

        private bool tryJudgeNestedObject(HitObject nestedObject, double judgementTime)
        {
            if (!OsuSliderJudgement.ArePrerequisitesJudged(nestedObject, isJudged, lastScoredNested))
                return false;

            if (!slider.ClassicSliderBehaviour && !isJudged(head))
                return false;

            bool hit;

            if (slider.ClassicSliderBehaviour)
            {
                hit = StableClassicSliderRules.WasTrackingAtDueTime(
                    trackingStartTime,
                    OsuSliderJudgement.NestedDueTime(nestedObject, slider.StartTime, slider.Duration));
            }
            else
                hit = tracking;

            if (!hit && !OsuSliderJudgement.IsNestedSettled(nestedObject, judgementTime, slider.ClassicSliderBehaviour, slider.StartTime, slider.Duration))
            {
                // 尚未到必须落判的截止时刻：把下一次评估挪到截止时刻，避免队首卡在已过的到期时刻。
                retryDeadlines[nestedObject] = OsuSliderJudgement.NestedDeadline(nestedObject, slider.ClassicSliderBehaviour, slider.StartTime, slider.Duration);
                return false;
            }

            return applyNestedForcefully(nestedObject, hit, judgementTime);
        }

        private bool applyNestedForcefully(HitObject nestedObject, bool hit, double judgementTime)
        {
            if (isJudged(nestedObject))
                return false;

            HitResult result = hit ? nestedObject.Judgement.MaxResult : nestedObject.Judgement.MinResult;
            applyJudgement(nestedObject, result, judgementTime, null);
            markJudged(nestedObject, result, judgementTime);
            return true;
        }

        private void applySliderBodyJudgement(double judgementTime)
        {
            sliderBodyJudged = true;
            sliderBodyJudgedTime = judgementTime;
            HitResult result = OsuSliderJudgement.BodyResultFromNestedObjects(slider, wasHit);
            emit(OsuJudgementEvent.Hit(slider, result, judgementTime));
        }

        private void track(double time)
        {
            if (samples.IsRewinding)
            {
                restoreTrackingHistory(time);
                return;
            }

            updateTracking(time, samples.PositionAt(time), forceValidPosition: null);
        }

        private void restoreTrackingHistory(double time)
        {
            while (trackingHistory.Count > 0 && trackingHistory[trackingHistory.Count - 1].time > time)
                trackingHistory.RemoveAt(trackingHistory.Count - 1);

            if (trackingHistory.Count == 0)
            {
                tracking = false;
                trackingStartTime = null;
                return;
            }

            var last = trackingHistory[trackingHistory.Count - 1];
            tracking = last.tracking;
            trackingStartTime = last.tracking ? last.time : null;
        }

        private void updateTracking(double time, Vector2 cursorPosition, bool? forceValidPosition)
        {
            bool isValidTrackingPosition = forceValidPosition ?? isMouseInFollowArea(cursorPosition, time, expanded: tracking);
            bool leftPressed = isPressedAt(OsuAction.LeftButton, time);
            bool rightPressed = isPressedAt(OsuAction.RightButton, time);

            if (headHitAction == null)
                timeToAcceptAnyKeyAfter = null;
            else if (timeToAcceptAnyKeyAfter == null)
            {
                // 与 Drawable 侧一致：看「上一帧」是否按着另一键。同帧双键起手时另一键尚未算作已按，
                // 否则同时按两键的滑条会永远停在「只能用起手键跟踪」。
                OsuAction otherKey = headHitAction == OsuAction.RightButton ? OsuAction.LeftButton : OsuAction.RightButton;

                if (!samples.WasPressedAt(otherKey, time))
                    timeToAcceptAnyKeyAfter = time;
            }

            bool validTrackingAction = false;

            if (leftPressed && isValidTrackingAction(OsuAction.LeftButton, time))
                validTrackingAction = true;

            if (rightPressed && isValidTrackingAction(OsuAction.RightButton, time))
                validTrackingAction = true;

            bool allJudged = sliderBodyJudged;

            if (allJudged)
            {
                for (int i = 0; i < nestedInOrder.Count; i++)
                {
                    if (!isJudged(nestedInOrder[i]))
                    {
                        allJudged = false;
                        break;
                    }
                }
            }

            bool wasTracking = tracking;

            tracking = (!allJudged || time <= slider.EndTime)
                       && isValidTrackingPosition
                       && validTrackingAction;

            if (tracking && !wasTracking)
            {
                trackingStartTime = time;
                trackingHistory.Add((time, true));
            }
            else if (!tracking && wasTracking)
            {
                trackingStartTime = null;
                trackingHistory.Add((time, false));
            }
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

        private bool isPressedAt(OsuAction action, double time)
            => action switch
            {
                OsuAction.LeftButton => samples.IsPressedAt(OsuAction.LeftButton, time),
                OsuAction.RightButton => samples.IsPressedAt(OsuAction.RightButton, time),
                _ => false,
            };

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

        private void markJudged(HitObject hitObject, HitResult result, double judgementTime)
        {
            judged.Add(hitObject);
            results[hitObject] = result;
            judgementTimes[hitObject] = judgementTime;
            retryDeadlines.Remove(hitObject);
        }

        private void applyJudgement(HitObject hitObject, HitResult result, double judgementTime, Vector2? cursorPosition)
            => emit(OsuJudgementEvent.Hit(hitObject, result, judgementTime, cursorPosition));
    }
}
