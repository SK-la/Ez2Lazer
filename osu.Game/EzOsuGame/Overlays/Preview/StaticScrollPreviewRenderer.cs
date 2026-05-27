// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public partial class StaticScrollPreviewRenderer : CompositeDrawable, IManiaStaticPreviewRenderer
    {
        private readonly Container content;
        private readonly ManiaPreviewBatchDrawable batchDrawable;

        private ManiaPreviewData data;
        private List<ManiaPreviewLayoutEntry> layoutEntries = new List<ManiaPreviewLayoutEntry>();
        private ManiaPreviewColumnLayout layout;
        private int totalRows = 1;
        private bool hasData;
        private float density = 1f;
        private float scrollOffset;
        private float lastViewportWidth;
        private float lastViewportHeight;
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

            if (DrawWidth != lastViewportWidth || DrawHeight != lastViewportHeight || getMeasuresPerColumn() != lastMeasuresPerColumn)
                rebuild();

            applyScrollOffset();
        }

        public void SetData(ManiaPreviewData data)
        {
            this.data = data;
            layoutEntries = ManiaPreviewFixedLayout.Build(data);
            totalRows = ManiaPreviewFixedLayout.GetTotalRows(layoutEntries);
            hasData = true;
            lastMeasuresPerColumn = -1;
            rebuild();
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
        }

        public void AdjustScroll(float delta)
        {
            float maxOffset = getMaxScrollOffset();
            scrollOffset = Math.Clamp(scrollOffset + delta, 0, maxOffset);
            applyScrollOffset();
        }

        public void SetScrollProgress(float progress)
        {
            float maxOffset = getMaxScrollOffset();
            scrollOffset = Math.Clamp(progress, 0, 1) * maxOffset;
            applyScrollOffset();
        }

        public float GetScrollProgress()
        {
            float maxOffset = getMaxScrollOffset();
            return maxOffset > 0 ? scrollOffset / maxOffset : 0;
        }

        private float getMaxScrollOffset() => Math.Max(0, content.DrawWidth - DrawWidth);

        private void applyScrollOffset()
        {
            scrollOffset = Math.Clamp(scrollOffset, 0, getMaxScrollOffset());
            content.X = -scrollOffset;
        }

        private void rebuild()
        {
            if (DrawWidth <= 1 || DrawHeight <= 1)
                return;

            lastViewportWidth = DrawWidth;
            lastViewportHeight = DrawHeight;
            lastMeasuresPerColumn = getMeasuresPerColumn();

            layout = ManiaPreviewColumnLayout.ForScroll(totalRows, DrawWidth, DrawHeight, density);

            content.Height = DrawHeight;
            batchDrawable.Size = new Vector2(layout.ContentWidth, DrawHeight);

            var quads = new List<PreviewQuad>(layoutEntries.Count + layout.ColumnCount * (data.TotalColumns + layout.RowsPerColumn));
            ManiaPreviewColumnRenderer.BuildColumnPanels(quads, layoutEntries, layout, data, totalRows, DrawHeight);

            batchDrawable.SetQuads(quads);
            scrollOffset = Math.Clamp(scrollOffset, 0, getMaxScrollOffset());
            applyScrollOffset();
        }

        private int getMeasuresPerColumn() => Math.Clamp((int)Math.Round(2f / density), 1, 8);
    }
}
