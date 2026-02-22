// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShiftChord : ManiaModPatternShiftPatternBase
    {
        protected override KeyPatternType PatternType => KeyPatternType.Chord;
        protected override string PatternName => "Chord";
        protected override string PatternAcronym => "PSC";
        public override LocalisableString Description => EzManiaModStrings.PatternShift_Description_Chord;
        protected override int DefaultLevel => 4;
        protected override EzOscillator.EzWaveform DefaultWaveform => EzOscillator.EzWaveform.Sine;
        protected override int DefaultOscillationBeats => 1;
        protected override int DefaultWindowProcessInterval => 1;
        protected override int DefaultWindowProcessOffset => 0;
        protected override int DefaultApplyOrder => 50;

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.PatternShift_Level_Label), nameof(EzManiaModStrings.PatternShift_Level_Description))]
        public new BindableNumber<int> Level => base.Level;

        protected override void ApplyPatternForWindow(List<ManiaHitObject> windowObjects,
                                                      ManiaBeatmap beatmap,
                                                      double windowStart,
                                                      double windowEnd,
                                                      KeyPatternSettings settings,
                                                      Random rng,
                                                      int maxIterationsPerWindow)
        {
            if (windowObjects.Count == 0)
                return;

            double beatLength = getFixedBeatLength(beatmap, windowStart);
            if (beatLength <= 0)
                return;

            double activeBeatFraction = ManiaKeyPatternHelp.GetActiveBeatFraction(windowObjects, beatmap, 1.0 / 4.0);
            double mainFraction = Math.Max(activeBeatFraction, 1.0 / 2.0);
            double mainStep = Math.Max(1, beatLength * mainFraction);
            double subStep = mainStep / 2.0;

            if (subStep <= 0)
                return;

            int availableSteps = (int)Math.Floor((windowEnd - (windowStart + subStep)) / subStep) + 1;
            if (availableSteps <= 0)
                return;

            double t = windowStart + subStep;
            int minK = Math.Max(0, settings.MinK);
            int maxK = Math.Max(minK, settings.MaxK);

            relocateNotesToCoarseGrid(windowObjects, beatmap, beatLength, windowStart, windowEnd, rng);
            applyChordPattern(beatmap, t, beatLength, windowStart, windowEnd, minK, maxK, rng);
        }

        private static void relocateNotesToCoarseGrid(List<ManiaHitObject> windowObjects,
                                                      ManiaBeatmap beatmap,
                                                      double beatLength,
                                                      double windowStart,
                                                      double windowEnd,
                                                      Random rng)
        {
            if (beatLength <= 0)
                return;

            bool isOnDivisionLocal(double t, int divisor)
            {
                if (divisor <= 0 || beatLength <= 0)
                    return false;

                double interval = beatLength / divisor;
                double mod = t % interval;
                return mod <= TIME_TOLERANCE || Math.Abs(interval - mod) <= TIME_TOLERANCE;
            }

            bool isOnHalfOrBeat(double t) => isOnDivisionLocal(t, 1) || isOnDivisionLocal(t, 2);

            bool isFinerThanQuarter(double t)
            {
                bool on1To4 = isOnDivisionLocal(t, 1)
                              || isOnDivisionLocal(t, 2)
                              || isOnDivisionLocal(t, 3)
                              || isOnDivisionLocal(t, 4);
                return !on1To4;
            }

            double getBeatAnchor(double t)
            {
                double anchor = Math.Floor(t / beatLength) * beatLength;
                return anchor < 0 ? 0 : anchor;
            }

            // int countAtTime(double time)
            // {
            //     var cols = ManiaKeyPatternHelp.GetColumnsAtTime(windowObjects, time, TIME_TOLERANCE);
            //     return cols.Count;
            // }

            foreach (var obj in windowObjects)
            {
                if (isOnHalfOrBeat(obj.StartTime))
                    continue;

                double anchor = getBeatAnchor(obj.StartTime);
                double quarter = beatLength / 4.0;
                double half = anchor + (beatLength / 2.0);

                if (anchor < windowStart - TIME_TOLERANCE || anchor > windowEnd + TIME_TOLERANCE)
                    continue;

                if (half < windowStart - TIME_TOLERANCE || half > windowEnd + TIME_TOLERANCE)
                    continue;

                // If note is finer than quarter, try promoting it to the nearest coarse grid
                // (beat / half / quarter) ordered by proximity.
                if (isFinerThanQuarter(obj.StartTime))
                {
                    double[] coarseIntervals = new[] { beatLength, beatLength / 2.0, beatLength / 4.0 };
                    var candidates = new List<double>(8);

                    foreach (double interval in coarseIntervals)
                    {
                        if (interval <= 0)
                            continue;

                        double nearest = Math.Round(obj.StartTime / interval) * interval;
                        candidates.Add(nearest);

                        const int max_steps = 2;

                        for (int i = 1; i <= max_steps; i++)
                        {
                            candidates.Add(nearest - i * interval);
                            candidates.Add(nearest + i * interval);
                        }
                    }

                    // Filter to window and dedupe, then try closest candidates first.
                    var distinctCandidates = candidates
                                             .Where(c => c >= windowStart - TIME_TOLERANCE && c <= windowEnd + TIME_TOLERANCE)
                                             .Distinct()
                                             .ToList();

                    distinctCandidates.Sort((a, b) => Math.Abs(a - obj.StartTime).CompareTo(Math.Abs(b - obj.StartTime)));

                    bool moved = false;

                    foreach (double c in distinctCandidates)
                    {
                        double offset = c - obj.StartTime;

                        if (ManiaKeyPatternHelp.TryApplyDelayOffset(beatmap, obj, offset, out _))
                        {
                            moved = true;
                            break;
                        }
                    }

                    if (moved)
                        continue;
                }

                // obj is on quarter grid. If surrounding quarter-range (previous/next quarter within half-beat) has notes,
                // try to move this note horizontally (column) to a nearby column that is free in both prev and next quarter times.
                double qTime = Math.Round(obj.StartTime / quarter) * quarter;
                double prevTime = qTime - quarter;
                double nextTime = qTime + quarter;

                int totalColumns = Math.Max(1, beatmap.TotalColumns);

                // If current column is fine (no notes at prev/next quarter), keep it.
                bool conflictPrev = ManiaKeyPatternHelp.HasNoteAtTime(beatmap, obj.Column, prevTime, null, TIME_TOLERANCE);
                bool conflictNext = ManiaKeyPatternHelp.HasNoteAtTime(beatmap, obj.Column, nextTime, null, TIME_TOLERANCE);

                if (!conflictPrev && !conflictNext)
                    continue;

                // Use helper to find nearest available column that is free at prev/next quarter.
                int? chosen = ManiaKeyPatternHelp.FindNearestAvailableColumnForQuarter(beatmap, obj.Column, qTime, prevTime, nextTime, totalColumns, TIME_TOLERANCE);
                if (chosen.HasValue)
                    obj.Column = chosen.Value;
            }
        }

        private static void applyChordPattern(ManiaBeatmap beatmap,
                                              double time,
                                              double beatLength,
                                              double windowStart,
                                              double windowEnd,
                                              int minK,
                                              int maxK,
                                              Random rng)
        {
            if (beatLength <= 0)
                return;

            double anchor = Math.Floor(time / beatLength) * beatLength;
            double half = anchor + (beatLength / 2.0);

            double candidateTime = Math.Abs(time - half) < Math.Abs(time - anchor) ? half : anchor;
            if (candidateTime < windowStart - TIME_TOLERANCE || candidateTime > windowEnd + TIME_TOLERANCE)
                return;

            // Snap to the nearest half-beat grid to avoid floating-point drift (ensure exact 1/1 or 1/2 alignment).
            double grid = beatLength / 2.0;
            if (grid > 0)
                candidateTime = Math.Round(candidateTime / grid) * grid;

            if (candidateTime < windowStart - TIME_TOLERANCE || candidateTime > windowEnd + TIME_TOLERANCE)
                return;

            int totalColumns = Math.Max(1, beatmap.TotalColumns);
            var columns = new List<int>(totalColumns);

            for (int i = 0; i < totalColumns; i++)
                columns.Add(i);

            // Filter out columns that already have a note at the snapped time (within TIME_TOLERANCE)
            var existingCols = ManiaKeyPatternHelp.GetColumnsAtTime(beatmap.HitObjects.ToList(), candidateTime, TIME_TOLERANCE);
            columns.RemoveAll(c => existingCols.Contains(c));

            // 禁止产生 1/4 间隙的叠键：若同列在前后 1/4 内已有 note，则不在该列添加
            double quarter = beatLength / 4.0;

            if (quarter > 0)
            {
                columns.RemoveAll(c => hasNoteWithinQuarterGap(beatmap, c, candidateTime, quarter));
            }

            int count = rng.Next(minK, maxK + 1);
            count = Math.Clamp(count, 0, columns.Count);

            for (int i = 0; i < count; i++)
                ManiaKeyPatternHelp.TryAddNoteFromCandidates(beatmap, columns, null, rng, candidateTime);
        }

        private static bool hasNoteWithinQuarterGap(ManiaBeatmap beatmap, int column, double time, double quarter)
        {
            double minTime = time - quarter - TIME_TOLERANCE;
            double maxTime = time + quarter + TIME_TOLERANCE;

            foreach (var obj in beatmap.HitObjects)
            {
                if (obj.Column != column)
                    continue;

                double objTime = obj.StartTime;
                if (objTime >= minTime && objTime <= maxTime)
                    return true;
            }

            return false;
        }

        private static double getFixedBeatLength(ManiaBeatmap beatmap, double fallbackTime)
        {
            var timingPoints = beatmap.ControlPointInfo.TimingPoints;
            if (timingPoints.Count > 0)
                return timingPoints[0].BeatLength;

            return beatmap.ControlPointInfo.TimingPointAt(fallbackTime).BeatLength;
        }
    }
}
