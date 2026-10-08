// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Layout;
using osu.Game.EzOsuGame.Acrylic;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// KPS折线图
    /// </summary>
    /// 支持可选的尾部补零，默认单色 TriangleBorder 折线。
    public partial class EzDisplayKpsGraph : CompositeDrawable
    {
        private const float line_thickness = 1.5f;
        private const float border_thickness = 0.15f;
        private const float border_texel_size = 0.005f;
        private const int max_display_points = 128;

        private readonly Container graphColourContainer;
        private readonly TriangleBorderPath graphPath;

        private float[] values;
        private Color4 lineColour = Colour4.CornflowerBlue;

        public float ActualMaxValue { get; private set; } = float.NaN;
        public float ActualMinValue { get; private set; } = float.NaN;
        public float? MaxValue { get; set; }
        public float? MinValue { get; set; }

        public Color4 LineColour
        {
            get => lineColour;
            set
            {
                if (lineColour == value)
                    return;

                lineColour = value;
                graphColourContainer.Colour = ColourInfo.SingleColour(value);
            }
        }

        private int valuesCount;
        private bool hasData;
        private bool lastExtendToBaseline;
        private double lastSourceLengthMs;
        private double lastBaselineLengthMs;

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

        private readonly LayoutValue pathCached = new LayoutValue(Invalidation.DrawSize);

        /// <summary>
        /// 不参与父级 AutoSize（用于谱面 Panel 等紧凑布局：折线叠在指标行右侧，不撑开上下行）。
        /// </summary>
        public bool ExcludeFromParentAutoSize
        {
            get => BypassAutoSizeAxes == Axes.Both;
            set => BypassAutoSizeAxes = value ? Axes.Both : Axes.None;
        }

        /// <summary>
        /// 与 <see cref="ExcludeFromParentAutoSize"/> 配合：折线叠在该行右侧并随其尺寸更新。
        /// </summary>
        public Drawable OverlayAnchorRow { get; set; }

        public float OverlayLeftMargin { get; set; } = 4f;

        /// <summary>
        /// 将折线叠在已排版指标行右侧（需 <see cref="ExcludeFromParentAutoSize"/>）。
        /// </summary>
        public void AlignBesideRow(Drawable row, float leftMargin = 4f)
        {
            float y = (row.DrawHeight - DrawHeight) * 0.5f;
            Position = new Vector2(row.DrawWidth + leftMargin, y);
        }

        public EzDisplayKpsGraph()
        {
            Blending = BlendingParameters.Additive;

            AddInternal(graphColourContainer = new Container
            {
                Masking = true,
                RelativeSizeAxes = Axes.Both,
                Colour = ColourInfo.SingleColour(lineColour),
                Child = graphPath = new TriangleBorderPath(border_thickness, border_texel_size)
                {
                    AutoSizeAxes = Axes.None,
                    RelativeSizeAxes = Axes.Both,
                    PathRadius = line_thickness / 2f,
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
                        CornerRadius = EzAcrylicStyle.CORNER_RADIUS_DISPLAY,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Colour4.Black, Alpha = 0.35f
                            },
                            hoverText = new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                                Margin = new MarginPadding(4),
                                Colour = Colour4.White,
                            }
                        }
                    }
                }
            };

            AddInternal(hoverRoot);
            hoverCreated = true;
        }

        public void SetPoints(IReadOnlyList<double> source, double sourceLengthMs = 0, double baselineLengthMs = 0, bool extendToBaseline = false)
        {
            if (source == null)
                return;

            int count = source.Count;

            if (count == 0)
            {
                // 空数据时仅在有旧数据的情况下做一次清理，避免高频重复无效更新。
                if (!hasData && valuesCount == 0)
                    return;

                hasData = false;
                valuesCount = 0;
                ActualMaxValue = float.NaN;
                ActualMinValue = float.NaN;

                graphPath.ClearVertices();
                pathCached.Invalidate();
                lastExtendToBaseline = extendToBaseline;
                lastSourceLengthMs = sourceLengthMs;
                lastBaselineLengthMs = baselineLengthMs;
                return;
            }

            int effectiveCount = count;

            if (extendToBaseline && sourceLengthMs > 0 && baselineLengthMs > sourceLengthMs)
            {
                double expandedCount = Math.Ceiling(count * baselineLengthMs / sourceLengthMs);

                if (expandedCount > int.MaxValue)
                    effectiveCount = int.MaxValue;
                else
                    effectiveCount = Math.Max(count, (int)expandedCount);
            }

            int sampledCount = Math.Min(effectiveCount, max_display_points);

            if (values == null || values.Length < sampledCount)
                values = new float[sampledCount];

            float max = float.MinValue;
            float min = float.MaxValue;
            bool same = hasData && valuesCount == sampledCount && lastExtendToBaseline == extendToBaseline;

            if (same && extendToBaseline)
                same = lastSourceLengthMs == sourceLengthMs && lastBaselineLengthMs == baselineLengthMs;

            for (int i = 0; i < sampledCount; i++)
            {
                int sourceIndex = getSourceIndex(i, sampledCount, effectiveCount);
                float v = sourceIndex < count ? (float)source[sourceIndex] : 0;

                if (same && values[i] != v)
                    same = false;

                values[i] = v;
                if (v > max) max = v;
                if (v < min) min = v;
            }

            if (MaxValue > max) max = MaxValue.Value;
            if (MinValue < min) min = MinValue.Value;

            ActualMaxValue = max;
            ActualMinValue = min;

            hasData = true;
            valuesCount = sampledCount;
            lastExtendToBaseline = extendToBaseline;
            lastSourceLengthMs = sourceLengthMs;
            lastBaselineLengthMs = baselineLengthMs;

            if (same)
                return;

            pathCached.Invalidate();
        }

        protected override void Update()
        {
            base.Update();

            if (!pathCached.IsValid)
            {
                applyPath();
                pathCached.Validate();
            }

            if (OverlayAnchorRow != null && ExcludeFromParentAutoSize)
                AlignBesideRow(OverlayAnchorRow, OverlayLeftMargin);
        }

        private void applyPath()
        {
            graphPath.ClearVertices();

            if (valuesCount < 2)
                return;

            float inset = 2 * graphPath.PathRadius;
            float availableWidth = Math.Max(0, graphPath.DrawWidth - inset);
            float availableHeight = Math.Max(0, graphPath.DrawHeight - inset);
            int denominator = Math.Max(1, valuesCount - 1);

            for (int i = 0; i < valuesCount; i++)
            {
                float x = i / (float)denominator * availableWidth;
                float y = getYPosition(values[i]) * availableHeight;
                graphPath.AddVertex(new Vector2(x, y));
            }
        }

        private static int getSourceIndex(int index, int sampledCount, int sourceCount)
        {
            if (sampledCount <= 1 || sourceCount <= 1)
                return 0;

            return (int)Math.Clamp(MathF.Round(index / (float)(sampledCount - 1) * (sourceCount - 1)), 0, sourceCount - 1);
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

            // 将屏幕空间坐标转换到图表容器的本地坐标，以匹配当前线段布局。
            var thisLocal = ToLocalSpace(screenSpaceMousePos);
            var graphLocal = graphColourContainer.ToLocalSpace(screenSpaceMousePos);

            float availableWidth = Math.Max(0, graphColourContainer.DrawWidth);
            if (availableWidth <= 0) availableWidth = Math.Max(0, DrawWidth);

            float xInAvailable = Math.Clamp(graphLocal.X, 0, availableWidth);

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

        private static float getYPosition(float value, float minValue, float maxValue)
        {
            if (maxValue == minValue)
                return value > 1 ? 0 : 1;

            return (maxValue - value) / (maxValue - minValue);
        }

        private float getYPosition(float value) => getYPosition(value, ActualMinValue, ActualMaxValue);
    }
}
