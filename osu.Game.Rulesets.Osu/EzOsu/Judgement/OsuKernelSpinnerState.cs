// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.Judgement
{
    /// <summary>
    /// 转盘状态：旋转累计由驱动侧持有的 <see cref="SpinnerSpinHistory"/> 独占，内核只按已完成的整圈数派发 tick、
    /// 并在结束时刻结算主体。这样同一次旋转不会被复制成两份状态。
    /// </summary>
    internal sealed class OsuKernelSpinnerState : IOsuKernelDeadlineOwner
    {
        private readonly Spinner spinner;
        private readonly List<HitObject> ticksInOrder;
        private readonly Action<OsuJudgementEvent> emit;

        private SpinnerSpinHistory? history;
        private int judgedTicks;
        private bool spinnerBodyJudged;
        private double? timeStarted;
        private double? timeCompleted;

        public OsuKernelSpinnerState(Spinner spinner, int deadlineOrder, Action<OsuJudgementEvent> emit)
        {
            this.spinner = spinner;
            this.emit = emit;
            DeadlineOrder = deadlineOrder;

            ticksInOrder = spinner.NestedHitObjects
                                  .Where(o => o is SpinnerTick)
                                  .OrderBy(o => o.StartTime)
                                  .ToList();
        }

        public int DeadlineOrder { get; }

        /// <summary>主体只在结束时刻结算；tick 由旋转累计就地派发，不占 deadline 槽位。</summary>
        public double? NextDeadline => spinnerBodyJudged ? null : spinner.EndTime;

        /// <summary>
        /// 送入当前旋转状态。驱动每帧调用，<paramref name="history"/> 可能因 drawable 重新兜底而更换实例。
        /// </summary>
        public void Track(SpinnerSpinHistory history, bool tracking, double time)
        {
            this.history = history;

            if (tracking && timeStarted == null)
                timeStarted = time;

            if (progress >= 1)
                timeCompleted ??= time;

            dispatchTicks(time);
        }

        public void SettleAt(double deadline)
        {
            if (spinnerBodyJudged)
                return;

            for (int i = judgedTicks; i < ticksInOrder.Count; i++)
                applyTick(ticksInOrder[i], hit: false, deadline);

            judgedTicks = ticksInOrder.Count;
            spinnerBodyJudged = true;

            HitResult result = OsuSpinnerJudgement.BodyResultForProgress(progress, spinner.Judgement.MinResult);
            emit(OsuJudgementEvent.SpinnerBody(spinner, result, deadline, totalRotation, timeStarted, timeCompleted));
        }

        /// <summary>倒带：撤销该时刻之后的落判。旋转累计由 <see cref="SpinnerSpinHistory"/> 自己回退。</summary>
        public void RewindTo(double time)
        {
            spinnerBodyJudged = false;
            timeCompleted = null;

            if (time >= spinner.StartTime)
                return;

            timeStarted = null;
            judgedTicks = 0;
        }

        private void dispatchTicks(double time)
        {
            int spins = history?.CompletedSpins ?? 0;

            // 倒带后历史圈数减少，已派发的 tick 会由驱动侧一并回退。
            if (spins < judgedTicks)
            {
                judgedTicks = spins;
                return;
            }

            while (judgedTicks < spins)
            {
                HitObject? tick = judgedTicks < ticksInOrder.Count ? ticksInOrder[judgedTicks] : null;
                judgedTicks++;

                // 超过 tick 数量后不再有评分点，与上游「只放音效」的口径一致。
                if (tick != null)
                    applyTick(tick, hit: true, judgeTime(tick, time));
            }
        }

        /// <summary>
        /// tick 的落判时刻取「排定时刻」与「实际完成时刻」的较早者：旋转已越过排定点时按排定时刻落判，
        /// 使判定时刻不随驱动侧的采样间隔变化；旋转提前到账时退回实际时刻，避免把结果写到未来。
        /// </summary>
        private static double judgeTime(HitObject tick, double time) => Math.Min(tick.StartTime, time);

        private void applyTick(HitObject tick, bool hit, double judgementTime)
            => emit(OsuJudgementEvent.Hit(tick, hit ? tick.Judgement.MaxResult : tick.Judgement.MinResult, judgementTime));

        private float totalRotation => history?.TotalRotation ?? 0;

        private float progress => spinner.SpinsRequired == 0
            ? 1
            : Math.Clamp(totalRotation / 360 / spinner.SpinsRequired, 0, 1);
    }
}
