// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Layout;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.EzOsuGame.Analysis;
using osuTK;

namespace osu.Game.EzOsuGame.UserInterface
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

        /// <summary>
        /// 是否启用悬浮显示当前横坐标的 KPS 值（仅在外部显式开启时创建相关容器）。
        /// </summary>
        public bool HoverValueEnabled
        {
            get => hoverValueEnabled;
            set
            {
                if (value == hoverValueEnabled) return;

                hoverValueEnabled = value;
                if (hoverValueEnabled)
                    Schedule(createHoverContainers);
            }
        }

        private bool hoverValueEnabled;

        // hover 相关控件（仅在启用时创建）
        private Container hoverRoot = null!;
        private Container hoverLabel = null!;
        private OsuSpriteText hoverText = null!;
        private bool hoverCreated;

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

        private void createHoverContainers()
        {
            if (hoverCreated) return;

            hoverRoot = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                Children = new Drawable[]
                {
                    hoverLabel = new Container
                    {
                        AutoSizeAxes = Axes.Both,
                        Origin = Anchor.TopCentre,
                        Position = new Vector2(0, -4),
                        Masking = true,
                        CornerRadius = 4,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.Black, Alpha = 0.8f },
                            hoverText = new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                                Colour = Colour4.White,
                            }
                        }
                    }
                }
            };

            AddInternal(hoverRoot);
            hoverCreated = true;
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

        protected override bool OnHover(HoverEvent e)
        {
            if (!HoverValueEnabled)
                return base.OnHover(e);

            if (valuesCount <= 0)
                return base.OnHover(e);

            if (!hoverCreated)
                createHoverContainers();

            updateHover(e.ScreenSpaceMousePosition);
            hoverRoot.FadeIn(100, Easing.Out);
            return true;
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            if (HoverValueEnabled && hoverCreated && IsHovered)
            {
                updateHover(e.ScreenSpaceMousePosition);
                return true;
            }

            return base.OnMouseMove(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            if (hoverCreated)
                hoverRoot.FadeOut(100, Easing.Out);

            base.OnHoverLost(e);
        }

        private void updateHover(Vector2 screenSpaceMousePos)
        {
            if (!hoverCreated) return;

            if (valuesCount <= 0)
            {
                hoverText.Text = string.Empty;
                return;
            }

            // 将屏幕空间坐标转换到 path 的本地坐标，以考虑 path 的实际绘制宽度与 PathRadius
            var thisLocal = ToLocalSpace(screenSpaceMousePos);
            var pathLocal = path.ToLocalSpace(screenSpaceMousePos);

            float availableWidth = Math.Max(0, path.DrawWidth - 2 * path.PathRadius);
            if (availableWidth <= 0) availableWidth = Math.Max(0, DrawWidth - 2 * path.PathRadius);

            float xInAvailable = Math.Clamp(pathLocal.X - path.PathRadius, 0, availableWidth);

            int denom = Math.Max(1, valuesCount - 1);
            int index = 0;
            if (availableWidth > 0)
                index = (int)Math.Clamp(MathF.Round(xInAvailable / availableWidth * denom), 0, valuesCount - 1);

            float value = values[index];
            hoverText.Text = value.ToString("0.##");

            // 将标签置于当前控件的本地 X 位置，并略微抬高于图表顶部
            hoverLabel.X = Math.Clamp(thisLocal.X, 0, DrawWidth);
            hoverLabel.Y = -(hoverLabel.DrawHeight + 4);
        }

        private void applyPath()
        {
            path.ClearVertices();

            int count = valuesCount;
            if (count <= 0)
                return;

            // Use the path's own draw size so vertices are in the path's local space.
            float availableWidth = Math.Max(0, path.DrawWidth - 2 * path.PathRadius);
            float availableHeight = Math.Max(0, path.DrawHeight - 2 * path.PathRadius);

            // Fallback to parent draw size if path hasn't been sized yet.
            if (availableWidth <= 0) availableWidth = Math.Max(0, DrawWidth - 2 * path.PathRadius);
            if (availableHeight <= 0) availableHeight = Math.Max(0, DrawHeight - 2 * path.PathRadius);

            // Map points across the actual number of values so the graph fills the container.
            int denom = Math.Max(1, count - 1);

            for (int i = 0; i < count; i++)
            {
                // account for the radius margin so vertices are inset by PathRadius on all sides
                float x = path.PathRadius + (count == 1 ? 0f : i / (float)denom * availableWidth);
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
