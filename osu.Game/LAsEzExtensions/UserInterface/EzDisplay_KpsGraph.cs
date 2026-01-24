// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Layout;
using osu.Game.LAsEzExtensions.Analysis;
using osuTK;

namespace osu.Game.LAsEzExtensions.UserInterface
{
    public partial class EzDisplayKpsGraph : CompositeDrawable
    {
        private const int max_draw_points = OptimizedBeatmapCalculator.DEFAULT_KPS_GRAPH_POINTS;

        private readonly LayoutValue pathCached = new LayoutValue(Invalidation.DrawSize);
        private readonly Path path;

        // private readonly BufferedContainer bufferedHost;

        private float[] values;

        public float ActualMaxValue { get; private set; } = float.NaN;
        public float ActualMinValue { get; private set; } = float.NaN;
        public float? MaxValue { get; set; }
        public float? MinValue { get; set; }
        private const double transform_duration = 1000;

        private readonly Container<Path> maskingContainer;
        private int valuesCount;

        public EzDisplayKpsGraph()
        {
            AddInternal(maskingContainer = new Container<Path>
            {
                Masking = true,
                RelativeSizeAxes = Axes.Both,
                Child = path = new SmoothPath
                {
                    AutoSizeAxes = Axes.None,
                    RelativeSizeAxes = Axes.Both,
                    PathRadius = 1.5f,
                    Colour = Colour4.CornflowerBlue
                }
            });

            AddLayout(pathCached);
        }

        public void SetPoints(IReadOnlyList<double> source)
        {
            if (source == null)
                return;

            int count = source.Count;
            valuesCount = count;

            if (count == 0)
            {
                ActualMaxValue = float.NaN;
                ActualMinValue = float.NaN;
                pathCached.Invalidate();
                return;
            }

            if (values == null || values.Length < count)
                values = new float[count];

            float max = float.MinValue;
            float min = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                float v = (float)source[i];
                values[i] = v;
                if (v > max) max = v;
                if (v < min) min = v;
            }

            if (MaxValue > max) max = MaxValue.Value;
            if (MinValue < min) min = MinValue.Value;

            ActualMaxValue = max;
            ActualMinValue = min;

            pathCached.Invalidate();

            maskingContainer.Width = 0;
            maskingContainer.ResizeWidthTo(1, transform_duration, Easing.OutQuint);
        }

        protected override void Update()
        {
            base.Update();

            if (!pathCached.IsValid)
            {
                applyPath();
                pathCached.Validate();
            }
        }

        private void applyPath()
        {
            path.ClearVertices();

            int count = valuesCount;
            if (count <= 0)
                return;

            int totalCount = Math.Max(count, max_draw_points);

            // Use the path's own draw size so vertices are in the path's local space.
            float availableWidth = Math.Max(0, path.DrawWidth - 2 * path.PathRadius);
            float availableHeight = Math.Max(0, path.DrawHeight - 2 * path.PathRadius);

            // Fallback to parent draw size if path hasn't been sized yet.
            if (availableWidth <= 0) availableWidth = Math.Max(0, DrawWidth - 2 * path.PathRadius);
            if (availableHeight <= 0) availableHeight = Math.Max(0, DrawHeight - 2 * path.PathRadius);

            int denom = Math.Max(1, totalCount - 1);

            for (int i = 0; i < count; i++)
            {
                // account for the radius margin so vertices are inset by PathRadius on all sides
                float x = path.PathRadius + (i + totalCount - count) / (float)denom * availableWidth;
                float y = path.PathRadius + GetYPosition(values[i]) * availableHeight;
                path.AddVertex(new Vector2(x, y));
            }
        }

        protected float GetYPosition(float value)
        {
            if (ActualMaxValue == ActualMinValue)
                return value > 1 ? 0 : 1;

            return (ActualMaxValue - value) / (ActualMaxValue - ActualMinValue);
        }
    }
}
