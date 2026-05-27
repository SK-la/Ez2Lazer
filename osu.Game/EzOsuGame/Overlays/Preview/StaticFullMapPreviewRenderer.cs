// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public partial class StaticFullMapPreviewRenderer : CompositeDrawable, IManiaStaticPreviewRenderer
    {
        private readonly ManiaPreviewBatchDrawable batchDrawable;
        private ManiaPreviewData data;
        private bool hasData;
        private bool focusZoomed;

        public StaticFullMapPreviewRenderer()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChild = batchDrawable = new ManiaPreviewBatchDrawable();
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (hasData)
                rebuild();
        }

        public void SetData(ManiaPreviewData data)
        {
            this.data = data;
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
            if (focusZoomed == zoomed)
                return;

            focusZoomed = zoomed;
            Scale = new osuTK.Vector2(focusZoomed ? 1.25f : 1.0f);
        }

        private void rebuild()
        {
            if (DrawWidth <= 1 || DrawHeight <= 1)
                return;

            var quads = new List<PreviewQuad>(data.Notes.Count + data.BarLines.Count + data.TotalColumns + 2);

            float laneWidth = DrawWidth / Math.Max(1, data.TotalColumns);
            float timeSpan = (float)Math.Max(1, data.MaxTime - data.MinTime);
            float laneLineThickness = Math.Max(1, DrawWidth * 0.0015f);
            float barThickness = Math.Max(1, DrawHeight * 0.0015f);
            float noteHeight = Math.Max(1.5f, DrawHeight * 0.0035f);

            for (int c = 1; c < data.TotalColumns; c++)
            {
                float x = c * laneWidth - laneLineThickness * 0.5f;
                quads.Add(new PreviewQuad(x, 0, laneLineThickness, DrawHeight, Color4.White.Opacity(0.14f)));
            }

            foreach (double barTime in data.BarLines)
            {
                float y = (float)((barTime - data.MinTime) / timeSpan * DrawHeight);
                quads.Add(new PreviewQuad(0, y, DrawWidth, barThickness, Color4.White.Opacity(0.25f)));
            }

            foreach (ManiaPreviewNote note in data.Notes)
            {
                float x = note.Column * laneWidth + laneWidth * 0.15f;
                float width = laneWidth * 0.7f;

                float y0 = (float)((note.StartTime - data.MinTime) / timeSpan * DrawHeight);
                float y1 = (float)((note.EndTime - data.MinTime) / timeSpan * DrawHeight);
                float h = Math.Max(noteHeight, y1 - y0);

                var colour = note.EndTime - note.StartTime > 1 ? new Color4(120, 198, 255, 220) : new Color4(255, 210, 120, 230);
                quads.Add(new PreviewQuad(x, y0, width, h, colour));
            }

            batchDrawable.SetQuads(quads);
        }
    }
}
