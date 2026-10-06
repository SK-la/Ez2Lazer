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
            var activeHoldByColumn = new Dictionary<int, HeadNote>();
            var ez2AcHoldStates = new Dictionary<HoldNote, Ez2AcHoldState>();
            var judgedTicks = new HashSet<HoldNoteTick>();

            // 对齐局内 Column.ProcessAutoMiss：越过 late miss 边界立刻被动 Miss，
            // 否则未命中会拖到 end-sweep，中段 combo 不断 → MaxCombo 虚高。
            var autoMissQueue = buildAutoMissQueue(pressColumns, releaseColumns, environment.ManiaHitMode);
            int autoMissCursor = 0;

            foreach (var input in inputData.SortedEvents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 间隙内到期的被动 Miss 必须先于本次按键生效（否则 MaxCombo 会跨过空窗继续涨）。
                // 与局内同帧「先输入、后 UpdateAfterChildren.ProcessAutoMiss」对齐：本帧 =t 的到期留到输入之后。
                applyAutoMissesUpTo(
                    input.Time,
                    autoMissQueue,
                    ref autoMissCursor,
                    holdByHead,
                    headByTail,
                    activeHoldByColumn,
                    inputData.PressTimesByColumn,
                    scoreProcessor,
                    gameplayRate,
                    environment.ManiaHitMode,
                    timelineRecorder,
                    endExclusive: true);

                bool wasHoldingBeforeEvent = keyHeldByColumn.TryGetValue(input.Column, out bool held) && held;

                // 任意 continue 都不得跳过「输入后 auto-miss」，否则同帧到期尾/头会拖到下一事件，配对漂移。
                try
                {
                    processInputEvent();
                }
                finally
                {
                    applyAutoMissesUpTo(
                        input.Time,
                        autoMissQueue,
                        ref autoMissCursor,
                        holdByHead,
                        headByTail,
                        activeHoldByColumn,
                        inputData.PressTimesByColumn,
                        scoreProcessor,
                        gameplayRate,
                        environment.ManiaHitMode,
                        timelineRecorder,
                        endExclusive: false);
                }

                void processInputEvent()
                {
                    // 局内：Column 路由 apply 失败时 columnRoutedPressTarget 仍为 null，Hold.OnPressed 可重臂。
                    // Session 在「选中候选但未 Apply」的 return 路径上必须同样重臂，不能只在 candidates 空时做。
                    bool pressRouteApplied = false;

                    try
                    {
                        processInputEventCore(ref pressRouteApplied);
                    }
                    finally
                    {
                        if (input.IsPress
                            && !pressRouteApplied
                            && !activeHoldByColumn.ContainsKey(input.Column)
                            && pressColumns.TryGetValue(input.Column, out var rearmLaneStates))
                        {
                            tryRearmActiveHold(
                                input.Column,
                                input.Time,
                                rearmLaneStates,
                                releaseColumns,
                                holdByHead,
                                holdStrategy,
                                activeHoldByColumn);
                        }
                    }
                }

                void processInputEventCore(ref bool pressRouteApplied)
                {
                    // Drawable：同帧先 Update tick 再（或交错）处理按键。
                    // 松手：先结算 <t 的持有 tick，再松，再结算 =t 为 Miss。
                    // 重按：先结算 <=t（仍为 Broken）为 Miss，再 Recover，避免 =t 被算进涨 combo。
                    applyEz2AcTicksUpTo(
                        input.Time,
                        environment,
                        holdByHead,
                        headWasHit,
                        keyHeldByColumn,
                        ez2AcHoldStates,
                        judgedTicks,
                        scoreProcessor,
                        gameplayRate,
                        timelineRecorder,
                        endExclusive: true);

                    if (!input.IsPress)
                    {
                        keyHeldByColumn[input.Column] = false;
                        tryApplyEz2AcHoldRelease(input.Column, holdByHead, headWasHit, headByTail, releaseColumns, ez2AcHoldStates, environment);

                        applyEz2AcTicksUpTo(
                            input.Time,
                            environment,
                            holdByHead,
                            headWasHit,
                            keyHeldByColumn,
                            ez2AcHoldStates,
                            judgedTicks,
                            scoreProcessor,
                            gameplayRate,
                            timelineRecorder,
                            endExclusive: false);
                    }
                    else
                    {
                        applyEz2AcTicksUpTo(
                            input.Time,
                            environment,
                            holdByHead,
                            headWasHit,
                            keyHeldByColumn,
                            ez2AcHoldStates,
                            judgedTicks,
                            scoreProcessor,
                            gameplayRate,
                            timelineRecorder,
                            endExclusive: false);

                        keyHeldByColumn[input.Column] = true;
                        tryApplyEz2AcHoldRepress(input.Column, holdByHead, headWasHit, headByTail, releaseColumns, ez2AcHoldStates, environment);
                    }

                    var perColumnDict = input.IsPress ? pressColumns : releaseColumns;
                    if (!perColumnDict.TryGetValue(input.Column, out var laneStates))
                        return;

                    // 局内只在按下时刷新 press-time BPM（Column.OnPressed）；松手沿用按下那一刻的值，
                    // 尾判取的是同一份 BPM。
                    if (judgementRound.IsO2Jam && input.IsPress)
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

                    LaneTargetState? activeTail = null;
                    HeadNote? releasingHoldHead = null;

                    if (!input.IsPress)
                    {
                        // 局内松手只作用于「此刻正按住的那条 LN」（Column.OnReleased → LaneController.ActiveHold），
                        // 且 TryColumnHoldTailRelease 不再做优先级/note-lock 判定，故此处直接以该尾为目标：
                        // 不能再走 selectCandidate 的 Earliest 阻挡检查，否则更晚的尾已开始（StartTime <= 松手时刻）
                        // 会把本次松手否决，导致这条尾留到后续按键被 ForceMissEarlier 补成 Miss。
                        if (wasHoldingBeforeEvent && activeHoldByColumn.TryGetValue(input.Column, out var heldHead))
                            releasingHoldHead = heldHead;

                        activeTail = resolveActiveHoldTail(input.Column, activeHoldByColumn, holdByHead, wasHoldingBeforeEvent, candidates);

                        activeHoldByColumn.Remove(input.Column);

                        if (activeTail == null)
                        {
                            tryApplyEz2AcHoldRelease(input.Column, holdByHead, headWasHit, headByTail, releaseColumns, ez2AcHoldStates, environment);
                            tryApplyEarlyHoldBreakBody(releasingHoldHead, input.Time, environment, holdByHead, releaseColumns, scoreProcessor, gameplayRate, timelineRecorder);
                            return;
                        }
                    }

                    if (candidates.Count == 0 && activeTail == null)
                    {
                        // 按下重臂改由 processInputEvent.finally 统一处理。
                        // [parity] 松手落在候选窗口外也可能断连（局内断连不受窗口限制）。
                        if (!input.IsPress)
                            tryApplyEarlyHoldBreakBody(releasingHoldHead, input.Time, environment, holdByHead, releaseColumns, scoreProcessor, gameplayRate, timelineRecorder);

                        return;
                    }

                    var selected = activeTail ?? selectCandidate(
                        candidates, laneStates, input.Time, environment);

                    if (selected == null || selected.Judged)
                    {
                        if (!input.IsPress)
                        {
                            tryApplyEz2AcHoldRelease(input.Column, holdByHead, headWasHit, headByTail, releaseColumns, ez2AcHoldStates, environment);
                            tryApplyEarlyHoldBreakBody(releasingHoldHead, input.Time, environment, holdByHead, releaseColumns, scoreProcessor, gameplayRate, timelineRecorder);
                        }
                        else
                            tryApplyEz2AcHoldRepress(input.Column, holdByHead, headWasHit, headByTail, releaseColumns, ez2AcHoldStates, environment);

                        return;
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

                    // 局内 OnReleased 先判尾、后写 Body，因此这一投看到的 Body 断连必然仍是 false：
                    // 由「提前松手」反推 HoldBroken 会把合法的提前松手压成 Meh（Common，Lazer/Classic）或直接否决（O2）。
                    // 仅 BMS 尾语义按自身规则消费 HoldBroken。
                    if (HitModeHelper.IsBMSHitMode(environment.ManiaHitMode) && !input.IsPress && isTail && headHit && wasHoldingBeforeEvent && rawOffset < 0)
                        selected.HoldBroken = true;

                    if (input.IsPress && target is HeadNote headNote && holdByHead.TryGetValue(headNote, out var hold))
                    {
                        if (!holdStrategy.CanBeginHoldAt(input.Time, hold.Tail))
                            return;
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
                                {
                                    tryApplyEz2AcHoldRelease(input.Column, holdByHead, headWasHit, headByTail, releaseColumns, ez2AcHoldStates, environment);
                                    tryApplyEarlyHoldBreakBody(releasingHoldHead, input.Time, environment, holdByHead, releaseColumns, scoreProcessor, gameplayRate, timelineRecorder);
                                }

                                return;
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

                            // Lazer / Classic：窗外 None → 不判尾（断连 / 重臂 / auto-miss）；
                            // Miss 窗内 ResultFor=Miss 须落判（对齐 DrawableHoldNoteTail），不得吞成 None。
                            if (!judgementRound.IsEzHitMode && result == HitResult.None)
                            {
                                // [parity] 本次松手未判定尾键 → 检查是否构成断连（Body ComboBreak）。
                                if (!input.IsPress)
                                    tryApplyEarlyHoldBreakBody(releasingHoldHead, input.Time, environment, holdByHead, releaseColumns, scoreProcessor, gameplayRate, timelineRecorder);
                                return;
                            }
                        }
                    }
                    else if (bms != null && target.HitWindows is ManiaHitWindows bmsWindows)
                    {
                        var sessionOutcome = bms.EvaluateSessionPress(bmsWindows, timeOffsetForJudgement, selected.BmsRoute, poorEnabled);

                        if (sessionOutcome.Kind == BmsHitModeJudgement.SessionPressKind.None)
                            return;

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
                            return;
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
                            return;
                    }
                    else
                    {
                        var outcome = noteStrategy.EvaluatePress(timeOffsetForJudgement, target.HitWindows!);

                        if (outcome.Kind != ManiaNoteJudgementOutcomeKind.Apply)
                            return;

                        result = outcome.Result;
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

                    // 按下已落到 Note/Head（含 Miss）：等价局内 columnRoutedPressTarget != null，finally 不再重臂。
                    if (input.IsPress && !isTail)
                        pressRouteApplied = true;

                    // 局内 Column.OnNewResult：仅 IsHit 时 handleHit → CollectForceMissBefore；
                    // 仍 CanBeHit 的更早物件 continue 跳过（不 abort 本次已落判定）。
                    // 旧 Session 在 Apply 前 ForceMiss 且用 return，会把本次命中整段吞掉。
                    //
                    // 局内 TryCreateEntry 拒绝 Head/Tail，ForceMiss 只钉 HoldNote（按头）与 Note；
                    // Session 的 releaseColumns 是 Tail，绝不能 ForceMiss 尾——否则松手命中后一条
                    // 会把更早、尚未 auto-miss 的尾提前钉成 Miss（CanBeHit 还不带 release lenience）。
                    if (result.IsHit())
                    {
                        foreach (var forced in ForceMissEarlier(laneStates, target.StartTime))
                        {
                            if (forced.Target is TailNote)
                                continue;

                            if (isStillUserTriggerJudgeable(forced.Target, input.Time, headWasHit, holdByHead))
                                continue;

                            forced.Judged = true;
                            forced.Result = HitResult.Miss;
                            double forcedOffset = ComputeStoredTimeOffset(input.Time, forced.Target);
                            ApplyFinalResult(
                                scoreProcessor,
                                forced.Target,
                                HitResult.Miss,
                                forcedOffset,
                                input.Time,
                                gameplayRate,
                                environment.ManiaHitMode,
                                timelineRecorder);
                        }
                    }

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
                    {
                        headWasHit[head] = result.IsHit();

                        // 局内 DrawableHoldNote.beginHoldAndJudgeHead 先 beginHoldAt（ReportHoldState(true)）再判头，
                        // 因此头即使判 Miss，只要按下落在头 miss 窗内就进入持有；「松手判尾」只要求 IsHolding。
                        // 头 miss 时尾仍会被尾判封顶（!Head.IsHit），故此处不按头结果过滤。
                        if (holdByHead.ContainsKey(head) && isWithinHeadBeginHoldWindow(head, input.Time))
                            activeHoldByColumn[head.Column] = head;

                        if (environment.ManiaHitMode == EzEnumHitMode.EZ2AC
                            && holdByHead.TryGetValue(head, out var judgedHold))
                        {
                            var state = getEz2AcState(ez2AcHoldStates, judgedHold);
                            state.OnHeadJudged(Ez2AcHitModeJudgement.FromHitResult(result), preHeld: wasHoldingBeforeEvent);
                        }
                    }
                } // processInputEventCore
            }

            // 收尾：剩余 tick + EZ2AC 未判尾（持满不松）
            double endTime = inputData.SortedEvents.Count > 0
                ? inputData.SortedEvents[^1].Time + 1
                : beatmap.HitObjects.LastOrDefault()?.GetEndTime() ?? 0;

            applyAutoMissesUpTo(
                endTime + 10_000,
                autoMissQueue,
                ref autoMissCursor,
                holdByHead,
                headByTail,
                activeHoldByColumn,
                inputData.PressTimesByColumn,
                scoreProcessor,
                gameplayRate,
                environment.ManiaHitMode,
                timelineRecorder,
                endExclusive: false);

            applyEz2AcTicksUpTo(
                endTime + 10_000,
                environment,
                holdByHead,
                headWasHit,
                keyHeldByColumn,
                ez2AcHoldStates,
                judgedTicks,
                scoreProcessor,
                gameplayRate,
                timelineRecorder);

            finalizeEz2AcOpenTails(
                environment,
                holdByHead,
                headByTail,
                releaseColumns,
                headWasHit,
                keyHeldByColumn,
                scoreProcessor,
                gameplayRate,
                timelineRecorder,
                endTime + 10_000);
        }

        private static Ez2AcHoldState getEz2AcState(Dictionary<HoldNote, Ez2AcHoldState> map, HoldNote hold)
        {
            if (!map.TryGetValue(hold, out var state))
                map[hold] = state = new Ez2AcHoldState();

            return state;
        }

        private static List<(double Deadline, LaneTargetState State)> buildAutoMissQueue(
            Dictionary<int, List<LaneTargetState>> pressColumns,
            Dictionary<int, List<LaneTargetState>> releaseColumns,
            EzEnumHitMode hitMode)
        {
            var queue = new List<(double Deadline, LaneTargetState State)>();

            foreach (var list in pressColumns.Values.Concat(releaseColumns.Values))
            {
                foreach (var state in list)
                    queue.Add((GetSessionAutoMissDeadline(state.Target, hitMode), state));
            }

            queue.Sort((a, b) => a.Deadline.CompareTo(b.Deadline));
            return queue;
        }

        /// <summary>
        /// 对齐局内 <see cref="ManiaLaneController.GetAutoMissEvaluationTime"/> + Tail 松手宽限下
        /// <c>CanBeHit(offset / RELEASE_WINDOW_LENIENCE)</c> 首次失败的时刻。
        /// </summary>
        internal static double GetSessionAutoMissDeadline(HitObject target, EzEnumHitMode hitMode)
        {
            if (target.HitWindows == null || ReferenceEquals(target.HitWindows, HitWindows.Empty))
                return target.GetEndTime();

            // Note/Head：与 GetAutoMissEvaluationTime 一致（晚侧 Miss 窗起开始被动 Miss）。
            // Tail：局内从 MissLate 起轮询，真正 Apply 在 !CanBeHit(offset/lenience) 时
            // （LowestSuccessful=Meh），即 End + MehWindow * RELEASE_WINDOW_LENIENCE。
            double missLate = target.HitWindows is ManiaHitWindows maniaWindows
                ? maniaWindows.MissLateWindow
                : target.HitWindows.WindowFor(HitResult.Miss);

            double deadline = target.GetEndTime() + missLate;

            if (target is TailNote && usesTailReleaseLenience(hitMode))
            {
                // CanBeHit 用 LowestSuccessful（Mania=Meh）；Tail 先 / lenience 再判断。
                double canBeHitWindow = target.HitWindows.WindowFor(HitResult.Meh) * TailNote.RELEASE_WINDOW_LENIENCE;
                deadline = Math.Max(deadline, target.GetEndTime() + canBeHitWindow);
            }

            return deadline;
        }

        private static void applyAutoMissesUpTo(
            double time,
            List<(double Deadline, LaneTargetState State)> autoMissQueue,
            ref int autoMissCursor,
            Dictionary<HeadNote, HoldNote> holdByHead,
            Dictionary<TailNote, HeadNote> headByTail,
            Dictionary<int, HeadNote> activeHoldByColumn,
            IReadOnlyDictionary<int, List<double>> pressTimesByColumn,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            EzEnumHitMode hitMode,
            ManiaReplayTimelineRecorder? timelineRecorder,
            bool endExclusive = false)
        {
            while (autoMissCursor < autoMissQueue.Count
                   && (endExclusive
                       ? autoMissQueue[autoMissCursor].Deadline < time
                       : autoMissQueue[autoMissCursor].Deadline <= time))
            {
                var (deadline, state) = autoMissQueue[autoMissCursor];
                autoMissCursor++;

                if (state.Judged)
                    continue;

                state.Judged = true;
                state.Result = HitResult.Miss;

                double missEventTime = deadline;
                double storedOffset = ResolveMissStoredOffset(state.Target, pressTimesByColumn, missEventTime);

                ApplyFinalResult(
                    scoreProcessor,
                    state.Target,
                    HitResult.Miss,
                    storedOffset,
                    missEventTime,
                    gameplayRate,
                    hitMode,
                    timelineRecorder);

                clearActiveHoldAfterPassiveMiss(state.Target, headByTail, activeHoldByColumn);

                if (state.Target is TailNote tailNote
                    && headByTail.TryGetValue(tailNote, out var linkedHead)
                    && holdByHead.TryGetValue(linkedHead, out var hold))
                {
                    if (hold.Body != null && !state.BodyJudged)
                    {
                        state.BodyJudged = true;
                        ApplyAuxiliaryResult(scoreProcessor, hold.Body, HitResult.ComboBreak, storedOffset, missEventTime, gameplayRate, timelineRecorder);
                    }

                    ApplyAuxiliaryResult(scoreProcessor, hold, HitResult.IgnoreMiss, storedOffset, missEventTime, gameplayRate, timelineRecorder);
                }
            }
        }

        private static void clearActiveHoldAfterPassiveMiss(
            HitObject target,
            Dictionary<TailNote, HeadNote> headByTail,
            Dictionary<int, HeadNote> activeHoldByColumn)
        {
            HeadNote? head = target switch
            {
                HeadNote h => h,
                TailNote t when headByTail.TryGetValue(t, out var linked) => linked,
                _ => null,
            };

            if (head == null)
                return;

            if (activeHoldByColumn.TryGetValue(head.Column, out var active) && ReferenceEquals(active, head))
                activeHoldByColumn.Remove(head.Column);
        }

        private static void tryApplyEz2AcHoldRelease(
            int column,
            Dictionary<HeadNote, HoldNote> holdByHead,
            Dictionary<HeadNote, bool> headWasHit,
            Dictionary<TailNote, HeadNote> headByTail,
            Dictionary<int, List<LaneTargetState>> releaseColumns,
            Dictionary<HoldNote, Ez2AcHoldState> ez2AcHoldStates,
            IGameplayEnvironment environment)
        {
            if (environment.ManiaHitMode != EzEnumHitMode.EZ2AC)
                return;

            foreach (var (head, hold) in holdByHead)
            {
                if (hold.Column != column)
                    continue;

                if (!headWasHit.TryGetValue(head, out bool hit) || !hit)
                    continue;

                getEz2AcState(ez2AcHoldStates, hold).OnRelease();
                return;
            }
        }

        private static void tryApplyEz2AcHoldRepress(
            int column,
            Dictionary<HeadNote, HoldNote> holdByHead,
            Dictionary<HeadNote, bool> headWasHit,
            Dictionary<TailNote, HeadNote> headByTail,
            Dictionary<int, List<LaneTargetState>> releaseColumns,
            Dictionary<HoldNote, Ez2AcHoldState> ez2AcHoldStates,
            IGameplayEnvironment environment)
        {
            if (environment.ManiaHitMode != EzEnumHitMode.EZ2AC)
                return;

            foreach (var (head, hold) in holdByHead)
            {
                if (hold.Column != column)
                    continue;

                if (!headWasHit.TryGetValue(head, out bool hit) || !hit)
                    continue;

                getEz2AcState(ez2AcHoldStates, hold).OnRepress();
                return;
            }
        }

        private static void applyEz2AcTicksUpTo(
            double time,
            IGameplayEnvironment environment,
            Dictionary<HeadNote, HoldNote> holdByHead,
            Dictionary<HeadNote, bool> headWasHit,
            Dictionary<int, bool> keyHeldByColumn,
            Dictionary<HoldNote, Ez2AcHoldState> ez2AcHoldStates,
            HashSet<HoldNoteTick> judgedTicks,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            ManiaReplayTimelineRecorder? timelineRecorder,
            bool endExclusive = false)
        {
            if (environment.ManiaHitMode != EzEnumHitMode.EZ2AC)
                return;

            double cutoff = time + environment.OffsetPlusMania;

            foreach (var hold in holdByHead.Values)
            {
                if (hold.Ticks == null)
                    continue;

                bool holding = keyHeldByColumn.TryGetValue(hold.Column, out bool held) && held;
                var state = getEz2AcState(ez2AcHoldStates, hold);

                foreach (var tick in hold.Ticks)
                {
                    if (judgedTicks.Contains(tick))
                        continue;

                    if (endExclusive ? tick.StartTime >= cutoff : tick.StartTime > cutoff)
                        continue;

                    // 涨 combo 由态机（是否已接上/未断开）+ 按住决定；不另查 head 档位。
                    var result = Ez2AcHitModeJudgement.Instance.EvaluateTick(state, holding);
                    judgedTicks.Add(tick);
                    // 与 Drawable 到点结算对齐：事件时间取 tick.StartTime，TimeOffset≈0。
                    ApplyAuxiliaryResult(
                        scoreProcessor,
                        tick,
                        result,
                        ComputeStoredTimeOffset(tick.StartTime, tick),
                        tick.StartTime,
                        gameplayRate,
                        timelineRecorder);
                }
            }
        }

        private static void finalizeEz2AcOpenTails(
            IGameplayEnvironment environment,
            Dictionary<HeadNote, HoldNote> holdByHead,
            Dictionary<TailNote, HeadNote> headByTail,
            Dictionary<int, List<LaneTargetState>> releaseColumns,
            Dictionary<HeadNote, bool> headWasHit,
            Dictionary<int, bool> keyHeldByColumn,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            ManiaReplayTimelineRecorder? timelineRecorder,
            double eventTime)
        {
            if (environment.ManiaHitMode != EzEnumHitMode.EZ2AC)
                return;

            // Tail 绑 IgnoreHit 后不进 releaseColumns，直接扫 HoldNote。
            foreach (var (head, hold) in holdByHead)
            {
                if (hold.Tail == null)
                    continue;

                // 已在常规路径判过尾则跳过（少见：尾若被重新纳入 targets）。
                bool headHit = headWasHit.TryGetValue(head, out bool hit) && hit;

                ApplyFinalResult(
                    scoreProcessor,
                    hold.Tail,
                    HitResult.IgnoreHit,
                    ComputeStoredTimeOffset(eventTime, hold.Tail),
                    eventTime,
                    gameplayRate,
                    environment.ManiaHitMode,
                    timelineRecorder);

                if (hold.Body != null)
                {
                    ApplyAuxiliaryResult(scoreProcessor, hold.Body, HitResult.IgnoreHit,
                        ComputeStoredTimeOffset(eventTime, hold.Body), eventTime, gameplayRate, timelineRecorder);
                }

                ApplyAuxiliaryResult(
                    scoreProcessor,
                    hold,
                    headHit ? HitResult.IgnoreHit : HitResult.IgnoreMiss,
                    ComputeStoredTimeOffset(eventTime, hold),
                    eventTime,
                    gameplayRate,
                    timelineRecorder);
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

        /// <summary>
        /// 定位「此刻正按住的那条 LN」的尾候选，等价于局内 <c>Column.OnReleased</c> 读到的 <c>LaneController.ActiveHold</c>。
        /// </summary>
        /// <remarks>
        /// 仅当该尾落在本次候选窗口内（即局内 <c>Tail.UpdateResult()</c> 会产生判定）时才返回；
        /// 窗口外局内不判定尾、只由 Body 断连收束，故返回 null 让调用方走断连分支并保持尾未判（留给 auto-miss）。
        /// 返回 null 表示本次松手不判任何尾（局内该分支不会去够同列相邻 LN）。
        /// </remarks>
        private static LaneTargetState? resolveActiveHoldTail(
            int column,
            Dictionary<int, HeadNote> activeHoldByColumn,
            Dictionary<HeadNote, HoldNote> holdByHead,
            bool wasHolding,
            IReadOnlyList<LaneTargetState> candidates)
        {
            // 局内要求 activeHold.IsHolding：未持有（松手时 reading 为 false）时 OnReleased 不判尾。
            if (!wasHolding || !activeHoldByColumn.TryGetValue(column, out var activeHead))
                return null;

            if (!holdByHead.TryGetValue(activeHead, out var activeHold))
                return null;

            foreach (var state in candidates)
            {
                if (ReferenceEquals(state.Target, activeHold.Tail))
                    return state;
            }

            return null;
        }

        /// <summary>
        /// 局内重臂：仅当列路由未选中目标时，<c>DrawableHoldNote.OnPressed</c> 仍会
        /// <c>TryBeginHoldPress → beginHoldAt</c>（头已判定、尾未收束 → 重新 holding）。
        /// 列已路由到其它物件时 <c>ShouldSkipColumnRoutedPress</c> 为真，不会重臂。
        /// </summary>
        private static void tryRearmActiveHold(
            int column,
            double time,
            IReadOnlyList<LaneTargetState> pressLaneStates,
            IReadOnlyDictionary<int, List<LaneTargetState>> releaseColumns,
            Dictionary<HeadNote, HoldNote> holdByHead,
            IManiaHoldJudgementStrategy holdStrategy,
            Dictionary<int, HeadNote> activeHoldByColumn)
        {
            if (!releaseColumns.TryGetValue(column, out var releaseStates))
                return;

            HeadNote? rearmed = null;
            double rearmedHeadStart = double.PositiveInfinity;

            foreach (var state in pressLaneStates)
            {
                // 仅头已判定的 LN 需要重臂；头未判定者由常规候选路径处理。
                if (state.Target is not HeadNote head || !state.Judged)
                    continue;

                if (!holdByHead.TryGetValue(head, out var hold))
                    continue;

                if (!holdStrategy.CanBeginHoldAt(time, hold.Tail))
                    continue;

                if (!isWithinHeadBeginHoldWindow(head, time))
                    continue;

                bool tailJudged = false;

                foreach (var releaseState in releaseStates)
                {
                    if (ReferenceEquals(releaseState.Target, hold.Tail))
                    {
                        tailJudged = releaseState.Judged;
                        break;
                    }
                }

                // 尾已判定 → 局内父物件已收束，不再重臂。
                if (tailJudged)
                    continue;

                if (head.StartTime < rearmedHeadStart)
                {
                    rearmed = head;
                    rearmedHeadStart = head.StartTime;
                }
            }

            if (rearmed != null)
                activeHoldByColumn[column] = rearmed;
        }

        /// <summary>
        /// 对齐 <c>DrawableHoldNote.beginHoldAt</c> 的守卫：按下早于头 Miss 窗左界时不进入持有。
        /// </summary>
        private static bool isWithinHeadBeginHoldWindow(HeadNote head, double time)
        {
            double missWindow = head.HitWindows?.WindowFor(HitResult.Miss) ?? 0;
            return time - head.StartTime >= -missWindow;
        }

        /// <summary>
        /// Session 版 <see cref="OrderedHitPolicyHelper.IsUserTriggerJudgeableNow"/>：
        /// 仍落在 LowestSuccessful（Mania=Meh）窗内则不可被 ForceMiss 提前钉死。
        /// </summary>
        private static bool isStillUserTriggerJudgeable(
            HitObject target,
            double time,
            IReadOnlyDictionary<HeadNote, bool> headWasHit,
            IReadOnlyDictionary<HeadNote, HoldNote> holdByHead)
        {
            if (target.HitWindows == null || ReferenceEquals(target.HitWindows, HitWindows.Empty))
                return false;

            if (target is HeadNote head && holdByHead.TryGetValue(head, out var hold))
            {
                if (!head.HitWindows.CanBeHit(time - head.StartTime))
                    return false;

                if (time > hold.Tail.StartTime)
                {
                    bool headHit = headWasHit.TryGetValue(head, out bool hit) && hit;

                    if (!headHit
                        && hold.Tail.HitWindows != null
                        && !ReferenceEquals(hold.Tail.HitWindows, HitWindows.Empty)
                        && !hold.Tail.HitWindows.CanBeHit(time - hold.Tail.StartTime))
                    {
                        return false;
                    }
                }

                return true;
            }

            return target.HitWindows.CanBeHit(time - target.StartTime);
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
        /// 仅作用于松手前的 <paramref name="activeHead"/>（局内 ActiveHold），不得扫同列未来 LN。
        /// 头 Miss 仍可能持有（<c>beginHoldAt</c>），故不要求 headHit。
        /// Malody 例外：局内走 <c>TryMalodyHoldOnReleased</c>（Body 记 IgnoreHit），不在此处理。
        /// </summary>
        private static void tryApplyEarlyHoldBreakBody(
            HeadNote? activeHead,
            double eventTime,
            IGameplayEnvironment environment,
            Dictionary<HeadNote, HoldNote> holdByHead,
            IReadOnlyDictionary<int, List<LaneTargetState>> releaseColumns,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            ManiaReplayTimelineRecorder? timelineRecorder)
        {
            if (activeHead == null)
                return;

            if (MalodyHitModeJudgement.IsMalodyMode(environment.ManiaHitMode)
                || environment.ManiaHitMode == EzEnumHitMode.EZ2AC)
                return;

            if (!holdByHead.TryGetValue(activeHead, out var hold) || hold.Body == null)
                return;

            // 局内 isEarlyHoldRelease：Time < Tail.StartTime。
            if (eventTime - hold.Tail.StartTime + environment.OffsetPlusMania >= 0)
                return;

            if (!releaseColumns.TryGetValue(activeHead.Column, out var releaseStates))
                return;

            LaneTargetState? state = null;

            foreach (var candidate in releaseStates)
            {
                if (ReferenceEquals(candidate.Target, hold.Tail))
                {
                    state = candidate;
                    break;
                }
            }

            if (state == null || state.Judged || state.BodyJudged)
                return;

            state.HoldBroken = true;
            state.BodyJudged = true;
            ApplyAuxiliaryResult(scoreProcessor, hold.Body, HitResult.ComboBreak,
                ComputeStoredTimeOffset(eventTime, hold.Body), eventTime, gameplayRate, timelineRecorder);
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
