// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
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
    /// Catch Session（TSL-001 bootstrap）：按 replay 插值接盘位置判定可接水果；其余到期 Miss。
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

            float catchWidth = Catcher.CalculateCatchWidth(beatmap.Difficulty);
            float halfWidth = catchWidth * 0.5f;
            double inputOffset = environment.OffsetPlusNonMania;

            var frames = score.Replay.Frames.OfType<CatchReplayFrame>().OrderBy(f => f.Time).ToList();
            var judged = new HashSet<HitObject>();

            foreach (var hitObject in beatmap.HitObjects)
                judgeTree(hitObject, frames, halfWidth, inputOffset, scoreProcessor, gameplayRate, recorder, judged, cancellationToken);

            return (scoreProcessor, recorder?.Build());
        }

        private static void judgeTree(
            HitObject hitObject,
            IReadOnlyList<CatchReplayFrame> frames,
            float halfCatchWidth,
            double inputOffset,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            EzReplayTimelineRecorder? recorder,
            HashSet<HitObject> judged,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (hitObject is PalpableCatchHitObject fruit && !judged.Contains(fruit) && fruit.Judgement.MaxResult != HitResult.IgnoreHit)
            {
                // Drawable：timeOffset>=0 开始检、>0 才 Miss。近似下一帧 Update（~1ms）再给一次机会。
                bool caught = isCaughtAt(fruit, frames, halfCatchWidth, fruit.StartTime)
                              || isCaughtAt(fruit, frames, halfCatchWidth, fruit.StartTime + 1);
                apply(fruit, caught ? fruit.Judgement.MaxResult : fruit.Judgement.MinResult, fruit.StartTime, scoreProcessor, gameplayRate, inputOffset, recorder);
                judged.Add(fruit);
            }
            else if (!judged.Contains(hitObject) && hitObject is JuiceStream or BananaShower)
            {
                // Drawable：CanCatch 对非 Palpable 恒 false → ApplyMinResult（IgnoreMiss）。
                apply(hitObject, hitObject.Judgement.MinResult, hitObject.GetEndTime(), scoreProcessor, gameplayRate, inputOffset, recorder);
                judged.Add(hitObject);
            }
            else if (!judged.Contains(hitObject) && hitObject.Judgement.MaxResult != HitResult.IgnoreHit)
            {
                apply(hitObject, hitObject.Judgement.MinResult, hitObject.GetEndTime(), scoreProcessor, gameplayRate, inputOffset, recorder);
                judged.Add(hitObject);
            }

            foreach (var nested in hitObject.NestedHitObjects)
                judgeTree(nested, frames, halfCatchWidth, inputOffset, scoreProcessor, gameplayRate, recorder, judged, cancellationToken);
        }

        private static bool isCaughtAt(
            PalpableCatchHitObject fruit,
            IReadOnlyList<CatchReplayFrame> frames,
            float halfCatchWidth,
            double time)
        {
            float catcherX = interpolateCatcherX(frames, time);
            return fruit.EffectiveX >= catcherX - halfCatchWidth && fruit.EffectiveX <= catcherX + halfCatchWidth;
        }

        private static float interpolateCatcherX(IReadOnlyList<CatchReplayFrame> frames, double time)
        {
            if (frames.Count == 0)
                return 0;

            if (time <= frames[0].Time)
                return frames[0].Position;

            for (int i = 1; i < frames.Count; i++)
            {
                if (time <= frames[i].Time)
                {
                    double span = frames[i].Time - frames[i - 1].Time;
                    if (span <= 0)
                        return frames[i].Position;

                    float t = (float)((time - frames[i - 1].Time) / span);
                    return frames[i - 1].Position + (frames[i].Position - frames[i - 1].Position) * t;
                }
            }

            return frames[^1].Position;
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
