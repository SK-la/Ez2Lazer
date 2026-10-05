// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Rulesets.Taiko.Replays;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Taiko.EzTaiko.ReplayJudge
{
    /// <summary>
    /// Taiko Session（TTL-001 bootstrap）：Hit 按键判定 + 其余对象到期 Miss。
    /// Mapping 形态入口；DrumRoll/Swell 完整状态机仍 open（非永久 Shadow 树）。
    /// </summary>
    public static class TaikoReplaySession
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
            double inputOffset = environment.OffsetPlusNonMania;

            recorder?.RecordInitial(scoreProcessor, gameplayRate);

            if (score.Replay.Frames.Count == 0)
            {
                forceMissRemaining(beatmap.HitObjects, scoreProcessor, gameplayRate, inputOffset, recorder, judged: new HashSet<HitObject>());
                return (scoreProcessor, recorder?.Build());
            }

            TaikoReplaySessionEngine.Run(score, beatmap, scoreProcessor, gameplayRate, inputOffset, recorder, cancellationToken);
            return (scoreProcessor, recorder?.Build());
        }

        private static void forceMissRemaining(
            IEnumerable<HitObject> objects,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            double inputOffset,
            EzReplayTimelineRecorder? recorder,
            HashSet<HitObject> judged)
        {
            foreach (var hitObject in objects)
            {
                if (judged.Contains(hitObject))
                    continue;

                if (hitObject.Judgement.MaxResult == HitResult.IgnoreHit)
                    continue;

                apply(hitObject, HitResult.Miss, hitObject.GetEndTime(), scoreProcessor, gameplayRate, inputOffset, recorder);
                judged.Add(hitObject);

                forceMissRemaining(hitObject.NestedHitObjects, scoreProcessor, gameplayRate, inputOffset, recorder, judged);
            }
        }

        internal static void apply(
            HitObject hitObject,
            HitResult result,
            double judgementClockTime,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            double inputOffset,
            EzReplayTimelineRecorder? recorder)
        {
            var judgementResult = new JudgementResult(hitObject, hitObject.Judgement) { Type = result };
            double timeOffset = Math.Min(judgementClockTime - hitObject.GetEndTime() + inputOffset, hitObject.MaximumJudgementOffset);
            JudgementResultTimingHelper.ApplyTiming(judgementResult, timeOffset, gameplayRate);
            scoreProcessor.ApplyResult(judgementResult);
            recorder?.Record(scoreProcessor, judgementClockTime, gameplayRate);
        }
    }

    internal static class TaikoReplaySessionEngine
    {
        public static void Run(
            Score score,
            IBeatmap beatmap,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            double inputOffset,
            EzReplayTimelineRecorder? recorder,
            CancellationToken cancellationToken)
        {
            var frames = score.Replay.Frames.OfType<TaikoReplayFrame>().OrderBy(f => f.Time).ToList();
            var hits = beatmap.HitObjects.OfType<Hit>().OrderBy(h => h.StartTime).ToList();
            var judged = new HashSet<HitObject>();
            int hitIndex = 0;

            var pressTimes = new List<(double Time, TaikoAction Action)>();

            for (int i = 0; i < frames.Count; i++)
            {
                IReadOnlyList<TaikoAction> prev = i > 0 ? frames[i - 1].Actions : Array.Empty<TaikoAction>();
                foreach (var action in frames[i].Actions)
                {
                    if (!prev.Contains(action))
                        pressTimes.Add((frames[i].Time, action));
                }
            }

            foreach (var (time, action) in pressTimes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                while (hitIndex < hits.Count && judged.Contains(hits[hitIndex]))
                    hitIndex++;

                if (hitIndex >= hits.Count)
                    break;

                var hit = hits[hitIndex];
                double missWindow = hit.HitWindows?.WindowFor(HitResult.Miss) ?? 0;

                if (time < hit.StartTime - missWindow)
                    continue;

                if (time > hit.StartTime + missWindow)
                {
                    TaikoReplaySession.apply(hit, HitResult.Miss, hit.StartTime + missWindow, scoreProcessor, gameplayRate, inputOffset, recorder);
                    judged.Add(hit);
                    hitIndex++;
                    continue;
                }

                bool valid = hit.Type == HitType.Centre
                    ? action is TaikoAction.LeftCentre or TaikoAction.RightCentre
                    : action is TaikoAction.LeftRim or TaikoAction.RightRim;

                double offset = time - hit.StartTime + inputOffset;
                HitResult result = hit.HitWindows!.ResultFor(offset);

                if (result == HitResult.None)
                    continue;

                if (!valid)
                    result = hit.Judgement.MinResult;

                TaikoReplaySession.apply(hit, result, time, scoreProcessor, gameplayRate, inputOffset, recorder);
                judged.Add(hit);
                hitIndex++;
            }

            foreach (var hitObject in beatmap.HitObjects)
                missTree(hitObject, judged, scoreProcessor, gameplayRate, inputOffset, recorder);
        }

        private static void missTree(
            HitObject hitObject,
            HashSet<HitObject> judged,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            double inputOffset,
            EzReplayTimelineRecorder? recorder)
        {
            if (!judged.Contains(hitObject) && hitObject.Judgement.MaxResult != HitResult.IgnoreHit)
            {
                double t = hitObject.StartTime + (hitObject.HitWindows?.WindowFor(HitResult.Miss) ?? 0);
                TaikoReplaySession.apply(hitObject, HitResult.Miss, t, scoreProcessor, gameplayRate, inputOffset, recorder);
                judged.Add(hitObject);
            }

            foreach (var nested in hitObject.NestedHitObjects)
                missTree(nested, judged, scoreProcessor, gameplayRate, inputOffset, recorder);
        }
    }
}
