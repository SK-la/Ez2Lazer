// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Utils;
using osu.Game.Rulesets.Judgements;

namespace osu.Game.Rulesets.Scoring
{
    public abstract partial class HealthProcessor : JudgementProcessor
    {
        /// <summary>
        /// Invoked when the <see cref="ScoreProcessor"/> is in a failed state.
        /// Return true if the fail was permitted.
        /// </summary>
        public event Func<bool>? Failed;

        /// <summary>
        /// Additional conditions on top of <see cref="CheckDefaultFailCondition"/> that cause a failing state.
        /// </summary>
        public event Func<HealthProcessor, JudgementResult, bool>? FailConditions;

        /// <summary>
        /// The current health.
        /// </summary>
        public readonly BindableDouble Health = new BindableDouble(1) { MinValue = 0, MaxValue = 1 };

        /// <summary>
        /// Whether this ScoreProcessor has already triggered the failed state.
        /// </summary>
        public bool HasFailed { get; private set; }

        /// <summary>
        /// Immediately triggers a failure for this HealthProcessor.
        /// </summary>
        public void TriggerFailure()
        {
            if (HasFailed)
                return;

            if (Failed?.Invoke() != false)
                HasFailed = true;
        }

        /// <summary>
        /// [Ez] gameplay 入口调用：将当前全局 Ez 游玩环境（如 mania 血量模式）冻结注入本处理器。
        /// 必须在 <see cref="JudgementProcessor.ApplyBeatmap"/> 之前调用。
        /// 无头/转换路径不得调用，以保持 ppy 上游官方行为；未调用时按各规则集的默认模式处理。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="ScoreProcessor.ApplyEzGameplayEnvironment"/> 同源：调用方只看这一处，
        /// 局内行为一律读注入后的实例状态，不再回读全局配置。
        /// </remarks>
        public virtual void ApplyEzGameplayEnvironment()
        {
        }

        protected override void ApplyResultInternal(JudgementResult result)
        {
            result.HealthAtJudgement = Health.Value;
            result.FailedAtJudgement = HasFailed;

            if (HasFailed)
                return;

            Health.Value += GetHealthIncreaseFor(result);

            if (meetsAnyFailCondition(result))
                TriggerFailure();
        }

        protected override void RevertResultInternal(JudgementResult result)
        {
            HasFailed = result.FailedAtJudgement;

            if (HasFailed)
                return;

            Health.Value = result.HealthAtJudgement;
        }

        /// <summary>
        /// Retrieves the health increase for a <see cref="JudgementResult"/>.
        /// </summary>
        /// <param name="result">The <see cref="JudgementResult"/>.</param>
        /// <returns>The health increase.</returns>
        protected virtual double GetHealthIncreaseFor(JudgementResult result) => result.HealthIncrease;

        /// <summary>
        /// Checks whether the default conditions for failing are met.
        /// </summary>
        /// <returns><see langword="true"/> if failure should be invoked.</returns>
        protected virtual bool CheckDefaultFailCondition(JudgementResult result) => Precision.AlmostBigger(Health.MinValue, Health.Value);

        /// <summary>
        /// Whether the current state of <see cref="HealthProcessor"/> or the provided <paramref name="result"/> meets any fail condition.
        /// </summary>
        /// <param name="result">The judgement result.</param>
        private bool meetsAnyFailCondition(JudgementResult result)
        {
            if (CheckDefaultFailCondition(result))
                return true;

            if (FailConditions != null)
            {
                foreach (var condition in FailConditions.GetInvocationList())
                {
                    bool conditionResult = (bool)condition.Method.Invoke(condition.Target, new object[] { this, result })!;
                    if (conditionResult)
                        return true;
                }
            }

            return false;
        }

        protected override void Reset(bool storeResults)
        {
            base.Reset(storeResults);

            Health.Value = 1;
            HasFailed = false;
        }
    }
}
