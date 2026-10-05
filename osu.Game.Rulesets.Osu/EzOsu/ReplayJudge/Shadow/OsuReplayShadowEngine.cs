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
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Shadow
{
    /// <summary>
    /// Osu 影子判定主循环：replay 时钟 → Shadow 状态 → 一遍 <see cref="JudgementProcessor.ApplyResult"/>。
    /// </summary>
    internal static class OsuReplayShadowEngine
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

            double inputOffset = environment.OffsetPlusNonMania;

            var frames = score.Replay.Frames.OfType<OsuReplayFrame>().OrderBy(f => f.Time).ToList();

            if (frames.Count == 0)
                return;

            var cursor = new OsuShadowReplayCursor(frames);
            var scheduler = OsuReplayObjectScheduler.Create(beatmap, cancellationToken);
            var sliders = OsuShadowSliderState.CreateAll(beatmap, cancellationToken);
            var spinners = OsuShadowSpinnerState.CreateAll(beatmap, cancellationToken);
            var pressEdges = OsuShadowReplayCursor.CollectPressEdges(frames);
            int nextPressIndex = 0;

            var simulationTimes = OsuShadowReplayCursor.CollectSimulationTimes(
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

                    foreach (var slider in sliders)
                    {
                        slider.ProcessHeadPress(edge.Time, edge.Position, edge.Action, cursor.GetPressedActions(), (hitObject, result, judgementTime, cursorPosition) =>
                            applyJudgement(hitObject, result, judgementTime, cursorPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder));
                    }

                    scheduler.ProcessPress(edge.Time, edge.Position, (target, result, judgementTime, hitPosition) =>
                        applyJudgement(target.HitObject, result, judgementTime, hitPosition, gameplayRate, inputOffset, scoreProcessor, timelineRecorder));
                }

                foreach (var slider in sliders)
                {
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

            // 对齐 Mania：判定侧加 input offset（帧时间不平移）。
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
