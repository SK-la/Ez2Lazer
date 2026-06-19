// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Scoring;
using osu.Game.Utils;
using static osu.Game.Rulesets.Mania.EzMania.ReplayJudge.ManiaColumnSimulator;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    internal static class ManiaReplaySessionSimulator
    {
        internal static void Simulate(
            Score score,
            IBeatmap beatmap,
            IGameplayEnvironment environment,
            List<LaneTargetState> targets,
            Dictionary<int, List<LaneTargetState>> pressColumns,
            Dictionary<int, List<LaneTargetState>> releaseColumns,
            Dictionary<HeadNote, HoldNote> holdByHead,
            Dictionary<TailNote, HeadNote> headByTail,
            IManiaNoteJudgementStrategy noteStrategy,
            IManiaHoldJudgementStrategy holdStrategy,
            ScoreProcessor scoreProcessor,
            ManiaReplayTimelineRecorder? timelineRecorder,
            CancellationToken cancellationToken)
        {
            bool poorEnabled = environment is IManiaGameplayEnvironment maniaEnvironment
                               && HealthModeHelper.IsBMSHealthMode(environment.ManiaHealthMode)
                               && maniaEnvironment.BmsPoorHitResultEnable;

            bool pillModeEnabled = environment.ManiaHealthMode.ToString().Contains("O2Jam");
            var bms = noteStrategy as BmsHitModeJudgement;

            var hitWindowHelper = new HitModeHelper(environment.ManiaHitMode)
            {
                OverallDifficulty = beatmap.Difficulty.OverallDifficulty,
                BPM = getBpmAtTime(beatmap, 0),
            };

            var judgementState = new ManiaReplayJudgementState();
            var headWasHit = new Dictionary<HeadNote, bool>();
            var keyHeldByColumn = new Dictionary<int, bool>();
            var pressTimesByColumn = buildPressTimesByColumn(score.Replay);
            double gameplayRate = ModUtils.CalculateRateWithMods(score.ScoreInfo.Mods);

            foreach (var input in ManiaReplayInputParser.Parse(score.Replay))
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool wasHoldingBeforeEvent = keyHeldByColumn.TryGetValue(input.Column, out bool held) && held;
                keyHeldByColumn[input.Column] = input.IsPress;

                var perColumnDict = input.IsPress ? pressColumns : releaseColumns;
                if (!perColumnDict.TryGetValue(input.Column, out var laneStates))
                    continue;

                hitWindowHelper.BPM = getBpmAtTime(beatmap, input.Time);

                var candidates = collectCandidatesForInput(laneStates, beatmap, input.Time, hitWindowHelper, environment.ManiaHitMode).ToList();

                if (input.IsPress && bms != null)
                {
                    bms.TryRoutePostBadKPoor(
                        laneStates,
                        candidates,
                        input.Time,
                        environment.OffsetPlusMania,
                        hitWindowHelper,
                        (target, result) => ApplyTransientResult(
                            scoreProcessor,
                            target,
                            result,
                            computeStoredTimeOffset(input.Time, target),
                            input.Time,
                            gameplayRate,
                            timelineRecorder));
                }

                if (candidates.Count == 0)
                    continue;

                var selected = selectCandidate(
                    candidates, laneStates, beatmap, input.Time, environment, noteStrategy, hitWindowHelper);

                if (selected == null || selected.Judged)
                    continue;

                var target = selected.Target;
                bool isTail = selected.IsTail;
                bool useTailReleaseLenience = isTail && usesTailReleaseLenience(environment.ManiaHitMode);
                double lenienceFactor = useTailReleaseLenience ? TailNote.RELEASE_WINDOW_LENIENCE : 1;

                double rawOffset = input.Time - target.StartTime + environment.OffsetPlusMania;
                bool holdBreak = isTail && holdStrategy.IsHoldBreak(rawOffset, target.HitWindows!);
                double timeOffsetForJudgement = useTailReleaseLenience ? rawOffset / TailNote.RELEASE_WINDOW_LENIENCE : rawOffset;

                bool headHit = target is TailNote tailNote && headByTail.TryGetValue(tailNote, out var linkedHead)
                                                           && headWasHit.TryGetValue(linkedHead, out bool wasHit) && wasHit;

                if (!input.IsPress && isTail && headHit && wasHoldingBeforeEvent && rawOffset < 0)
                    selected.HoldBroken = true;

                if (input.IsPress && target is HeadNote headNote && holdByHead.TryGetValue(headNote, out var hold))
                {
                    if (!holdStrategy.CanBeginHoldAt(input.Time, hold.Tail))
                        continue;
                }

                HitResult result;

                if (isTail)
                {
                    result = holdStrategy.EvaluateTail(new HoldTailEvaluationContext
                    {
                        RawOffset = rawOffset,
                        TimeOffsetForJudgement = timeOffsetForJudgement,
                        HitWindows = target.HitWindows!,
                        HeadHit = headHit,
                        HoldBreak = holdBreak,
                        HoldBroken = selected.HoldBroken,
                        WasHoldingBeforeRelease = wasHoldingBeforeEvent,
                        State = judgementState,
                        EventTime = input.Time,
                        Bpm = hitWindowHelper.BPM,
                        PillModeEnabled = pillModeEnabled,
                    });

                    if (environment.ManiaHitMode == EzEnumHitMode.Lazer && result == HitResult.None)
                        continue;
                }
                else if (bms != null && target.HitWindows is ManiaHitWindows bmsWindows)
                {
                    var sessionOutcome = bms.EvaluateSessionPress(bmsWindows, timeOffsetForJudgement, selected.BmsRoute, poorEnabled);

                    if (sessionOutcome.Kind == BmsHitModeJudgement.SessionPressKind.None)
                        continue;

                    if (sessionOutcome.Kind == BmsHitModeJudgement.SessionPressKind.DispatchExtra)
                    {
                        ApplyTransientResult(
                            scoreProcessor,
                            target,
                            BmsHitModeJudgement.MapTo(sessionOutcome.Judge),
                            computeStoredTimeOffset(input.Time, target),
                            input.Time,
                            gameplayRate,
                            timelineRecorder);
                        continue;
                    }

                    result = BmsHitModeJudgement.MapTo(sessionOutcome.Judge);
                    selected.BmsRoute.CanRouteToKPoor = sessionOutcome.EnableCanRouteToKPoor;
                }
                else
                {
                    result = evaluateNotePress(
                        target, noteStrategy, timeOffsetForJudgement, rawOffset, judgementState, hitWindowHelper.BPM, pillModeEnabled, environment.ManiaHitMode);

                    if (result == HitResult.None)
                        continue;
                }

                foreach (var forced in ForceMissEarlier(laneStates, target.StartTime))
                {
                    forced.Judged = true;
                    forced.Result = HitResult.Miss;
                    ApplyFinalResult(
                        scoreProcessor,
                        forced.Target,
                        HitResult.Miss,
                        computeStoredTimeOffset(input.Time, forced.Target),
                        input.Time,
                        gameplayRate,
                        timelineRecorder);
                }

                selected.Judged = true;
                selected.Result = result;

                ApplyFinalResult(
                    scoreProcessor,
                    target,
                    result,
                    computeStoredTimeOffset(input.Time, target),
                    input.Time,
                    gameplayRate,
                    timelineRecorder);

                if (target is HeadNote head)
                    headWasHit[head] = result.IsHit();
            }
        }

        private static HitResult evaluateNotePress(
            HitObject target,
            IManiaNoteJudgementStrategy noteStrategy,
            double timeOffsetForJudgement,
            double rawOffset,
            ManiaReplayJudgementState state,
            double bpm,
            bool pillModeEnabled,
            EzEnumHitMode hitMode)
        {
            ManiaNoteJudgementOutcome outcome;

            if (hitMode == EzEnumHitMode.O2Jam && noteStrategy is O2HitModeJudgement o2)
            {
                outcome = o2.EvaluatePress(timeOffsetForJudgement, target.HitWindows!, new O2HitModeJudgement.NotePressContext
                {
                    RawOffset = rawOffset,
                    Bpm = bpm,
                    PillModeEnabled = pillModeEnabled,
                    State = state,
                });
            }
            else if (hitMode == EzEnumHitMode.EZ2AC && noteStrategy is Ez2AcHitModeJudgement ez2Ac)
            {
                outcome = ez2Ac.EvaluatePress(timeOffsetForJudgement, target.HitWindows!, target is HeadNote);
            }
            else
            {
                outcome = noteStrategy.EvaluatePress(timeOffsetForJudgement, target.HitWindows!);
            }

            if (outcome.Kind == ManiaNoteJudgementOutcomeKind.None)
                return HitResult.None;

            return outcome.Result;
        }

        private static LaneTargetState? selectCandidate(
            List<LaneTargetState> candidates,
            IReadOnlyList<LaneTargetState> laneStates,
            IBeatmap beatmap,
            double inputTime,
            IGameplayEnvironment environment,
            IManiaNoteJudgementStrategy noteStrategy,
            HitModeHelper hitWindowHelper)
        {
            if (environment.JudgePrecedence == EzEnumJudgePrecedence.Earliest
                && environment.ManiaHitMode is EzEnumHitMode.Lazer or EzEnumHitMode.Classic)
            {
                return selectEarliestCandidate(candidates, laneStates, inputTime, noteStrategy, environment);
            }

            return selectCandidateByPrecedence(candidates, beatmap, inputTime, environment, hitWindowHelper, noteStrategy);
        }

        private static LaneTargetState? selectEarliestCandidate(
            List<LaneTargetState> candidates,
            IReadOnlyList<LaneTargetState> laneStates,
            double time,
            IManiaNoteJudgementStrategy noteStrategy,
            IGameplayEnvironment environment)
        {
            candidates.Sort((a, b) => a.Target.StartTime.CompareTo(b.Target.StartTime));

            foreach (var candidate in candidates)
            {
                int index = laneStates.ToList().FindIndex(s => ReferenceEquals(s, candidate));
                if (index < 0 || !IsHittableEarliest(laneStates, index, time))
                    continue;

                if (candidate.IsTail)
                    return candidate;

                double judgedOffset = time - candidate.Target.GetEndTime() + environment.OffsetPlusMania;
                var outcome = noteStrategy.EvaluatePress(judgedOffset, candidate.Target.HitWindows!);

                if (outcome.Kind == ManiaNoteJudgementOutcomeKind.Apply)
                    return candidate;
            }

            return null;
        }

        private static LaneTargetState? selectCandidateByPrecedence(
            List<LaneTargetState> candidates,
            IBeatmap beatmap,
            double inputTime,
            IGameplayEnvironment environment,
            HitModeHelper hitWindowHelper,
            IManiaNoteJudgementStrategy noteStrategy)
        {
            if (candidates.Count == 0)
                return null;

            candidates.Sort((a, b) => a.Target.StartTime.CompareTo(b.Target.StartTime));

            if (ManiaJudgementRegistry.IsBmsLikeMode(environment.ManiaHitMode))
            {
                return environment.JudgePrecedence switch
                {
                    EzEnumJudgePrecedence.Combo => OrderedHitPolicyHelper.SelectFold(
                        candidates,
                        s => s.Judged,
                        s => s.Target.StartTime,
                        s => s.Target.HitWindows as ManiaHitWindows,
                        inputTime,
                        comboAlgorithm: true),
                    EzEnumJudgePrecedence.Duration => OrderedHitPolicyHelper.SelectFold(
                        candidates,
                        s => s.Judged,
                        s => s.Target.StartTime,
                        s => s.Target.HitWindows as ManiaHitWindows,
                        inputTime,
                        comboAlgorithm: false),
                    _ => candidates[0]
                } ?? candidates[0];
            }

            LaneTargetState selected = candidates[0];

            for (int i = 1; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                bool shouldReplace = environment.JudgePrecedence switch
                {
                    EzEnumJudgePrecedence.Combo => compareCombo(selected, candidate, beatmap, inputTime, hitWindowHelper),
                    EzEnumJudgePrecedence.Duration => OrderedHitPolicyHelper.CompareDurationByPrecedence(
                        selected.Target.StartTime,
                        candidate.Target.StartTime,
                        inputTime),
                    _ => false
                };

                if (shouldReplace)
                    selected = candidate;
            }

            return selected;
        }

        private static bool compareCombo(LaneTargetState t1, LaneTargetState t2, IBeatmap beatmap, double inputTime, HitModeHelper hitWindowHelper)
        {
            hitWindowHelper.BPM = getBpmAtTime(beatmap, inputTime);
            double comboEarly = hitWindowHelper.WindowFor(HitResult.Good, true);
            double comboLate = hitWindowHelper.WindowFor(HitResult.Good, false);

            return OrderedHitPolicyHelper.CompareComboByPrecedence(
                t1.Target.StartTime,
                t2.Target.StartTime,
                inputTime,
                comboEarly,
                comboLate);
        }

        private static IEnumerable<LaneTargetState> collectCandidatesForInput(
            IEnumerable<LaneTargetState> laneStates,
            IBeatmap beatmap,
            double eventTime,
            HitModeHelper hitWindowHelper,
            EzEnumHitMode hitMode)
        {
            foreach (var state in laneStates)
            {
                if (state.Judged)
                    continue;

                if (state.Target.HitWindows == null || ReferenceEquals(state.Target.HitWindows, HitWindows.Empty))
                    continue;

                bool useTailReleaseLenience = state.IsTail && usesTailReleaseLenience(hitMode);
                double lenienceFactor = useTailReleaseLenience ? TailNote.RELEASE_WINDOW_LENIENCE : 1;

                hitWindowHelper.BPM = getBpmAtTime(beatmap, eventTime);

                double missEarlyWindow = hitWindowHelper.WindowFor(HitResult.Miss, true) * lenienceFactor;
                double missLateWindow = hitWindowHelper.WindowFor(HitResult.Miss, false) * lenienceFactor;

                if (HitModeHelper.IsBMSHitMode(hitMode))
                    BmsHitModeJudgement.ExpandMissCollectionWindows(hitWindowHelper, lenienceFactor, ref missEarlyWindow, ref missLateWindow);

                double minTime = state.Target.StartTime - missEarlyWindow;
                double maxTime = state.Target.StartTime + missLateWindow;

                if (eventTime >= minTime && eventTime <= maxTime)
                    yield return state;
            }
        }

        private static bool usesTailReleaseLenience(EzEnumHitMode hitMode)
            => hitMode == EzEnumHitMode.Lazer || hitMode == EzEnumHitMode.Classic;

        private static double getBpmAtTime(IBeatmap beatmap, double time)
        {
            double bpm = beatmap.ControlPointInfo.TimingPointAt(time).BPM;

            if (bpm <= 0)
                bpm = beatmap.BeatmapInfo.BPM;

            if (bpm <= 0)
                bpm = 120;

            return bpm;
        }

        internal static void ApplyFinalResult(
            ScoreProcessor scoreProcessor,
            HitObject target,
            HitResult result,
            double timeOffset,
            double eventTime,
            double gameplayRate,
            ManiaReplayTimelineRecorder? timelineRecorder = null)
        {
            var judgementResult = new JudgementResult(target, target.Judgement)
            {
                Type = result,
            };

            JudgementResultTimingHelper.ApplyTiming(judgementResult, timeOffset, gameplayRate);

            if (result == HitResult.Meh || result == HitResult.Miss)
                judgementResult.IsComboHit = false;

            scoreProcessor.ApplyResult(judgementResult);
            timelineRecorder?.Record(scoreProcessor, eventTime, result);
        }

        internal static void ApplyTransientResult(
            ScoreProcessor scoreProcessor,
            HitObject target,
            HitResult result,
            double timeOffset,
            double eventTime,
            double gameplayRate,
            ManiaReplayTimelineRecorder? timelineRecorder = null)
        {
            var judgementResult = new JudgementResult(target, target.Judgement)
            {
                Type = result,
                IsFinal = false,
            };

            JudgementResultTimingHelper.ApplyTiming(judgementResult, timeOffset, gameplayRate);

            scoreProcessor.ApplyResult(judgementResult);
            timelineRecorder?.Record(scoreProcessor, eventTime, result);
        }

        internal static double computeStoredTimeOffset(double eventTime, HitObject target)
            => eventTime - target.GetEndTime();

        internal static Dictionary<int, List<double>> buildPressTimesByColumn(Replay replay)
        {
            var dict = new Dictionary<int, List<double>>();

            foreach (var input in ManiaReplayInputParser.Parse(replay))
            {
                if (!input.IsPress)
                    continue;

                if (!dict.TryGetValue(input.Column, out var list))
                {
                    list = new List<double>();
                    dict[input.Column] = list;
                }

                list.Add(input.Time);
            }

            foreach (var list in dict.Values)
                list.Sort();

            return dict;
        }

        /// <summary>
        /// Miss 存储偏移：优先该列 replay 最近邻 press；无输入则 0（Graph 侧 projectOffsetToY 压边）。
        /// </summary>
        internal static double resolveMissStoredOffset(
            HitObject target,
            IReadOnlyDictionary<int, List<double>> pressTimesByColumn,
            double? beforeTimeInclusive = null)
        {
            double eventTime = resolveMissEventTime(target, pressTimesByColumn, beforeTimeInclusive);
            return computeStoredTimeOffset(eventTime, target);
        }

        internal static double resolveMissEventTime(
            HitObject target,
            IReadOnlyDictionary<int, List<double>> pressTimesByColumn,
            double? beforeTimeInclusive = null)
        {
            if (target is not IHasColumn hasColumn)
                return target.GetEndTime();

            if (!pressTimesByColumn.TryGetValue(hasColumn.Column, out var times) || times.Count == 0)
                return target.GetEndTime();

            IEnumerable<double> candidates = times;

            if (beforeTimeInclusive.HasValue)
                candidates = times.Where(t => t <= beforeTimeInclusive.Value);

            var candidateList = candidates.ToList();

            if (candidateList.Count == 0)
                return beforeTimeInclusive ?? target.GetEndTime();

            double reference = target.GetEndTime();
            return candidateList.MinBy(t => Math.Abs(t - reference));
        }
    }
}
