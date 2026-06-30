// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
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
        /// 谱面级共享数据：targets、hold 映射。同一谱面的所有 ghost 只构建一次。
        /// </summary>
        private sealed class SharedBeatmapData
        {
            public IBeatmap? Beatmap;
            public EzEnumHitMode HitMode;
            public List<LaneTargetState> Targets = null!;
            public Dictionary<HeadNote, HoldNote> HoldByHead = null!;
            public Dictionary<TailNote, HeadNote> HeadByTail = null!;
        }

        /// <summary>
        /// 缓存 ApplyBeatmap 后的最大分值状态，避免同一谱面重复 SimulateAutoplay（遍历 6000+ HitObject）。
        /// </summary>
        private struct ScoreProcessorMaxState
        {
            public double MaximumBaseScore;
            public double MaximumComboPortion;
            public int MaximumAccuracyJudgementCount;
            public long MaximumTotalScore;
            public int MaximumCombo;
            public Dictionary<HitResult, int> MaximumResultCounts;
        }

        /// <summary>
        /// 每线程缓存：共享的谱面数据 + ApplyBeatmap 后的最大分值状态。
        /// 同一线程处理的所有 ghost 共享同一谱面，避免重复 buildTargets / SimulateAutoplay。
        /// </summary>
        [ThreadStatic]
        private static SharedBeatmapData? sharedBeatmapData;

        [ThreadStatic]
        private static ScoreProcessorMaxState? cachedMaxState;

        /// <summary>
        /// 缓存反射字段查找，避免每次 ghost 重复反射开销。
        /// </summary>
        private static FieldInfo[]? maxStateFields;

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

            var hitMode = environment.ManiaHitMode;
            var ruleset = score.ScoreInfo.Ruleset.CreateInstance();

            // 检查 / 填充 ThreadStatic 缓存：谱面共享数据 + ScoreProcessor 最大分值状态。
            // 同一线程的所有 ghost 共享同一谱面引用，避免重复 SimulateAutoplay（遍历 6000+ HitObject）
            // 和 buildTargets / alignHitWindows（遍历 + 排序）。
            ScoreProcessor scoreProcessor;

            if (cachedMaxState != null
                && sharedBeatmapData != null
                && ReferenceEquals(sharedBeatmapData.Beatmap, beatmap)
                && sharedBeatmapData.HitMode == hitMode)
            {
                // 缓存命中：创建新 processor + 反射写入 max state，跳过 SimulateAutoplay。
                scoreProcessor = createScoreProcessorFromCachedMax(ruleset, beatmap, score);
            }
            else
            {
                // 缓存未命中：完整初始化 processor + 构建共享谱面数据。
                scoreProcessor = ruleset.CreateScoreProcessor();
                scoreProcessor.Mods.Value = score.ScoreInfo.Mods;
                scoreProcessor.ApplyBeatmap(beatmap);

                if (scoreProcessor is ManiaScoreProcessor maniaScoreProcessor)
                    maniaScoreProcessor.TimelineHitModeOverride = hitMode;

                if (score.ScoreInfo.IsLegacyScore)
                    scoreProcessor.IsLegacyScore = true;

                foreach (var mod in score.ScoreInfo.Mods.OfType<IApplicableToScoreProcessor>())
                    mod.ApplyToScoreProcessor(scoreProcessor);

                var shared = new SharedBeatmapData
                {
                    Beatmap = beatmap,
                    HitMode = hitMode,
                };

                alignHitWindows(beatmap, environment);
                shared.Targets = buildTargets(beatmap);

                shared.HoldByHead = new Dictionary<HeadNote, HoldNote>();
                shared.HeadByTail = new Dictionary<TailNote, HeadNote>();

                foreach (var hitObject in beatmap.HitObjects)
                {
                    if (hitObject is HoldNote hold)
                    {
                        shared.HeadByTail[hold.Tail] = hold.Head;
                        shared.HoldByHead[hold.Head] = hold;
                    }
                }

                sharedBeatmapData = shared;

                // 缓存最大分值状态，后续 ghost 跳过 SimulateAutoplay。
                cachedMaxState = captureMaxState(scoreProcessor);
            }

            // 每 ghost 独立的 targets 副本：simulation 修改 Judged / Result / HoldBroken / BmsRoute。
            // LaneTargetState.Target（HitObject 引用）为只读，可安全共享。
            var sharedTargets = sharedBeatmapData.Targets;
            var ghostTargets = new List<LaneTargetState>(sharedTargets.Count);

            for (int i = 0; i < sharedTargets.Count; i++)
                ghostTargets.Add(new LaneTargetState(sharedTargets[i].Target));

            var recorder = recordTimeline ? new ManiaReplayTimelineRecorder() : null;
            recorder?.RecordInitial(scoreProcessor);

            if (score.Replay.Frames.Count == 0)
            {
                // Zero frames: still need to generate all-miss HitEvents
                // so that extended statistics can display.
                var emptyPressTimes = new Dictionary<int, List<double>>();
                applyForcedMisses(scoreProcessor, ghostTargets, emptyPressTimes, CancellationToken.None, recorder);
                scoreProcessor.PopulateScore(score.ScoreInfo);

                return (scoreProcessor, recordTimeline ? new EzScoreTimeline(Array.Empty<EzScoreTimelineSnapshot>()) : null);
            }

            var noteStrategy = ManiaJudgementRegistry.GetNoteStrategy(environment);
            var holdStrategy = ManiaJudgementRegistry.GetHoldStrategy(environment);

            // 列映射从每 ghost 的 targets 副本构建（轻量 O(N)）。
            buildColumnMaps(ghostTargets, out var pressColumns, out var releaseColumns);

            double gameplayRate = ModUtils.CalculateRateWithMods(score.ScoreInfo.Mods);

            var pressTimesByColumn = ManiaReplaySessionSimulator.BuildPressTimesByColumn(score.Replay);

            ManiaReplaySessionSimulator.Simulate(
                score,
                beatmap,
                environment,
                ghostTargets,
                pressColumns,
                releaseColumns,
                sharedBeatmapData.HoldByHead,
                sharedBeatmapData.HeadByTail,
                noteStrategy,
                holdStrategy,
                scoreProcessor,
                recorder,
                cancellationToken);

            applyForcedMisses(scoreProcessor, ghostTargets, pressTimesByColumn, cancellationToken, recorder);

            return (scoreProcessor, recorder?.Build());
        }

        #region ScoreProcessor max state reflection helpers

        private static ScoreProcessor createScoreProcessorFromCachedMax(Ruleset ruleset, IBeatmap beatmap, Score score)
        {
            var processor = ruleset.CreateScoreProcessor();
            processor.Beatmap.Value = beatmap;
            processor.Mods.Value = score.ScoreInfo.Mods;

            foreach (var mod in score.ScoreInfo.Mods.OfType<IApplicableToScoreProcessor>())
                mod.ApplyToScoreProcessor(processor);

            if (score.ScoreInfo.IsLegacyScore)
                processor.IsLegacyScore = true;

            applyCachedMaxState(processor);
            return processor;
        }

        private static ScoreProcessorMaxState captureMaxState(ScoreProcessor processor)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

            var maxResultCountsField = typeof(ScoreProcessor).GetField("MaximumResultCounts", flags);
            var srcDict = (Dictionary<HitResult, int>?)maxResultCountsField?.GetValue(processor) ?? new Dictionary<HitResult, int>();

            var maxTotalScoreField = typeof(ScoreProcessor).GetField("MaximumTotalScore", flags);
            long maxTotalScore = maxTotalScoreField != null ? (long)maxTotalScoreField.GetValue(processor)! : 0;

            var maxComboField = typeof(ScoreProcessor).GetField("MaximumCombo", flags);
            int maxCombo = maxComboField != null ? (int)maxComboField.GetValue(processor)! : 0;

            var maxBaseScoreField = typeof(ScoreProcessor).GetField("maximumBaseScore", flags);
            double maxBaseScore = maxBaseScoreField != null ? (double)maxBaseScoreField.GetValue(processor)! : 0;

            var maxComboPortionField = typeof(ScoreProcessor).GetField("maximumComboPortion", flags);
            double maxComboPortion = maxComboPortionField != null ? (double)maxComboPortionField.GetValue(processor)! : 0;

            var maxAccJudgementCountField = typeof(ScoreProcessor).GetField("maximumAccuracyJudgementCount", flags);
            int maxAccJudgementCount = maxAccJudgementCountField != null ? (int)maxAccJudgementCountField.GetValue(processor)! : 0;

            return new ScoreProcessorMaxState
            {
                MaximumBaseScore = maxBaseScore,
                MaximumComboPortion = maxComboPortion,
                MaximumAccuracyJudgementCount = maxAccJudgementCount,
                MaximumTotalScore = maxTotalScore,
                MaximumCombo = maxCombo,
                MaximumResultCounts = new Dictionary<HitResult, int>(srcDict),
            };
        }

        private static void applyCachedMaxState(ScoreProcessor processor)
        {
            if (!cachedMaxState.HasValue) return;

            var state = cachedMaxState.Value;
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

            if (maxStateFields == null)
            {
                var t = typeof(ScoreProcessor);
                maxStateFields = new[]
                {
                    t.GetField("maximumBaseScore", flags)!,
                    t.GetField("maximumComboPortion", flags)!,
                    t.GetField("maximumAccuracyJudgementCount", flags)!,
                    t.GetField("MaximumTotalScore", flags)!,
                    t.GetField("MaximumCombo", flags)!,
                    t.GetField("MaximumResultCounts", flags)!,
                    t.GetField("beatmapApplied", flags)!,
                };
            }

            maxStateFields[0].SetValue(processor, state.MaximumBaseScore);
            maxStateFields[1].SetValue(processor, state.MaximumComboPortion);
            maxStateFields[2].SetValue(processor, state.MaximumAccuracyJudgementCount);
            maxStateFields[3].SetValue(processor, state.MaximumTotalScore);
            maxStateFields[4].SetValue(processor, state.MaximumCombo);

            var dict = (Dictionary<HitResult, int>)maxStateFields[5].GetValue(processor)!;
            dict.Clear();
            foreach (var kvp in state.MaximumResultCounts)
                dict[kvp.Key] = kvp.Value;

            maxStateFields[6].SetValue(processor, true);
        }

        #endregion

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
            IReadOnlyList<LaneTargetState> targets,
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
