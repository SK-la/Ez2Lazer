// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public partial class StaticFullMapPreviewRenderer : CompositeDrawable, IManiaStaticPreviewRenderer
    {
        private readonly ManiaPreviewBatchDrawable batchDrawable;
        private ManiaPreviewData data;
        private List<ManiaPreviewLayoutEntry> layoutEntries = new List<ManiaPreviewLayoutEntry>();
        private int totalRows = 1;
        private bool hasData;
        private bool focusZoomed;
        private float lastWidth;
        private float lastHeight;

        public StaticFullMapPreviewRenderer()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChild = batchDrawable = new ManiaPreviewBatchDrawable
            {
                RelativeSizeAxes = Axes.Both,
            };
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (hasData && (DrawWidth != lastWidth || DrawHeight != lastHeight))
                rebuild();
        }

        public void SetData(ManiaPreviewData data)
        {
            this.data = data;
            layoutEntries = ManiaPreviewFixedLayout.Build(data);
            totalRows = ManiaPreviewFixedLayout.GetTotalRows(layoutEntries);
            hasData = true;
            rebuild();
        }

        public void SetCurrentTime(double time)
        {
        }

        public void SetDensity(float density)
        {
        }

        public void SetZoom(bool zoomed)
        {
            focusZoomed = zoomed;
            Scale = new osuTK.Vector2(1f);
        }

        private void rebuild()
        {
            if (DrawWidth <= 1 || DrawHeight <= 1)
                return;

            lastWidth = DrawWidth;
            lastHeight = DrawHeight;

            var quads = new List<PreviewQuad>(layoutEntries.Count + totalRows / ManiaPreviewFixedLayout.ROWS_PER_BEAT + data.TotalColumns);

            (float rowStep, float noteHeight) = ManiaPreviewDrawHelper.ComputeRowMetrics(totalRows, DrawHeight);
            float laneLineThickness = Math.Max(0.5f, DrawWidth * 0.001f);
            float beatLineThickness = Math.Max(0.5f, rowStep * 0.08f);

            ManiaPreviewDrawHelper.AddLaneLines(quads, data.TotalColumns, 0, DrawWidth, DrawHeight, laneLineThickness);
            ManiaPreviewDrawHelper.AddBeatLines(quads, totalRows, rowStep, 0, DrawWidth, beatLineThickness);
            ManiaPreviewDrawHelper.AddLayoutEntries(quads, layoutEntries, data.TotalColumns, 0, DrawWidth, rowStep, noteHeight, flatNotes: true);

            batchDrawable.SetQuads(quads);
        }
    }
}
