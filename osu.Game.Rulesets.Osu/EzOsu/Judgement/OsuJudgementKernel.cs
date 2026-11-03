// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.Judgement
{
    /// <summary>
    /// osu!standard 的唯一判定内核：Drawable（实机）与 Session（回放/后台）都只负责喂输入、收判定。
    /// 判定时刻由内核的 deadline 决定，不读取驱动侧当前时钟。
    /// </summary>
    public sealed class OsuJudgementKernel
    {
        private readonly OsuJudgementKernelSettings settings;
        private readonly IOsuJudgementSampleSource samples;
        private readonly OsuKernelDeadlineQueue deadlines = new OsuKernelDeadlineQueue();
        private readonly OsuKernelCircleQueue circles;
        private readonly OsuKernelNoteLock noteLock;
        private readonly List<OsuKernelSliderState> sliders = new List<OsuKernelSliderState>();
        private readonly List<OsuKernelSpinnerState> spinners = new List<OsuKernelSpinnerState>();
        private readonly Dictionary<HitObject, OsuKernelSliderState> slidersByContainer = new Dictionary<HitObject, OsuKernelSliderState>();
        private readonly Dictionary<HitObject, OsuKernelSpinnerState> spinnersByObject = new Dictionary<HitObject, OsuKernelSpinnerState>();
        private readonly Dictionary<SliderHeadCircle, OsuKernelSliderState> sliderByHead = new Dictionary<SliderHeadCircle, OsuKernelSliderState>();
        private readonly List<OsuKernelMissTarget> forceMissScratch = new List<OsuKernelMissTarget>();
        private readonly List<IOsuKernelDeadlineOwner> deferredScratch = new List<IOsuKernelDeadlineOwner>();

        private double lastAdvancedTo = double.NegativeInfinity;
        private long emittedCount;
        private double maxDeadline;
        private int sliderCursor;

        public event Action<OsuJudgementEvent>? Judged;

        /// <summary>
        /// 实机仲裁回调：由 Drawable 适配器注入 <see cref="IHitPolicy"/> 查询，使点击反馈（shake / ignore）与可替换策略仍由 playfield 决定。
        /// 回放路径保持 <see langword="null"/>，直接走内核 note lock。
        /// </summary>
        public Func<HitObject, double, HitResult, ClickAction>? LiveArbitration { get; set; }

        public OsuJudgementKernel(IBeatmap beatmap, OsuJudgementKernelSettings settings, IOsuJudgementSampleSource samples)
        {
            this.settings = settings;
            this.samples = samples;

            var ordered = beatmap.HitObjects.OrderBy(o => o.StartTime).ToList();
            var circleTargets = new List<OsuKernelCircleQueue.Target>();
            int order = 0;

            foreach (var hitObject in ordered)
            {
                int objectOrder = order++;

                switch (hitObject)
                {
                    case Slider slider:
                    {
                        var state = new OsuKernelSliderState(slider, objectOrder, samples, emit);
                        sliders.Add(state);
                        slidersByContainer[slider] = state;

                        foreach (var nested in slider.NestedHitObjects.OfType<SliderHeadCircle>())
                            sliderByHead[nested] = state;

                        maxDeadline = Math.Max(maxDeadline, state.LastActiveTime);
                        break;
                    }

                    case Spinner spinner:
                    {
                        var state = new OsuKernelSpinnerState(spinner, objectOrder, emit);
                        spinners.Add(state);
                        spinnersByObject[spinner] = state;
                        maxDeadline = Math.Max(maxDeadline, spinner.EndTime);
                        break;
                    }

                    default:
                    {
                        var target = OsuKernelCircleQueue.CreateTarget(hitObject, beatmap);

                        if (target != null)
                        {
                            circleTargets.Add(target);
                            maxDeadline = Math.Max(maxDeadline, hitObject.StartTime + target.MissWindow);
                        }

                        break;
                    }
                }
            }

            circles = new OsuKernelCircleQueue(circleTargets, order++, samples, emit);
            noteLock = new OsuKernelNoteLock(beatmap, settings.UseLegacyNoteLock, settings.HittableRange, default);

            deadlines.Push(circles);

            foreach (var slider in sliders)
                deadlines.Push(slider);

            foreach (var spinner in spinners)
                deadlines.Push(spinner);
        }

        /// <summary>下一个必须结算的时刻；<see langword="null"/> 表示已无待判定目标。</summary>
        public double? NextDeadline => deadlines.Peek();

        /// <summary>推进到 <paramref name="time"/>，所有到期 deadline 使用其自身时刻结算。</summary>
        public void AdvanceTo(double time) => advance(time, inclusive: true);

        /// <summary>只结算严格早于 <paramref name="time"/> 的 deadline；同一时刻留给输入先手。</summary>
        public void AdvanceBefore(double time) => advance(time, inclusive: false);

        private void advance(double time, bool inclusive)
        {
            if (time < lastAdvancedTo)
                return;

            // 先按各自到期时刻结算 deadline：SettleAt 内部会自行采样到期时刻的跟踪状态。
            // 到期项可能暂时无法结算（前序评分点未判、滑条头未判等），此时保留在队列里等下一次
            // 推进一步再试；不能中断整轮结算，否则会饿死同一时刻之后的所有到期项。
            deferredScratch.Clear();

            while (true)
            {
                double? next = deadlines.Peek();

                if (next == null || (inclusive ? next.Value > time : next.Value >= time))
                    break;

                if (!deadlines.TryPop(out var owner, out double deadline))
                    break;

                long before = emittedCount;
                owner.SettleAt(deadline);

                if (emittedCount == before)
                    deferredScratch.Add(owner);
                else
                    deadlines.Push(owner);
            }

            for (int i = 0; i < deferredScratch.Count; i++)
                deadlines.Push(deferredScratch[i]);

            // 再把跟踪状态推进到当前时刻；恢复跟踪时补判仍处宽限期的评分点。
            advanceSliders(time);

            lastAdvancedTo = time;
        }

        /// <summary>输入时刻已由驱动侧解析；内核不再叠加任何偏移。</summary>
        public void ApplyInput(in OsuInputEvent input)
        {
            if (input.Kind != OsuInputKind.Press)
                return;

            if (settings.AllowMultiJudgementPerPress)
                applyMultiPress(input);
            else
                applySinglePress(input);
        }

        /// <summary>
        /// 送入转盘当前旋转状态。tick 由内核按已完成整圈数就地派发，因此不需要额外的 deadline。
        /// </summary>
        public void TrackSpinner(Spinner spinner, SpinnerSpinHistory history, bool tracking, double time)
        {
            if (!spinnersByObject.TryGetValue(spinner, out var state))
                return;

            state.Track(history, tracking, time);
        }

        /// <summary>note lock 查询（供 Drawable 的 <c>IHitPolicy</c> 退化使用）。</summary>
        public ClickAction CheckHittable(HitObject target, double time) => checkNoteLock(target, time);

        private ClickAction checkNoteLock(HitObject target, double time)
        {
            if (target is SliderHeadCircle head && sliderByHead.TryGetValue(head, out var slider))
                return noteLock.Check(slider.ContainerObject, head.StartTime, time, targetIsNested: true);

            return noteLock.Check(target, target.StartTime, time, targetIsNested: false);
        }

        private ClickAction arbitrate(HitObject target, double time, HitResult result)
            => LiveArbitration != null ? LiveArbitration(target, time, result) : checkNoteLock(target, time);

        /// <summary>曲终：结算全部剩余目标，保证没有悬空的物件。</summary>
        public void FinalizeRun()
        {
            AdvanceTo(maxDeadline);

            circles.FinalizeRemaining();

            for (int i = 0; i < sliders.Count; i++)
                sliders[i].FinalizeAt(Math.Max(sliders[i].LastActiveTime, sliders[i].EndTime));

            for (int i = 0; i < spinners.Count; i++)
                spinners[i].SettleAt(spinners[i].NextDeadline ?? maxDeadline);
        }

        private void applySinglePress(in OsuInputEvent input)
        {
            OsuKernelSliderState? sliderCandidate = null;

            for (int i = 0; i < sliders.Count; i++)
            {
                var slider = sliders[i];

                if (!slider.IsHeadPressWindowAt(input.Time))
                    continue;

                if (!slider.CanAcceptHeadPress(input.Time, input.Position))
                    continue;

                if (sliderCandidate == null || slider.HeadStartTime < sliderCandidate.HeadStartTime)
                    sliderCandidate = slider;
            }

            var circleCandidate = circles.FindPressCandidate(input.Time, input.Position);

            bool sliderWins = sliderCandidate != null
                              && (circleCandidate == null || sliderCandidate.HeadStartTime < circleCandidate.HitObject.StartTime);

            if (sliderWins)
            {
                tryPressSlider(sliderCandidate!, input);
                return;
            }

            if (circleCandidate != null)
                tryPressCircle(circleCandidate, input);
        }

        /// <summary>
        /// 实机按下：目标已由 Drawable 的输入分发选定（含叠堆遮挡 / 悬停 / 输入消费语义），内核只做窗口、顺序与落判。
        /// </summary>
        public bool ApplyTargetPress(HitObject target, double time, Vector2 position, OsuAction action)
        {
            if (target is SliderHeadCircle head && sliderByHead.TryGetValue(head, out var slider))
            {
                // 滑条头已判后，Drawable 仍会吞掉按下（叠堆遮挡）；此时不应再询问策略。
                if (slider.HeadJudged)
                    return false;

                HitResult headResult = slider.HeadResultAt(time, position);

                if (arbitrate(head, time, headResult) != ClickAction.Hit || headResult == HitResult.None)
                    return false;

                if (!slider.ProcessHeadPress(time, position, action))
                    return false;

                deadlines.Push(slider);
                applyForceMisses(slider.HeadStartTime, time);
                return true;
            }

            OsuKernelCircleQueue.Target? circleTarget = circles.Find(target);

            if (circleTarget == null)
                return false;

            HitResult result = circles.ResultAt(circleTarget, time);

            if (arbitrate(target, time, result) != ClickAction.Hit || result == HitResult.None)
                return false;

            if (!circles.ProcessPress(circleTarget, time, position))
                return false;

            deadlines.Push(circles);
            applyForceMisses(target.StartTime, time);
            return true;
        }

        private void tryPressSlider(OsuKernelSliderState slider, in OsuInputEvent input)
        {
            HitResult headResult = slider.HeadResultAt(input.Time, input.Position);

            if (arbitrate(slider.HeadObject, input.Time, headResult) != ClickAction.Hit)
                return;

            if (headResult == HitResult.None)
                return;

            if (slider.ProcessHeadPress(input.Time, input.Position, input.Action))
            {
                deadlines.Push(slider);
                applyForceMisses(slider.HeadStartTime, input.Time);
            }
        }

        private void tryPressCircle(OsuKernelCircleQueue.Target target, in OsuInputEvent input)
        {
            HitResult result = circles.ResultAt(target, input.Time);

            if (arbitrate(target.HitObject, input.Time, result) != ClickAction.Hit)
                return;

            if (result == HitResult.None)
                return;

            if (circles.ProcessPress(target, input.Time, input.Position))
            {
                deadlines.Push(circles);
                applyForceMisses(target.HitObject.StartTime, input.Time);
            }
        }

        private void applyMultiPress(in OsuInputEvent input)
        {
            for (int i = 0; i < sliders.Count; i++)
            {
                var slider = sliders[i];

                if (!slider.IsHeadPressWindowAt(input.Time))
                    continue;

                HitResult headResult = slider.HeadResultAt(input.Time, input.Position);

                if (arbitrate(slider.HeadObject, input.Time, headResult) != ClickAction.Hit)
                    continue;

                if (headResult == HitResult.None)
                    continue;

                if (slider.ProcessHeadPress(input.Time, input.Position, input.Action))
                    deadlines.Push(slider);
            }

            var circleCandidate = circles.FindPressCandidate(input.Time, input.Position);

            if (circleCandidate == null)
                return;

            HitResult result = circles.ResultAt(circleCandidate, input.Time);

            if (arbitrate(circleCandidate.HitObject, input.Time, result) != ClickAction.Hit || result == HitResult.None)
                return;

            if (circles.ProcessPress(circleCandidate, input.Time, input.Position))
                deadlines.Push(circles);
        }

        private void applyForceMisses(double targetStartTime, double judgementTime)
        {
            forceMissScratch.Clear();
            noteLock.CollectForceMissTargets(targetStartTime, forceMissScratch);

            for (int i = 0; i < forceMissScratch.Count; i++)
            {
                var target = forceMissScratch[i];

                if (target.Target is SliderHeadCircle)
                {
                    if (slidersByContainer.TryGetValue(target.ContainerObject, out var slider))
                    {
                        if (slider.ForceMissHead(judgementTime))
                            deadlines.Push(slider);
                    }
                }
                else
                {
                    if (circles.ForceMiss(target.Target, judgementTime))
                        deadlines.Push(circles);
                }
            }
        }

        private void advanceSliders(double time)
        {
            while (sliderCursor < sliders.Count && sliders[sliderCursor].LastActiveTime < time)
                sliderCursor++;

            for (int i = sliderCursor; i < sliders.Count; i++)
            {
                var slider = sliders[i];

                // 早于滑条起始时刻也可能已经在跟踪（滑条头可提前按下），取样必须从按下窗口起点开始。
                if (slider.EarliestActiveTime > time)
                    break;

                if (slider.Track(time))
                    deadlines.Push(slider);
            }
        }

        private void emit(OsuJudgementEvent judgement)
        {
            emittedCount++;
            noteLock.MarkJudged(judgement.Target, judgement.Time);
            Judged?.Invoke(judgement);
        }

        /// <summary>
        /// 回放倒带：撤销判定时刻晚于 <paramref name="time"/> 的落判，并把跟踪状态恢复到该时刻。
        /// 与 <c>Playfield</c> 的 revert 口径一致（只回退 <c>RawTime</c> 晚于当前时刻的结果）。
        /// </summary>
        public void RewindTo(double time)
        {
            circles.RewindTo(time);
            noteLock.RewindTo(time);

            for (int i = 0; i < sliders.Count; i++)
                sliders[i].RewindTo(time);

            for (int i = 0; i < spinners.Count; i++)
                spinners[i].RewindTo(time);

            deadlines.Clear();
            sliderCursor = 0;
            lastAdvancedTo = time;

            deadlines.Push(circles);

            for (int i = 0; i < sliders.Count; i++)
                deadlines.Push(sliders[i]);

            for (int i = 0; i < spinners.Count; i++)
                deadlines.Push(spinners[i]);
        }
    }
}
