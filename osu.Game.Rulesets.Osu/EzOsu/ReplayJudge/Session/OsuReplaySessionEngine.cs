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
using osu.Game.Rulesets.Osu.EzOsu.Judgement;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement;
using osu.Game.Rulesets.Osu.EzOsu.StableClassic;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Session
{
    /// <summary>
    /// Session 驱动：把 replay 转成采样源与输入事件，喂给 <see cref="OsuJudgementKernel"/>，
    /// 再把内核判定写回 <see cref="ScoreProcessor"/>。判定状态全部在内核里，这里不持有。
    /// </summary>
    internal static class OsuReplaySessionEngine
    {
        private sealed class SpinnerRuntime
        {
            public Spinner Spinner = null!;
            public SpinnerSpinHistory History = new SpinnerSpinHistory();
            public float? LastAngle;
        }

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

            var samples = new OsuReplaySampleSource(frames);
            var settings = createSettings(score, environment, scoreProcessor);
            var kernel = new OsuJudgementKernel(beatmap, settings, samples);

            kernel.Judged += judgement => applyJudgement(judgement, gameplayRate, inputOffset, scoreProcessor, timelineRecorder);

            var spinners = beatmap.HitObjects.OfType<Spinner>()
                                  .Select(s => new SpinnerRuntime { Spinner = s })
                                  .ToList();

            timelineRecorder?.RecordInitial(scoreProcessor, gameplayRate);

            var pressEdges = OsuReplayCursor.CollectPressEdges(frames);
            int frameIndex = 0;
            int pressIndex = 0;
            double? previousTime = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double? nextFrame = frameIndex < frames.Count ? frames[frameIndex].Time : null;
                double? nextPress = pressIndex < pressEdges.Count ? pressEdges[pressIndex].Time : null;
                double? nextDeadline = kernel.NextDeadline;

                double? step = min(nextFrame, nextPress);

                if (nextDeadline is double deadline && deadline > (previousTime ?? double.NegativeInfinity) && (step == null || deadline < step))
                    step = deadline;

                if (step == null || step <= (previousTime ?? double.NegativeInfinity))
                    break;

                double time = step.Value;

                if (previousTime is double previous)
                    progressSpinners(spinners, previous, time, gameplayRate, samples, kernel);

                // 同一时刻先处理输入，再结算到期目标（与旧引擎 previousTime -> 输入 -> 到期一致）。
                kernel.AdvanceBefore(time);

                while (frameIndex < frames.Count && frames[frameIndex].Time <= time)
                    frameIndex++;

                while (pressIndex < pressEdges.Count && pressEdges[pressIndex].Time <= time)
                {
                    var edge = pressEdges[pressIndex++];
                    kernel.ApplyInput(new OsuInputEvent(edge.Time, edge.Action, edge.Position, OsuInputKind.Press));
                }

                kernel.AdvanceTo(time);

                previousTime = time;
            }

            kernel.FinalizeRun();
        }

        private static OsuJudgementKernelSettings createSettings(Score score, IGameplayEnvironment environment, ScoreProcessor scoreProcessor)
        {
            bool useLegacy = OsuStableClassic.IsActive(environment.JudgeMode)
                             || score.ScoreInfo.Mods.OfType<OsuModClassic>().Any(m => m.ClassicNoteLock.Value);

            double range = OsuHitWindows.MISS_WINDOW - (score.ScoreInfo.Mods.OfType<OsuModAutopilot>().Any() ? 200 : 0);

            return new OsuJudgementKernelSettings
            {
                UseLegacyNoteLock = useLegacy,
                HittableRange = range,
                AllowMultiJudgementPerPress = scoreProcessor is OsuScoreProcessor { AllowMultiJudgementPerPress: true },
            };
        }

        private static void progressSpinners(
            IReadOnlyList<SpinnerRuntime> spinners,
            double fromTime,
            double toTime,
            double gameplayRate,
            OsuReplaySampleSource samples,
            OsuJudgementKernel kernel)
        {
            if (toTime <= fromTime)
                return;

            for (int i = 0; i < spinners.Count; i++)
            {
                var runtime = spinners[i];
                Spinner spinner = runtime.Spinner;

                if (fromTime >= spinner.EndTime || toTime <= spinner.StartTime)
                    continue;

                double sampleStart = Math.Max(fromTime, spinner.StartTime);
                double sampleEnd = Math.Min(toTime, spinner.EndTime);

                if (sampleEnd <= sampleStart)
                    continue;

                bool tracking = samples.IsPressedAt(OsuAction.LeftButton, sampleEnd) || samples.IsPressedAt(OsuAction.RightButton, sampleEnd);

                float angleAtEnd = computeAngle(samples.PositionAt(sampleEnd), spinner.StackedPosition);

                if (runtime.LastAngle == null)
                    runtime.LastAngle = computeAngle(samples.PositionAt(sampleStart), spinner.StackedPosition);

                float angleAtStart = runtime.LastAngle.Value;
                runtime.LastAngle = angleAtEnd;

                if (tracking)
                {
                    float delta = OsuSpinnerJudgement.NormaliseRotationDelta(angleAtStart, angleAtEnd, gameplayRate);
                    runtime.History.ReportDelta(sampleEnd, delta);
                }

                kernel.TrackSpinner(spinner, runtime.History, tracking, sampleEnd);
            }
        }

        private static float computeAngle(Vector2 cursorPosition, Vector2 spinnerCentre)
        {
            Vector2 relative = cursorPosition - spinnerCentre;
            return -float.RadiansToDegrees(MathF.Atan2(relative.X, relative.Y));
        }

        private static double? min(double? a, double? b)
        {
            if (a == null) return b;
            if (b == null) return a;
            return Math.Min(a.Value, b.Value);
        }

        private static void applyJudgement(
            in OsuJudgementEvent judgement,
            double gameplayRate,
            double inputOffset,
            ScoreProcessor scoreProcessor,
            OsuReplayTimelineRecorder? timelineRecorder)
        {
            JudgementResult result = createJudgementResult(judgement.Target, judgement.Target.Judgement);
            result.Type = judgement.Result;

            if (result is OsuHitCircleJudgementResult circleResult)
                circleResult.CursorPositionAtHit = judgement.CursorPosition;

            if (result is OsuSpinnerJudgementResult spinnerResult)
            {
                if (judgement.TotalRotation is float rotation)
                    spinnerResult.History.ReportDelta(judgement.Target.StartTime, rotation);

                spinnerResult.TimeStarted = judgement.SpinnerTimeStarted;
                spinnerResult.TimeCompleted = judgement.SpinnerTimeCompleted;
            }

            double timeOffset = Math.Min(judgement.Time - judgement.Target.GetEndTime() + inputOffset, judgement.Target.MaximumJudgementOffset);
            JudgementResultTimingHelper.ApplyTiming(result, timeOffset, gameplayRate);
            scoreProcessor.ApplyResult(result);

            timelineRecorder?.Record(scoreProcessor, judgement.Time, gameplayRate);
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
