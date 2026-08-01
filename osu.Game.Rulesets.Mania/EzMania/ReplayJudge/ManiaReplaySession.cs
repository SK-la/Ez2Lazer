// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

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

        /// <summary>
        /// 一遍 Session 判定：同时写回 <see cref="Score"/> 并返回 <see cref="EzScoreTimeline"/>（Service 层多出口）。
        /// </summary>
        public static (Score Score, EzScoreTimeline Timeline) RunWithTimeline(Score score, IBeatmap beatmap, IGameplayEnvironment environment,
                                                                              CancellationToken cancellationToken = default)
        {
            var (scoreProcessor, timeline) = run(score, beatmap, environment, recordTimeline: true, cancellationToken);

            score.ScoreInfo.HitEvents = scoreProcessor.HitEvents.ToList();
            scoreProcessor.PopulateScore(score.ScoreInfo);

            return (score, timeline ?? new EzScoreTimeline(Array.Empty<EzScoreTimelineSnapshot>()));
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

            if (scoreProcessor is ManiaScoreProcessor maniaScoreProcessor)
                maniaScoreProcessor.HitModeOverride = environment.ManiaHitMode;

            scoreProcessor.ApplyBeatmap(beatmap);

            if (score.ScoreInfo.IsLegacyScore)
                scoreProcessor.IsLegacyScore = true;

            foreach (var mod in score.ScoreInfo.Mods.OfType<IApplicableToScoreProcessor>())
                mod.ApplyToScoreProcessor(scoreProcessor);

            var recorder = recordTimeline ? new ManiaReplayTimelineRecorder() : null;

            double gameplayRate = ModUtils.CalculateRateWithMods(score.ScoreInfo.Mods);
            recorder?.RecordInitial(scoreProcessor, gameplayRate);

            var simulationEnvironment = createSimulationEnvironment(environment);

            ManiaWindowBaker.Align(beatmap, simulationEnvironment);
            ManiaEnvironmentJudgements.ApplyToBeatmap(beatmap, simulationEnvironment.ManiaHitMode);
            var targets = buildTargets(beatmap);

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

            if (score.Replay.Frames.Count == 0)
            {
                // Zero frames: still need to generate all-miss HitEvents
                // so that extended statistics can display.
                var emptyPressTimes = new Dictionary<int, List<double>>();
                applyForcedMisses(scoreProcessor, targets, emptyPressTimes, holdByHead, headByTail, simulationEnvironment.ManiaHitMode, gameplayRate, CancellationToken.None, recorder);
                scoreProcessor.PopulateScore(score.ScoreInfo);

                return (scoreProcessor, recordTimeline ? new EzScoreTimeline(Array.Empty<EzScoreTimelineSnapshot>()) : null);
            }

            var noteStrategy = ManiaJudgementRegistry.GetNoteStrategy(simulationEnvironment);

            var holdStrategy = ManiaJudgementRegistry.GetHoldStrategy(simulationEnvironment);

            buildColumnMaps(targets, out var pressColumns, out var releaseColumns);

            var inputData = parseReplayFrames(score.Replay, environment);

            ManiaReplaySessionSimulator.Simulate(
                beatmap,
                simulationEnvironment,
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

            applyForcedMisses(scoreProcessor, targets, inputData.PressTimesByColumn, holdByHead, headByTail, simulationEnvironment.ManiaHitMode, gameplayRate, cancellationToken, recorder);

            return (scoreProcessor, recorder?.Build());
        }

        private static IGameplayEnvironment createSimulationEnvironment(IGameplayEnvironment environment)
        {
            if (!environment.ApplyInputOffsetViaReplayFrameShift || environment.OffsetPlusMania == 0)
                return environment;

            return new GameplayEnvironment
            {
                ManiaHitMode = environment.ManiaHitMode,
                ManiaHealthMode = environment.ManiaHealthMode,
                JudgePrecedence = environment.JudgePrecedence,
                OffsetPlusMania = 0,
                BmsPoorHitResultEnable = environment.BmsPoorHitResultEnable,
            };
        }

        /// <summary>
        /// ReplayFrame → 边沿（BatchAllEvents）。实现见 <see cref="ManiaReplayFrameEdgeParser"/>。
        /// </summary>
        private static ManiaReplayInputData parseReplayFrames(Replay replay, IGameplayEnvironment environment)
        {
            double frameTimeOffset = environment.ApplyInputOffsetViaReplayFrameShift ? environment.OffsetPlusMania : 0;
            return ManiaReplayFrameEdgeParser.ParseAll(replay, frameTimeOffset);
        }

        private static void applyForcedMisses(
            ScoreProcessor scoreProcessor,
            List<LaneTargetState> targets,
            Dictionary<int, List<double>> pressTimesByColumn,
            Dictionary<HeadNote, HoldNote> holdByHead,
            Dictionary<TailNote, HeadNote> headByTail,
            EzEnumHitMode hitMode,
            double gameplayRate,
            CancellationToken cancellationToken,
            ManiaReplayTimelineRecorder? recorder)
        {
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

                // For forced-missed tail notes, also apply HoldNote parent and Body
                // auxiliary results so statistics match live play.
                if (state.Target is TailNote tailNote && headByTail.TryGetValue(tailNote, out var linkedHead)
                                                      && holdByHead.TryGetValue(linkedHead, out var hold))
                {
                    double storedOffset = ManiaReplaySessionSimulator.ResolveMissStoredOffset(state.Target, pressTimesByColumn);

                    if (hold.Body != null)
                        ManiaReplaySessionSimulator.ApplyAuxiliaryResult(scoreProcessor, hold.Body, HitResult.ComboBreak, storedOffset, missEventTime, gameplayRate, recorder);

                    ManiaReplaySessionSimulator.ApplyAuxiliaryResult(scoreProcessor, hold, HitResult.IgnoreMiss, storedOffset, missEventTime, gameplayRate, recorder);
                }
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
