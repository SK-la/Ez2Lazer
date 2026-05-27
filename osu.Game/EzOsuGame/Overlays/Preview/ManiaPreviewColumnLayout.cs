// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public readonly struct ManiaPreviewColumnLayout
    {
        public const float COLUMN_SPACING = 16f;
        public const float REFERENCE_NOTE_HEIGHT = 8f;

        public int MeasuresPerColumn { get; init; }
        public int RowsPerColumn { get; init; }
        public int ColumnCount { get; init; }
        public float ColumnWidth { get; init; }
        public float RowStep { get; init; }
        public float NoteHeight { get; init; }
        public float PanelHeight { get; init; }
        public float ContentWidth { get; init; }
        public float FitScale { get; init; }

        public static ManiaPreviewColumnLayout ForScroll(int totalRows, float viewportWidth, float viewportHeight, float density)
        {
            int measuresPerColumn = Math.Clamp((int)Math.Round(2f / density), 1, 8);
            int rowsPerColumn = measuresPerColumn * ManiaPreviewFixedLayout.ROWS_PER_MEASURE;
            int columnCount = Math.Max(1, (totalRows + rowsPerColumn - 1) / rowsPerColumn);
            float columnWidth = Math.Max(96f, viewportWidth * 0.22f);
            (float rowStep, float noteHeight) = ManiaPreviewDrawHelper.ComputeRowMetrics(rowsPerColumn, viewportHeight);

            return new ManiaPreviewColumnLayout
            {
                MeasuresPerColumn = measuresPerColumn,
                RowsPerColumn = rowsPerColumn,
                ColumnCount = columnCount,
                ColumnWidth = columnWidth,
                RowStep = rowStep,
                NoteHeight = noteHeight,
                PanelHeight = viewportHeight,
                ContentWidth = columnCount * columnWidth + Math.Max(0, columnCount - 1) * COLUMN_SPACING,
                FitScale = 1f,
            };
        }

        /// <summary>
        /// Build a scroll-style layout at natural note size, then compute scale to fit the viewport.
        /// </summary>
        public static ManiaPreviewColumnLayout ForFullMap(int totalRows, float viewportWidth, float viewportHeight)
        {
            ManiaPreviewColumnLayout best = default;
            float bestScore = float.MaxValue;

            for (int measures = 1; measures <= 6; measures++)
            {
                int rowsPerColumn = measures * ManiaPreviewFixedLayout.ROWS_PER_MEASURE;
                int columnCount = Math.Max(1, (totalRows + rowsPerColumn - 1) / rowsPerColumn);
                float columnWidth = Math.Clamp(viewportWidth / Math.Max(1, Math.Min(columnCount, 5)), 72f, 140f);

                float rowStep = REFERENCE_NOTE_HEIGHT * 2f;
                float noteHeight = REFERENCE_NOTE_HEIGHT;
                float panelHeight = rowsPerColumn * rowStep;
                float contentWidth = columnCount * columnWidth + Math.Max(0, columnCount - 1) * COLUMN_SPACING;

                float scale = Math.Min(viewportWidth / contentWidth, viewportHeight / panelHeight) * 0.94f;
                scale = Math.Clamp(scale, 0.05f, 1f);

                int usedRowsInLastColumn = totalRows - (columnCount - 1) * rowsPerColumn;
                float trailingBlank = 1f - usedRowsInLastColumn / (float)rowsPerColumn;
                float score = Math.Abs(1f - scale) + trailingBlank * 0.35f + columnCount * 0.02f;

                if (score >= bestScore)
                    continue;

                bestScore = score;
                best = new ManiaPreviewColumnLayout
                {
                    MeasuresPerColumn = measures,
                    RowsPerColumn = rowsPerColumn,
                    ColumnCount = columnCount,
                    ColumnWidth = columnWidth,
                    RowStep = rowStep,
                    NoteHeight = noteHeight,
                    PanelHeight = panelHeight,
                    ContentWidth = contentWidth,
                    FitScale = scale,
                };
            }

            if (best.ColumnCount == 0)
            {
                return ForScroll(Math.Max(1, totalRows), viewportWidth, viewportHeight, 1f) with
                {
                    PanelHeight = REFERENCE_NOTE_HEIGHT * 2f * ManiaPreviewFixedLayout.ROWS_PER_MEASURE,
                    FitScale = 1f,
                };
            }

            return best;
        }
    }
}
