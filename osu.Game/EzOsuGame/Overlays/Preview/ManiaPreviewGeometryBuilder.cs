// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public static class ManiaPreviewGeometryBuilder
    {
        private const double hold_threshold_ms = 1;

        public static ManiaPreviewData Build(IBeatmap beatmap)
        {
            var hitObjects = beatmap.HitObjects;
            if (hitObjects.Count == 0)
                return new ManiaPreviewData(4, Array.Empty<ManiaPreviewNote>());

            int totalColumns = getTotalColumns(beatmap, hitObjects);
            var notes = new List<ManiaPreviewNote>();

            foreach (HitObject obj in hitObjects)
            {
                if (!tryGetColumn(obj, totalColumns, out int column))
                    continue;

                double endTime = obj is IHasDuration hasDuration ? obj.StartTime + Math.Max(0, hasDuration.Duration) : obj.StartTime;
                bool isHold = endTime - obj.StartTime > hold_threshold_ms;

                if (!isHold)
                {
                    notes.Add(new ManiaPreviewNote(obj.StartTime, endTime, column, ManiaPreviewNoteKind.Tap));
                    continue;
                }

                notes.Add(new ManiaPreviewNote(obj.StartTime, endTime, column, ManiaPreviewNoteKind.HoldHead));
            }

            notes.Sort((a, b) =>
            {
                int cmp = a.StartTime.CompareTo(b.StartTime);
                return cmp != 0 ? cmp : a.Kind.CompareTo(b.Kind);
            });

            return new ManiaPreviewData(totalColumns, notes);
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

        private static bool tryGetColumn(HitObject obj, int totalColumns, out int column)
        {
            if (obj is IHasColumn hasColumn)
            {
                column = Math.Clamp(hasColumn.Column, 0, totalColumns - 1);
                return true;
            }

            if (obj is IHasXPosition hasX)
            {
                column = Math.Clamp((int)Math.Round(hasX.X), 0, totalColumns - 1);
                return true;
            }

            column = 0;
            return false;
        }
    }
}
