// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public partial class StaticScrollPreviewRenderer : CompositeDrawable, IManiaStaticPreviewRenderer
    {
        private const float column_spacing = 16f;

        private readonly Container content;
        private readonly ManiaPreviewBatchDrawable batchDrawable;

        private ManiaPreviewData data;
        private List<ManiaPreviewLayoutEntry> layoutEntries = new List<ManiaPreviewLayoutEntry>();
        private int totalRows = 1;
        private bool hasData;
        private float density = 1f;
        private float scrollOffset;
        private float lastViewportWidth;
        private int lastMeasuresPerColumn = -1;

        public StaticScrollPreviewRenderer()
        {
            RelativeSizeAxes = Axes.Both;
            Masking = true;

            InternalChild = content = new Container
            {
                RelativeSizeAxes = Axes.Y,
                AutoSizeAxes = Axes.X,
                Child = batchDrawable = new ManiaPreviewBatchDrawable
                {
                    RelativeSizeAxes = Axes.None,
                }
            };
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (!hasData)
                return;

            int measuresPerColumn = getMeasuresPerColumn();

            if (DrawWidth != lastViewportWidth || measuresPerColumn != lastMeasuresPerColumn)
                rebuild();

            updateScrollOffset();
        }

        public void SetData(ManiaPreviewData data)
        {
            this.data = data;
            layoutEntries = ManiaPreviewFixedLayout.Build(data);
            totalRows = ManiaPreviewFixedLayout.GetTotalRows(layoutEntries);
            hasData = true;
            lastMeasuresPerColumn = -1;
            rebuild();
            updateScrollOffset();
        }

        public void SetCurrentTime(double time)
        {
        }

        public void SetDensity(float density)
        {
            float clamped = Math.Clamp(density, 0.1f, 5f);

            if (Math.Abs(this.density - clamped) <= 0.001f)
                return;

            this.density = clamped;
            lastMeasuresPerColumn = -1;
            rebuild();
            updateScrollOffset();
        }

        public void AdjustScroll(float delta)
        {
            float maxOffset = Math.Max(0, content.DrawWidth - DrawWidth);
            scrollOffset = Math.Clamp(scrollOffset + delta, 0, maxOffset);
            content.X = -scrollOffset;
        }

        private void rebuild()
        {
            if (DrawWidth <= 1 || DrawHeight <= 1)
                return;

            lastViewportWidth = DrawWidth;
            int measuresPerColumn = getMeasuresPerColumn();
            lastMeasuresPerColumn = measuresPerColumn;

            int rowsPerColumn = measuresPerColumn * ManiaPreviewFixedLayout.ROWS_PER_MEASURE;
            int columnCount = Math.Max(1, (totalRows + rowsPerColumn - 1) / rowsPerColumn);

            float columnWidth = Math.Max(96f, DrawWidth * 0.22f);
            float contentWidth = columnCount * columnWidth + Math.Max(0, columnCount - 1) * column_spacing;
            (float rowStep, float noteHeight) = ManiaPreviewDrawHelper.ComputeRowMetrics(rowsPerColumn, DrawHeight);

            content.Height = DrawHeight;
            batchDrawable.Size = new Vector2(contentWidth, DrawHeight);

            var quads = new List<PreviewQuad>(layoutEntries.Count + columnCount * (data.TotalColumns + rowsPerColumn));

            float laneLineThickness = Math.Max(0.5f, columnWidth * 0.004f);
            float beatLineThickness = Math.Max(0.5f, rowStep * 0.08f);

            for (int col = 0; col < columnCount; col++)
            {
                float panelX = col * (columnWidth + column_spacing);
                int rowStart = col * rowsPerColumn;
                int rowEnd = Math.Min(totalRows, rowStart + rowsPerColumn);

                quads.Add(new PreviewQuad(panelX, 0, 1f, DrawHeight, Color4.White.Opacity(0.18f)));
                quads.Add(new PreviewQuad(panelX + columnWidth - 1f, 0, 1f, DrawHeight, Color4.White.Opacity(0.18f)));

                ManiaPreviewDrawHelper.AddLaneLines(quads, data.TotalColumns, panelX, columnWidth, DrawHeight, laneLineThickness);

                for (int row = rowStart + ManiaPreviewFixedLayout.ROWS_PER_BEAT; row <= rowEnd; row += ManiaPreviewFixedLayout.ROWS_PER_BEAT)
                {
                    float localRow = row - rowStart;
                    float y = localRow * rowStep - beatLineThickness * 0.5f;
                    quads.Add(new PreviewQuad(panelX, y, columnWidth, beatLineThickness, Color4.White.Opacity(0.2f)));
                }
            }

            foreach (ManiaPreviewLayoutEntry entry in layoutEntries)
            {
                int col = entry.Row / rowsPerColumn;
                if (col >= columnCount)
                    continue;

                int rowStart = col * rowsPerColumn;
                int rowEnd = Math.Min(totalRows, rowStart + rowsPerColumn);

                if (entry.Row >= rowEnd)
                    continue;

                float panelX = col * (columnWidth + column_spacing);
                var localEntry = new ManiaPreviewLayoutEntry(
                    entry.Column,
                    entry.Row - rowStart,
                    Math.Min(entry.EndRow, rowEnd - 1) - rowStart,
                    entry.Kind);

                ManiaPreviewDrawHelper.AddLayoutEntries(
                    quads,
                    new[] { localEntry },
                    data.TotalColumns,
                    panelX,
                    columnWidth,
                    rowStep,
                    noteHeight,
                    flatNotes: false);
            }

            batchDrawable.SetQuads(quads);
            scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, contentWidth - DrawWidth));
            content.X = -scrollOffset;
        }

        private void updateScrollOffset()
        {
            float maxOffset = Math.Max(0, content.DrawWidth - DrawWidth);
            scrollOffset = Math.Clamp(scrollOffset, 0, maxOffset);
            content.X = -scrollOffset;
        }

        private int getMeasuresPerColumn() => Math.Clamp((int)Math.Round(2f / density), 1, 8);
    }
}
