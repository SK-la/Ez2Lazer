// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// KPS折线图
    /// </summary>
    /// 支持可选的尾部补零与热力颜色渲染，默认关闭。
    public partial class EzDisplayKpsGraph : CompositeDrawable
    {
        private const float line_thickness = 1.5f;
        private const int max_display_points = 128;

        private readonly KpsLineDrawable graphDrawable;

        private float[] values;

        public float ActualMaxValue { get; private set; } = float.NaN;
        public float ActualMinValue { get; private set; } = float.NaN;
        public float? MaxValue { get; set; }
        public float? MinValue { get; set; }

        private int valuesCount;
        private bool hasData;
        private bool lastExtendToBaseline;
        private bool lastHeatmapEnabled;
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

        public EzDisplayKpsGraph()
        {
            AddInternal(new Container
            {
                Masking = true,
                RelativeSizeAxes = Axes.Both,
                Child = graphDrawable = new KpsLineDrawable
                {
                    RelativeSizeAxes = Axes.Both,
                }
            });
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

        public void SetPoints(IReadOnlyList<double> source, double sourceLengthMs = 0, double baselineLengthMs = 0, bool extendToBaseline = false, bool heatmapEnabled = false)
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

                // maskingContainer.ClearTransforms();
                // maskingContainer.Width = 1;
                graphDrawable.Clear();
                lastExtendToBaseline = extendToBaseline;
                lastHeatmapEnabled = heatmapEnabled;
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
            bool same = hasData && valuesCount == sampledCount && lastExtendToBaseline == extendToBaseline && lastHeatmapEnabled == heatmapEnabled;

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
            lastHeatmapEnabled = heatmapEnabled;
            lastSourceLengthMs = sourceLengthMs;
            lastBaselineLengthMs = baselineLengthMs;

            if (same)
                return;

            graphDrawable.SetValues(values, valuesCount, ActualMinValue, ActualMaxValue, line_thickness, heatmapEnabled);

            // // 直接显示完整图表，避免从左到右的展开动画。
            // maskingContainer.ClearTransforms();
            // maskingContainer.Width = 1;
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
            var graphLocal = graphDrawable.ToLocalSpace(screenSpaceMousePos);

            float availableWidth = Math.Max(0, graphDrawable.DrawWidth);
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

        private partial class KpsLineDrawable : Drawable
        {
            private readonly List<float> points = new List<float>();
            private float minValue;
            private float maxValue;
            private float thickness;
            private bool heatmapEnabled;
            private long version;

            private Texture texture = null!;
            private IShader shader = null!;

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer, ShaderManager shaders)
            {
                texture = renderer.WhitePixel;
                shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
            }

            public void SetValues(float[] source, int count, float minValue, float maxValue, float thickness, bool heatmapEnabled)
            {
                points.Clear();

                for (int i = 0; i < count; i++)
                    points.Add(source[i]);

                this.minValue = minValue;
                this.maxValue = maxValue;
                this.thickness = thickness;
                this.heatmapEnabled = heatmapEnabled;
                version++;

                Invalidate(Invalidation.DrawNode);
            }

            public void Clear()
            {
                if (points.Count == 0)
                    return;

                points.Clear();
                version++;
                Invalidate(Invalidation.DrawNode);
            }

            protected override DrawNode CreateDrawNode() => new KpsLineDrawNode(this);

            private class KpsLineDrawNode : DrawNode
            {
                private Texture texture = null!;
                private IShader shader = null!;
                private readonly List<float> points = new List<float>();
                private Vector2 drawSize;
                private float minValue;
                private float maxValue;
                private float thickness;
                private bool heatmapEnabled;
                private long version = -1;

                private IVertexBatch<TexturedVertex2D> quadBatch;

                protected new KpsLineDrawable Source => (KpsLineDrawable)base.Source;

                public KpsLineDrawNode(KpsLineDrawable source)
                    : base(source)
                {
                }

                public override void ApplyState()
                {
                    base.ApplyState();

                    texture = Source.texture;
                    shader = Source.shader;
                    drawSize = Source.DrawSize;
                    minValue = Source.minValue;
                    maxValue = Source.maxValue;
                    thickness = Source.thickness;
                    heatmapEnabled = Source.heatmapEnabled;

                    if (version == Source.version)
                        return;

                    points.Clear();
                    points.AddRange(Source.points);
                    version = Source.version;
                }

                protected override void Draw(IRenderer renderer)
                {
                    base.Draw(renderer);

                    if (points.Count < 2 || !texture.Available)
                        return;

                    shader.Bind();

                    if (!renderer.BindTexture(texture))
                        return;

                    quadBatch ??= renderer.CreateQuadBatch<TexturedVertex2D>(Math.Min(points.Count - 1, 1024), 4);

                    renderer.PushLocalMatrix(DrawInfo.Matrix);

                    RectangleF textureRect = texture.GetTextureRect();
                    Vector4 textureRectangle = new Vector4(0, 0, 1, 1);
                    Vector2 blendRange = Vector2.One;
                    var add = quadBatch.AddAction;

                    float halfThickness = thickness / 2f;
                    int denominator = Math.Max(1, points.Count - 1);

                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        Vector2 start = new Vector2(i / (float)denominator * drawSize.X, getYPosition(points[i]) * drawSize.Y);
                        Vector2 end = new Vector2((i + 1) / (float)denominator * drawSize.X, getYPosition(points[i + 1]) * drawSize.Y);
                        Colour4 startColour = heatmapEnabled ? getHeatColour(points[i]) : Colour4.CornflowerBlue;
                        Colour4 endColour = heatmapEnabled ? getHeatColour(points[i + 1]) : Colour4.CornflowerBlue;

                        Vector2 direction = end - start;
                        float length = direction.Length;

                        if (length <= 0)
                            continue;

                        direction /= length;
                        Vector2 extension = direction * halfThickness;
                        Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * halfThickness;

                        Vector2 topLeft = start - extension - perpendicular;
                        Vector2 topRight = end + extension - perpendicular;
                        Vector2 bottomRight = end + extension + perpendicular;
                        Vector2 bottomLeft = start - extension + perpendicular;

                        add(new TexturedVertex2D(renderer)
                        {
                            Position = topLeft,
                            TexturePosition = textureRect.TopLeft,
                            TextureRect = textureRectangle,
                            BlendRange = blendRange,
                            Colour = startColour
                        });
                        add(new TexturedVertex2D(renderer)
                        {
                            Position = topRight,
                            TexturePosition = textureRect.TopRight,
                            TextureRect = textureRectangle,
                            BlendRange = blendRange,
                            Colour = endColour
                        });
                        add(new TexturedVertex2D(renderer)
                        {
                            Position = bottomRight,
                            TexturePosition = textureRect.BottomRight,
                            TextureRect = textureRectangle,
                            BlendRange = blendRange,
                            Colour = endColour
                        });
                        add(new TexturedVertex2D(renderer)
                        {
                            Position = bottomLeft,
                            TexturePosition = textureRect.BottomLeft,
                            TextureRect = textureRectangle,
                            BlendRange = blendRange,
                            Colour = startColour
                        });
                    }

                    quadBatch.Draw();
                    renderer.PopLocalMatrix();
                    shader.Unbind();
                }

                private float getYPosition(float value)
                {
                    if (maxValue == minValue)
                        return value > 1 ? 0 : 1;

                    return (maxValue - value) / (maxValue - minValue);
                }

                private Colour4 getHeatColour(float value)
                {
                    if (value <= 0 || maxValue <= 0)
                        return Colour4.White;

                    float t = Math.Clamp(value / maxValue, 0, 1);
                    return new Colour4(1f, 1f - t, 1f - t, 1f);
                }

                protected override void Dispose(bool isDisposing)
                {
                    base.Dispose(isDisposing);
                    quadBatch?.Dispose();
                }
            }
        }
    }
}
