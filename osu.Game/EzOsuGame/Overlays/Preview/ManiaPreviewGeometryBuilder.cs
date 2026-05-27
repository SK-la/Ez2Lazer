// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public static class ManiaPreviewGeometryBuilder
    {
        public static ManiaPreviewData Build(IBeatmap beatmap)
        {
            var hitObjects = beatmap.HitObjects;
            if (hitObjects.Count == 0)
                return new ManiaPreviewData(4, 0, 1, Array.Empty<double>(), Array.Empty<ManiaPreviewNote>());

            int totalColumns = getTotalColumns(beatmap, hitObjects);
            double minTime = hitObjects.Min(h => h.StartTime);
            double maxTime = hitObjects.Max(h => h.GetEndTime());
            maxTime = Math.Max(maxTime, minTime + 1);

            var notes = new List<ManiaPreviewNote>(hitObjects.Count);

            foreach (HitObject obj in hitObjects)
            {
                if (obj is not IHasColumn hasColumn)
                    continue;

                double endTime = obj is IHasDuration hasDuration ? obj.StartTime + Math.Max(0, hasDuration.Duration) : obj.StartTime;
                notes.Add(new ManiaPreviewNote(obj.StartTime, Math.Max(endTime, obj.StartTime), Math.Clamp(hasColumn.Column, 0, totalColumns - 1)));
            }

            notes.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            return new ManiaPreviewData(
                totalColumns,
                minTime,
                maxTime,
                generateBarLines(beatmap.ControlPointInfo, minTime, maxTime),
                notes);
        }

        private static int getTotalColumns(IBeatmap beatmap, IReadOnlyList<HitObject> objects)
        {
            int byDifficulty = (int)Math.Round(beatmap.BeatmapInfo.Difficulty.CircleSize);
            int maxColumn = 0;

            foreach (HitObject obj in objects)
            {
                if (obj is IHasColumn hasColumn)
                    maxColumn = Math.Max(maxColumn, hasColumn.Column + 1);
            }

            return Math.Max(1, Math.Max(byDifficulty, maxColumn));
        }

        private static List<double> generateBarLines(ControlPointInfo controlPoints, double minTime, double maxTime)
        {
            var barLines = new List<double>();
            var timingPoints = controlPoints.TimingPoints;

            if (timingPoints.Count == 0)
            {
                fillDefaultBarLines(barLines, minTime, maxTime);
                return barLines;
            }

            for (int i = 0; i < timingPoints.Count; i++)
            {
                TimingControlPoint current = timingPoints[i];
                double segmentStart = Math.Max(current.Time, minTime);
                double segmentEnd = i + 1 < timingPoints.Count ? Math.Min(timingPoints[i + 1].Time, maxTime) : maxTime;

                if (segmentEnd <= segmentStart)
                    continue;

                double barLength = Math.Max(1, current.BeatLength * Math.Max(1, current.TimeSignature.Numerator));
                double first = current.OmitFirstBarLine ? current.Time + barLength : current.Time;

                if (first > segmentEnd)
                    continue;

                double line = first + Math.Ceiling((segmentStart - first) / barLength) * barLength;

                for (; line <= segmentEnd; line += barLength)
                {
                    if (line >= minTime && line <= maxTime)
                        barLines.Add(line);
                }
            }

            if (barLines.Count == 0)
                fillDefaultBarLines(barLines, minTime, maxTime);

            barLines.Sort();
            return barLines;
        }

        private static void fillDefaultBarLines(List<double> barLines, double minTime, double maxTime)
        {
            const double default_bar_length = 2000;
            for (double t = minTime; t <= maxTime; t += default_bar_length)
                barLines.Add(t);
        }
    }
}
