// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mania.LAsEzMania.Analysis
{
    /// <summary>
    /// Generates <see cref="HitEvent"/>s for mania scores by re-evaluating a score's replay input against a provided playable beatmap.
    /// This is intended for results/statistics usage where <see cref="ScoreInfo.HitEvents"/> are not persisted.
    /// </summary>
    public static class ManiaScoreHitEventGenerator
    {
        /// <summary>
        /// Attempt to generate <see cref="HitEvent"/>s for a mania <paramref name="score"/>.
        /// Returns <see langword="null"/> if replay frames are missing or not in a supported format.
        /// </summary>
        public static List<HitEvent>? Generate(Score score, IBeatmap playableBeatmap, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (score.ScoreInfo.Ruleset.OnlineID != 3)
                return null;

            Replay replay = score.Replay;

            // Legacy decoding should have produced mania frames.
            if (replay?.Frames == null || replay.Frames.Count == 0)
                return null;

            if (replay.Frames.Any(f => f is not ManiaReplayFrame))
                return null;

            var frames = replay.Frames.Cast<ManiaReplayFrame>().OrderBy(f => f.Time).ToList();

            // Build per-column input transitions.
            var pressTimesByColumn = new List<double>[32];
            var releaseTimesByColumn = new List<double>[32];

            for (int i = 0; i < pressTimesByColumn.Length; i++)
            {
                pressTimesByColumn[i] = new List<double>();
                releaseTimesByColumn[i] = new List<double>();
            }

            HashSet<ManiaAction> last = new HashSet<ManiaAction>();

            foreach (var frame in frames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var current = new HashSet<ManiaAction>(frame.Actions);

                foreach (var action in current)
                {
                    if (last.Contains(action))
                        continue;

                    int column = (int)action;
                    if (column >= 0 && column < pressTimesByColumn.Length)
                        pressTimesByColumn[column].Add(frame.Time);
                }

                foreach (var action in last)
                {
                    if (current.Contains(action))
                        continue;

                    int column = (int)action;
                    if (column >= 0 && column < releaseTimesByColumn.Length)
                        releaseTimesByColumn[column].Add(frame.Time);
                }

                last = current;
            }

            // If keys are still held at the end of replay, treat them as released at the last frame time.
            if (last.Count > 0)
            {
                double endTime = frames[^1].Time;

                foreach (var action in last)
                {
                    int column = (int)action;
                    if (column >= 0 && column < releaseTimesByColumn.Length)
                        releaseTimesByColumn[column].Add(endTime);
                }
            }

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

            var hitEvents = new List<HitEvent>(targets.Count);
            HitObject? lastHitObject = null;

            // Track head hit results for later tail capping.
            var headWasHit = new Dictionary<HeadNote, bool>();

            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (target.HitWindows == null || ReferenceEquals(target.HitWindows, HitWindows.Empty))
                    continue;

                if (target is not IHasColumn hasColumn)
                    continue;

                int column = hasColumn.Column;
                if (column < 0 || column >= pressTimesByColumn.Length)
                    continue;

                bool isTail = target is TailNote;
                double lenienceFactor = isTail ? TailNote.RELEASE_WINDOW_LENIENCE : 1;

                // We treat judgement windows as symmetrical around StartTime.
                double missWindow = target.HitWindows.WindowFor(HitResult.Miss) * lenienceFactor;

                List<double> times = isTail ? releaseTimesByColumn[column] : pressTimesByColumn[column];

                int idx = times.FindIndex(t => t >= target.StartTime - missWindow && t <= target.StartTime + missWindow);

                double timeOffsetForJudgement;
                HitResult result;

                bool holdBreak = false;

                if (idx >= 0)
                {
                    double eventTime = times[idx];
                    times.RemoveAt(idx);

                    double rawOffset = eventTime - target.StartTime;
                    if (isTail && rawOffset < 0)
                        holdBreak = true;

                    timeOffsetForJudgement = rawOffset / lenienceFactor;

                    // Use the ruleset-provided mapping, but coerce outside-of-window to Miss (ResultFor() would return None).
                    if (Math.Abs(rawOffset) > missWindow)
                        result = HitResult.Miss;
                    else
                        result = target.HitWindows.ResultFor(timeOffsetForJudgement);

                    if (result == HitResult.None)
                        result = HitResult.Miss;

                    if (target is HeadNote head)
                        headWasHit[head] = result.IsHit();

                    if (target is TailNote tail && headByTail.TryGetValue(tail, out var headNote))
                    {
                        bool headHit = headWasHit.TryGetValue(headNote, out bool wasHit) && wasHit;

                        if (result > HitResult.Meh && (!headHit || holdBreak))
                            result = HitResult.Meh;
                    }
                }
                else
                {
                    // No matching input event. Treat as a miss.
                    timeOffsetForJudgement = 0;
                    result = HitResult.Miss;

                    if (target is HeadNote head)
                        headWasHit[head] = false;
                }

                hitEvents.Add(new HitEvent(timeOffsetForJudgement, gameplayRate, result, target, lastHitObject, null));
                lastHitObject = target;
            }

            return hitEvents;
        }

        private static void collectJudgementTargets(HitObject hitObject, List<HitObject> targets, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (hitObject.HitWindows != null && !ReferenceEquals(hitObject.HitWindows, HitWindows.Empty) && hitObject.Judgement.MaxResult != HitResult.IgnoreHit)
                targets.Add(hitObject);

            foreach (var nested in hitObject.NestedHitObjects)
                collectJudgementTargets(nested, targets, cancellationToken);
        }
    }
}
