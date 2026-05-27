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
        private float columnSpacing { get; } = 16f;
        private float laneInsetRatio { get; } = 0.12f;

        private readonly Container content;
        private readonly ManiaPreviewBatchDrawable batchDrawable;

        private ManiaPreviewData data;
        private bool hasData;
        private double currentTime;
        private float density = 1;

        public StaticScrollPreviewRenderer()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChild = content = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Child = batchDrawable = new ManiaPreviewBatchDrawable()
            };
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (hasData)
            {
                rebuild();
                updateScrollOffset();
            }
        }

        public void SetData(ManiaPreviewData data)
        {
            this.data = data;
            hasData = true;
            rebuild();
            updateScrollOffset();
        }

        public void SetCurrentTime(double time)
        {
            currentTime = time;
            updateScrollOffset();
        }

        public void SetDensity(float density)
        {
            float clamped = Math.Clamp(density, 0.1f, 5f);

            if (Math.Abs(this.density - clamped) <= 0.001f)
                return;

            this.density = clamped;
            rebuild();
            updateScrollOffset();
        }

        private void rebuild()
        {
            if (DrawWidth <= 1 || DrawHeight <= 1)
                return;

            int measuresPerColumn = getMeasuresPerColumn();
            var quads = new List<PreviewQuad>(data.Notes.Count + data.BarLines.Count + data.TotalColumns * 4);

            IReadOnlyList<double> boundaries = buildMeasureBoundaries();
            int segmentCount = Math.Max(1, boundaries.Count - 1);
            int columnCount = Math.Max(1, (segmentCount + measuresPerColumn - 1) / measuresPerColumn);

            float columnWidth = Math.Max(80, DrawWidth * 0.18f);
            float laneWidth = columnWidth / Math.Max(1, data.TotalColumns);
            float contentWidth = columnCount * columnWidth + Math.Max(0, columnCount - 1) * columnSpacing;
            float laneLineThickness = Math.Max(1, DrawWidth * 0.0014f);
            float barThickness = Math.Max(1, DrawHeight * 0.0016f);
            float minNoteHeight = Math.Max(1.2f, DrawHeight * 0.004f);

            content.Size = new Vector2(contentWidth, DrawHeight);
            batchDrawable.Size = new Vector2(contentWidth, DrawHeight);

            for (int col = 0; col < columnCount; col++)
            {
                float panelX = col * (columnWidth + columnSpacing);
                int startSegment = col * measuresPerColumn;
                int endSegment = Math.Min(segmentCount, startSegment + measuresPerColumn);
                double startTime = boundaries[startSegment];
                double endTime = boundaries[endSegment];
                float span = (float)Math.Max(1, endTime - startTime);

                quads.Add(new PreviewQuad(panelX, 0, 1.2f, DrawHeight, Color4.White.Opacity(0.2f)));
                quads.Add(new PreviewQuad(panelX + columnWidth, 0, 1.2f, DrawHeight, Color4.White.Opacity(0.2f)));

                for (int lane = 1; lane < data.TotalColumns; lane++)
                {
                    float x = panelX + lane * laneWidth - laneLineThickness * 0.5f;
                    quads.Add(new PreviewQuad(x, 0, laneLineThickness, DrawHeight, Color4.White.Opacity(0.12f)));
                }

                for (int seg = startSegment; seg <= endSegment; seg++)
                {
                    float y = (float)((boundaries[seg] - startTime) / span * DrawHeight);
                    quads.Add(new PreviewQuad(panelX, y, columnWidth, barThickness, Color4.White.Opacity(0.23f)));
                }
            }

            foreach (ManiaPreviewNote note in data.Notes)
            {
                int segIndex = locateSegment(boundaries, note.StartTime);
                int col = segIndex / measuresPerColumn;
                int colFirstSegment = col * measuresPerColumn;
                int colLastSegment = Math.Min(segmentCount, colFirstSegment + measuresPerColumn);
                double colStartTime = boundaries[colFirstSegment];
                double colEndTime = boundaries[colLastSegment];

                float panelX = col * (columnWidth + columnSpacing);
                float span = (float)Math.Max(1, colEndTime - colStartTime);
                float laneX = panelX + note.Column * laneWidth + laneWidth * laneInsetRatio;
                float laneW = laneWidth * (1 - laneInsetRatio * 2);

                float y0 = (float)((note.StartTime - colStartTime) / span * DrawHeight);
                float y1 = (float)((Math.Min(note.EndTime, colEndTime) - colStartTime) / span * DrawHeight);
                float h = Math.Max(minNoteHeight, y1 - y0);

                var colour = note.EndTime - note.StartTime > 1 ? new Color4(121, 201, 255, 220) : new Color4(255, 214, 124, 230);
                quads.Add(new PreviewQuad(laneX, y0, laneW, h, colour));
            }

            batchDrawable.SetQuads(quads);
        }

        private void updateScrollOffset()
        {
            if (!hasData || content.DrawWidth <= DrawWidth)
            {
                content.X = 0;
                return;
            }

            double ratio = (currentTime - data.MinTime) / Math.Max(1, data.MaxTime - data.MinTime);
            ratio = Math.Clamp(ratio, 0, 1);
            float maxOffset = content.DrawWidth - DrawWidth;
            content.X = (float)(maxOffset * ratio);
        }

        private IReadOnlyList<double> buildMeasureBoundaries()
        {
            if (data.BarLines.Count < 2)
                return new[] { data.MinTime, data.MaxTime };

            var boundaries = new List<double>(data.BarLines.Count + 2);

            if (data.BarLines[0] > data.MinTime)
                boundaries.Add(data.MinTime);

            boundaries.AddRange(data.BarLines);

            if (boundaries[^1] < data.MaxTime)
                boundaries.Add(data.MaxTime);

            return boundaries;
        }

        private int getMeasuresPerColumn() => Math.Clamp((int)Math.Round(2f / density), 1, 8);

        private static int locateSegment(IReadOnlyList<double> boundaries, double time)
        {
            int last = boundaries.Count - 2;

            for (int i = 0; i <= last; i++)
            {
                if (time < boundaries[i + 1])
                    return i;
            }

            return Math.Max(0, last);
        }
    }
}
