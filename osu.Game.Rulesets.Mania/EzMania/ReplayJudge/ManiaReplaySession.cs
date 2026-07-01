// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;
using static osu.Game.Rulesets.Mania.EzMania.ReplayJudge.ManiaColumnSimulator;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// 无绘制 replay 判定 Session：replay 边沿 → ColumnSimulator → Strategy(Replica) → <see cref="ScoreProcessor.HitEvents"/>。
    /// </summary>
    public static class ManiaReplaySession
    {
        /// <summary>
        /// 一遍 Session 判定，经 <see cref="ScoreProcessor.PopulateScore"/> 写回 <paramref name="score"/> 并返回。
        /// </summary>
        public static Score Run(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
        {
            var (scoreProcessor, _) = run(score, beatmap, environment, recordTimeline: false, cancellationToken);

            // Transfer HitEvents from scoreProcessor to ScoreInfo.
            score.ScoreInfo.HitEvents = scoreProcessor.HitEvents.ToList();
            scoreProcessor.PopulateScore(score.ScoreInfo);

            return score;
        }

        /// <summary>
        /// 工具 API：返回 <see cref="Run"/> 产出的 HitEvents；当前生产路径不调用。
        /// </summary>
        public static IReadOnlyList<HitEvent> RunHitEvents(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
            => Run(score, beatmap, environment, cancellationToken).ScoreInfo.HitEvents;

        /// <summary>
        /// 一遍 Session 判定同时采集分数时间线（与 <see cref="Run"/> 同源 SP，不经 HitEvents 二次重放）。
        /// </summary>
        public static EzScoreTimeline RunTimeline(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
        {
            var (_, timeline) = run(score, beatmap, environment, recordTimeline: true, cancellationToken);
            return timeline ?? new EzScoreTimeline(Array.Empty<EzScoreTimelineSnapshot>());
        }

        private static (ScoreProcessor scoreProcessor, EzScoreTimeline? timeline) run(
            Score score,
            IBeatmap beatmap,
            IGameplayEnvironment environment,
            bool recordTimeline,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(score);
            ArgumentNullException.ThrowIfNull(score.Replay);
            ArgumentNullException.ThrowIfNull(beatmap);

            var ruleset = score.ScoreInfo.Ruleset.CreateInstance();
            var scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = score.ScoreInfo.Mods;
            scoreProcessor.ApplyBeatmap(beatmap);

            if (scoreProcessor is ManiaScoreProcessor maniaScoreProcessor)
                maniaScoreProcessor.TimelineHitModeOverride = environment.ManiaHitMode;

            if (score.ScoreInfo.IsLegacyScore)
                scoreProcessor.IsLegacyScore = true;

            foreach (var mod in score.ScoreInfo.Mods.OfType<IApplicableToScoreProcessor>())
                mod.ApplyToScoreProcessor(scoreProcessor);

            var recorder = recordTimeline ? new ManiaReplayTimelineRecorder() : null;
            recorder?.RecordInitial(scoreProcessor);

            var targets = buildTargets(beatmap);
            alignHitWindows(beatmap, environment);

            if (score.Replay.Frames.Count == 0)
            {
                // Zero frames: still need to generate all-miss HitEvents
                // so that extended statistics can display.
                var emptyPressTimes = new Dictionary<int, List<double>>();
                applyForcedMisses(scoreProcessor, targets, emptyPressTimes, environment.ManiaHitMode, CancellationToken.None, recorder);
                scoreProcessor.PopulateScore(score.ScoreInfo);

                return (scoreProcessor, recordTimeline ? new EzScoreTimeline(Array.Empty<EzScoreTimelineSnapshot>()) : null);
            }

            var noteStrategy = ManiaJudgementRegistry.GetNoteStrategy(environment);

            var holdStrategy = ManiaJudgementRegistry.GetHoldStrategy(environment);

            buildColumnMaps(targets, out var pressColumns, out var releaseColumns);

            var holdByHead = new Dictionary<HeadNote, HoldNote>();
            var headByTail = new Dictionary<TailNote, HeadNote>();

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is HoldNote hold)
                {
                    headByTail[hold.Tail] = hold.Head;
                    holdByHead[hold.Head] = hold;
                }
            }

            double gameplayRate = ModUtils.CalculateRateWithMods(score.ScoreInfo.Mods);
            var inputData = parseReplay(score.Replay);

            ManiaReplaySessionSimulator.Simulate(
                beatmap,
                environment,
                pressColumns,
                releaseColumns,
                holdByHead,
                headByTail,
                noteStrategy,
                holdStrategy,
                scoreProcessor,
                gameplayRate,
                inputData,
                recorder,
                cancellationToken);

            applyForcedMisses(scoreProcessor, targets, inputData.PressTimesByColumn, environment.ManiaHitMode, cancellationToken, recorder);

            return (scoreProcessor, recorder?.Build());
        }

        /// <summary>
        /// 单次解析 replay 帧序列，同时产出有序输入事件流与每列 press 时间索引。
        /// 替代原来 <c>ManiaReplayInputParser.Parse</c> + <c>BuildPressTimesByColumn</c> 的两次独立解析。
        /// </summary>
        private static ManiaReplayInputData parseReplay(Replay replay)
        {
            var frames = replay.Frames.OfType<ManiaReplayFrame>().OrderBy(f => f.Time).ToList();
            var inputEvents = new List<ManiaReplayInputEvent>(frames.Count * 2);
            var pressTimes = new Dictionary<int, List<double>>();

            var lastActions = new List<ManiaAction>();

            foreach (var frame in frames)
            {
                var current = frame.Actions.ToList();

                // 检测新按下（current 有但 lastActions 无）
                foreach (var action in current)
                {
                    if (lastActions.Contains(action))
                        continue;

                    int column = (int)action;

                    if (column >= 0)
                    {
                        inputEvents.Add(new ManiaReplayInputEvent(frame.Time, column, true));

                        if (!pressTimes.TryGetValue(column, out var list))
                        {
                            list = new List<double>();
                            pressTimes[column] = list;
                        }

                        list.Add(frame.Time);
                    }
                }

                // 检测释放（lastActions 有但 current 无）
                foreach (var action in lastActions)
                {
                    if (current.Contains(action))
                        continue;

                    int column = (int)action;
                    if (column >= 0)
                        inputEvents.Add(new ManiaReplayInputEvent(frame.Time, column, false));
                }

                lastActions = current;
            }

            // 末尾未释放的键补 Release 事件
            if (lastActions.Count > 0)
            {
                double endTime = frames[^1].Time;

                foreach (var action in lastActions)
                {
                    int column = (int)action;
                    if (column >= 0)
                        inputEvents.Add(new ManiaReplayInputEvent(endTime, column, false));
                }
            }

            // 排序：时间升序 → release 优先 → 列索引升序
            inputEvents.Sort((a, b) =>
            {
                int timeComparison = a.Time.CompareTo(b.Time);
                if (timeComparison != 0)
                    return timeComparison;

                if (a.IsPress != b.IsPress)
                    return a.IsPress ? 1 : -1;

                return a.Column.CompareTo(b.Column);
            });

            foreach (var list in pressTimes.Values)
                list.Sort();

            return new ManiaReplayInputData(inputEvents, pressTimes);
        }

        private static void alignHitWindows(IBeatmap beatmap, IGameplayEnvironment environment)
        {
            bool isO2Jam = environment.ManiaHitMode == EzEnumHitMode.O2Jam;

            foreach (var hitObject in beatmap.HitObjects)
                alignHitWindowsRecursive(hitObject, beatmap, environment, isO2Jam);
        }

        private static void alignHitWindowsRecursive(HitObject hitObject, IBeatmap beatmap, IGameplayEnvironment environment, bool isO2Jam)
        {
            if (hitObject.HitWindows is ManiaHitWindows maniaHitWindows)
            {
                maniaHitWindows.SetHitMode(environment.ManiaHitMode);

                // O2Jam 判定窗口依赖 BPM 缩放，必须按 hitObject 时间查谱面 BPM 写入。
                // 不设 BPM 则 ManiaHitWindows 默认 BPM=0 → safeBpm=75 → 窗口加倍，误判严重。
                if (isO2Jam)
                    maniaHitWindows.UpdateO2JamBpmFromTime(hitObject.StartTime);
            }

            foreach (var nested in hitObject.NestedHitObjects)
                alignHitWindowsRecursive(nested, beatmap, environment, isO2Jam);
        }

        private static void applyForcedMisses(
            ScoreProcessor scoreProcessor,
            List<LaneTargetState> targets,
            Dictionary<int, List<double>> pressTimesByColumn,
            EzEnumHitMode hitMode,
            CancellationToken cancellationToken,
            ManiaReplayTimelineRecorder? recorder)
        {
            double gameplayRate = 1.0; // ScoreProcessor internally uses ModUtils for rate; fixed for miss snapshots

            foreach (var state in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (state.Judged)
                    continue;

                state.Judged = true;
                state.Result = HitResult.Miss;

                double missEventTime = ManiaReplaySessionSimulator.ResolveMissEventTime(state.Target, pressTimesByColumn);
                ManiaReplaySessionSimulator.ApplyFinalResult(
                    scoreProcessor,
                    state.Target,
                    HitResult.Miss,
                    ManiaReplaySessionSimulator.ResolveMissStoredOffset(state.Target, pressTimesByColumn),
                    missEventTime,
                    gameplayRate,
                    hitMode,
                    recorder);
            }
        }

        private static List<LaneTargetState> buildTargets(IBeatmap beatmap)
        {
            var targets = new List<LaneTargetState>();

            foreach (var hitObject in beatmap.HitObjects)
                collectJudgementTargets(hitObject, targets);

            targets.Sort((a, b) =>
            {
                int timeComparison = a.Target.StartTime.CompareTo(b.Target.StartTime);
                if (timeComparison != 0)
                    return timeComparison;

                int colA = (a.Target as IHasColumn)?.Column ?? 0;
                int colB = (b.Target as IHasColumn)?.Column ?? 0;
                return colA.CompareTo(colB);
            });

            return targets;
        }

        private static void collectJudgementTargets(HitObject hitObject, List<LaneTargetState> targets)
        {
            if (hitObject.HitWindows != null
                && !ReferenceEquals(hitObject.HitWindows, HitWindows.Empty)
                && hitObject.Judgement.MaxResult != HitResult.IgnoreHit)
            {
                targets.Add(new LaneTargetState(hitObject));
            }

            foreach (var nested in hitObject.NestedHitObjects)
                collectJudgementTargets(nested, targets);
        }

        private static void buildColumnMaps(
            List<LaneTargetState> targets,
            out Dictionary<int, List<LaneTargetState>> pressColumns,
            out Dictionary<int, List<LaneTargetState>> releaseColumns)
        {
            pressColumns = new Dictionary<int, List<LaneTargetState>>();
            releaseColumns = new Dictionary<int, List<LaneTargetState>>();

            foreach (var state in targets)
            {
                if (state.Target is not IHasColumn hasColumn)
                    continue;

                var dict = state.IsTail ? releaseColumns : pressColumns;

                if (!dict.TryGetValue(hasColumn.Column, out var list))
                {
                    list = new List<LaneTargetState>();
                    dict[hasColumn.Column] = list;
                }

                list.Add(state);
            }

            foreach (var list in pressColumns.Values.Concat(releaseColumns.Values))
                list.Sort((a, b) => a.Target.StartTime.CompareTo(b.Target.StartTime));
        }
    }
}
