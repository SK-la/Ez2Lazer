// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Shadow
{
    /// <summary>
    /// Spinner 影子状态：移植 <see cref="SpinnerRotationTracker"/> + <see cref="DrawableSpinner"/> tick/EndTime 判定。
    /// </summary>
    internal sealed class OsuShadowSpinnerState
    {
        internal delegate void JudgementApplier(HitObject hitObject, HitResult result, double judgementClockTime, Vector2? cursorPositionAtHit, Action<JudgementResult>? configureResult);

        private readonly Spinner spinner;
        private readonly List<HitObject> ticksInOrder;
        private readonly SpinnerSpinHistory rotationHistory = new SpinnerSpinHistory();
        private readonly HashSet<HitObject> judgedTicks = new HashSet<HitObject>();

        private float? lastAngle;
        private int completedFullSpins;
        private bool spinnerBodyJudged;
        private double? timeStarted;
        private double? timeCompleted;

        private OsuShadowSpinnerState(Spinner spinner, List<HitObject> ticksInOrder)
        {
            this.spinner = spinner;
            this.ticksInOrder = ticksInOrder;
        }

        public static IReadOnlyList<OsuShadowSpinnerState> CreateAll(IBeatmap beatmap, CancellationToken cancellationToken)
        {
            var list = new List<OsuShadowSpinnerState>();

            foreach (var hitObject in beatmap.HitObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (hitObject is not Spinner spinner)
                    continue;

                var ticks = spinner.NestedHitObjects
                                   .Where(o => o is SpinnerTick)
                                   .OrderBy(o => o.StartTime)
                                   .ToList();

                list.Add(new OsuShadowSpinnerState(spinner, ticks));
            }

            return list;
        }

        public IEnumerable<double> CollectSimulationTimes()
        {
            yield return spinner.EndTime;
        }

        public void ProcessRotationInterval(
            double fromTime,
            double toTime,
            IReadOnlyList<OsuReplayFrame> frames,
            double gameplayRate,
            JudgementApplier apply)
        {
            if (toTime <= fromTime)
                return;

            if (fromTime >= spinner.EndTime || toTime <= spinner.StartTime)
                return;

            double sampleStart = Math.Max(fromTime, spinner.StartTime);
            double sampleEnd = Math.Min(toTime, spinner.EndTime);

            if (sampleEnd <= sampleStart)
                return;

            bool tracking = isTrackingAt(frames, sampleEnd);

            if (tracking && timeStarted == null)
                timeStarted = sampleStart;

            float? angleAtStart = lastAngle ?? computeAngle(OsuShadowReplayCursor.InterpolatePosition(frames, sampleStart), spinner.StackedPosition);
            float angleAtEnd = computeAngle(OsuShadowReplayCursor.InterpolatePosition(frames, sampleEnd), spinner.StackedPosition);

            lastAngle = angleAtEnd;

            if (!tracking || angleAtStart == null)
                return;

            float delta = angleAtEnd - angleAtStart.Value;

            if (delta > 180)
                delta -= 360;
            if (delta < -180)
                delta += 360;

            delta = (float)(delta * Math.Abs(gameplayRate));
            rotationHistory.ReportDelta(sampleEnd, delta);

            if (progress >= 1)
                timeCompleted ??= sampleEnd;

            updateBonusTicks(sampleEnd, apply);
        }

        public void ProcessEnd(double time, JudgementApplier apply)
        {
            if (spinnerBodyJudged || time < spinner.EndTime)
                return;

            foreach (var tick in ticksInOrder)
            {
                if (judgedTicks.Contains(tick))
                    continue;

                applyTick(tick, hit: false, time, apply);
            }

            HitResult result;

            if (progress >= 1)
                result = HitResult.Great;
            else if (progress > .9)
                result = HitResult.Ok;
            else if (progress > .75)
                result = HitResult.Meh;
            else
                result = spinner.Judgement.MinResult;

            spinnerBodyJudged = true;

            apply(spinner, result, time, null, judgementResult =>
            {
                if (judgementResult is OsuSpinnerJudgementResult spinnerResult)
                {
                    copyRotationHistory(spinnerResult.History);
                    spinnerResult.TimeStarted = timeStarted;
                    spinnerResult.TimeCompleted = timeCompleted;
                }
            });
        }

        private void updateBonusTicks(double judgementTime, JudgementApplier apply)
        {
            int spins = (int)(rotationHistory.TotalRotation / 360);

            if (spins < completedFullSpins)
            {
                completedFullSpins = spins;
                return;
            }

            while (completedFullSpins != spins)
            {
                var tick = ticksInOrder.FirstOrDefault(t => !judgedTicks.Contains(t));

                if (tick != null)
                    applyTick(tick, hit: true, judgementTime, apply);

                completedFullSpins++;
            }
        }

        private void applyTick(HitObject tick, bool hit, double judgementTime, JudgementApplier apply)
        {
            if (!judgedTicks.Add(tick))
                return;

            HitResult result = hit ? tick.Judgement.MaxResult : tick.Judgement.MinResult;
            apply(tick, result, judgementTime, null, null);
        }

        private void copyRotationHistory(SpinnerSpinHistory target)
        {
            // Re-report cumulative rotation as a single delta for parity with drawable result payload.
            target.ReportDelta(spinner.StartTime, rotationHistory.TotalRotation);
        }

        private float progress => spinner.SpinsRequired == 0
            ? 1
            : Math.Clamp(rotationHistory.TotalRotation / 360 / spinner.SpinsRequired, 0, 1);

        private static bool isTrackingAt(IReadOnlyList<OsuReplayFrame> frames, double time)
        {
            if (frames.Count == 0 || time < frames[0].Time)
                return false;

            foreach (var action in OsuShadowReplayCursor.GetPressedActionsAt(frames, time))
            {
                if (action is OsuAction.LeftButton or OsuAction.RightButton)
                    return true;
            }

            return false;
        }

        private static float computeAngle(Vector2 cursorPosition, Vector2 spinnerCentre)
        {
            Vector2 relative = cursorPosition - spinnerCentre;
            return -float.RadiansToDegrees(MathF.Atan2(relative.X, relative.Y));
        }
    }
}
