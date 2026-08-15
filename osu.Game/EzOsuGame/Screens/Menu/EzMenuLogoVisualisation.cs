// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
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
        private const int band_count = 64;
        private const int wave_catmull_detail = 3;

        private Bindable<EzLogoVisualisationStyle> style = new Bindable<EzLogoVisualisationStyle>(EzLogoVisualisationStyle.RadialBars);

        protected override int SpectrumIndexChange => style.Value == EzLogoVisualisationStyle.RadialBars ? 5 : 0;

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(Ez2ConfigManager? ezConfig)
        {
            if (ezConfig != null)
                style = ezConfig.GetBindable<EzLogoVisualisationStyle>(Ez2Setting.MenuLogoVisualisationStyle);
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

            // Bars stack 5 additive rounds at 0.2; a single stroke needs near-opaque vertices to match.
            private static readonly Color4 contour_white = Color4.White.Opacity(0.85f);

            private readonly float[] barAmplitudes = new float[256];
            private readonly float[] bands = new float[band_count];
            private readonly float[] waveform = new float[ChannelAmplitudes.WAVEFORM_SIZE];
            private readonly Vector2[] controlPoints = new Vector2[ChannelAmplitudes.WAVEFORM_SIZE];
            private readonly Vector2[] contourPoints = new Vector2[ChannelAmplitudes.WAVEFORM_SIZE * wave_catmull_detail];

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
                Source.BeatSyncProvider.CurrentAmplitudes.WaveformSamples.Span.CopyTo(waveform);
            }

            private void fillBandsFromPeakHold()
            {
                const int src_per_band = 3;

                for (int i = 0; i < band_count; i++)
                {
                    int src = i * src_per_band;
                    float mag = barAmplitudes[src];

                    if (src + 1 < barAmplitudes.Length)
                        mag = Math.Max(mag, barAmplitudes[src + 1]);
                    if (src + 2 < barAmplitudes.Length)
                        mag = Math.Max(mag, barAmplitudes[src + 2]);

                    bands[i] = mag;
                }
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
                colourInfo.ApplyChild(style == EzLogoVisualisationStyle.RadialBars ? transparent_white : contour_white);

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
                int count = fillSpectrumPoints(controlPoints, band_count);
                drawClosedLine(renderer, colourInfo, inflation, controlPoints, count, Math.Max(size * 0.012f, 3.5f));
            }

            private void drawWaveform(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation)
            {
                float radius = size / 2;
                var centre = new Vector2(radius);
                const float scale = bar_length;
                int sampleCount = waveform.Length;

                for (int i = 0; i < sampleCount; i++)
                {
                    float angle = i / (float)sampleCount * MathF.Tau;
                    float r = radius + waveform[i] * scale;
                    controlPoints[i] = centre + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
                }

                int count = fillClosedCatmull(controlPoints, sampleCount, contourPoints, wave_catmull_detail);
                drawClosedLine(renderer, colourInfo, inflation, contourPoints, count, Math.Max(size * 0.007f, 2.2f));
            }

            private void drawDots(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation)
            {
                float radius = size / 2;
                var centre = new Vector2(radius);
                const float scale = bar_length;

                for (int i = 0; i < band_count; i++)
                {
                    float angle = i / (float)band_count * MathF.Tau;
                    float r = radius + scale * bands[i];
                    Vector2 pos = centre + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
                    float dot = Math.Max(size * 0.018f, 4.5f) * (0.55f + 0.7f * bands[i]);
                    drawDot(renderer, colourInfo, inflation, pos, dot);
                }
            }

            private void drawNet(IRenderer renderer, ColourInfo colourInfo, Vector2 inflation)
            {
                float radius = size / 2;
                var centre = new Vector2(radius);
                const float scale = bar_length;
                float thickness = Math.Max(size * 0.006f, 2f);

                for (int i = 0; i < band_count; i++)
                {
                    float angle = i / (float)band_count * MathF.Tau;
                    var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    controlPoints[i] = centre + dir * (radius + scale * bands[i]);
                    contourPoints[i] = centre + dir * radius;
                }

                drawClosedLine(renderer, colourInfo, inflation, controlPoints, band_count, thickness);

                for (int i = 0; i < band_count; i += 2)
                    drawSegment(renderer, colourInfo, inflation, controlPoints[i], contourPoints[i], thickness);
            }

            private int fillSpectrumPoints(Vector2[] destination, int count)
            {
                float radius = size / 2;
                var centre = new Vector2(radius);
                const float scale = bar_length;

                for (int i = 0; i < count; i++)
                {
                    float angle = i / (float)count * MathF.Tau;
                    float r = radius + scale * bands[i];
                    destination[i] = centre + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
                }

                return count;
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
