// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Screens.Menu;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Screens.Menu
{
    /// <summary>
    /// Menu logo visualiser with selectable geometry around the cookie.
    /// </summary>
    public partial class EzMenuLogoVisualisation : MenuLogoVisualisation
    {
        private const float bar_length = 600;
        private const int bars_per_visualiser = 200;
        private const int visualiser_rounds = 5;
        private const float amplitude_dead_zone = 1f / bar_length;
        private const int band_count = 200;
        /// <summary>
        /// miller198 WaveStroke: Android capture 512, averagePool(5). We have 256 interleaved frames → 128 mono → 25 controls.
        /// </summary>
        private const int wave_pool_size = 5;
        private const int wave_control_count = 25;
        private const int wave_catmull_steps = 10;
        private const int wave_draw_count = wave_control_count * wave_catmull_steps + 1;
        private const float wave_anim_tau = 0.04f;
        private const int ring2_index_change = -3;
        private const float ring_update_ms = 50;

        private static readonly Vector2[] unit_circle = createUnitCircle(band_count);

        private Bindable<EzLogoVisualisationStyle> style = new Bindable<EzLogoVisualisationStyle>(EzLogoVisualisationStyle.RadialBars);

        protected override int SpectrumIndexChange => style.Value == EzLogoVisualisationStyle.Off ? 0 : 5;

        private readonly float[] waveControl = new float[wave_control_count];
        private readonly float[] waveWork = new float[wave_control_count];
        private int ring2IndexOffset;
        private double ring2Timer;

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(Ez2ConfigManager? ezConfig)
        {
            if (ezConfig != null)
                style = ezConfig.GetBindable<EzLogoVisualisationStyle>(Ez2Setting.MenuLogoVisualisationStyle);
        }

        protected override void Update()
        {
            base.Update();

            if (style.Value != EzLogoVisualisationStyle.Off && style.Value != EzLogoVisualisationStyle.RadialBars)
            {
                ring2Timer += Time.Elapsed;

                while (ring2Timer >= ring_update_ms)
                {
                    ring2Timer -= ring_update_ms;
                    ring2IndexOffset = (ring2IndexOffset + ring2_index_change + band_count) % band_count;
                }
            }

            if (style.Value == EzLogoVisualisationStyle.CircularWave)
                updateMillerWave();
        }

        /// <summary>
        /// miller198 WaveStroke: mix to mono, unsigned 0–255, averagePool(5), z-score, min-max, 120ms tween.
        /// https://github.com/miller198/ComposeCircleAudioVisualizer
        /// </summary>
        private void updateMillerWave()
        {
            var samples = BeatSyncProvider.CurrentAmplitudes.WaveformSamples.Span;

            if (samples.Length < wave_pool_size * 2)
                return;

            int frames = samples.Length / 2;

            for (int i = 0; i < wave_control_count; i++)
            {
                int start = i * wave_pool_size;
                float sum = 0;

                for (int k = 0; k < wave_pool_size; k++)
                {
                    int frame = start + k;
                    if (frame >= frames)
                        break;

                    float mixed = 0.5f * (samples[frame * 2] + samples[frame * 2 + 1]);
                    sum += (mixed + 1f) * 127.5f;
                }

                waveWork[i] = sum / wave_pool_size;
            }

            applyZScore(waveWork);
            applyMinMax(waveWork);

            float blend = 1 - MathF.Exp(-(float)Time.Elapsed / 1000f / wave_anim_tau);

            for (int i = 0; i < wave_control_count; i++)
                waveControl[i] += (waveWork[i] - waveControl[i]) * blend;
        }

        private static void applyZScore(float[] data)
        {
            float mean = 0;

            for (int i = 0; i < data.Length; i++)
                mean += data[i];

            mean /= data.Length;

            float variance = 0;

            for (int i = 0; i < data.Length; i++)
            {
                float d = data[i] - mean;
                variance += d * d;
            }

            float stdDev = MathF.Sqrt(variance / data.Length);

            if (stdDev <= 0.0001f || float.IsNaN(stdDev))
            {
                Array.Clear(data);
                return;
            }

            for (int i = 0; i < data.Length; i++)
                data[i] = (data[i] - mean) / stdDev;
        }

        private static void applyMinMax(float[] data)
        {
            float min = data[0];
            float max = data[0];

            for (int i = 1; i < data.Length; i++)
            {
                min = Math.Min(min, data[i]);
                max = Math.Max(max, data[i]);
            }

            float range = max - min;

            if (range <= 1f)
            {
                Array.Clear(data);
                return;
            }

            for (int i = 0; i < data.Length; i++)
                data[i] = (data[i] - min) / range;
        }

        private static Vector2[] createUnitCircle(int count)
        {
            var points = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * MathF.Tau;
                points[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }

            return points;
        }

        protected override DrawNode CreateDrawNode() => new EzVisualisationDrawNode(this);

        private class EzVisualisationDrawNode : DrawNode
        {
            protected new EzMenuLogoVisualisation Source => (EzMenuLogoVisualisation)base.Source;

            private IShader shader = null!;
            private Texture texture = null!;
            private float size;
            private EzLogoVisualisationStyle style;

            private static readonly Color4 transparent_white = Color4.White.Opacity(0.2f);
            private static readonly Color4 dots_white = Color4.White.Opacity(0.38f);

            private Vector2 centre;
            private float radius;

            private readonly float[] barAmplitudes = new float[256];
            private readonly float[] bands = new float[band_count];
            private readonly float[] waveControl = new float[wave_control_count];
            private readonly Vector2[] roundPoints = new Vector2[band_count];
            private readonly Vector2[] innerPoints = new Vector2[band_count];
            private readonly Vector2[] rotatedPoints = new Vector2[band_count];
            private readonly Vector2[] waveOuterControl = new Vector2[wave_control_count];
            private readonly Vector2[] waveInnerControl = new Vector2[wave_control_count];
            private readonly Vector2[] wavePoints = new Vector2[wave_draw_count * 2];

            private IVertexBatch<TexturedVertex2D>? vertexBatch;

            public EzVisualisationDrawNode(EzMenuLogoVisualisation source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                shader = Source.Shader;
                texture = Source.Texture;
                size = Source.DrawSize.X;
                style = Source.style.Value;
                radius = size / 2;
                centre = new Vector2(radius);

                Source.FrequencyAmplitudes.AsSpan().CopyTo(barAmplitudes);
                int ring2Offset = Source.ring2IndexOffset;
                float ring1Spin = Source.SpectrumIndexOffset / (float)bars_per_visualiser * MathF.Tau;
                float ring2Spin = ring2Offset / (float)band_count * MathF.Tau;

                switch (style)
                {
                    case EzLogoVisualisationStyle.CircularPolyline:
                    case EzLogoVisualisationStyle.CircularDots:
                    case EzLogoVisualisationStyle.CircularNet:
                        fillSpectrumEnvelope(ring2Offset);
                        break;

                    case EzLogoVisualisationStyle.CircularWave:
                        Source.waveControl.AsSpan().CopyTo(waveControl);
                        fillWaveRing(ring1Spin, ring2Spin);
                        break;
                }
            }

            private void fillSpectrumEnvelope(int ring2Offset)
            {
                float height = size / 5f;

                for (int i = 0; i < band_count; i++)
                {
                    float mag = barAmplitudes[i];
                    bands[i] = mag;
                    Vector2 dir = unit_circle[i];
                    float bulge = height * mag;
                    roundPoints[i] = centre + dir * (radius + bulge);

                    float mag2 = barAmplitudes[(i + ring2Offset) % band_count];
                    rotatedPoints[i] = centre + dir * (radius + height - height * mag2);
                    innerPoints[i] = centre + dir * radius;
                }
            }

            private void fillWaveRing(float ring1Spin, float ring2Spin)
            {
                float maxEffectHeight = size / 5f;

                for (int i = 0; i < wave_control_count; i++)
                {
                    float baseAngle = i / (float)wave_control_count * MathF.Tau - MathF.PI / 2;
                    float mag = waveControl[i];
                    float bulge = maxEffectHeight * mag;

                    float a1 = baseAngle + ring1Spin;
                    waveOuterControl[i] = centre + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * (radius + bulge);

                    float a2 = baseAngle + ring2Spin;
                    waveInnerControl[i] = centre + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * (radius + maxEffectHeight - bulge);
                }

                fillCatmullClosed(waveOuterControl, wavePoints, 0, wave_catmull_steps);
                fillCatmullClosed(waveInnerControl, wavePoints, wave_draw_count, wave_catmull_steps);
            }

            private static void fillCatmullClosed(Vector2[] source, Vector2[] dest, int destOffset, int steps)
            {
                int n = source.Length;
                int d = destOffset;

                for (int i = 0; i < n; i++)
                {
                    Vector2 p0 = source[(i - 1 + n) % n];
                    Vector2 p1 = source[i];
                    Vector2 p2 = source[(i + 1) % n];
                    Vector2 p3 = source[(i + 2) % n];
                    int lastT = i == n - 1 ? steps : steps - 1;

                    for (int t = 0; t <= lastT; t++)
                    {
                        float s = t / (float)steps;
                        float s2 = s * s;
                        float s3 = s2 * s;

                        dest[d++] = 0.5f * (
                            2f * p1
                            + (-p0 + p2) * s
                            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * s2
                            + (-p0 + 3f * p1 - 3f * p2 + p3) * s3);
                    }
                }
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                if (style == EzLogoVisualisationStyle.Off || size <= 0)
                    return;

                vertexBatch ??= renderer.CreateQuadBatch<TexturedVertex2D>(1000, 4);

                shader.Bind();

                Vector2 inflation = DrawInfo.MatrixInverse.ExtractScale().Xy;

                ColourInfo colourInfo = DrawColourInfo.Colour;
                colourInfo.ApplyChild(style == EzLogoVisualisationStyle.CircularDots ? dots_white : transparent_white);

                switch (style)
                {
                    case EzLogoVisualisationStyle.CircularPolyline:
                        drawPolyline(renderer, colourInfo, inflation);
                        break;

                    case EzLogoVisualisationStyle.CircularWave:
                        drawWaveform(renderer, colourInfo, inflation);
                        break;

                    case EzLogoVisualisationStyle.CircularDots:
                        drawDots(renderer, colourInfo, inflation);
                        break;

                    case EzLogoVisualisationStyle.CircularNet:
                        drawNet(renderer, colourInfo, inflation);
                        break;

                    default:
                        drawRadialBars(renderer, colourInfo, inflation);
                        break;
                }

                shader.Unbind();
            }

            private void drawRadialBars(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation)
            {
                for (int j = 0; j < visualiser_rounds; j++)
                {
                    for (int i = 0; i < bars_per_visualiser; i++)
                    {
                        if (barAmplitudes[i] < amplitude_dead_zone)
                            continue;

                        float rotation = float.DegreesToRadians(i / (float)bars_per_visualiser * 360 + j * 360 / visualiser_rounds);
                        float rotationCos = MathF.Cos(rotation);
                        float rotationSin = MathF.Sin(rotation);
                        var barPosition = new Vector2(rotationCos / 2 + 0.5f, rotationSin / 2 + 0.5f) * size;

                        var barSize = new Vector2(size * MathF.Sqrt(2 * (1 - MathF.Cos(float.DegreesToRadians(360f / bars_per_visualiser)))) / 2f, bar_length * barAmplitudes[i]);
                        var bottomOffset = new Vector2(-rotationSin * barSize.X / 2, rotationCos * barSize.X / 2);
                        var amplitudeOffset = new Vector2(rotationCos * barSize.Y, rotationSin * barSize.Y);

                        var rectangle = new Quad(
                            Vector2Extensions.Transform(barPosition - bottomOffset, DrawInfo.Matrix),
                            Vector2Extensions.Transform(barPosition - bottomOffset + amplitudeOffset, DrawInfo.Matrix),
                            Vector2Extensions.Transform(barPosition + bottomOffset, DrawInfo.Matrix),
                            Vector2Extensions.Transform(barPosition + bottomOffset + amplitudeOffset, DrawInfo.Matrix)
                        );

                        renderer.DrawQuad(
                            texture,
                            rectangle,
                            colourInfo,
                            null,
                            vertexBatch!.AddAction,
                            Vector2.Divide(inflation, barSize.Yx));
                    }
                }
            }

            private void drawPolyline(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation)
            {
                float thickness = Math.Max(size * 0.012f, 3.5f);
                drawClosedLine(renderer, colourInfo, inflation, roundPoints, 0, band_count, thickness);
                drawClosedLine(renderer, colourInfo, inflation, rotatedPoints, 0, band_count, thickness);
            }

            private void drawWaveform(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation)
            {
                float thickness = Math.Max(size * 0.006f, 3f);
                drawOpenLine(renderer, colourInfo, inflation, wavePoints, 0, wave_draw_count, thickness);
                drawOpenLine(renderer, colourInfo, inflation, wavePoints, wave_draw_count, wave_draw_count, thickness);
            }

            private void drawDots(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation)
            {
                float baseDot = Math.Max(size * 0.018f, 4.5f);

                for (int i = 0; i < band_count; i++)
                {
                    if (bands[i] < amplitude_dead_zone)
                        continue;

                    float dot = baseDot * (0.55f + 0.7f * bands[i]);
                    drawDot(renderer, colourInfo, inflation, roundPoints[i], dot);
                }
            }

            private void drawNet(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation)
            {
                float thickness = Math.Max(size * 0.006f, 2f);
                drawClosedLine(renderer, colourInfo, inflation, roundPoints, 0, band_count, thickness);

                for (int i = 0; i < band_count; i += 2)
                    drawSegment(renderer, colourInfo, inflation, roundPoints[i], innerPoints[i], thickness);
            }

            private void drawClosedLine(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation, Vector2[] points, int start, int pointCount, float thickness)
            {
                drawOpenLine(renderer, colourInfo, inflation, points, start, pointCount, thickness);

                if (pointCount > 1)
                    drawSegment(renderer, colourInfo, inflation, points[start + pointCount - 1], points[start], thickness);
            }

            private void drawOpenLine(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation, Vector2[] points, int start, int pointCount, float thickness)
            {
                int last = start + pointCount - 1;

                for (int i = start; i < last; i++)
                    drawSegment(renderer, colourInfo, inflation, points[i], points[i + 1], thickness);
            }

            private void drawSegment(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation, Vector2 start, Vector2 end, float thickness)
            {
                Vector2 delta = end - start;
                float length = delta.Length;

                if (length < 0.001f)
                    return;

                float halfThickness = thickness / 2;
                var normal = new Vector2(-delta.Y / length, delta.X / length) * halfThickness;

                var rectangle = new Quad(
                    Vector2Extensions.Transform(start - normal, DrawInfo.Matrix),
                    Vector2Extensions.Transform(end - normal, DrawInfo.Matrix),
                    Vector2Extensions.Transform(start + normal, DrawInfo.Matrix),
                    Vector2Extensions.Transform(end + normal, DrawInfo.Matrix)
                );

                renderer.DrawQuad(
                    texture,
                    rectangle,
                    colourInfo,
                    null,
                    vertexBatch!.AddAction,
                    Vector2.Divide(inflation, new Vector2(thickness, length)));
            }

            private void drawDot(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation, Vector2 dotCentre, float diameter)
            {
                float half = diameter / 2;
                var offset = new Vector2(half);

                var rectangle = new Quad(
                    Vector2Extensions.Transform(dotCentre + new Vector2(-half, -half), DrawInfo.Matrix),
                    Vector2Extensions.Transform(dotCentre + new Vector2(half, -half), DrawInfo.Matrix),
                    Vector2Extensions.Transform(dotCentre + new Vector2(-half, half), DrawInfo.Matrix),
                    Vector2Extensions.Transform(dotCentre + new Vector2(half, half), DrawInfo.Matrix)
                );

                renderer.DrawQuad(
                    texture,
                    rectangle,
                    colourInfo,
                    null,
                    vertexBatch!.AddAction,
                    Vector2.Divide(inflation, offset));
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                vertexBatch?.Dispose();
            }
        }
    }
}
