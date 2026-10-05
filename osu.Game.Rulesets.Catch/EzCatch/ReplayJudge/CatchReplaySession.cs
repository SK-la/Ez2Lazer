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

            var events = new List<(double Time, HitObject HitObject, HitResult Result)>();
            collectJudgements(beatmap.HitObjects, frames, halfWidth, earlyMs, lateMs, events, cancellationToken);
            events.Sort((a, b) =>
            {
                int cmp = a.Time.CompareTo(b.Time);
                return cmp != 0 ? cmp : a.HitObject.NestedHitObjects.Count.CompareTo(b.HitObject.NestedHitObjects.Count);
            });

            foreach (var (time, hitObject, result) in events)
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
            List<(double Time, HitObject HitObject, HitResult Result)> events,
            CancellationToken cancellationToken)
        {
            foreach (var hitObject in hitObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (hitObject is PalpableCatchHitObject fruit && fruit.Judgement.MaxResult != HitResult.IgnoreHit)
                {
                    bool caught = CatchPlateJudgement.IsCaughtInWindow(fruit, frames, halfCatchWidth, earlyMs, lateMs);
                    events.Add((fruit.StartTime, fruit, caught ? fruit.Judgement.MaxResult : fruit.Judgement.MinResult));
                }
                else if (hitObject is JuiceStream or BananaShower)
                {
                    // Drawable：CanCatch 对非 Palpable 恒 false → ApplyMinResult（IgnoreMiss），时刻为 EndTime。
                    events.Add((hitObject.GetEndTime(), hitObject, hitObject.Judgement.MinResult));
                }
                else if (hitObject.Judgement.MaxResult != HitResult.IgnoreHit)
                {
                    events.Add((hitObject.GetEndTime(), hitObject, hitObject.Judgement.MinResult));
                }

                if (hitObject.NestedHitObjects.Count > 0)
                    collectJudgements(hitObject.NestedHitObjects, frames, halfCatchWidth, earlyMs, lateMs, events, cancellationToken);
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
