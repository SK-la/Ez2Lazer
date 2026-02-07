// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Mods.KrrConversion;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public static class ManiaNoteCleanupTool
    {
        public static void CleanupBeatmap(ManiaBeatmap beatmap, int maxNotesPerWindow, int windowQuarterBeats = 2, double? minGapMs = null, int? seed = null)
        {
            if (beatmap.HitObjects.Count == 0)
                return;

            double gap = minGapMs ?? getDefaultMinimumGapMs(beatmap);

            if (beatmap.TotalColumns > 0)
            {
                int usedSeed = seed ?? KrrConversionHelper.ComputeSeedFromBeatmap(beatmap);
                var rng = new Random(usedSeed);
                var resolved = KrrConversionHelper.ResolveFinalConflicts(beatmap.HitObjects.ToList(), beatmap.TotalColumns, gap, rng);
                beatmap.HitObjects.Clear();
                beatmap.HitObjects.AddRange(resolved);
            }

            CleanOverlaps(beatmap);
            SimplifyDenseNotes(beatmap, maxNotesPerWindow, windowQuarterBeats);
            if (gap > 0)
                EnforceMinimumGaps(beatmap, gap);
        }

        public static void CleanOverlaps(ManiaBeatmap beatmap)
        {
            if (beatmap.HitObjects.Count == 0)
                return;

            var toRemove = new HashSet<ManiaHitObject>();

            foreach (var group in beatmap.HitObjects.GroupBy(h => h.Column))
            {
                double currentEnd = double.MinValue;
                ManiaHitObject? current = null;

                foreach (var obj in group.OrderBy(h => h.StartTime))
                {
                    double objEnd = obj is HoldNote hold ? hold.EndTime : obj.StartTime;

                    if (current != null && obj.StartTime < currentEnd)
                    {
                        toRemove.Add(obj);
                        continue;
                    }

                    current = obj;
                    currentEnd = Math.Max(currentEnd, objEnd);
                }
            }

            if (toRemove.Count == 0)
                return;

            foreach (var obj in toRemove)
                beatmap.HitObjects.Remove(obj);
        }

        public static void SimplifyDenseNotes(ManiaBeatmap beatmap, int maxNotesPerWindow, int windowQuarterBeats = 2)
        {
            if (beatmap.HitObjects.Count == 0 || maxNotesPerWindow <= 0)
                return;

            var objects = beatmap.HitObjects
                                 .OrderBy(h => h.StartTime)
                                 .ToList();

            var toRemove = new HashSet<ManiaHitObject>();
            var window = new Queue<ManiaHitObject>();
            int windowQuarterBeatsSafe = Math.Max(1, windowQuarterBeats);

            foreach (var obj in objects)
            {
                double beatLength = beatmap.ControlPointInfo.TimingPointAt(obj.StartTime).BeatLength;
                double windowDuration = beatLength / 4.0 * windowQuarterBeatsSafe;

                while (window.Count > 0 && obj.StartTime - window.Peek().StartTime > windowDuration)
                    window.Dequeue();

                window.Enqueue(obj);

                while (window.Count > maxNotesPerWindow)
                {
                    var remove = window.Last();
                    toRemove.Add(remove);
                    window = new Queue<ManiaHitObject>(window.Where(o => o != remove));
                }
            }

            if (toRemove.Count == 0)
                return;

            foreach (var obj in toRemove)
                beatmap.HitObjects.Remove(obj);
        }

        public static void EnforceMinimumGaps(ManiaBeatmap beatmap, double minGapMs)
        {
            if (beatmap.HitObjects.Count == 0 || minGapMs <= 0)
                return;

            var cleaned = KrrConversionHelper.EnforceMinimumGaps(beatmap.HitObjects.ToList(), beatmap.TotalColumns, minGapMs);
            beatmap.HitObjects.Clear();
            beatmap.HitObjects.AddRange(cleaned);
        }

        private static double getDefaultMinimumGapMs(ManiaBeatmap beatmap)
        {
            if (beatmap.HitObjects.Count == 0)
                return 0;

            double startTime = beatmap.HitObjects.Min(h => h.StartTime);
            double beatLength = beatmap.ControlPointInfo.TimingPointAt(startTime).BeatLength;
            double gap = beatLength / 4.0;
            double lowerBound = beatLength / 6.0;
            return Math.Max(gap, lowerBound);
        }
    }
}
