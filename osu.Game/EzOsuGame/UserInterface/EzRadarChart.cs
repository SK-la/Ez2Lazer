// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.Layout;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Triangle = osu.Framework.Graphics.Primitives.Triangle;

namespace osu.Game.EzOsuGame.UserInterface
{
    public partial class EzRadarChart : CompositeDrawable
    {
        private const float triangle_border_thickness = 0.15f;
        private const float triangle_border_texel_size = 0.005f;

        private int axisCount = 6;
        private float[] dataRatios = new float[6];
        private float[]? secondaryDataRatios;

        private readonly EzRadarChartFillLayer fillLayer;
        private readonly Container pointsLayer;
        private readonly List<Circle> primaryDots = new List<Circle>();
        private readonly List<Circle> secondaryDots = new List<Circle>();
        private readonly Container strokeLayer;
        private readonly List<SmoothPath> gridPaths = new List<SmoothPath>();
        private readonly List<SmoothPath> axisPaths = new List<SmoothPath>();
        private readonly SmoothPath outerRingPath;
        private readonly TriangleBorderPath primaryOutline;
        private readonly TriangleBorderPath secondaryOutline;

        private readonly LayoutValue geometryCached = new LayoutValue(Invalidation.DrawSize);

        public int AxisCount
        {
            get => axisCount;
            set
            {
                int clamped = Math.Max(3, value);

                if (axisCount == clamped)
                    return;

                axisCount = clamped;
                Array.Resize(ref dataRatios, axisCount);
                if (secondaryDataRatios != null)
                    Array.Resize(ref secondaryDataRatios, axisCount);
                invalidateGeometry();
            }
        }

        public int GridLevels { get; set; } = 4;

        public float RadiusRatio { get; set; } = 0.82f;

        public float GridThickness { get; set; } = 2.5f;

        public float AxisThickness { get; set; } = 2.5f;

        /// <summary>
        /// 最外圈描边粗细（不受 <see cref="GridThickness"/> 影响）。
        /// </summary>
        public float OuterRingThickness { get; set; } = 2.5f;

        /// <summary>
        /// 最外圈路径中心线相对底色外缘的径向外扩距离（像素）。
        /// <para>推荐值为 <see cref="OuterRingThickness"/> - 1.5f</para>
        /// </summary>
        public float OuterRingRadialOffset { get; set; } = 1f;

        public float DataOutlineThickness { get; set; } = 3.5f;

        public float DataPointSize { get; set; } = 5f;

        /// <summary>
        /// 数据节点圆点相对描边色的不透明度系数。
        /// </summary>
        public float DataPointAlpha { get; set; } = 0.72f;

        public Color4 GridColour { get; set; } = new Color4(255, 255, 210, 110);

        public Color4 AxisColour { get; set; } = new Color4(255, 255, 210, 95);

        public Color4 OuterRingColour { get; set; } = new Color4(255, 255, 255, 95);

        public Color4 BaseFillColour { get; set; } = new Color4(255, 255, 200, 30);

        public Color4 DataFillColour { get; set; } = new Color4(255, 215, 0, 95);

        public Color4 DataStrokeColour { get; set; } = new Color4(255, 230, 128, 230);

        public Color4 DataPointColour { get; set; } = new Color4(255, 242, 176, 255);

        public Color4 SecondaryDataFillColour { get; set; } = new Color4(80, 220, 120, 95);

        public Color4 SecondaryDataStrokeColour { get; set; } = new Color4(80, 220, 120, 230);

        public Color4 SecondaryDataPointColour { get; set; } = new Color4(120, 240, 160, 255);

        public EzRadarChart()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            outerRingPath = createSmoothStrokePath();

            InternalChildren = new Drawable[]
            {
                fillLayer = new EzRadarChartFillLayer(this)
                {
                    RelativeSizeAxes = Axes.Both,
                },
                strokeLayer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Blending = BlendingParameters.Additive,
                    Children = new Drawable[]
                    {
                        primaryOutline = createTriangleBorderStrokePath(),
                        secondaryOutline = createTriangleBorderStrokePath(),
                    }
                },
                pointsLayer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
            };

            AddLayout(geometryCached);
        }

        private static SmoothPath createSmoothStrokePath() => new SmoothPath
        {
            AutoSizeAxes = Axes.None,
            RelativeSizeAxes = Axes.Both,
        };

        private TriangleBorderPath createTriangleBorderStrokePath() => new TriangleBorderPath(triangle_border_thickness, triangle_border_texel_size)
        {
            AutoSizeAxes = Axes.None,
            RelativeSizeAxes = Axes.Both,
        };

        private void invalidateGeometry()
        {
            geometryCached.Invalidate();
            fillLayer.Invalidate(Invalidation.DrawNode);
        }

        public void SetData(IReadOnlyList<float> ratios)
        {
            for (int i = 0; i < axisCount; i++)
                dataRatios[i] = i < ratios.Count ? Math.Clamp(ratios[i], 0, 1) : 0;

            invalidateGeometry();
        }

        public void SetSecondaryData(IReadOnlyList<float> ratios)
        {
            secondaryDataRatios ??= new float[axisCount];
            if (secondaryDataRatios.Length != axisCount)
                Array.Resize(ref secondaryDataRatios, axisCount);

            for (int i = 0; i < axisCount; i++)
                secondaryDataRatios[i] = i < ratios.Count ? Math.Clamp(ratios[i], 0, 1) : 0;

            invalidateGeometry();
        }

        public void ClearSecondaryData()
        {
            secondaryDataRatios = null;
            invalidateGeometry();
        }

        public void InvalidateChartGeometry() => invalidateGeometry();

        protected override void Update()
        {
            base.Update();

            if (!geometryCached.IsValid)
            {
                rebuildStrokeGeometry();
                geometryCached.Validate();
            }
        }

        private void rebuildStrokeGeometry()
        {
            float radius = Math.Min(DrawWidth, DrawHeight) * 0.5f * Math.Clamp(RadiusRatio, 0.1f, 1);
            if (radius <= 0)
                return;

            Vector2 center = DrawSize * 0.5f;
            int levels = Math.Max(1, GridLevels);
            int internalLevels = Math.Max(0, levels - 1);

            ensureSmoothPathPool(gridPaths, internalLevels);
            ensureSmoothPathPool(axisPaths, axisCount);

            strokeLayer.Clear(false);
            for (int i = 0; i < internalLevels; i++)
                strokeLayer.Add(gridPaths[i]);
            strokeLayer.Add(outerRingPath);
            for (int i = 0; i < axisCount; i++)
                strokeLayer.Add(axisPaths[i]);

            var outerVertices = new Vector2[axisCount];
            var outerRingVertices = new Vector2[axisCount];
            var levelVertices = new Vector2[axisCount];
            var primaryVertices = new Vector2[axisCount];
            var secondaryVertices = new Vector2[axisCount];

            fillVerticesUniform(outerVertices, center, radius, 1f);
            fillVerticesUniform(outerRingVertices, center, radius, 1f, Math.Max(0f, OuterRingRadialOffset));

            for (int level = 1; level <= internalLevels; level++)
            {
                float ratio = level / (float)levels;
                fillVerticesUniform(levelVertices, center, radius, ratio);
                updateClosedPath(gridPaths[level - 1], levelVertices, GridColour, GridThickness);
            }

            updateClosedPath(outerRingPath, outerRingVertices, OuterRingColour, OuterRingThickness);

            for (int i = 0; i < axisCount; i++)
                updateOpenPath(axisPaths[i], center, outerVertices[i], AxisColour, AxisThickness);

            fillVerticesFromRatios(primaryVertices, center, radius, dataRatios);

            if (secondaryDataRatios == null)
            {
                updateClosedPath(primaryOutline, primaryVertices, DataStrokeColour, DataOutlineThickness);
                secondaryOutline.Alpha = 0;
                strokeLayer.Add(primaryOutline);
            }
            else
            {
                fillVerticesFromRatios(secondaryVertices, center, radius, secondaryDataRatios);
                secondaryOutline.Alpha = 1;

                bool primaryIsSmaller = averageRatio(dataRatios) <= averageRatio(secondaryDataRatios);

                if (primaryIsSmaller)
                {
                    updateClosedPath(secondaryOutline, secondaryVertices, SecondaryDataStrokeColour, DataOutlineThickness);
                    updateClosedPath(primaryOutline, primaryVertices, DataStrokeColour, DataOutlineThickness);
                    strokeLayer.Add(secondaryOutline);
                    strokeLayer.Add(primaryOutline);
                }
                else
                {
                    updateClosedPath(primaryOutline, primaryVertices, DataStrokeColour, DataOutlineThickness);
                    updateClosedPath(secondaryOutline, secondaryVertices, SecondaryDataStrokeColour, DataOutlineThickness);
                    strokeLayer.Add(primaryOutline);
                    strokeLayer.Add(secondaryOutline);
                }
            }

            rebuildPointGeometry(primaryVertices, secondaryDataRatios == null ? null : secondaryVertices);
        }

        private void rebuildPointGeometry(Vector2[] primaryVertices, Vector2[]? secondaryVertices)
        {
            ensureDotPool(primaryDots, axisCount);
            Color4 primaryDotColour = applyDataPointAlpha(DataPointColour);

            for (int i = 0; i < axisCount; i++)
                applyDot(primaryDots[i], primaryVertices[i], primaryDotColour);

            if (secondaryVertices == null)
            {
                for (int i = 0; i < secondaryDots.Count; i++)
                    secondaryDots[i].Alpha = 0;
                return;
            }

            ensureDotPool(secondaryDots, axisCount);
            Color4 secondaryDotColour = applyDataPointAlpha(SecondaryDataPointColour);

            for (int i = 0; i < axisCount; i++)
                applyDot(secondaryDots[i], secondaryVertices[i], secondaryDotColour);
        }

        private Color4 applyDataPointAlpha(Color4 colour) =>
            new Color4(colour.R, colour.G, colour.B, colour.A * Math.Clamp(DataPointAlpha, 0, 1));

        private void ensureDotPool(List<Circle> pool, int count)
        {
            while (pool.Count < count)
            {
                var dot = new Circle
                {
                    Origin = Anchor.Centre,
                };
                pool.Add(dot);
                pointsLayer.Add(dot);
            }

            for (int i = 0; i < pool.Count; i++)
                pool[i].Alpha = i < count ? 1 : 0;
        }

        private void applyDot(Circle dot, Vector2 position, Color4 colour)
        {
            dot.Position = position;
            dot.Size = new Vector2(DataPointSize);
            dot.Colour = ColourInfo.SingleColour(colour);
            dot.Alpha = 1;
        }

        private void ensureSmoothPathPool(List<SmoothPath> pool, int count)
        {
            while (pool.Count < count)
                pool.Add(createSmoothStrokePath());
        }

        private static void updateClosedPath(Path path, IReadOnlyList<Vector2> polygonVertices, Color4 colour, float thickness)
        {
            path.PathRadius = Math.Max(0.5f, thickness) / 2f;
            path.Colour = ColourInfo.SingleColour(colour);
            path.ClearVertices();

            for (int i = 0; i < polygonVertices.Count; i++)
                path.AddVertex(polygonVertices[i]);

            path.AddVertex(polygonVertices[0]);
        }

        private static void updateOpenPath(Path path, Vector2 start, Vector2 end, Color4 colour, float thickness)
        {
            path.PathRadius = Math.Max(0.5f, thickness) / 2f;
            path.Colour = ColourInfo.SingleColour(colour);
            path.ClearVertices();
            path.AddVertex(start);
            path.AddVertex(end);
        }

        private void fillVerticesUniform(Vector2[] vertices, Vector2 center, float radius, float ratio, float radialOffset = 0f)
        {
            float distance = radius * ratio + radialOffset;

            for (int i = 0; i < axisCount; i++)
            {
                float angle = MathHelper.DegreesToRadians(360f / axisCount * i - 90);
                vertices[i] = new Vector2(
                    center.X + distance * (float)Math.Cos(angle),
                    center.Y + distance * (float)Math.Sin(angle));
            }
        }

        private void fillVerticesFromRatios(Vector2[] vertices, Vector2 center, float radius, IReadOnlyList<float> axisRatios)
        {
            for (int i = 0; i < axisCount; i++)
            {
                float clampedRatio = Math.Clamp(axisRatios[i], 0, 1);
                float angle = MathHelper.DegreesToRadians(360f / axisCount * i - 90);
                vertices[i] = new Vector2(
                    center.X + radius * clampedRatio * (float)Math.Cos(angle),
                    center.Y + radius * clampedRatio * (float)Math.Sin(angle));
            }
        }

        private static float averageRatio(IReadOnlyList<float> values)
        {
            if (values.Count == 0)
                return 0;

            float sum = 0;

            for (int i = 0; i < values.Count; i++)
                sum += Math.Clamp(values[i], 0, 1);

            return sum / values.Count;
        }

        private partial class EzRadarChartFillLayer : Drawable
        {
            private readonly EzRadarChart chart;
            private Texture? whitePixel;

            public EzRadarChartFillLayer(EzRadarChart chart)
            {
                this.chart = chart;
            }

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer)
            {
                whitePixel = createWhitePixelTexture(renderer);
            }

            private static Texture createWhitePixelTexture(IRenderer renderer)
            {
                var texture = renderer.CreateTexture(1, 1, true);
                var image = new Image<Rgba32>(1, 1);
                image[0, 0] = new Rgba32(255, 255, 255, 255);
                texture.SetData(new TextureUpload(image));
                return texture;
            }

            protected override void Dispose(bool isDisposing)
            {
                if (isDisposing)
                    whitePixel?.Dispose();

                base.Dispose(isDisposing);
            }

            protected override DrawNode CreateDrawNode() => new EzRadarChartFillDrawNode(this);

            private class EzRadarChartFillDrawNode : DrawNode
            {
                private readonly EzRadarChartFillLayer source;

                private float[] ratios = Array.Empty<float>();
                private float[]? secondaryRatios;
                private int axisCount;
                private float radiusRatio;
                private Color4 baseFillColour;
                private Color4 dataFillColour;
                private Color4 secondaryDataFillColour;
                private Vector2 drawSize;
                private Texture? texture;

                private Vector2[] outerVertices = Array.Empty<Vector2>();
                private Vector2[] primaryVertices = Array.Empty<Vector2>();
                private Vector2[] secondaryVertices = Array.Empty<Vector2>();

                public EzRadarChartFillDrawNode(EzRadarChartFillLayer layer)
                    : base(layer)
                {
                    source = layer;
                }

                public override void ApplyState()
                {
                    base.ApplyState();

                    texture = source.whitePixel;
                    var chart = source.chart;

                    drawSize = source.DrawSize;
                    axisCount = chart.axisCount;
                    radiusRatio = Math.Clamp(chart.RadiusRatio, 0.1f, 1);
                    baseFillColour = chart.BaseFillColour;
                    dataFillColour = chart.DataFillColour;
                    secondaryDataFillColour = chart.SecondaryDataFillColour;

                    if (ratios.Length != axisCount)
                        Array.Resize(ref ratios, axisCount);

                    ensureVertexCapacity(ref outerVertices, axisCount);
                    ensureVertexCapacity(ref primaryVertices, axisCount);
                    ensureVertexCapacity(ref secondaryVertices, axisCount);

                    for (int i = 0; i < axisCount; i++)
                        ratios[i] = chart.dataRatios[i];

                    if (chart.secondaryDataRatios == null)
                        secondaryRatios = null;
                    else
                    {
                        secondaryRatios ??= new float[axisCount];
                        if (secondaryRatios.Length != axisCount)
                            Array.Resize(ref secondaryRatios, axisCount);

                        for (int i = 0; i < axisCount; i++)
                            secondaryRatios[i] = chart.secondaryDataRatios[i];
                    }
                }

                protected override void Draw(IRenderer renderer)
                {
                    if (texture == null)
                        return;

                    float radius = Math.Min(drawSize.X, drawSize.Y) * 0.5f * radiusRatio;
                    if (radius <= 0)
                        return;

                    Vector2 center = drawSize * 0.5f;

                    renderer.PushLocalMatrix(DrawInfo.Matrix);

                    fillVerticesUniform(outerVertices, center, radius, 1, axisCount);
                    drawPolygonFill(renderer, outerVertices, baseFillColour);

                    fillVerticesFromRatios(primaryVertices, center, radius, ratios, axisCount);

                    if (secondaryRatios == null)
                    {
                        drawFanFill(renderer, center, primaryVertices, dataFillColour);
                    }
                    else
                    {
                        fillVerticesFromRatios(secondaryVertices, center, radius, secondaryRatios, axisCount);
                        bool primaryIsSmaller = averageRatio(ratios) <= averageRatio(secondaryRatios);

                        if (primaryIsSmaller)
                        {
                            drawFanFill(renderer, center, secondaryVertices, secondaryDataFillColour);
                            drawFanFill(renderer, center, primaryVertices, dataFillColour);
                        }
                        else
                        {
                            drawFanFill(renderer, center, primaryVertices, dataFillColour);
                            drawFanFill(renderer, center, secondaryVertices, secondaryDataFillColour);
                        }
                    }

                    renderer.PopLocalMatrix();
                }

                private static void ensureVertexCapacity(ref Vector2[] buffer, int count)
                {
                    if (buffer.Length != count)
                        Array.Resize(ref buffer, count);
                }

                private static void fillVerticesUniform(Vector2[] vertices, Vector2 center, float radius, float ratio, int axisCount)
                {
                    for (int i = 0; i < axisCount; i++)
                    {
                        float angle = MathHelper.DegreesToRadians(360f / axisCount * i - 90);
                        vertices[i] = new Vector2(
                            center.X + radius * ratio * (float)Math.Cos(angle),
                            center.Y + radius * ratio * (float)Math.Sin(angle));
                    }
                }

                private void drawPolygonFill(IRenderer renderer, IReadOnlyList<Vector2> polygonVertices, Color4 colour)
                {
                    for (int i = 1; i < polygonVertices.Count - 1; i++)
                    {
                        renderer.DrawTriangle(
                            texture!,
                            new Triangle(
                                polygonVertices[0],
                                polygonVertices[i],
                                polygonVertices[i + 1]),
                            colour);
                    }
                }

                private static void fillVerticesFromRatios(Vector2[] vertices, Vector2 center, float radius, IReadOnlyList<float> axisRatios, int axisCount)
                {
                    for (int i = 0; i < axisCount; i++)
                    {
                        float clampedRatio = Math.Clamp(axisRatios[i], 0, 1);
                        float angle = MathHelper.DegreesToRadians(360f / axisCount * i - 90);
                        vertices[i] = new Vector2(
                            center.X + radius * clampedRatio * (float)Math.Cos(angle),
                            center.Y + radius * clampedRatio * (float)Math.Sin(angle));
                    }
                }

                private void drawFanFill(IRenderer renderer, Vector2 center, IReadOnlyList<Vector2> polygonVertices, Color4 colour)
                {
                    for (int i = 0; i < polygonVertices.Count; i++)
                    {
                        renderer.DrawTriangle(
                            texture!,
                            new Triangle(
                                center,
                                polygonVertices[i],
                                polygonVertices[(i + 1) % polygonVertices.Count]),
                            colour);
                    }
                }
            }
        }
    }
}
