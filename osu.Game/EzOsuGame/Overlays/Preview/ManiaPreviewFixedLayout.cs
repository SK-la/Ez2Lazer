// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public static class ManiaPreviewFixedLayout
    {
        public const int ROWS_PER_BEAT = 4;
        public const int ROWS_PER_MEASURE = 16;

        public static List<ManiaPreviewLayoutEntry> Build(ManiaPreviewData data)
        {
            var entries = new List<ManiaPreviewLayoutEntry>();
            if (data.Notes.Count == 0)
                return entries;

            var timeGroups = data.Notes
                                 .GroupBy(n => n.StartTime)
                                 .OrderBy(g => g.Key)
                                 .ToList();

            var rowByTime = new Dictionary<double, int>();
            int nextRow = 0;

            foreach (var group in timeGroups)
            {
                rowByTime[group.Key] = nextRow;
                nextRow++;
            }

            var holdEndRows = buildHoldEndRows(data.Notes, rowByTime, nextRow);

            foreach (ManiaPreviewNote note in data.Notes)
            {
                int startRow = rowByTime[note.StartTime];

                switch (note.Kind)
                {
                    case ManiaPreviewNoteKind.Tap:
                        entries.Add(new ManiaPreviewLayoutEntry(note.Column, startRow, startRow, note.Kind));
                        break;

                    case ManiaPreviewNoteKind.HoldHead:
                    {
                        int endRow = holdEndRows.TryGetValue(note, out int row) ? row : startRow + 1;
                        endRow = Math.Max(startRow + 1, endRow);
                        // startRow = press (bottom), endRow = release (top).
                        entries.Add(new ManiaPreviewLayoutEntry(note.Column, startRow, startRow, ManiaPreviewNoteKind.HoldHead));
                        entries.Add(new ManiaPreviewLayoutEntry(note.Column, startRow, endRow, ManiaPreviewNoteKind.HoldBody));
                        entries.Add(new ManiaPreviewLayoutEntry(note.Column, endRow, endRow, ManiaPreviewNoteKind.HoldTail));
                        break;
                    }

                    case ManiaPreviewNoteKind.HoldTail:
                        break;
                }
            }

            return entries;
        }

        public static List<ManiaPreviewLayoutEntry> BuildTimeBased(ManiaPreviewData data)
        {
            var entries = new List<ManiaPreviewLayoutEntry>();
            if (data.Notes.Count == 0 || data.MsPerMeasure <= 0)
                return entries;

            double msPerRow = data.MsPerMeasure / ROWS_PER_MEASURE;

            int timeToRow(double time) => Math.Max(0, (int)Math.Round((time - data.StartTimeMs) / msPerRow));

            foreach (ManiaPreviewNote note in data.Notes)
            {
                int startRow = timeToRow(note.StartTime);

                switch (note.Kind)
                {
                    case ManiaPreviewNoteKind.Tap:
                        entries.Add(new ManiaPreviewLayoutEntry(note.Column, startRow, startRow, note.Kind));
                        break;

                    case ManiaPreviewNoteKind.HoldHead:
                    {
                        int endRow = Math.Max(startRow + 1, timeToRow(note.EndTime));
                        entries.Add(new ManiaPreviewLayoutEntry(note.Column, startRow, startRow, ManiaPreviewNoteKind.HoldHead));
                        entries.Add(new ManiaPreviewLayoutEntry(note.Column, startRow, endRow, ManiaPreviewNoteKind.HoldBody));
                        entries.Add(new ManiaPreviewLayoutEntry(note.Column, endRow, endRow, ManiaPreviewNoteKind.HoldTail));
                        break;
                    }

                    case ManiaPreviewNoteKind.HoldTail:
                        break;
                }
            }

            return entries;
        }

        public static int GetTotalRows(IReadOnlyList<ManiaPreviewLayoutEntry> entries)
        {
            if (entries.Count == 0)
                return 1;

            return entries.Max(e => Math.Max(e.Row, e.EndRow)) + 1;
        }

        private static Dictionary<ManiaPreviewNote, int> buildHoldEndRows(
            IReadOnlyList<ManiaPreviewNote> notes,
            IReadOnlyDictionary<double, int> rowByTime,
            int totalRows)
        {
            var result = new Dictionary<ManiaPreviewNote, int>();
            var orderedTimes = rowByTime.Keys.OrderBy(t => t).ToList();

            foreach (ManiaPreviewNote note in notes.Where(n => n.Kind == ManiaPreviewNoteKind.HoldHead))
            {
                int startRow = rowByTime[note.StartTime];
                int endRow = startRow + 1;

                foreach (double time in orderedTimes)
                {
                    if (time <= note.StartTime)
                        continue;

                    if (time <= note.EndTime)
                        endRow = rowByTime[time];
                    else
                        break;
                }

                result[note] = Math.Min(totalRows - 1, Math.Max(startRow + 1, endRow));
            }

            return result;
        }
    }
}
