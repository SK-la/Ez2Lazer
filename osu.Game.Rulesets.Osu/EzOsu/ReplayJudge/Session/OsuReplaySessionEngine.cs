// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
using osu.Game.Rulesets.Osu.UI;
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
            var noteLock = OsuSessionNoteLock.Create(beatmap, score, environment, cancellationToken);
            var pressEdges = OsuReplayCursor.CollectPressEdges(frames);
            int nextPressIndex = 0;

            var slidersByContainer = sliders.ToDictionary(s => s.ContainerObject);
            var forceMissTargets = new List<OsuNoteLockMissTarget>();

            // 实机语义：一次输入只落一个物件。特殊标记（mod 等）可放开为多判。
            bool allowMultiJudgementPerPress = scoreProcessor is OsuScoreProcessor { AllowMultiJudgementPerPress: true };

            // lazer StartTimeOrderedHitPolicy.HandleHit：命中后立刻结算被抢拍的前序目标（滑条头交给所属滑条状态）。
            void applyForceMisses(double targetStartTime, double time)
            {
                forceMissTargets.Clear();
                noteLock.CollectForceMissTargets(targetStartTime, forceMissTargets);

                foreach (var missTarget in forceMissTargets)
                {
                    if (missTarget.Target is SliderHeadCircle)
                    {
                        if (slidersByContainer.TryGetValue(missTarget.ContainerObject, out var sliderState))
                            sliderState.ForceMissHead(time, applySliderHead);
                    }
                    else
                    {
                        scheduler.ForceMissTarget(missTarget.Target, time, applyCircle);
                    }
                }
            }

            void applySliderHead(HitObject hitObject, HitResult result, double judgementTime, Vector2? cursorPosition)
                => applyJudgement(hitObject, result, judgementTime, cursorPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder, noteLock);

            void applyCircle(OsuSessionCircleScheduler.PendingTarget target, HitResult result, double judgementTime, Vector2? hitPosition)
                => applyJudgement(target.HitObject, result, judgementTime, hitPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder, noteLock);

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
                        applyJudgement(hitObject, result, judgementTime, cursorPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder, noteLock, configure));
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
                    {
                        // note lock 挡下的输入不落判定（实机等价 Shake/Ignore）：目标随后按常规过期成 Miss。
                        if (noteLock.Check(sliderCandidate!.ContainerObject, sliderCandidate.HeadStartTime, edge.Time, targetIsNested: true) != ClickAction.Hit)
                            continue;

                        if (sliderCandidate.ProcessHeadPress(edge.Time, edge.Position, edge.Action, cursor.GetPressedActions(), applySliderHead))
                            applyForceMisses(sliderCandidate.HeadStartTime, edge.Time);
                    }
                    else if (circleCandidate != null)
                    {
                        if (noteLock.Check(circleCandidate.HitObject, circleCandidate.HitObject.StartTime, edge.Time, targetIsNested: false) != ClickAction.Hit)
                            continue;

                        if (scheduler.ProcessPress(edge.Time, edge.Position, applyCircle))
                            applyForceMisses(circleCandidate.HitObject.StartTime, edge.Time);
                    }
                }

                foreach (var slider in sliders)
                {
                    // 非活跃滑条跳过：避免「每帧 × 全图滑条」二次放大。
                    if (time < slider.StartTime || time > slider.LastActiveTime + 1)
                        continue;

                    slider.ProcessTime(time, cursor.Position, cursor.GetPressedActions(), (hitObject, result, judgementTime, cursorPosition) =>
                        applyJudgement(hitObject, result, judgementTime, cursorPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder, noteLock));
                }

                scheduler.ProcessExpiredMisses(time, (target, result, judgementTime, hitPosition) =>
                    applyJudgement(target.HitObject, result, judgementTime, hitPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder, noteLock));

                foreach (var spinner in spinners)
                {
                    spinner.ProcessEnd(time, (hitObject, result, judgementTime, cursorPosition, configure) =>
                        applyJudgement(hitObject, result, judgementTime, cursorPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder, noteLock, configure));
                }

                previousTime = time;
            }

            scheduler.FinalizeRemainingMisses((target, result, judgementTime, hitPosition) =>
                applyJudgement(target.HitObject, result, judgementTime, hitPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder, noteLock));
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
            OsuSessionNoteLock noteLock,
            Action<JudgementResult>? configureResult = null)
        {
            JudgementResult judgementResult = createJudgementResult(hitObject, hitObject.Judgement);
            judgementResult.Type = result;

            if (judgementResult is OsuHitCircleJudgementResult circleResult)
                circleResult.CursorPositionAtHit = cursorPositionAtHit;

            configureResult?.Invoke(judgementResult);

            // note lock 需要全局已判集合：顶层 + 滑条头/tick/尾 + 转盘 tick 与主体都从这里过一遍。
            noteLock.MarkJudged(hitObject);

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
