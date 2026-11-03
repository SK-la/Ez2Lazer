// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public readonly struct ManiaPreviewColumnLayout
    {
        public const float COLUMN_SPACING = 16f;

        /// <summary>Fixed row height in the time grid (maps to a fixed ms-per-row via beatmap timing).</summary>
        public const float UNIT_ROW_STEP = 8f;

        /// <summary>Note thickness in the unit grid. Screen thickness stays at the density-1 size; density scales ms→px only.</summary>
        public const float UNIT_NOTE_HEIGHT = UNIT_ROW_STEP * 0.25f;

        /// <summary>Fixed measure height in the abstract grid.</summary>
        public const float UNIT_MEASURE_HEIGHT = ManiaPreviewFixedLayout.ROWS_PER_MEASURE * UNIT_ROW_STEP;

        /// <summary>Fixed measure width in the abstract grid.</summary>
        public const float UNIT_MEASURE_WIDTH = 96f;

        public const float REFERENCE_NOTE_HEIGHT = 8f;

        public int MeasuresPerColumn { get; init; }
        public int RowsPerColumn { get; init; }
        public int ColumnCount { get; init; }
        public int TotalMeasures { get; init; }
        public float ColumnWidth { get; init; }
        public float RowStep { get; init; }
        public float NoteHeight { get; init; }
        public float PanelHeight { get; init; }
        public float ContentWidth { get; init; }
        public float FitScale { get; init; }

        /// <summary>Horizontal fit. Vertical scale is <see cref="FitScale" /> and carries the ms→px ratio.</summary>
        public float WidthScale { get; init; }

        public bool TimeMapped { get; init; }

        public int TotalGridRows => TotalMeasures * ManiaPreviewFixedLayout.ROWS_PER_MEASURE;

        public static ManiaPreviewColumnLayout ForScroll(int totalRows, float viewportWidth, float viewportHeight, float density)
        {
            float noteHeight = noteHeightAtUnitDensity(viewportHeight);
            float rowStep = noteHeight / (UNIT_NOTE_HEIGHT / UNIT_ROW_STEP) * density;
            float measureHeight = ManiaPreviewFixedLayout.ROWS_PER_MEASURE * rowStep;
            int maxMeasures = Math.Max(1, (totalRows + ManiaPreviewFixedLayout.ROWS_PER_MEASURE - 1) / ManiaPreviewFixedLayout.ROWS_PER_MEASURE);
            int measuresPerColumn = Math.Clamp((int)Math.Floor(viewportHeight / Math.Max(1f, measureHeight)), 1, Math.Min(8, maxMeasures));
            int rowsPerColumn = measuresPerColumn * ManiaPreviewFixedLayout.ROWS_PER_MEASURE;
            int columnCount = Math.Max(1, (totalRows + rowsPerColumn - 1) / rowsPerColumn);
            float columnWidth = Math.Max(96f, viewportWidth * 0.22f);

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
                WidthScale = 1f
            };
        }

        /// <summary>
        ///     Full-map layout: fixed measure units, time-based rows, search column split to match viewport aspect and fill.
        /// </summary>
        /// <param name="density">Multiplies the density-1 ms→px ratio. Note thickness stays at the density-1 screen size.</param>
        public static ManiaPreviewColumnLayout ForFullMapMeasureGrid(double durationMs, double msPerMeasure, float viewportWidth, float viewportHeight, float density = 1f)
        {
            if (msPerMeasure <= 0)
                msPerMeasure = 2000;

            if (durationMs <= 0)
                durationMs = msPerMeasure;

            int totalMeasures = Math.Max(1, (int)Math.Ceiling(durationMs / msPerMeasure));

            float viewportAspect = viewportWidth / Math.Max(1f, viewportHeight);

            ManiaPreviewColumnLayout best = default;

            float bestScore = float.MinValue;

            for (int measuresPerColumn = 1; measuresPerColumn <= totalMeasures; measuresPerColumn++)
            {
                var layout = buildMeasureGridLayout(totalMeasures, measuresPerColumn);
                float scale = computeFitScale(layout, viewportWidth, viewportHeight);
                float contentAspect = layout.ContentWidth / Math.Max(1f, layout.PanelHeight);
                float fillRatio = layout.ContentWidth * scale * layout.PanelHeight * scale / (viewportWidth * viewportHeight);
                float aspectPenalty = Math.Abs(MathF.Log(viewportAspect) - MathF.Log(contentAspect));
                int measuresInLastColumn = totalMeasures - (layout.ColumnCount - 1) * measuresPerColumn;
                float trailingBlank = 1f - measuresInLastColumn / (float)measuresPerColumn;
                float score = fillRatio - aspectPenalty * 0.18f - trailingBlank * 0.08f;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = layout with { FitScale = scale };
            }

            if (best.ColumnCount != 0)
                return applyTimeScale(best, totalMeasures, viewportWidth, viewportHeight, density);

            float fallbackScale = Math.Clamp(
                Math.Min(viewportWidth / UNIT_MEASURE_WIDTH, viewportHeight / UNIT_MEASURE_HEIGHT),
                0.05f,
                16f);

            return buildMeasureGridLayout(totalMeasures, 1) with
            {
                FitScale = fallbackScale,
                WidthScale = fallbackScale,
                TimeMapped = true,
            };
        }

        private const int scroll_measures_at_unit_density = 2;

        private static float noteHeightAtUnitDensity(float viewportHeight)
        {
            int rows = scroll_measures_at_unit_density * ManiaPreviewFixedLayout.ROWS_PER_MEASURE;
            float rowStep = viewportHeight / Math.Max(1, rows);
            return rowStep * (UNIT_NOTE_HEIGHT / UNIT_ROW_STEP);
        }

        private static ManiaPreviewColumnLayout applyTimeScale(ManiaPreviewColumnLayout fitted, int totalMeasures, float viewportWidth, float viewportHeight, float density)
        {
            float baseScale = fitted.FitScale;
            float scaleY = baseScale * density;
            float screenMeasure = UNIT_MEASURE_HEIGHT * scaleY;
            int measuresPerColumn = Math.Abs(density - 1f) <= 0.001f
                ? fitted.MeasuresPerColumn
                : Math.Clamp((int)Math.Floor(viewportHeight / Math.Max(1f, screenMeasure)), 1, totalMeasures);

            var laid = buildMeasureGridLayout(totalMeasures, measuresPerColumn);
            float scaleX = Math.Min(baseScale, viewportWidth / Math.Max(1f, laid.ContentWidth));

            return laid with
            {
                FitScale = scaleY,
                WidthScale = scaleX,
                NoteHeight = UNIT_NOTE_HEIGHT * baseScale / scaleY,
                TimeMapped = true,
            };
        }

        private static float computeFitScale(in ManiaPreviewColumnLayout layout, float viewportWidth, float viewportHeight)
        {
            return Math.Clamp(Math.Min(viewportWidth / layout.ContentWidth, viewportHeight / layout.PanelHeight), 0.05f, 16f);
        }

        private static ManiaPreviewColumnLayout buildMeasureGridLayout(int totalMeasures, int measuresPerColumn)
        {
            int columnCount = (totalMeasures + measuresPerColumn - 1) / measuresPerColumn;
            int rowsPerColumn = measuresPerColumn * ManiaPreviewFixedLayout.ROWS_PER_MEASURE;
            float panelHeight = measuresPerColumn * UNIT_MEASURE_HEIGHT;
            float contentWidth = columnCount * UNIT_MEASURE_WIDTH + Math.Max(0, columnCount - 1) * COLUMN_SPACING;

            return new ManiaPreviewColumnLayout
            {
                MeasuresPerColumn = measuresPerColumn,
                RowsPerColumn = rowsPerColumn,
                ColumnCount = columnCount,
                TotalMeasures = totalMeasures,
                ColumnWidth = UNIT_MEASURE_WIDTH,
                RowStep = UNIT_ROW_STEP,
                NoteHeight = UNIT_NOTE_HEIGHT,
                PanelHeight = panelHeight,
                ContentWidth = contentWidth,
                FitScale = 1f
            };
        }
    }
}
