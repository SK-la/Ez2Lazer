// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using static osu.Game.Rulesets.Mania.EzMania.ReplayJudge.ManiaColumnSimulator;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    internal static class ManiaReplaySessionSimulator
    {
        internal static void Simulate(
            IBeatmap beatmap,
            IGameplayEnvironment environment,
            Dictionary<int, List<LaneTargetState>> pressColumns,
            Dictionary<int, List<LaneTargetState>> releaseColumns,
            Dictionary<HeadNote, HoldNote> holdByHead,
            Dictionary<TailNote, HeadNote> headByTail,
            IManiaNoteJudgementStrategy noteStrategy,
            IManiaHoldJudgementStrategy holdStrategy,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            ManiaReplayInputData inputData,
            ManiaReplayTimelineRecorder? timelineRecorder,
            CancellationToken cancellationToken)
        {
            bool poorEnabled = HealthModeHelper.ComputeKPoorEnabled(environment.ManiaHealthMode, environment.BmsPoorHitResultEnable);

            bool pillModeEnabled = environment.ManiaHealthMode.ToString().Contains("O2Jam");
            var bms = noteStrategy as BmsHitModeJudgement;
            var judgementRound = createJudgementRound(environment, beatmap);

            var hitWindowHelper = new HitModeHelper(environment.ManiaHitMode)
            {
                OverallDifficulty = beatmap.Difficulty.OverallDifficulty,
                BPM = resolveSimulationBpm(beatmap, 0, environment.ManiaHitMode),
            };

            var headWasHit = new Dictionary<HeadNote, bool>();
            var keyHeldByColumn = new Dictionary<int, bool>();

            foreach (var input in inputData.SortedEvents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool wasHoldingBeforeEvent = keyHeldByColumn.TryGetValue(input.Column, out bool held) && held;
                keyHeldByColumn[input.Column] = input.IsPress;

                var perColumnDict = input.IsPress ? pressColumns : releaseColumns;
                if (!perColumnDict.TryGetValue(input.Column, out var laneStates))
                    continue;

                if (judgementRound.IsO2Jam)
                    judgementRound.NotifyO2InputAt(input.Time);

                hitWindowHelper.BPM = resolveSimulationBpm(beatmap, input.Time, environment.ManiaHitMode);

                var candidates = collectCandidatesForInput(laneStates, beatmap, input.Time, hitWindowHelper, environment.ManiaHitMode).ToList();

                if (input.IsPress && bms != null && poorEnabled)
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
                            ComputeStoredTimeOffset(input.Time, target),
                            input.Time,
                            gameplayRate,
                            timelineRecorder));
                }

                if (candidates.Count == 0)
                {
                    // [parity] 松手落在候选窗口外也可能断连（局内断连不受窗口限制）。
                    if (!input.IsPress)
                        tryApplyEarlyHoldBreakBody(laneStates, input.Time, wasHoldingBeforeEvent, environment, headByTail, holdByHead, headWasHit, scoreProcessor, gameplayRate, timelineRecorder);
                    continue;
                }

                var selected = selectCandidate(
                    candidates, laneStates, input.Time, environment);

                if (selected == null || selected.Judged)
                {
                    if (!input.IsPress)
                        tryApplyEarlyHoldBreakBody(laneStates, input.Time, wasHoldingBeforeEvent, environment, headByTail, holdByHead, headWasHit, scoreProcessor, gameplayRate, timelineRecorder);
                    continue;
                }

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

                double pressBpm = judgementRound.IsO2Jam ? judgementRound.O2PressBpm : hitWindowHelper.BPM;

                HitResult result;

                if (isTail)
                {
                    if (judgementRound.IsEzHitMode)
                    {
                        var tailEval = ManiaJudgementKernel.EvaluateHoldTail(new ManiaJudgementKernel.HoldTailEvaluationRequest
                        {
                            Round = judgementRound,
                            TimeOffset = timeOffsetForJudgement,
                            RawOffset = rawOffset,
                            HitWindows = target.HitWindows!,
                            UserTriggered = true,
                            HeadHit = headHit,
                            HoldBroken = selected.HoldBroken,
                            WasHolding = wasHoldingBeforeEvent,
                            HasHoldBreak = holdBreak,
                            EventTime = input.Time,
                            PressBpm = pressBpm,
                            FrameStableId = (long)(input.Time * 1000),
                            BmsState = bms != null ? selected.BmsRoute : null,
                        });

                        if (!tryMapTailEvaluation(tailEval, out result))
                        {
                            // [parity] 本次松手未判定尾键 → 检查是否构成断连（Body ComboBreak）。
                            if (!input.IsPress)
                                tryApplyEarlyHoldBreakBody(laneStates, input.Time, wasHoldingBeforeEvent, environment, headByTail, holdByHead, headWasHit, scoreProcessor, gameplayRate, timelineRecorder);
                            continue;
                        }
                    }
                    else
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
                            State = judgementRound.MutableState,
                            EventTime = input.Time,
                            Bpm = hitWindowHelper.BPM,
                            PillModeEnabled = pillModeEnabled,
                        });

                        if (environment.ManiaHitMode == EzEnumHitMode.Lazer && result == HitResult.None)
                        {
                            // [parity] 本次松手未判定尾键 → 检查是否构成断连（Body ComboBreak）。
                            if (!input.IsPress)
                                tryApplyEarlyHoldBreakBody(laneStates, input.Time, wasHoldingBeforeEvent, environment, headByTail, holdByHead, headWasHit, scoreProcessor, gameplayRate, timelineRecorder);
                            continue;
                        }
                    }
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
                            ComputeStoredTimeOffset(input.Time, target),
                            input.Time,
                            gameplayRate,
                            timelineRecorder);
                        continue;
                    }

                    result = BmsHitModeJudgement.MapTo(sessionOutcome.Judge);
                    selected.BmsRoute.CanRouteToKPoor = sessionOutcome.EnableCanRouteToKPoor;
                }
                else if (judgementRound.IsEzHitMode)
                {
                    var noteEval = ManiaJudgementKernel.EvaluateNote(new ManiaJudgementKernel.NoteEvaluationRequest
                    {
                        Round = judgementRound,
                        TimeOffset = timeOffsetForJudgement,
                        HitWindows = target.HitWindows!,
                        UserTriggered = true,
                        IsLnHead = target is HeadNote,
                        EventTime = input.Time,
                        PressBpm = pressBpm,
                        FrameStableId = (long)(input.Time * 1000),
                        BmsState = bms != null ? selected.BmsRoute : null,
                    });

                    if (!tryMapNoteEvaluation(noteEval, out result))
                        continue;
                }
                else
                {
                    var outcome = noteStrategy.EvaluatePress(timeOffsetForJudgement, target.HitWindows!);

                    if (outcome.Kind != ManiaNoteJudgementOutcomeKind.Apply)
                        continue;

                    result = outcome.Result;
                }

                foreach (var forced in ForceMissEarlier(laneStates, target.StartTime))
                {
                    if (!IsWithinMissWindow(forced.Target, input.Time, useTailReleaseLenience: false))
                        continue;

                    forced.Judged = true;
                    forced.Result = HitResult.Miss;
                    ApplyFinalResult(
                        scoreProcessor,
                        forced.Target,
                        HitResult.Miss,
                        ComputeStoredTimeOffset(input.Time, forced.Target),
                        input.Time,
                        gameplayRate,
                        environment.ManiaHitMode,
                        timelineRecorder);
                }

                selected.Judged = true;
                selected.Result = result;

                ApplyFinalResult(
                    scoreProcessor,
                    target,
                    result,
                    ComputeStoredTimeOffset(input.Time, target),
                    input.Time,
                    gameplayRate,
                    environment.ManiaHitMode,
                    timelineRecorder);

                // After tail judgement, also apply HoldNote parent and Body auxiliary results
                // to match live play behaviour (DrawableHoldNote.CheckForResult + DrawableHoldNoteBody.TriggerResult).
                // These produce IgnoreHit / ComboBreak / IgnoreMiss entries in ScoreResultCounts
                // that affect displayed statistics but not score, accuracy, or combo.
                if (target is TailNote judgedTail
                    && headByTail.TryGetValue(judgedTail, out var tailLinkedHead)
                    && holdByHead.TryGetValue(tailLinkedHead, out var tailHold))
                {
                    // HoldNoteBody: IgnoreHit on hit, ComboBreak on miss
                    // (matches DrawableHoldNoteBody.TriggerResult → ApplyMaxResult/ApplyMinResult)
                    // 断连时刻已产出 Body ComboBreak 的（BodyJudged），对齐局内不重复补判。
                    double tailStoredOffset = ComputeStoredTimeOffset(input.Time, judgedTail);

                    if (tailHold.Body != null && !selected.BodyJudged)
                    {
                        selected.BodyJudged = true;
                        HitResult bodyResult = result.IsHit() ? HitResult.IgnoreHit : HitResult.ComboBreak;
                        ApplyAuxiliaryResult(scoreProcessor, tailHold.Body, bodyResult, tailStoredOffset, input.Time, gameplayRate, timelineRecorder);
                    }

                    // HoldNote parent: IgnoreHit on hit, IgnoreMiss on miss
                    // (matches DrawableHoldNote.CheckForResult → ApplyMaxResult/MissForcefully)
                    HitResult holdAuxResult = result.IsHit() ? HitResult.IgnoreHit : HitResult.IgnoreMiss;
                    ApplyAuxiliaryResult(scoreProcessor, tailHold, holdAuxResult, tailStoredOffset, input.Time, gameplayRate, timelineRecorder);
                }

                if (target is HeadNote head)
                    headWasHit[head] = result.IsHit();
            }
        }

        private static ManiaJudgementRound createJudgementRound(IGameplayEnvironment environment, IBeatmap beatmap)
        {
            GameplayEnvironment gameplay = environment as GameplayEnvironment ?? new GameplayEnvironment
            {
                ManiaHitMode = environment.ManiaHitMode,
                ManiaHealthMode = environment.ManiaHealthMode,
                JudgePrecedence = environment.JudgePrecedence,
                OffsetPlusMania = environment.OffsetPlusMania,
                BmsPoorHitResultEnable = environment.BmsPoorHitResultEnable,
            };

            return ManiaJudgementRound.Create(gameplay, beatmap);
        }

        private static bool tryMapNoteEvaluation(ManiaJudgementKernel.NoteEvaluationResult evaluation, out HitResult result)
        {
            result = default;

            switch (evaluation.Kind)
            {
                case ManiaJudgementKernel.NoteEvaluationKind.ApplyNoteOutcome:
                    if (evaluation.NoteOutcome.Kind != ManiaNoteJudgementOutcomeKind.Apply)
                        return false;

                    result = evaluation.NoteOutcome.Result;
                    return true;

                case ManiaJudgementKernel.NoteEvaluationKind.ApplyBmsAction:
                    if (!evaluation.BmsAction.Handled || !evaluation.BmsAction.ApplyFinal)
                        return false;

                    result = BmsHitModeJudgement.MapTo(evaluation.BmsAction.Judge);
                    return result != HitResult.None;

                default:
                    return false;
            }
        }

        private static bool tryMapTailEvaluation(ManiaJudgementKernel.HoldTailEvaluationResult evaluation, out HitResult result)
        {
            result = default;

            if (!evaluation.Handled)
                return false;

            if (evaluation.ApplyMinResult)
            {
                result = HitResult.Miss;
                return true;
            }

            if (evaluation.FinalResult != null)
            {
                result = evaluation.FinalResult.Value;
                return result != HitResult.None;
            }

            if (evaluation.BmsAction.Handled && evaluation.BmsAction.ApplyFinal)
            {
                result = BmsHitModeJudgement.MapTo(evaluation.BmsAction.Judge);
                return result != HitResult.None;
            }

            return false;
        }

        private static LaneTargetState? selectCandidate(
            List<LaneTargetState> candidates,
            IReadOnlyList<LaneTargetState> laneStates,
            double inputTime,
            IGameplayEnvironment environment)
            => selectCandidateByPrecedence(candidates, laneStates, inputTime, environment);

        /// <summary>
        /// Earliest note-lock：与 <see cref="ManiaLaneController.SelectPressEntry"/> 一致，仅检查游标可打性，不在此预判 EvaluatePress。
        /// </summary>
        private static LaneTargetState? selectCandidateByPrecedence(
            List<LaneTargetState> candidates,
            IReadOnlyList<LaneTargetState> laneStates,
            double inputTime,
            IGameplayEnvironment environment)
        {
            if (candidates.Count == 0)
                return null;

            return ManiaLanePressSelector.SelectSessionTarget(
                candidates,
                laneStates,
                inputTime,
                environment.JudgePrecedence,
                HitModeHelper.IsBMSHitMode(environment.ManiaHitMode));
        }

        private static IEnumerable<LaneTargetState> collectCandidatesForInput(
            List<LaneTargetState> laneStates,
            IBeatmap beatmap,
            double eventTime,
            HitModeHelper hitWindowHelper,
            EzEnumHitMode hitMode)
        {
            if (laneStates.Count == 0)
                yield break;

            // 使用基准判定（非 tail lenience）计算时间窗口边界。
            // 对于 tail release，实际窗口更大，但二分查找只用于快速定位起始位置，
            // 后续线性扫描会逐条检查实际窗口（含 lenience）。
            hitWindowHelper.BPM = resolveSimulationBpm(beatmap, eventTime, hitMode);

            double baseEarlyWindow = hitWindowHelper.WindowFor(HitResult.Miss, true);
            double baseLateWindow = hitWindowHelper.WindowFor(HitResult.Miss, false);

            if (HitModeHelper.IsBMSHitMode(hitMode))
            {
                double lenienceForBms = 1;
                BmsHitModeJudgement.ExpandMissCollectionWindows(hitWindowHelper, lenienceForBms, ref baseEarlyWindow, ref baseLateWindow);
            }

            // 二分查找定位第一个 StartTime >= eventTime - missLateWindow 的位置。
            // 对于 tail release，使用放大后的窗口确保不遗漏。
            double maxTailLenience = usesTailReleaseLenience(hitMode) ? TailNote.RELEASE_WINDOW_LENIENCE : 1;
            double searchLowerBound = eventTime - baseLateWindow * maxTailLenience;

            int lo = 0, hi = laneStates.Count;

            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (laneStates[mid].Target.StartTime < searchLowerBound)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            double searchUpperBound = eventTime + baseEarlyWindow * maxTailLenience;

            for (int i = lo; i < laneStates.Count; i++)
            {
                var state = laneStates[i];

                // 已超出时间窗口，提前终止。
                if (state.Target.StartTime > searchUpperBound)
                    break;

                if (state.Judged)
                    continue;

                if (state.Target.HitWindows == null || ReferenceEquals(state.Target.HitWindows, HitWindows.Empty))
                    continue;

                bool useTailReleaseLenience = state.IsTail && usesTailReleaseLenience(hitMode);
                double lenienceFactor = useTailReleaseLenience ? TailNote.RELEASE_WINDOW_LENIENCE : 1;

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

        private static double resolveSimulationBpm(IBeatmap beatmap, double time, EzEnumHitMode hitMode)
        {
            if (hitMode == EzEnumHitMode.O2Jam)
                return O2HitModeExtension.GetBPMAtTime(time);

            return getBpmAtTime(beatmap, time);
        }

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
            EzEnumHitMode hitMode,
            ManiaReplayTimelineRecorder? timelineRecorder = null)
        {
            var judgementResult = new JudgementResult(target, target.Judgement)
            {
                Type = result,
            };

            JudgementResultTimingHelper.ApplyTiming(judgementResult, timeOffset, gameplayRate);

            if (result == HitResult.Miss
                || (result == HitResult.Meh && HitModeHelper.MehBreaksCombo(hitMode)))
            {
                judgementResult.IsComboHit = false;
            }

            scoreProcessor.ApplyResult(judgementResult);
            timelineRecorder?.Record(scoreProcessor, eventTime, gameplayRate);
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
            timelineRecorder?.Record(scoreProcessor, eventTime, gameplayRate);
        }

        /// <summary>
        /// Applies an auxiliary (non-gameplay-affecting) judgement result for HoldNote parent or HoldNoteBody,
        /// matching live play behaviour where these produce IgnoreHit/ComboBreak/IgnoreMiss entries
        /// in ScoreResultCounts despite having no effect on score, accuracy, or combo.
        /// </summary>
        /// <summary>
        /// [parity] 持有中提早松手且本次松手未判定尾键 → 断连时刻立即产出 Body ComboBreak，
        /// 对齐局内 <c>DrawableHoldNote.EzTriggerBodyAfterTailRelease</c>（isEarlyHoldRelease → <c>Body.TriggerResult(false)</c>）。
        /// 局内断连不受候选/判定窗口限制，因此在此按列扫描当前活动 LN。
        /// Malody 例外：局内走 <c>TryMalodyHoldOnReleased</c>（Body 记 IgnoreHit），不在此处理。
        /// </summary>
        private static void tryApplyEarlyHoldBreakBody(
            List<LaneTargetState> laneStates,
            double eventTime,
            bool wasHoldingBeforeEvent,
            IGameplayEnvironment environment,
            Dictionary<TailNote, HeadNote> headByTail,
            Dictionary<HeadNote, HoldNote> holdByHead,
            Dictionary<HeadNote, bool> headWasHit,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            ManiaReplayTimelineRecorder? timelineRecorder)
        {
            if (!wasHoldingBeforeEvent)
                return;

            if (MalodyHitModeJudgement.IsMalodyMode(environment.ManiaHitMode))
                return;

            foreach (var state in laneStates)
            {
                if (state.Judged || state.BodyJudged)
                    continue;

                if (state.Target is not TailNote tail
                    || !headByTail.TryGetValue(tail, out var head)
                    || !holdByHead.TryGetValue(head, out var hold)
                    || hold.Body == null)
                {
                    continue;
                }

                // 仅限已按到头判、且尚未到尾判时刻的活动 LN。
                if (!headWasHit.TryGetValue(head, out bool headHit) || !headHit)
                    continue;

                if (eventTime - tail.StartTime + environment.OffsetPlusMania >= 0)
                    continue;

                state.HoldBroken = true;
                state.BodyJudged = true;
                ApplyAuxiliaryResult(scoreProcessor, hold.Body, HitResult.ComboBreak,
                    ComputeStoredTimeOffset(eventTime, hold.Body), eventTime, gameplayRate, timelineRecorder);
                return; // 一次松手最多断一条 LN
            }
        }

        internal static void ApplyAuxiliaryResult(
            ScoreProcessor scoreProcessor,
            HitObject target,
            HitResult result,
            double storedTimeOffset,
            double eventTime,
            double gameplayRate,
            ManiaReplayTimelineRecorder? timelineRecorder)
        {
            var judgementResult = new JudgementResult(target, target.Judgement)
            {
                Type = result,
            };
            JudgementResultTimingHelper.ApplyTiming(judgementResult, storedTimeOffset, gameplayRate);
            scoreProcessor.ApplyResult(judgementResult);
            timelineRecorder?.Record(scoreProcessor, eventTime, gameplayRate);
        }

        internal static double ComputeStoredTimeOffset(double eventTime, HitObject target)
            => eventTime - target.GetEndTime();

        internal static double ResolveMissStoredOffset(
            HitObject target,
            IReadOnlyDictionary<int, List<double>> pressTimesByColumn,
            double? beforeTimeInclusive = null)
        {
            if (target is not IHasColumn hasColumn)
                return ComputeStoredTimeOffset(ResolveMissEventTime(target, pressTimesByColumn, beforeTimeInclusive), target);

            if (!pressTimesByColumn.TryGetValue(hasColumn.Column, out var times) || times.Count == 0)
                return ComputeStoredTimeOffset(target.GetEndTime(), target);

            return ResolveMissStoredOffset(target, times, beforeTimeInclusive);
        }

        /// <summary>
        /// Drawable 列 press 列表专用；避免 Dictionary/List 快照分配。
        /// </summary>
        internal static double ResolveMissStoredOffset(
            HitObject target,
            IReadOnlyList<double> columnPressTimes,
            double? beforeTimeInclusive = null)
        {
            double eventTime = ResolveMissEventTime(target, columnPressTimes, beforeTimeInclusive);
            return ComputeStoredTimeOffset(eventTime, target);
        }

        internal static double ResolveMissEventTime(
            HitObject target,
            IReadOnlyDictionary<int, List<double>> pressTimesByColumn,
            double? beforeTimeInclusive = null)
        {
            if (target is not IHasColumn hasColumn)
                return target.GetEndTime();

            if (!pressTimesByColumn.TryGetValue(hasColumn.Column, out var times) || times.Count == 0)
                return target.GetEndTime();

            return ResolveMissEventTime(target, times, beforeTimeInclusive);
        }

        internal static double ResolveMissEventTime(
            HitObject target,
            IReadOnlyList<double> columnPressTimes,
            double? beforeTimeInclusive = null)
        {
            if (columnPressTimes.Count == 0)
                return target.GetEndTime();

            double reference = target.GetEndTime();
            double bestTime = double.NaN;
            double bestDistance = double.PositiveInfinity;

            foreach (double pressTime in columnPressTimes)
            {
                if (pressTime > beforeTimeInclusive)
                    continue;

                double distance = Math.Abs(pressTime - reference);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTime = pressTime;
                }
            }

            if (double.IsNaN(bestTime))
                return beforeTimeInclusive ?? target.GetEndTime();

            return bestTime;
        }

        private static int indexOf(IReadOnlyList<LaneTargetState> laneStates, LaneTargetState candidate)
        {
            for (int i = 0; i < laneStates.Count; i++)
            {
                if (ReferenceEquals(laneStates[i], candidate))
                    return i;
            }

            return -1;
        }
    }
}
