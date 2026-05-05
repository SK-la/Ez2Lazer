// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Statistics;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mania.EzMania.Statistics
{
    /// <summary>
    /// Mania成绩的<see cref="HitEvent"/>生成器，通过将成绩的回放输入重新评估与提供的可玩谱面进行比较来生成<see cref="HitEvent"/>。
    /// <para>这个生成器主要用于结果/统计用途，其中<see cref="ScoreInfo.HitEvents"/>没有被持久化。</para>
    /// <para>上游通过反射注册到<see cref="EzScoreReloadBridge"/>，因此无法直接查看调用源。</para>
    /// </summary>
    public sealed class ManiaScoreHitEventGenerator : IScoreHitEventGenerator
    {
        public static ManiaScoreHitEventGenerator Instance { get; } = new ManiaScoreHitEventGenerator();

        static ManiaScoreHitEventGenerator()
        {
            EzScoreReloadBridge.RegisterImplementation("mania", Instance);
            EzScoreReloadBridge.RegisterImplementation("3", Instance);
        }

        public bool Validate(Score score)
        {
            if (score.ScoreInfo.Ruleset.OnlineID != 3)
                return false;

            var replay = score.Replay;

            if (replay == null || replay.Frames.Count == 0)
                return false;

            if (replay.Frames.Any(f => f is not ManiaReplayFrame))
                return false;

            return true;
        }

        /// <summary>
        /// Instance implementation of generator.
        /// </summary>
        public List<HitEvent> Generate(Score score, IBeatmap playableBeatmap, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Replay replay = score.Replay;
            EzEnumHitMode hitMode = GlobalConfigStore.EzConfig.Get<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            EzEnumJudgePrecedence judgePrecedence = GlobalConfigStore.EzConfig.Get<EzEnumJudgePrecedence>(Ez2Setting.JudgePrecedence);
            EzEnumHealthMode healthMode = GlobalConfigStore.EzConfig.Get<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode);
            bool poorEnabled = HealthModeHelper.IsBMSHealthMode(healthMode)
                               && GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.BmsPoorHitResultEnable);

            var modeUnit = new HitModeDataUnit(
                HitMode: hitMode,
                JudgePrecedence: judgePrecedence,
                PoorEnabled: poorEnabled,
                PillModeEnabled: healthMode.ToString().Contains("O2Jam"));

            // 与 DrawableHitObject.UpdateResult() 对齐：mania 使用 OffsetPlusMania 直接修正 timeOffset。
            double offsetPlusMania = GlobalConfigStore.EzConfig.Get<double>(Ez2Setting.OffsetPlusMania);

            var hitWindowHelper = new HitModeHelper(hitMode)
            {
                OverallDifficulty = playableBeatmap.Difficulty.OverallDifficulty,
                BPM = getBpmAtTime(playableBeatmap, 0),
            };

            var frames = replay.Frames.Cast<ManiaReplayFrame>().OrderBy(f => f.Time).ToList();

            // Build per-column input transitions and unified input event timeline.
            var inputEvents = new List<InputEvent>(frames.Count * 2);

            HashSet<ManiaAction> lastActions = new HashSet<ManiaAction>();

            foreach (var frame in frames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var current = new HashSet<ManiaAction>(frame.Actions);

                foreach (var action in current)
                {
                    if (lastActions.Contains(action))
                        continue;

                    int column = (int)action;
                    if (column >= 0)
                        inputEvents.Add(new InputEvent(frame.Time, column, true));
                }

                foreach (var action in lastActions)
                {
                    if (current.Contains(action))
                        continue;

                    int column = (int)action;
                    if (column >= 0)
                        inputEvents.Add(new InputEvent(frame.Time, column, false));
                }

                lastActions = current;
            }

            // If keys are still held at the end of replay, treat them as released at the last frame time.
            if (lastActions.Count > 0)
            {
                double endTime = frames[^1].Time;

                foreach (var action in lastActions)
                {
                    int column = (int)action;
                    if (column >= 0)
                        inputEvents.Add(new InputEvent(endTime, column, false));
                }
            }

            // Same-frame processing in gameplay checks key-down before key-up.
            inputEvents.Sort((a, b) =>
            {
                int timeComparison = a.Time.CompareTo(b.Time);
                if (timeComparison != 0)
                    return timeComparison;

                if (a.IsPress == b.IsPress)
                    return 0;

                return a.IsPress ? -1 : 1;
            });

            // Map tail -> head to support capping (combo-break conditions).
            var headByTail = new Dictionary<TailNote, HeadNote>();

            foreach (var hitObject in playableBeatmap.HitObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (hitObject is HoldNote hold)
                    headByTail[hold.Tail] = hold.Head;
            }

            var targets = new List<HitObject>();

            foreach (var hitObject in playableBeatmap.HitObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                collectJudgementTargets(hitObject, targets, cancellationToken);
            }

            // Ensure deterministic ordering.
            targets.Sort((a, b) =>
            {
                int timeComparison = a.StartTime.CompareTo(b.StartTime);
                if (timeComparison != 0)
                    return timeComparison;

                int colA = (a as IHasColumn)?.Column ?? 0;
                int colB = (b as IHasColumn)?.Column ?? 0;
                return colA.CompareTo(colB);
            });

            double gameplayRate = ModUtils.CalculateRateWithMods(score.ScoreInfo.Mods);

            var targetStates = targets.Select(t => new TargetState(t)).ToList();
            var statesByObject = targetStates.ToDictionary(s => s.Target, s => s);

            // Build lookup by column and input type.
            var statesByColumnForPress = new Dictionary<int, List<TargetState>>();
            var statesByColumnForRelease = new Dictionary<int, List<TargetState>>();

            foreach (var state in targetStates)
            {
                if (state.Target is not IHasColumn hasColumn)
                    continue;

                int column = hasColumn.Column;
                var dict = state.IsTail ? statesByColumnForRelease : statesByColumnForPress;

                if (!dict.TryGetValue(column, out var list))
                {
                    list = new List<TargetState>();
                    dict[column] = list;
                }

                list.Add(state);
            }

            var hitEvents = new List<HitEvent>(targets.Count);
            HitObject? lastHitObject = null;

            // Track head hit results for later tail capping.
            var headWasHit = new Dictionary<HeadNote, bool>();
            int o2PillCount = 0;
            int o2CoolCombo = 0;

            foreach (var inputEvent in inputEvents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var perColumnDict = inputEvent.IsPress ? statesByColumnForPress : statesByColumnForRelease;
                if (!perColumnDict.TryGetValue(inputEvent.Column, out var laneStates))
                    continue;

                var candidates = collectCandidatesForInput(laneStates, playableBeatmap, inputEvent.Time, hitWindowHelper, hitMode).ToList();
                hitWindowHelper.BPM = getBpmAtTime(playableBeatmap, inputEvent.Time);

                if (inputEvent.IsPress && isBmsLikeMode(modeUnit.HitMode))
                {
                    var postBadCandidate = laneStates
                                           .Where(s => s.Judged && s.CanRouteToKPoor && isWithinMissWindow(hitWindowHelper, s.Target.StartTime, inputEvent.Time))
                                           .OrderBy(s => distanceToNonBadWindow(s.Target.StartTime, inputEvent.Time, hitWindowHelper))
                                           .ThenBy(s => s.Target.StartTime)
                                           .FirstOrDefault();

                    if (postBadCandidate != null)
                    {
                        double postBadDistance = distanceToNonBadWindow(postBadCandidate.Target.StartTime, inputEvent.Time, hitWindowHelper);

                        double unjudgedDistance = candidates
                                                  .Where(s => !s.Judged)
                                                  .Select(s => distanceToNonBadWindow(s.Target.StartTime, inputEvent.Time, hitWindowHelper))
                                                  .DefaultIfEmpty(double.PositiveInfinity)
                                                  .Min();

                        if (postBadDistance <= unjudgedDistance)
                        {
                            if (postBadCandidate.HasLateKPoor)
                                continue;

                            postBadCandidate.HasLateKPoor = true;
                            postBadCandidate.CanRouteToKPoor = false;
                            hitEvents.Add(new HitEvent(inputEvent.Time - postBadCandidate.Target.StartTime + offsetPlusMania, gameplayRate, BMSJudgeMapping.KPoor,
                                postBadCandidate.Target, lastHitObject, null));
                            lastHitObject = postBadCandidate.Target;
                            continue;
                        }
                    }
                }

                if (candidates.Count == 0)
                    continue;

                var selected = selectCandidateByPrecedence(candidates, playableBeatmap, inputEvent.Time, modeUnit, hitWindowHelper);
                if (selected == null || selected.Judged)
                    continue;

                var target = selected.Target;
                bool isTail = selected.IsTail;
                bool useTailReleaseLenience = isTail && usesTailReleaseLenience(modeUnit.HitMode);
                double lenienceFactor = useTailReleaseLenience ? TailNote.RELEASE_WINDOW_LENIENCE : 1;

                double rawOffset = inputEvent.Time - target.StartTime + offsetPlusMania;
                double missWindow = getDirectionalWindow(hitWindowHelper, HitResult.Miss, rawOffset < 0) * lenienceFactor;
                bool holdBreak = isTail && rawOffset < -missWindow;
                double timeOffsetForJudgement = useTailReleaseLenience ? rawOffset / TailNote.RELEASE_WINDOW_LENIENCE : rawOffset;

                bool headHit = false;

                if (target is TailNote tail && headByTail.TryGetValue(tail, out var headNote))
                    headHit = headWasHit.TryGetValue(headNote, out bool wasHit) && wasHit;

                HitResult result = evaluateResult(target, hitWindowHelper, modeUnit, timeOffsetForJudgement, rawOffset, holdBreak, headHit,
                    playableBeatmap, inputEvent.Time, ref o2PillCount, ref o2CoolCombo);

                // Lazer 模式下，None 表示此次输入不应判定当前对象，保留对象待后续输入/超时 Miss。
                if (modeUnit.HitMode == EzEnumHitMode.Lazer && result == HitResult.None)
                    continue;

                // BMS 的 KPoor 在 Drawable 中是“产生判定但不终结对象”的结果：
                // - 晚按区间每个对象最多 1 次；
                // - 早按（空按模拟）可重复触发。
                if (isBmsLikeMode(modeUnit.HitMode) && result == BMSJudgeMapping.KPoor)
                {
                    double badLate = hitWindowHelper.WindowFor(BMSJudgeMapping.Bad, false);
                    bool isLatePress = timeOffsetForJudgement > badLate;

                    if (isLatePress && selected.HasLateKPoor)
                        continue;

                    if (isLatePress)
                        selected.HasLateKPoor = true;

                    hitEvents.Add(new HitEvent(timeOffsetForJudgement, gameplayRate, result, target, lastHitObject, null));
                    lastHitObject = target;
                    continue;
                }

                selected.Judged = true;
                selected.TimeOffset = timeOffsetForJudgement;
                selected.Result = result;

                selected.CanRouteToKPoor = isBmsLikeMode(modeUnit.HitMode) && result == BMSJudgeMapping.Bad && timeOffsetForJudgement < 0;

                if (target is HeadNote head)
                    headWasHit[head] = result.IsHit();
            }

            // Emit in deterministic object order.
            foreach (var target in targets)
            {
                var state = statesByObject[target];

                if (!state.Judged)
                {
                    state.Result = HitResult.Miss;
                    state.TimeOffset = estimateUnjudgedMissOffset(target, playableBeatmap, hitWindowHelper, modeUnit.HitMode);

                    if (target is HeadNote head)
                        headWasHit[head] = false;
                }

                hitEvents.Add(new HitEvent(state.TimeOffset, gameplayRate, state.Result, target, lastHitObject, null));
                lastHitObject = target;
            }

            return hitEvents;
        }

        // TODO: Ez2Ac没有实现
        private static HitResult evaluateResult(HitObject target, HitModeHelper hitModeHelper, HitModeDataUnit modeUnit,
                                                double timeOffsetForJudgement, double rawOffset, bool holdBreak, bool headHit,
                                                IBeatmap playableBeatmap, double eventTime, ref int o2PillCount, ref int o2CoolCombo)
        {
            bool isTail = target is TailNote;

            switch (modeUnit.HitMode)
            {
                case EzEnumHitMode.Lazer:
                    return evaluateLazerResult(target, timeOffsetForJudgement, isTail, holdBreak, headHit);

                case EzEnumHitMode.O2Jam:
                    return evaluateO2JamResult(hitModeHelper, timeOffsetForJudgement, rawOffset, isTail, holdBreak, headHit, playableBeatmap, eventTime, modeUnit,
                        ref o2PillCount, ref o2CoolCombo);

                case EzEnumHitMode.Malody_E:
                case EzEnumHitMode.Malody_B:
                    if (isTail)
                        return evaluateMalodyTailResult(hitModeHelper, rawOffset, headHit);

                    return evaluateCommonResult(hitModeHelper, timeOffsetForJudgement, isTail, holdBreak, headHit);

                case EzEnumHitMode.IIDX_HD:
                case EzEnumHitMode.LR2_HD:
                case EzEnumHitMode.Raja_NM:
                    return evaluateBmsLikeResult(hitModeHelper, modeUnit, timeOffsetForJudgement, isTail, holdBreak);

                default:
                    return evaluateCommonResult(hitModeHelper, timeOffsetForJudgement, isTail, holdBreak, headHit);
            }
        }

        private static HitResult evaluateLazerResult(HitObject target, double timeOffsetForJudgement, bool isTail, bool holdBreak, bool headHit)
        {
            HitResult result = target.HitWindows?.ResultFor(timeOffsetForJudgement) ?? HitResult.None;

            if (isTail && result > HitResult.Meh && (!headHit || holdBreak))
                result = HitResult.Meh;

            return result;
        }

        private static HitResult evaluateBmsLikeResult(HitModeHelper hitModeHelper, HitModeDataUnit modeUnit, double timeOffsetForJudgement, bool isTail, bool holdBreak)
        {
            double badLate = hitModeHelper.WindowFor(BMSJudgeMapping.Bad, false);

            HitResult result = hitModeHelper.ResultFor(timeOffsetForJudgement);

            if (result != HitResult.None)
                return result;

            if (BMSJudgeMapping.IsLateOutsideBad(timeOffsetForJudgement, badLate))
                return BMSJudgeMapping.Poor;

            if (isTail && holdBreak)
                return BMSJudgeMapping.Poor;

            if (!modeUnit.PoorEnabled)
                return HitResult.None;

            double badEarly = hitModeHelper.WindowFor(BMSJudgeMapping.Bad, true);
            double kPoorEarly = hitModeHelper.WindowFor(BMSJudgeMapping.KPoor, true);

            if (kPoorEarly > badEarly && timeOffsetForJudgement < -badEarly && timeOffsetForJudgement >= -kPoorEarly)
                return BMSJudgeMapping.KPoor;

            return HitResult.None;
        }

        private static HitResult evaluateO2JamResult(HitModeHelper hitModeHelper, double timeOffsetForJudgement, double rawOffset, bool isTail, bool holdBreak,
                                                     bool headHit, IBeatmap playableBeatmap, double eventTime, HitModeDataUnit modeUnit,
                                                     ref int o2PillCount, ref int o2CoolCombo)
        {
            HitResult result = hitModeHelper.ResultFor(timeOffsetForJudgement);

            if (modeUnit.PillModeEnabled)
                applyO2PillLogic(Math.Abs(rawOffset), getBpmAtTime(playableBeatmap, eventTime), ref o2PillCount, ref o2CoolCombo, ref result);

            if (isTail && (holdBreak || !headHit))
                result = HitResult.Miss;

            return result;
        }

        private static HitResult evaluateMalodyTailResult(HitModeHelper hitModeHelper, double rawOffset, bool headHit)
        {
            if (!headHit)
                return HitResult.Miss;

            return rawOffset > 0 || Math.Abs(rawOffset) <= hitModeHelper.WindowFor(HitResult.Meh) * TailNote.RELEASE_WINDOW_LENIENCE
                ? HitResult.Perfect
                : HitResult.Miss;
        }

        private static HitResult evaluateCommonResult(HitModeHelper hitModeHelper, double timeOffsetForJudgement, bool isTail, bool holdBreak, bool headHit)
        {
            HitResult result = hitModeHelper.ResultFor(timeOffsetForJudgement);

            if (isTail && result > HitResult.Meh && (!headHit || holdBreak))
                result = HitResult.Meh;

            return result;
        }

        private static void applyO2PillLogic(double absOffset, double bpm, ref int pillCount, ref int coolCombo, ref HitResult result)
        {
            double coolRange = O2HitModeExtension.BASE_COOL / bpm;
            double goodRange = O2HitModeExtension.BASE_GOOD / bpm;
            double badRange = O2HitModeExtension.BASE_BAD / bpm;

            if (absOffset <= coolRange)
            {
                coolCombo++;

                if (coolCombo >= 15)
                {
                    coolCombo = 0;
                    pillCount = Math.Clamp(pillCount + 1, 0, 5);
                }

                return;
            }

            if (absOffset <= goodRange)
            {
                coolCombo = 0;
                return;
            }

            if (absOffset > badRange)
                return;

            coolCombo = 0;

            if (pillCount <= 0)
                return;

            pillCount = Math.Clamp(pillCount - 1, 0, 5);
            result = HitResult.Perfect;
        }

        private static bool usesTailReleaseLenience(EzEnumHitMode hitMode)
            => hitMode == EzEnumHitMode.Lazer || hitMode == EzEnumHitMode.Classic;

        private static IEnumerable<TargetState> collectCandidatesForInput(IEnumerable<TargetState> laneStates, IBeatmap playableBeatmap, double eventTime, HitModeHelper hitWindowHelper, EzEnumHitMode hitMode)
        {
            foreach (var state in laneStates)
            {
                if (state.Judged)
                    continue;

                if (state.Target.HitWindows == null || ReferenceEquals(state.Target.HitWindows, HitWindows.Empty))
                    continue;

                bool useTailReleaseLenience = state.IsTail && usesTailReleaseLenience(hitMode);
                double lenienceFactor = useTailReleaseLenience ? TailNote.RELEASE_WINDOW_LENIENCE : 1;

                hitWindowHelper.BPM = getBpmAtTime(playableBeatmap, eventTime);

                double missEarlyWindow = getDirectionalWindow(hitWindowHelper, HitResult.Miss, true) * lenienceFactor;
                double missLateWindow = getDirectionalWindow(hitWindowHelper, HitResult.Miss, false) * lenienceFactor;
                double minTime = state.Target.StartTime - missEarlyWindow;
                double maxTime = state.Target.StartTime + missLateWindow;

                if (eventTime >= minTime && eventTime <= maxTime)
                    yield return state;
            }
        }

        private static TargetState? selectCandidateByPrecedence(List<TargetState> candidates, IBeatmap playableBeatmap, double inputTime,
                                                                HitModeDataUnit modeUnit, HitModeHelper hitWindowHelper)
        {
            if (candidates.Count == 0)
                return null;

            candidates.Sort((a, b) => a.Target.StartTime.CompareTo(b.Target.StartTime));

            if (HitModeHelper.IsBMSHitMode(modeUnit.HitMode))
            {
                return modeUnit.JudgePrecedence switch
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

            TargetState selected = candidates[0];

            for (int i = 1; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                bool shouldReplace = modeUnit.JudgePrecedence switch
                {
                    EzEnumJudgePrecedence.Combo => compareCombo(selected, candidate, playableBeatmap, inputTime, hitWindowHelper),
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

        /// <summary>
        /// Combo 优先级：当前选中的 note 已偏早越过连击最低可保持窗口早界，且候选仍在晚界内时，把路由让给候选。
        /// </summary>
        private static bool compareCombo(TargetState t1, TargetState t2, IBeatmap playableBeatmap, double inputTime, HitModeHelper hitWindowHelper)
        {
            hitWindowHelper.BPM = getBpmAtTime(playableBeatmap, inputTime);
            double comboEarly = getDirectionalWindow(hitWindowHelper, HitResult.Good, true);
            double comboLate = getDirectionalWindow(hitWindowHelper, HitResult.Good, false);

            return OrderedHitPolicyHelper.CompareComboByPrecedence(
                t1.Target.StartTime,
                t2.Target.StartTime,
                inputTime,
                comboEarly,
                comboLate);
        }

        private static double getDirectionalWindow(HitModeHelper hitWindowHelper, HitResult result, bool isEarly)
        {
            return hitWindowHelper.WindowFor(result, isEarly);
        }

        private static bool isWithinMissWindow(HitModeHelper hitWindowHelper, double noteTime, double inputTime)
        {
            double early = getDirectionalWindow(hitWindowHelper, HitResult.Miss, true);
            double late = getDirectionalWindow(hitWindowHelper, HitResult.Miss, false);
            return inputTime >= noteTime - early && inputTime <= noteTime + late;
        }

        private static double distanceToNonBadWindow(double noteTime, double inputTime, HitModeHelper hitWindowHelper)
        {
            double early = getDirectionalWindow(hitWindowHelper, HitResult.Good, true);
            double late = getDirectionalWindow(hitWindowHelper, HitResult.Good, false);
            double start = noteTime - early;
            double end = noteTime + late;

            if (inputTime < start)
                return start - inputTime;

            if (inputTime > end)
                return inputTime - end;

            return 0;
        }

        private static double estimateUnjudgedMissOffset(HitObject target, IBeatmap playableBeatmap, HitModeHelper hitWindowHelper, EzEnumHitMode hitMode)
        {
            bool useTailReleaseLenience = target is TailNote && usesTailReleaseLenience(hitMode);
            double lenienceFactor = useTailReleaseLenience ? TailNote.RELEASE_WINDOW_LENIENCE : 1;

            hitWindowHelper.BPM = getBpmAtTime(playableBeatmap, target.StartTime);
            double missLate = getDirectionalWindow(hitWindowHelper, HitResult.Miss, false) * lenienceFactor;

            // 保持在 miss 判定窗口内，避免后续按 offset 重算时被降级为更高判定或 None。
            return Math.Max(0, missLate - 0.01);
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

        private static bool isBmsLikeMode(EzEnumHitMode hitMode)
            => hitMode is EzEnumHitMode.IIDX_HD or EzEnumHitMode.LR2_HD or EzEnumHitMode.Raja_NM;

        private static void collectJudgementTargets(HitObject hitObject, List<HitObject> targets, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (hitObject.HitWindows != null && !ReferenceEquals(hitObject.HitWindows, HitWindows.Empty) && hitObject.Judgement.MaxResult != HitResult.IgnoreHit)
                targets.Add(hitObject);

            foreach (var nested in hitObject.NestedHitObjects)
                collectJudgementTargets(nested, targets, cancellationToken);
        }

        private sealed class TargetState
        {
            public readonly HitObject Target;
            public readonly bool IsTail;
            public bool Judged;
            public bool HasLateKPoor;
            public bool CanRouteToKPoor;
            public double TimeOffset;
            public HitResult Result;

            public TargetState(HitObject target)
            {
                Target = target;
                IsTail = target is TailNote;
            }
        }

        private readonly record struct HitModeDataUnit(
            EzEnumHitMode HitMode,
            EzEnumJudgePrecedence JudgePrecedence,
            bool PoorEnabled,
            bool PillModeEnabled);

        private readonly record struct InputEvent(double Time, int Column, bool IsPress);
    }
}
