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
        private const float visualiser_rounds = 5;
        private const float amplitude_dead_zone = 1f / bar_length;
        private const int band_count = 200;
        private const int wave_band_count = 64;
        private const int wave_catmull_detail = 3;
        private const int wave_rounds = 4;
        private const float wave_amplitude = bar_length * 0.72f;

        private Bindable<EzLogoVisualisationStyle> style = new Bindable<EzLogoVisualisationStyle>(EzLogoVisualisationStyle.RadialBars);

        protected override int SpectrumIndexChange => style.Value == EzLogoVisualisationStyle.Off ? 0 : 5;

        private readonly float[] smoothedWaveformLeft = new float[wave_band_count];
        private readonly float[] smoothedWaveformRight = new float[wave_band_count];
        private readonly float[] waveScratchLeft = new float[wave_band_count];
        private readonly float[] waveScratchRight = new float[wave_band_count];

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(Ez2ConfigManager? ezConfig)
        {
            if (ezConfig != null)
                style = ezConfig.GetBindable<EzLogoVisualisationStyle>(Ez2Setting.MenuLogoVisualisationStyle);
        }

        protected override void Update()
        {
            base.Update();

            if (style.Value == EzLogoVisualisationStyle.CircularWave)
                updateSmoothedWaveform();
        }

        private void updateSmoothedWaveform()
        {
            var samples = BeatSyncProvider.CurrentAmplitudes.WaveformSamples.Span;

            if (samples.Length < 2)
                return;

            downsampleChannel(samples, 0, waveScratchLeft);
            downsampleChannel(samples, 1, waveScratchRight);

            float blend = 1 - MathF.Exp(-(float)Time.Elapsed * 0.006f);
            smoothChannel(waveScratchLeft, smoothedWaveformLeft, blend);
            smoothChannel(waveScratchRight, smoothedWaveformRight, blend);
        }

        private static void downsampleChannel(ReadOnlySpan<float> samples, int channel, float[] destination)
        {
            int frames = samples.Length / 2;
            int perBand = Math.Max(1, frames / wave_band_count);

            for (int i = 0; i < wave_band_count; i++)
            {
                float sum = 0;
                int count = 0;
                int startFrame = i * perBand;

                for (int k = 0; k < perBand; k++)
                {
                    int src = (startFrame + k) * 2 + channel;

                    if (src >= samples.Length)
                        break;

                    sum += samples[src];
                    count++;
                }

                destination[i] = count > 0 ? sum / count : 0;
            }
        }

        private static void smoothChannel(float[] scratch, float[] smoothed, float blend)
        {
            for (int i = 0; i < wave_band_count; i++)
            {
                float prev = scratch[(i - 1 + wave_band_count) % wave_band_count];
                float next = scratch[(i + 1) % wave_band_count];
                float target = prev * 0.25f + scratch[i] * 0.5f + next * 0.25f;

                float current = smoothed[i];
                float factor = Math.Abs(target) > Math.Abs(current) ? Math.Min(1, blend * 2.5f) : blend;
                smoothed[i] = current + (target - current) * factor;
            }
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

            private float spectrumSpin;

            private readonly float[] barAmplitudes = new float[256];
            private readonly float[] bands = new float[band_count];
            private readonly float[] waveformLeft = new float[wave_band_count];
            private readonly float[] waveformRight = new float[wave_band_count];
            private readonly Vector2[] controlPoints = new Vector2[band_count];
            private readonly Vector2[] innerPoints = new Vector2[band_count];
            private readonly Vector2[] contourPoints = new Vector2[band_count];

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

                Source.FrequencyAmplitudes.AsSpan().CopyTo(barAmplitudes);
                fillBandsFromPeakHold();
                Source.smoothedWaveformLeft.AsSpan().CopyTo(waveformLeft);
                Source.smoothedWaveformRight.AsSpan().CopyTo(waveformRight);
                spectrumSpin = Source.SpectrumIndexOffset / (float)bars_per_visualiser * MathF.Tau;
            }

            private void fillBandsFromPeakHold()
            {
                for (int i = 0; i < band_count; i++)
                    bands[i] = barAmplitudes[i];
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                if (style == EzLogoVisualisationStyle.Off || size <= 0)
                    return;

                vertexBatch ??= renderer.CreateQuadBatch<TexturedVertex2D>(200, 10);

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

                for (int j = 0; j < visualiser_rounds; j++)
                {
                    fillSpectrumPoints(controlPoints, band_count, j * MathF.Tau / visualiser_rounds);
                    drawClosedLine(renderer, colourInfo, inflation, controlPoints, band_count, thickness);
                }
            }

            private void drawWaveform(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation)
            {
                float radius = size / 2;
                var centre = new Vector2(radius);
                float thickness = Math.Max(size * 0.007f, 2.2f);

                for (int j = 0; j < wave_rounds; j++)
                {
                    float offset = j * MathF.Tau / wave_rounds + spectrumSpin;
                    float[] channel = j % 2 == 0 ? waveformLeft : waveformRight;

                    for (int i = 0; i < wave_band_count; i++)
                    {
                        float angle = i / (float)wave_band_count * MathF.Tau + offset;
                        float r = radius + channel[i] * wave_amplitude;
                        controlPoints[i] = centre + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
                    }

                    int count = fillClosedCatmull(controlPoints, wave_band_count, contourPoints, wave_catmull_detail);
                    drawClosedLine(renderer, colourInfo, inflation, contourPoints, count, thickness);
                }
            }

            private void drawDots(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation)
            {
                float radius = size / 2;
                var centre = new Vector2(radius);

                for (int j = 0; j < visualiser_rounds; j++)
                {
                    float offset = j * MathF.Tau / visualiser_rounds;

                    for (int i = 0; i < band_count; i++)
                    {
                        float angle = i / (float)band_count * MathF.Tau + offset;
                        float r = radius + bar_length * bands[i];
                        Vector2 pos = centre + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
                        float dot = Math.Max(size * 0.018f, 4.5f) * (0.55f + 0.7f * bands[i]);
                        drawDot(renderer, colourInfo, inflation, pos, dot);
                    }
                }
            }

            private void drawNet(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation)
            {
                float radius = size / 2;
                var centre = new Vector2(radius);
                float thickness = Math.Max(size * 0.006f, 2f);

                for (int j = 0; j < visualiser_rounds; j++)
                {
                    float offset = j * MathF.Tau / visualiser_rounds;

                    for (int i = 0; i < band_count; i++)
                    {
                        float angle = i / (float)band_count * MathF.Tau + offset;
                        var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                        controlPoints[i] = centre + dir * (radius + bar_length * bands[i]);
                        innerPoints[i] = centre + dir * radius;
                    }

                    drawClosedLine(renderer, colourInfo, inflation, controlPoints, band_count, thickness);

                    for (int i = 0; i < band_count; i += 2)
                        drawSegment(renderer, colourInfo, inflation, controlPoints[i], innerPoints[i], thickness);
                }
            }

            private void fillSpectrumPoints(Vector2[] destination, int count, float angleOffset)
            {
                float radius = size / 2;
                var centre = new Vector2(radius);

                for (int i = 0; i < count; i++)
                {
                    float angle = i / (float)count * MathF.Tau + angleOffset;
                    float r = radius + bar_length * bands[i];
                    destination[i] = centre + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
                }
            }

            private static int fillClosedCatmull(Vector2[] source, int sourceCount, Vector2[] destination, int detail)
            {
                int count = 0;

                for (int i = 0; i < sourceCount; i++)
                {
                    Vector2 p0 = source[(i - 1 + sourceCount) % sourceCount];
                    Vector2 p1 = source[i];
                    Vector2 p2 = source[(i + 1) % sourceCount];
                    Vector2 p3 = source[(i + 2) % sourceCount];

                    for (int s = 0; s < detail; s++)
                        destination[count++] = catmull(p0, p1, p2, p3, s / (float)detail);
                }

                return count;
            }

            private static Vector2 catmull(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
            {
                float t2 = t * t;
                float t3 = t2 * t;

                return 0.5f * (
                    2f * p1
                    + (-p0 + p2) * t
                    + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                    + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
            }

            private void drawClosedLine(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation, Vector2[] points, int pointCount, float thickness)
            {
                for (int i = 0; i < pointCount; i++)
                    drawSegment(renderer, colourInfo, inflation, points[i], points[(i + 1) % pointCount], thickness);
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

            private void drawDot(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation, Vector2 centre, float diameter)
            {
                float half = diameter / 2;
                var offset = new Vector2(half);

                var rectangle = new Quad(
                    Vector2Extensions.Transform(centre + new Vector2(-half, -half), DrawInfo.Matrix),
                    Vector2Extensions.Transform(centre + new Vector2(half, -half), DrawInfo.Matrix),
                    Vector2Extensions.Transform(centre + new Vector2(-half, half), DrawInfo.Matrix),
                    Vector2Extensions.Transform(centre + new Vector2(half, half), DrawInfo.Matrix)
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
