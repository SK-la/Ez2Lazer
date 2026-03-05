// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.LAsEzExtensions.Localization;
using osu.Game.LAsEzExtensions.Screens;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Triangle = osu.Framework.Graphics.Primitives.Triangle;

namespace osu.Game.LAsEzExtensions.HUD
{
    /// <summary>
    /// 雷达图面板，显示当前谱面参数的六边形图形化表示
    /// </summary>
    public partial class EzComRadarPanel : CompositeDrawable, ISerialisableDrawable
    {
        private const float axis_label_padding = 52f;
        private const float axis_label_offset = 16f;
        private const float axis_label_alpha = 1f;

        private float[] parameterRatios = new float[6];
        private float[] parameterValues = new float[6];

        private static readonly string[] default_axis_labels = { "BPM", "STAR", "CS", "OD", "HP", "AR" };

        public bool UsesFixedAnchor { get; set; }

        public float MaxBpm { get; set; } = 200f;

        public float MaxStar { get; set; } = 10f;

        public float MaxCs { get; set; } = 10f;

        public float MaxOd { get; set; } = 10f;

        public float MaxDr { get; set; } = 10f;

        public float MaxAr { get; set; } = 10f;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_BASE_LINE_COLOUR), nameof(EzHUDStrings.RADAR_BASE_LINE_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 BaseLineColour { get; } = new BindableColour4(new Color4(255, 255, 210, 230));

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_BASE_AREA_COLOUR), nameof(EzHUDStrings.RADAR_BASE_AREA_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 BaseAreaColour { get; } = new BindableColour4(new Color4(255, 255, 200, 128));

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_DATA_LINE_COLOUR), nameof(EzHUDStrings.RADAR_DATA_LINE_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 DataLineColour { get; } = new BindableColour4(new Color4(255, 230, 128, 230));

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.RADAR_DATA_AREA_COLOUR), nameof(EzHUDStrings.RADAR_DATA_AREA_COLOUR_TOOLTIP), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 DataAreaColour { get; } = new BindableColour4(new Color4(255, 215, 0, 128));

        public int AxisCount
        {
            get => chart?.AxisCount ?? parameterRatios.Length;
            set
            {
                int clamped = Math.Max(3, value);

                if (parameterRatios.Length != clamped)
                {
                    Array.Resize(ref parameterRatios, clamped);
                    Array.Resize(ref parameterValues, clamped);
                }

                if (chart != null)
                {
                    chart.AxisCount = clamped;
                    chart.SetData(parameterRatios);
                }

                ensureAxisTexts();
                updateAxisTexts();
            }
        }

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        private IBindable<StarDifficulty>? difficultyBindable;
        private CancellationTokenSource? difficultyCancellationSource;
        private ModSettingChangeTracker? modSettingTracker;

        private Container? axisLabelContainer;
        private RadarChart? chart;
        private OsuSpriteText[] axisTexts = Array.Empty<OsuSpriteText>();

        public EzComRadarPanel()
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                chart = new RadarChart
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AxisCount = parameterRatios.Length,
                    Size = new Vector2(220),
                    GridLevels = 4,
                    GridColour = BaseLineColour.Value,
                    AxisColour = BaseLineColour.Value,
                    BaseFillColour = BaseAreaColour.Value,
                    DataFillColour = DataAreaColour.Value,
                    DataStrokeColour = DataLineColour.Value,
                    DataPointColour = DataLineColour.Value,
                },
                axisLabelContainer = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(220 + axis_label_padding * 2),
                },
            };

            ensureAxisTexts();
            updateAxisTexts();
            applyChartColours();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            bindPreserveAlpha(BaseLineColour);
            bindPreserveAlpha(BaseAreaColour);
            bindPreserveAlpha(DataLineColour);
            bindPreserveAlpha(DataAreaColour);

            BaseLineColour.BindValueChanged(_ => applyChartColours(), true);
            BaseAreaColour.BindValueChanged(_ => applyChartColours(), true);
            DataLineColour.BindValueChanged(_ => applyChartColours(), true);
            DataAreaColour.BindValueChanged(_ => applyChartColours(), true);

            chart?.SetData(parameterRatios);
            updateAxisTexts();

            beatmap.BindValueChanged(b =>
            {
                difficultyCancellationSource?.Cancel();
                difficultyCancellationSource = new CancellationTokenSource();

                difficultyBindable?.UnbindAll();
                difficultyBindable = difficultyCache.GetBindableDifficulty(b.NewValue.BeatmapInfo, difficultyCancellationSource.Token);
                difficultyBindable.BindValueChanged(d =>
                {
                    updateParameterRatios(d.NewValue);
                }, true);
            }, true);

            mods.BindValueChanged(m =>
            {
                modSettingTracker?.Dispose();
                modSettingTracker = new ModSettingChangeTracker(m.NewValue)
                {
                    SettingChanged = _ => updateParameterRatios(difficultyBindable?.Value ?? default)
                };
                updateParameterRatios(difficultyBindable?.Value ?? default);
            }, true);

            ruleset.BindValueChanged(_ => updateParameterRatios(difficultyBindable?.Value ?? default), true);
        }

        private static void bindPreserveAlpha(BindableColour4 colourBindable)
        {
            colourBindable.BindValueChanged(e =>
            {
                bool rgbChanged =
                    !nearlyEqual(e.OldValue.R, e.NewValue.R) ||
                    !nearlyEqual(e.OldValue.G, e.NewValue.G) ||
                    !nearlyEqual(e.OldValue.B, e.NewValue.B);

                bool alphaChanged = !nearlyEqual(e.OldValue.A, e.NewValue.A);

                if (!rgbChanged || !alphaChanged)
                    return;

                colourBindable.Value = new Color4(e.NewValue.R, e.NewValue.G, e.NewValue.B, e.OldValue.A);
            });
        }

        private static bool nearlyEqual(float x, float y) => Math.Abs(x - y) < 0.0001f;

        private void updateParameterRatios(StarDifficulty difficulty)
        {
            var beatmapInfo = beatmap.Value.BeatmapInfo;

            if (parameterRatios.Length > 0)
            {
                parameterValues[0] = (float)beatmapInfo.BPM;
                parameterRatios[0] = normalise(parameterValues[0], MaxBpm);
            }

            if (parameterRatios.Length > 1)
            {
                parameterValues[1] = (float)(difficulty.Stars > 0 ? difficulty.Stars : beatmapInfo.StarRating);
                parameterRatios[1] = normalise(parameterValues[1], MaxStar);
            }

            if (parameterRatios.Length > 2)
            {
                parameterValues[2] = beatmapInfo.Difficulty.CircleSize;
                parameterRatios[2] = normalise(parameterValues[2], MaxCs);
            }

            if (parameterRatios.Length > 3)
            {
                parameterValues[3] = beatmapInfo.Difficulty.OverallDifficulty;
                parameterRatios[3] = normalise(parameterValues[3], MaxOd);
            }

            if (parameterRatios.Length > 4)
            {
                parameterValues[4] = beatmapInfo.Difficulty.DrainRate;
                parameterRatios[4] = normalise(parameterValues[4], MaxDr);
            }

            if (parameterRatios.Length > 5)
            {
                parameterValues[5] = beatmapInfo.Difficulty.ApproachRate;
                parameterRatios[5] = normalise(parameterValues[5], MaxAr);
            }

            chart?.SetData(parameterRatios);
            updateAxisTexts();
        }

        private static float normalise(float value, float maxValue)
        {
            if (maxValue <= 0)
                return 0;

            return Math.Clamp(value / maxValue, 0, 1);
        }

        private void applyChartColours()
        {
            if (chart == null)
                return;

            chart.GridColour = BaseLineColour.Value;
            chart.AxisColour = BaseLineColour.Value;
            chart.BaseFillColour = BaseAreaColour.Value;
            chart.DataStrokeColour = DataLineColour.Value;
            chart.DataPointColour = DataLineColour.Value;
            chart.DataFillColour = DataAreaColour.Value;
            chart.Invalidate(Invalidation.DrawNode);

            foreach (var text in axisTexts)
                text.Colour = withFixedAlpha(DataLineColour.Value);
        }

        private void ensureAxisTexts()
        {
            if (axisLabelContainer == null || chart == null)
                return;

            int axisCount = chart.AxisCount;
            if (axisTexts.Length == axisCount)
                return;

            axisTexts = new OsuSpriteText[axisCount];
            Drawable[] drawables = new Drawable[axisCount];

            for (int i = 0; i < axisCount; i++)
            {
                axisTexts[i] = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = withFixedAlpha(DataLineColour.Value),
                };

                drawables[i] = axisTexts[i];
            }

            axisLabelContainer.Clear();

            foreach (var drawable in drawables)
                axisLabelContainer.Add(drawable);
        }

        private void updateAxisTexts()
        {
            if (axisTexts.Length == 0 || chart == null || axisLabelContainer == null)
                return;

            axisLabelContainer.Size = chart.Size + new Vector2(axis_label_padding * 2);

            float radius = Math.Min(chart.Size.X, chart.Size.Y) * 0.5f * chart.RadiusRatio;
            int axisCount = Math.Max(3, chart.AxisCount);

            for (int i = 0; i < axisCount && i < axisTexts.Length; i++)
            {
                float angle = MathHelper.DegreesToRadians(360f / axisCount * i - 90);
                Vector2 direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                Vector2 position = direction * (radius + axis_label_offset);

                axisTexts[i].Position = position;
                axisTexts[i].Text = $"{getAxisLabel(i)}\n{formatAxisValue(i)}";
            }
        }

        private string getAxisLabel(int index)
        {
            return index < default_axis_labels.Length ? default_axis_labels[index] : $"P{index + 1}";
        }

        private string formatAxisValue(int index)
        {
            if (index >= parameterValues.Length)
                return "0";

            float value = parameterValues[index];

            return index switch
            {
                0 => value.ToString("0"),
                1 => value.ToString("0.00"),
                _ => value.ToString("0.0"),
            };
        }

        private static Color4 withFixedAlpha(Color4 colour) => new Color4(colour.R, colour.G, colour.B, axis_label_alpha);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            difficultyCancellationSource?.Cancel();
            difficultyBindable?.UnbindAll();
            modSettingTracker?.Dispose();
        }
    }

    public partial class RadarChart : Drawable
    {
        private int axisCount = 6;
        private float[] dataRatios = new float[6];
        private Texture? whitePixel;

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
                Invalidate(Invalidation.DrawNode);
            }
        }

        public int GridLevels { get; set; } = 4;

        public float RadiusRatio { get; set; } = 0.82f;

        public float GridThickness { get; set; } = 1.5f;

        public float AxisThickness { get; set; } = 1.5f;

        public float DataOutlineThickness { get; set; } = 2.2f;

        public float DataPointSize { get; set; } = 5f;

        public Color4 GridColour { get; set; } = new Color4(255, 255, 210, 110);

        public Color4 AxisColour { get; set; } = new Color4(255, 255, 210, 95);

        public Color4 BaseFillColour { get; set; } = new Color4(255, 255, 200, 30);

        public Color4 DataFillColour { get; set; } = new Color4(255, 215, 0, 95);

        public Color4 DataStrokeColour { get; set; } = new Color4(255, 230, 128, 230);

        public Color4 DataPointColour { get; set; } = new Color4(255, 242, 176, 255);

        public RadarChart()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
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

        public void SetData(IReadOnlyList<float> ratios)
        {
            for (int i = 0; i < axisCount; i++)
                dataRatios[i] = i < ratios.Count ? Math.Clamp(ratios[i], 0, 1) : 0;

            Invalidate(Invalidation.DrawNode);
        }

        protected override DrawNode CreateDrawNode() => new RadarChartDrawNode(this);

        private class RadarChartDrawNode : DrawNode
        {
            private readonly RadarChart source;

            private float[] ratios = Array.Empty<float>();
            private int axisCount;

            private int gridLevels;
            private float radiusRatio;
            private float gridThickness;
            private float axisThickness;
            private float dataOutlineThickness;
            private float dataPointSize;

            private Color4 gridColour;
            private Color4 axisColour;
            private Color4 baseFillColour;
            private Color4 dataFillColour;
            private Color4 dataStrokeColour;
            private Color4 dataPointColour;

            private Vector2 drawSize;
            private Texture? texture;

            public RadarChartDrawNode(RadarChart chart)
                : base(chart)
            {
                source = chart;
            }

            public override void ApplyState()
            {
                base.ApplyState();

                texture = source.whitePixel;

                drawSize = source.DrawSize;
                axisCount = source.AxisCount;

                if (ratios.Length != axisCount)
                    Array.Resize(ref ratios, axisCount);

                gridLevels = Math.Max(1, source.GridLevels);
                radiusRatio = Math.Clamp(source.RadiusRatio, 0.1f, 1);
                gridThickness = Math.Max(0.5f, source.GridThickness);
                axisThickness = Math.Max(0.5f, source.AxisThickness);
                dataOutlineThickness = Math.Max(0.5f, source.DataOutlineThickness);
                dataPointSize = Math.Max(1, source.DataPointSize);

                gridColour = source.GridColour;
                axisColour = source.AxisColour;
                baseFillColour = source.BaseFillColour;
                dataFillColour = source.DataFillColour;
                dataStrokeColour = source.DataStrokeColour;
                dataPointColour = source.DataPointColour;

                for (int i = 0; i < axisCount; i++)
                    ratios[i] = source.dataRatios[i];
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

                var outerVertices = createVertices(center, radius, 1);

                drawPolygonFill(renderer, outerVertices, baseFillColour);

                for (int level = 1; level <= gridLevels; level++)
                {
                    float ratio = level / (float)gridLevels;
                    var levelVertices = createVertices(center, radius, ratio);
                    drawPolygonOutline(renderer, levelVertices, gridColour, gridThickness);
                }

                for (int i = 0; i < axisCount; i++)
                    drawLine(renderer, center, outerVertices[i], axisColour, axisThickness);

                var dataVertices = createVertices(center, radius, ratios);

                drawFanFill(renderer, center, dataVertices, dataFillColour);
                drawPolygonOutline(renderer, dataVertices, dataStrokeColour, dataOutlineThickness);
                drawPoints(renderer, dataVertices, dataPointColour, dataPointSize);

                renderer.PopLocalMatrix();
            }

            private Vector2[] createVertices(Vector2 center, float radius, float ratio)
            {
                var vertices = new Vector2[axisCount];

                for (int i = 0; i < axisCount; i++)
                {
                    float angle = MathHelper.DegreesToRadians(360f / axisCount * i - 90);
                    vertices[i] = new Vector2(
                        center.X + radius * ratio * (float)Math.Cos(angle),
                        center.Y + radius * ratio * (float)Math.Sin(angle)
                    );
                }

                return vertices;
            }

            private Vector2[] createVertices(Vector2 center, float radius, IReadOnlyList<float> axisRatios)
            {
                var vertices = new Vector2[axisCount];

                for (int i = 0; i < axisCount; i++)
                {
                    float clampedRatio = Math.Clamp(axisRatios[i], 0, 1);
                    float angle = MathHelper.DegreesToRadians(360f / axisCount * i - 90);
                    vertices[i] = new Vector2(
                        center.X + radius * clampedRatio * (float)Math.Cos(angle),
                        center.Y + radius * clampedRatio * (float)Math.Sin(angle)
                    );
                }

                return vertices;
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

            private void drawPolygonOutline(IRenderer renderer, IReadOnlyList<Vector2> polygonVertices, Color4 colour, float thickness)
            {
                for (int i = 0; i < polygonVertices.Count; i++)
                {
                    Vector2 start = polygonVertices[i];
                    Vector2 end = polygonVertices[(i + 1) % polygonVertices.Count];
                    drawLine(renderer, start, end, colour, thickness);
                }
            }

            private void drawPoints(IRenderer renderer, IReadOnlyList<Vector2> polygonVertices, Color4 colour, float pointSize)
            {
                float half = pointSize * 0.5f;

                for (int i = 0; i < polygonVertices.Count; i++)
                {
                    Vector2 point = polygonVertices[i];

                    renderer.DrawTriangle(
                        texture!,
                        new Triangle(
                            new Vector2(point.X - half, point.Y - half),
                            new Vector2(point.X + half, point.Y - half),
                            new Vector2(point.X + half, point.Y + half)),
                        colour);

                    renderer.DrawTriangle(
                        texture!,
                        new Triangle(
                            new Vector2(point.X - half, point.Y - half),
                            new Vector2(point.X + half, point.Y + half),
                            new Vector2(point.X - half, point.Y + half)),
                        colour);
                }
            }

            private void drawLine(IRenderer renderer, Vector2 start, Vector2 end, Color4 colour, float thickness)
            {
                Vector2 direction = end - start;
                if (direction.LengthSquared <= float.Epsilon)
                    return;

                direction.Normalize();
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * (thickness * 0.5f);

                renderer.DrawQuad(
                    texture!,
                    new Quad(
                        start - perpendicular,
                        start + perpendicular,
                        end - perpendicular,
                        end + perpendicular),
                    colour);
            }
        }
    }
}
