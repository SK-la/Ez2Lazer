// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
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
        public static IReadOnlyList<HitEvent> Run(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
            => run(score, beatmap, environment, recordTimeline: false, cancellationToken).HitEvents;

        /// <summary>
        /// 一遍 Session 判定同时采集分数时间线（与 <see cref="Run"/> 同源 SP，不经 HitEvents 二次重放）。
        /// </summary>
        public static EzScoreTimeline RunTimeline(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
        {
            var result = run(score, beatmap, environment, recordTimeline: true, cancellationToken);
            return result.Timeline ?? new EzScoreTimeline(Array.Empty<EzScoreTimelineSnapshot>());
        }

        internal static long RunFinalTotalScore(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
            => run(score, beatmap, environment, recordTimeline: false, cancellationToken).FinalTotalScore;

        private readonly struct SessionRunResult
        {
            public IReadOnlyList<HitEvent> HitEvents { get; }
            public EzScoreTimeline? Timeline { get; }
            public long FinalTotalScore { get; }

            public SessionRunResult(IReadOnlyList<HitEvent> hitEvents, EzScoreTimeline? timeline, long finalTotalScore)
            {
                HitEvents = hitEvents;
                Timeline = timeline;
                FinalTotalScore = finalTotalScore;
            }
        }

        private static SessionRunResult run(Score score, IBeatmap beatmap, IGameplayEnvironment environment, bool recordTimeline, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(score);
            ArgumentNullException.ThrowIfNull(score.Replay);
            ArgumentNullException.ThrowIfNull(beatmap);
            ArgumentNullException.ThrowIfNull(environment);

            if (score.Replay.Frames.Count == 0)
                return new SessionRunResult(Array.Empty<HitEvent>(), recordTimeline ? new EzScoreTimeline(Array.Empty<EzScoreTimelineSnapshot>()) : null, 0);

            var noteStrategy = ManiaJudgementRegistry.GetNoteStrategy(environment);

            var holdStrategy = ManiaJudgementRegistry.GetHoldStrategy(environment);

            var ruleset = new ManiaRuleset();
            var scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = score.ScoreInfo.Mods;
            scoreProcessor.ApplyBeatmap(beatmap);

            if (scoreProcessor is ManiaScoreProcessor maniaScoreProcessor)
                maniaScoreProcessor.TimelineHitModeOverride = environment.ManiaHitMode;

            if (score.ScoreInfo.IsLegacyScore)
                scoreProcessor.IsLegacyScore = true;

            foreach (var mod in score.ScoreInfo.Mods.OfType<IApplicableToScoreProcessor>())
                mod.ApplyToScoreProcessor(scoreProcessor);

            alignHitWindows(beatmap, environment);

            var targets = buildTargets(beatmap);
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
            var recorder = recordTimeline ? new ManiaReplayTimelineRecorder() : null;
            recorder?.RecordInitial(scoreProcessor);

            ManiaReplaySessionSimulator.Simulate(
                score,
                beatmap,
                environment,
                targets,
                pressColumns,
                releaseColumns,
                holdByHead,
                headByTail,
                noteStrategy,
                holdStrategy,
                scoreProcessor,
                recorder,
                cancellationToken);

            foreach (var state in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (state.Judged)
                    continue;

                state.Judged = true;
                state.Result = HitResult.Miss;
                ManiaReplaySessionSimulator.ApplyFinalResult(scoreProcessor, state.Target, HitResult.Miss, state.Target.GetEndTime(), gameplayRate, recorder);
            }

            return new SessionRunResult(scoreProcessor.HitEvents.ToList(), recorder?.Build(), scoreProcessor.TotalScore.Value);
        }

        private static void alignHitWindows(IBeatmap beatmap, IGameplayEnvironment environment)
        {
            foreach (var hitObject in beatmap.HitObjects)
                alignHitWindowsRecursive(hitObject, environment);
        }

        private static void alignHitWindowsRecursive(HitObject hitObject, IGameplayEnvironment environment)
        {
            if (hitObject.HitWindows is ManiaHitWindows maniaHitWindows)
                maniaHitWindows.SetHitMode(environment.ManiaHitMode);

            foreach (var nested in hitObject.NestedHitObjects)
                alignHitWindowsRecursive(nested, environment);
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
