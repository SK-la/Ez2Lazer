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
using osu.Game.Rulesets.Taiko.EzTaiko.ReplayJudge.Judgement;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Rulesets.Taiko.Objects.Drawables;
using osu.Game.Rulesets.Taiko.Replays;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Taiko.EzTaiko.ReplayJudge
{
    /// <summary>
    /// Taiko Session（TTL-001）：Hit + DrumRoll tick + Swell 交替按键；其余到期 Miss。
    /// Mapping 形态入口；非永久 Shadow 树。
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

                apply(hitObject, hitObject.Judgement.MinResult, hitObject.GetEndTime(), scoreProcessor, gameplayRate, inputOffset, recorder);
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
            var judged = new HashSet<HitObject>();
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

            var pressUsed = new bool[pressTimes.Count];

            judgeHits(beatmap, pressTimes, pressUsed, judged, scoreProcessor, gameplayRate, inputOffset, recorder, cancellationToken);
            judgeDrumRolls(beatmap, pressTimes, pressUsed, judged, scoreProcessor, gameplayRate, inputOffset, recorder, cancellationToken);
            judgeSwells(beatmap, pressTimes, pressUsed, judged, scoreProcessor, gameplayRate, inputOffset, recorder, cancellationToken);

            foreach (var hitObject in beatmap.HitObjects)
                missTree(hitObject, judged, scoreProcessor, gameplayRate, inputOffset, recorder);
        }

        private static void judgeHits(
            IBeatmap beatmap,
            List<(double Time, TaikoAction Action)> pressTimes,
            bool[] pressUsed,
            HashSet<HitObject> judged,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            double inputOffset,
            EzReplayTimelineRecorder? recorder,
            CancellationToken cancellationToken)
        {
            var hits = beatmap.HitObjects.OfType<Hit>().OrderBy(h => h.StartTime).ToList();
            int hitIndex = 0;

            for (int p = 0; p < pressTimes.Count; p++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (pressUsed[p])
                    continue;

                while (hitIndex < hits.Count && judged.Contains(hits[hitIndex]))
                    hitIndex++;

                if (hitIndex >= hits.Count)
                    break;

                var (time, action) = pressTimes[p];
                var hit = hits[hitIndex];
                double missWindow = hit.HitWindows?.WindowFor(HitResult.Miss) ?? 0;

                if (time < hit.StartTime - missWindow)
                    continue;

                if (time > hit.StartTime + missWindow)
                {
                    TaikoReplaySession.apply(hit, HitResult.Miss, hit.StartTime + missWindow, scoreProcessor, gameplayRate, inputOffset, recorder);
                    judged.Add(hit);
                    hitIndex++;
                    p--;
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
                pressUsed[p] = true;

                // Strong nested：父击中后 30ms 内同色第二键 → LargeBonus，否则留给 missTree→IgnoreMiss
                foreach (var strong in hit.NestedHitObjects.OfType<Hit.StrongNestedHit>())
                {
                    if (judged.Contains(strong))
                        continue;

                    if (result.IsHit())
                    {
                        int second = findStrongSecondPress(pressTimes, pressUsed, time, hit.Type, p);
                        if (second >= 0)
                        {
                            pressUsed[second] = true;
                            TaikoReplaySession.apply(strong, strong.Judgement.MaxResult, pressTimes[second].Time, scoreProcessor, gameplayRate, inputOffset, recorder);
                            judged.Add(strong);
                        }
                    }
                    else
                    {
                        TaikoReplaySession.apply(strong, strong.Judgement.MinResult, time, scoreProcessor, gameplayRate, inputOffset, recorder);
                        judged.Add(strong);
                    }
                }

                hitIndex++;
            }
        }

        private static int findStrongSecondPress(
            List<(double Time, TaikoAction Action)> pressTimes,
            bool[] pressUsed,
            double firstPressTime,
            HitType hitType,
            int firstPressIndex)
        {
            const double second_hit_window = DrawableHit.StrongNestedHit.SECOND_HIT_WINDOW;

            int best = -1;
            double bestDelta = double.MaxValue;

            for (int i = 0; i < pressTimes.Count; i++)
            {
                if (i == firstPressIndex || pressUsed[i])
                    continue;

                double delta = pressTimes[i].Time - firstPressTime;
                if (delta < 0 || delta > second_hit_window)
                    continue;

                bool valid = hitType == HitType.Centre
                    ? pressTimes[i].Action is TaikoAction.LeftCentre or TaikoAction.RightCentre
                    : pressTimes[i].Action is TaikoAction.LeftRim or TaikoAction.RightRim;

                if (!valid)
                    continue;

                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = i;
                }
            }

            return best;
        }

        private static void judgeDrumRolls(
            IBeatmap beatmap,
            List<(double Time, TaikoAction Action)> pressTimes,
            bool[] pressUsed,
            HashSet<HitObject> judged,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            double inputOffset,
            EzReplayTimelineRecorder? recorder,
            CancellationToken cancellationToken)
        {
            foreach (var roll in beatmap.HitObjects.OfType<DrumRoll>().OrderBy(r => r.StartTime))
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var tick in roll.NestedHitObjects.OfType<DrumRollTick>().OrderBy(t => t.StartTime))
                {
                    if (judged.Contains(tick))
                        continue;

                    int pressIndex = findTickPress(pressTimes, pressUsed, tick.StartTime, tick.HitWindow);
                    bool hit = pressIndex >= 0;

                    if (hit)
                        pressUsed[pressIndex] = true;

                    HitResult tickResult = TaikoRollingJudgement.TickResult(hit, tick.Judgement.MaxResult, tick.Judgement.MinResult);
                    double clock = hit ? pressTimes[pressIndex].Time : tick.StartTime + tick.HitWindow;
                    TaikoReplaySession.apply(tick, tickResult, clock, scoreProcessor, gameplayRate, inputOffset, recorder);
                    judged.Add(tick);

                    foreach (var nested in tick.NestedHitObjects)
                    {
                        if (judged.Contains(nested))
                            continue;

                        HitResult nestedResult = TaikoRollingJudgement.StrongNestedResult(hit, nested.Judgement.MaxResult, nested.Judgement.MinResult);
                        TaikoReplaySession.apply(nested, nestedResult, clock, scoreProcessor, gameplayRate, inputOffset, recorder);
                        judged.Add(nested);
                    }
                }

                if (!judged.Contains(roll))
                {
                    // DrawableDrumRoll：结束后恒 ApplyMaxResult（DisplayResult=false，计分靠 tick）。
                    TaikoReplaySession.apply(roll, roll.Judgement.MaxResult, roll.EndTime, scoreProcessor, gameplayRate, inputOffset, recorder);
                    judged.Add(roll);
                }

                foreach (var nested in roll.NestedHitObjects)
                {
                    if (nested is DrumRollTick || judged.Contains(nested))
                        continue;

                    HitResult nestedResult = TaikoRollingJudgement.StrongNestedResult(true, nested.Judgement.MaxResult, nested.Judgement.MinResult);
                    TaikoReplaySession.apply(nested, nestedResult, roll.EndTime, scoreProcessor, gameplayRate, inputOffset, recorder);
                    judged.Add(nested);
                }
            }
        }

        private static void judgeSwells(
            IBeatmap beatmap,
            List<(double Time, TaikoAction Action)> pressTimes,
            bool[] pressUsed,
            HashSet<HitObject> judged,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            double inputOffset,
            EzReplayTimelineRecorder? recorder,
            CancellationToken cancellationToken)
        {
            foreach (var swell in beatmap.HitObjects.OfType<Swell>().OrderBy(s => s.StartTime))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var ticks = swell.NestedHitObjects.OfType<SwellTick>().ToList();
                int hitCount = 0;
                bool? lastWasCentre = null;

                for (int p = 0; p < pressTimes.Count && hitCount < swell.RequiredHits; p++)
                {
                    if (pressUsed[p])
                        continue;

                    var (time, action) = pressTimes[p];
                    if (time < swell.StartTime || time > swell.EndTime)
                        continue;

                    bool isCentre = TaikoRollingJudgement.IsCentreAction(action);
                    if (!TaikoRollingJudgement.IsValidSwellPress(isCentre, lastWasCentre, mustAlternate: true))
                        continue;

                    lastWasCentre = isCentre;
                    pressUsed[p] = true;

                    if (hitCount < ticks.Count && !judged.Contains(ticks[hitCount]))
                    {
                        TaikoReplaySession.apply(ticks[hitCount], ticks[hitCount].Judgement.MaxResult, time, scoreProcessor, gameplayRate, inputOffset, recorder);
                        judged.Add(ticks[hitCount]);
                    }

                    hitCount++;
                }

                for (int i = hitCount; i < ticks.Count; i++)
                {
                    if (judged.Contains(ticks[i]))
                        continue;

                    TaikoReplaySession.apply(ticks[i], ticks[i].Judgement.MinResult, swell.EndTime, scoreProcessor, gameplayRate, inputOffset, recorder);
                    judged.Add(ticks[i]);
                }

                if (!judged.Contains(swell))
                {
                    HitResult body = TaikoRollingJudgement.SwellBodyResult(
                        hitCount, swell.RequiredHits, swell.Judgement.MaxResult, swell.Judgement.MinResult);
                    TaikoReplaySession.apply(swell, body, swell.EndTime, scoreProcessor, gameplayRate, inputOffset, recorder);
                    judged.Add(swell);
                }
            }
        }

        private static int findTickPress(
            List<(double Time, TaikoAction Action)> pressTimes,
            bool[] pressUsed,
            double tickStartTime,
            double hitWindow)
        {
            int best = -1;
            double bestDelta = double.MaxValue;

            for (int i = 0; i < pressTimes.Count; i++)
            {
                if (pressUsed[i])
                    continue;

                if (!TaikoRollingJudgement.IsTickHit(pressTimes[i].Time, tickStartTime, hitWindow))
                    continue;

                double delta = Math.Abs(pressTimes[i].Time - tickStartTime);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = i;
                }
            }

            return best;
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
                    TaikoReplaySession.apply(hitObject, hitObject.Judgement.MinResult, t, scoreProcessor, gameplayRate, inputOffset, recorder);
                    judged.Add(hitObject);
                }

            foreach (var nested in hitObject.NestedHitObjects)
                missTree(nested, judged, scoreProcessor, gameplayRate, inputOffset, recorder);
        }
    }
}
