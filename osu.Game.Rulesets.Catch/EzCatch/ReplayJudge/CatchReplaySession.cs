// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Catch.EzCatch.ReplayJudge.Judgement;
using osu.Game.Rulesets.Catch.Judgements;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.Replays;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Catch.EzCatch.ReplayJudge
{
    /// <summary>
    /// Catch Session（TSL-001）：Mapping 形态；接盘与 <see cref="CatchPlateJudgement"/> / Drawable 同调；按判定时钟序 ApplyResult。
    /// </summary>
    public static class CatchReplaySession
    {
        public static Score Run(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
        {
            var (sp, _) = run(score, beatmap, environment, false, cancellationToken);
            score.ScoreInfo.HitEvents = sp.HitEvents.ToList();
            sp.PopulateScore(score.ScoreInfo);
            return score;
        }

        public static (Score Score, EzScoreTimeline Timeline) RunWithTimeline(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
        {
            var (sp, timeline) = run(score, beatmap, environment, true, cancellationToken);
            score.ScoreInfo.HitEvents = sp.HitEvents.ToList();
            sp.PopulateScore(score.ScoreInfo);
            return (score, timeline ?? new EzScoreTimeline(Array.Empty<EzScoreTimelineSnapshot>()));
        }

        private static (ScoreProcessor scoreProcessor, EzScoreTimeline? timeline) run(
            Score score, IBeatmap beatmap, IGameplayEnvironment environment, bool recordTimeline, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(score.Replay);

            var ruleset = score.ScoreInfo.Ruleset.CreateInstance();
            var scoreProcessor = ruleset.CreateScoreProcessor();
            var resolvedMods = EzModCompatibility.StripUnknown(score.ScoreInfo.Mods);
            scoreProcessor.Mods.Value = resolvedMods;

            var beatmapProcessor = ruleset.CreateBeatmapProcessor(beatmap);
            beatmapProcessor?.PreProcess();

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, cancellationToken);

            beatmapProcessor?.PostProcess();
            scoreProcessor.ApplyBeatmap(beatmap);

            foreach (var mod in resolvedMods.OfType<IApplicableToScoreProcessor>())
                mod.ApplyToScoreProcessor(scoreProcessor);

            var recorder = recordTimeline ? new EzReplayTimelineRecorder() : null;
            double gameplayRate = ModUtils.CalculateRateWithMods(resolvedMods);
            recorder?.RecordInitial(scoreProcessor, gameplayRate);

            float halfWidth = Catcher.CalculateCatchWidth(beatmap.Difficulty) * 0.5f;
            double inputOffset = environment.OffsetPlusNonMania;

            var frames = score.Replay.Frames.OfType<CatchReplayFrame>().OrderBy(f => f.Time).ToList();

            // Session 无 live Scratch 轴：assist=false（与默认键盘局同窗）。
            double earlyMs = CatchScratchJudgmentWindow.EarlyWindow(assist: false);
            double lateMs = CatchScratchJudgmentWindow.LateWindow(assist: false);

            var events = new List<(double Time, HitObject HitObject, HitResult Result, int NestDepth)>();
            collectJudgements(beatmap.HitObjects, frames, halfWidth, earlyMs, lateMs, events, nestDepth: 0, cancellationToken);
            // 同时刻：先深层 nested（水果/滴），再父级 JuiceStream/BananaShower IgnoreMiss。
            events.Sort((a, b) =>
            {
                int cmp = a.Time.CompareTo(b.Time);
                if (cmp != 0)
                    return cmp;

                cmp = b.NestDepth.CompareTo(a.NestDepth);
                if (cmp != 0)
                    return cmp;

                return a.HitObject.StartTime.CompareTo(b.HitObject.StartTime);
            });

            foreach (var (time, hitObject, result, _) in events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                apply(hitObject, result, time, scoreProcessor, gameplayRate, inputOffset, recorder);
            }

            return (scoreProcessor, recorder?.Build());
        }

        private static void collectJudgements(
            IEnumerable<HitObject> hitObjects,
            IReadOnlyList<CatchReplayFrame> frames,
            float halfCatchWidth,
            double earlyMs,
            double lateMs,
            List<(double Time, HitObject HitObject, HitResult Result, int NestDepth)> events,
            int nestDepth,
            CancellationToken cancellationToken)
        {
            foreach (var hitObject in hitObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (hitObject is PalpableCatchHitObject fruit && fruit.Judgement.MaxResult != HitResult.IgnoreHit)
                {
                    bool caught = CatchPlateJudgement.IsCaughtInWindow(fruit, frames, halfCatchWidth, earlyMs, lateMs);
                    events.Add((fruit.StartTime, fruit, caught ? fruit.Judgement.MaxResult : fruit.Judgement.MinResult, nestDepth));
                }
                else if (hitObject is JuiceStream or BananaShower)
                {
                    // Drawable：CanCatch 对非 Palpable 恒 false → ApplyMinResult（IgnoreMiss），时刻为 EndTime。
                    events.Add((hitObject.GetEndTime(), hitObject, hitObject.Judgement.MinResult, nestDepth));
                }
                else if (hitObject.Judgement.MaxResult != HitResult.IgnoreHit)
                {
                    events.Add((hitObject.GetEndTime(), hitObject, hitObject.Judgement.MinResult, nestDepth));
                }

                if (hitObject.NestedHitObjects.Count > 0)
                    collectJudgements(hitObject.NestedHitObjects, frames, halfCatchWidth, earlyMs, lateMs, events, nestDepth + 1, cancellationToken);
            }
        }

        private static void apply(
            HitObject hitObject,
            HitResult result,
            double judgementClockTime,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            double inputOffset,
            EzReplayTimelineRecorder? recorder)
        {
            var judgementResult = new CatchJudgementResult(hitObject, hitObject.Judgement) { Type = result };
            double timeOffset = Math.Min(judgementClockTime - hitObject.GetEndTime() + inputOffset, hitObject.MaximumJudgementOffset);
            JudgementResultTimingHelper.ApplyTiming(judgementResult, timeOffset, gameplayRate);
            scoreProcessor.ApplyResult(judgementResult);
            recorder?.Record(scoreProcessor, judgementClockTime, gameplayRate);
        }
    }
}
