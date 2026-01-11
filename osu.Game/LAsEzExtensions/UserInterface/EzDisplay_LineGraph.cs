// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Layout;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.UserInterface
{
    public partial class EzDisplayLineGraph : Container
    {
        public float? MaxValue { get; set; }

        public float? MinValue { get; set; }

        public float ActualMaxValue { get; private set; } = float.NaN;
        public float ActualMinValue { get; private set; } = float.NaN;

        private const double transform_duration = 1500;

        public int DefaultValueCount;

        private readonly Container<Path> maskingContainer;
        private readonly Path path;

        private float[] values;
        private int valuesCount;

        public Color4 LineColour
        {
            get => maskingContainer.Colour;
            set => maskingContainer.Colour = value;
        }

        public EzDisplayLineGraph()
        {
            Add(maskingContainer = new Container<Path>
            {
                Masking = true,
                RelativeSizeAxes = Axes.Both,
                Child = path = new SmoothPath
                {
                    AutoSizeAxes = Axes.None,
                    RelativeSizeAxes = Axes.Both,
                    PathRadius = 1
                }
            });

            AddLayout(pathCached);
        }

        public void SetValues(IReadOnlyList<double> source)
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

        private readonly LayoutValue pathCached = new LayoutValue(Invalidation.DrawSize);

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

            int totalCount = Math.Max(count, DefaultValueCount);

            for (int i = 0; i < count; i++)
            {
                float x = (i + totalCount - count) / (float)(totalCount - 1) * (DrawWidth - 2 * path.PathRadius);
                float y = GetYPosition(values[i]) * (DrawHeight - 2 * path.PathRadius);
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
