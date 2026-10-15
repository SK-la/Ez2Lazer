// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Session
{
    /// <summary>
    /// Osu Session 判定主循环（Mapping）：replay 事件时钟 → Session 状态 → 一遍 <see cref="JudgementProcessor.ApplyResult"/>。
    /// </summary>
    internal static class OsuReplaySessionEngine
    {
        internal static void Run(
            Score score,
            IBeatmap beatmap,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            IGameplayEnvironment environment,
            OsuReplayTimelineRecorder? timelineRecorder,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(score.Replay);

            // REGISTRY §1.7b：replay 帧已是有效输入；ForStored/分析路径 Resolve 后多为 0。
            // 禁止把 OffsetPlus 再叠进下方 ResultForPress / 滑条头窗（相对帧会判两次）。
            double inputOffset = environment.OffsetPlusNonMania;

            var frames = score.Replay.Frames.OfType<OsuReplayFrame>().OrderBy(f => f.Time).ToList();

            if (frames.Count == 0)
                return;

            var cursor = new OsuReplayCursor(frames);
            var scheduler = OsuSessionCircleScheduler.Create(beatmap, cancellationToken);
            var sliders = OsuSessionSliderState.CreateAll(beatmap, cancellationToken);
            var spinners = OsuSessionSpinnerState.CreateAll(beatmap, cancellationToken);
            var pressEdges = OsuReplayCursor.CollectPressEdges(frames);
            int nextPressIndex = 0;

            // 实机语义：一次输入只落一个物件。特殊标记（mod 等）可放开为多判。
            bool allowMultiJudgementPerPress = scoreProcessor is OsuScoreProcessor { AllowMultiJudgementPerPress: true };

            void applySliderHead(HitObject hitObject, HitResult result, double judgementTime, Vector2? cursorPosition)
                => applyJudgement(hitObject, result, judgementTime, cursorPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder);

            void applyCircle(OsuSessionCircleScheduler.PendingTarget target, HitResult result, double judgementTime, Vector2? hitPosition)
                => applyJudgement(target.HitObject, result, judgementTime, hitPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder);

            var simulationTimes = OsuReplayCursor.CollectSimulationTimes(
                frames,
                scheduler.CollectMissDeadlines(),
                sliders.Select(s => s.CollectSimulationTimes()).Concat(spinners.Select(s => s.CollectSimulationTimes())));

            timelineRecorder?.RecordInitial(scoreProcessor, gameplayRate);

            double previousTime = simulationTimes.Count > 0 ? Math.Min(0, simulationTimes[0] - 1) : 0;

            foreach (double time in simulationTimes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var spinner in spinners)
                {
                    spinner.ProcessRotationInterval(previousTime, time, frames, gameplayRate, (hitObject, result, judgementTime, cursorPosition, configure) =>
                        applyJudgement(hitObject, result, judgementTime, cursorPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder, configure));
                }

                cursor.Seek(time);

                while (nextPressIndex < pressEdges.Count && pressEdges[nextPressIndex].Time <= time)
                {
                    var edge = pressEdges[nextPressIndex++];

                    if (allowMultiJudgementPerPress)
                    {
                        foreach (var slider in sliders)
                            slider.ProcessHeadPress(edge.Time, edge.Position, edge.Action, cursor.GetPressedActions(), applySliderHead);

                        scheduler.ProcessPress(edge.Time, edge.Position, applyCircle);
                        continue;
                    }

                    // 单判：滑条头与圆共用候选集，取最早可判者。等价实机 StartTimeOrderedHitPolicy
                    // ―― 前一物件未判时后续物件不可判，因此一次输入只会落在一个物件上。
                    OsuSessionSliderState? sliderCandidate = null;

                    foreach (var slider in sliders)
                    {
                        if (!slider.CanAcceptHeadPress(edge.Time, edge.Position))
                            continue;

                        if (sliderCandidate == null || slider.HeadStartTime < sliderCandidate.HeadStartTime)
                            sliderCandidate = slider;
                    }

                    OsuSessionCircleScheduler.PendingTarget? circleCandidate = scheduler.FindAcceptableTarget(edge.Time, edge.Position);

                    bool sliderWins = sliderCandidate != null
                                      && (circleCandidate == null || sliderCandidate.HeadStartTime < circleCandidate.HitObject.StartTime);

                    if (sliderWins)
                        sliderCandidate!.ProcessHeadPress(edge.Time, edge.Position, edge.Action, cursor.GetPressedActions(), applySliderHead);
                    else if (circleCandidate != null)
                        scheduler.ProcessPress(edge.Time, edge.Position, applyCircle);
                }

                foreach (var slider in sliders)
                {
                    // 非活跃滑条跳过：避免「每帧 × 全图滑条」二次放大。
                    if (time < slider.StartTime || time > slider.EndTime + 1)
                        continue;

                    slider.ProcessTime(time, cursor.Position, cursor.GetPressedActions(), (hitObject, result, judgementTime, cursorPosition) =>
                        applyJudgement(hitObject, result, judgementTime, cursorPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder));
                }

                scheduler.ProcessExpiredMisses(time, (target, result, judgementTime, hitPosition) =>
                    applyJudgement(target.HitObject, result, judgementTime, hitPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder));

                foreach (var spinner in spinners)
                {
                    spinner.ProcessEnd(time, (hitObject, result, judgementTime, cursorPosition, configure) =>
                        applyJudgement(hitObject, result, judgementTime, cursorPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder, configure));
                }

                previousTime = time;
            }

            scheduler.FinalizeRemainingMisses((target, result, judgementTime, hitPosition) =>
                applyJudgement(target.HitObject, result, judgementTime, hitPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder));
        }

        private static void applyJudgement(
            HitObject hitObject,
            HitResult result,
            double judgementClockTime,
            Vector2? cursorPositionAtHit,
            double gameplayRate,
            double inputOffset,
            ScoreProcessor scoreProcessor,
            OsuReplayTimelineRecorder? timelineRecorder,
            Action<JudgementResult>? configureResult = null)
        {
            JudgementResult judgementResult = createJudgementResult(hitObject, hitObject.Judgement);
            judgementResult.Type = result;

            if (judgementResult is OsuHitCircleJudgementResult circleResult)
                circleResult.CursorPositionAtHit = cursorPositionAtHit;

            configureResult?.Invoke(judgementResult);

            // HitEvent.TimeOffset 元数据：可带 env offset。判窗已在 Scheduler/Slider 用裸帧时刻算完 Result，此处不得再改 Type。
            double timeOffset = Math.Min(judgementClockTime - hitObject.GetEndTime() + inputOffset, hitObject.MaximumJudgementOffset);
            JudgementResultTimingHelper.ApplyTiming(judgementResult, timeOffset, gameplayRate);
            scoreProcessor.ApplyResult(judgementResult);

            timelineRecorder?.Record(scoreProcessor, judgementClockTime, gameplayRate);
        }

        private static JudgementResult createJudgementResult(HitObject hitObject, osu.Game.Rulesets.Judgements.Judgement judgement)
        {
            return hitObject switch
            {
                HitCircle => new OsuHitCircleJudgementResult(hitObject, judgement),
                Slider => new OsuSliderJudgementResult(hitObject, judgement),
                Spinner => new OsuSpinnerJudgementResult(hitObject, judgement),
                _ => new JudgementResult(hitObject, judgement),
            };
        }
    }
}
