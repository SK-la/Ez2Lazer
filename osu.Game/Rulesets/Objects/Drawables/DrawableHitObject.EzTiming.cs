// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Objects.Drawables
{
    public abstract partial class DrawableHitObject
    {
        /// <summary>
        /// 与 <see cref="ApplyResult{T}"/> 相同，但在触发 <see cref="OnNewResult"/> 前写入 stored <paramref name="timeOffset"/>。
        /// </summary>
        protected void ApplyResultWithStoredTiming<T>(Action<JudgementResult, T> application, T state, double timeOffset)
        {
            if (Result.HasResult)
                throw new InvalidOperationException("Cannot apply result on a hitobject that already has a result.");

            application.Invoke(Result, state);

            if (!Result.HasResult)
                throw new InvalidOperationException($"{GetType().Name} applied a {nameof(JudgementResult)} but did not update {nameof(JudgementResult.Type)}.");

            HitResultExtensions.ValidateHitResultPair(Result.Judgement.MaxResult, Result.Judgement.MinResult);

            if (!Result.Type.IsValidHitResult(Result.Judgement.MinResult, Result.Judgement.MaxResult))
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} applied an invalid hit result (was: {Result.Type}, expected: [{Result.Judgement.MinResult} ... {Result.Judgement.MaxResult}]).");
            }

            double gameplayRate = (Clock as IGameplayClock)?.GetTrueGameplayRate() ?? Clock.Rate;
            JudgementResultTimingHelper.ApplyTiming(Result, timeOffset, gameplayRate);

            autoPlayPlusSampleTriggered = true;

            if (Result.HasResult)
                UpdateState(Result.IsHit ? ArmedState.Hit : ArmedState.Miss);

            OnNewResult?.Invoke(this, Result);
        }
    }
}
