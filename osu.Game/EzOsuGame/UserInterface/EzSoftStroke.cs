// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// Draws a polyline as two quads per segment. These strokes use a 2px ramp, so alpha falls from the centre line to the outer edge.
    /// </summary>
    internal static class EzSoftStroke
    {
        public static Color4 Tint(Color4 colour, DrawColourInfo drawColourInfo)
        {
            var tinted = drawColourInfo.Colour;
            tinted.ApplyChild(colour);
            return tinted.TopLeft;
        }

        public static void Draw(IRenderer renderer, Texture texture, ReadOnlySpan<Vector2> points, float thickness, Color4 colour, bool closed)
        {
            int count = points.Length;

            if (count < 2 || thickness <= 0)
                return;

            float half = thickness * 0.5f;
            var drawColour = getStrokeColour(colour);
            int segments = closed ? count : count - 1;

            for (int i = 0; i < segments; i++)
            {
                if (!tryCreateQuads(points, i, half, closed, out var first, out var second))
                    continue;

                renderer.DrawQuad(texture, first, drawColour);
                renderer.DrawQuad(texture, second, drawColour);
            }
        }

        // The destination must hold up to two quads per segment.
        public static int BuildOpenQuads(ReadOnlySpan<Vector2> points, float thickness, Span<Quad> quads)
        {
            if (thickness <= 0)
                return 0;

            int count = 0;
            float half = thickness * 0.5f;

            for (int i = 0; i < points.Length - 1; i++)
            {
                if (!tryCreateQuads(points, i, half, false, out var first, out var second))
                    continue;

                quads[count++] = first;
                quads[count++] = second;
            }

            return count;
        }

        public static void Draw(IRenderer renderer, Texture texture, ReadOnlySpan<Quad> quads, Color4 colour)
        {
            var drawColour = getStrokeColour(colour);

            foreach (var quad in quads)
                renderer.DrawQuad(texture, quad, drawColour);
        }

        private static ColourInfo getStrokeColour(Color4 core)
        {
            var edge = new Color4(core.R, core.G, core.B, 0);
            return new ColourInfo
            {
                TopLeft = edge,
                TopRight = edge,
                BottomLeft = core,
                BottomRight = core,
            };
        }

        private static bool tryCreateQuads(ReadOnlySpan<Vector2> points, int index, float half, bool closed, out Quad first, out Quad second)
        {
            int next = closed ? (index + 1) % points.Length : index + 1;
            Vector2 start = points[index];
            Vector2 end = points[next];
            Vector2 delta = end - start;

            if (delta.LengthSquared < 0.000001f)
            {
                first = second = default;
                return false;
            }

            Vector2 side = perpendicular(delta);
            Vector2 startOffset = miterOffset(points, index, side, half, closed);
            Vector2 endOffset = miterOffset(points, next, side, half, closed);

            first = new Quad(start + startOffset, end + endOffset, start, end);
            second = new Quad(start + -startOffset, end + -endOffset, start, end);
            return true;
        }

        private static Vector2 miterOffset(ReadOnlySpan<Vector2> points, int index, Vector2 segmentSide, float half, bool closed)
        {
            Vector2 side = vertexSide(points, index, closed);
            float align = Vector2.Dot(side, segmentSide);

            if (align < 0.25f)
                return segmentSide * half;

            return side * (half / align);
        }

        private static Vector2 vertexSide(ReadOnlySpan<Vector2> points, int index, bool closed)
        {
            int count = points.Length;
            int previous = closed ? (index - 1 + count) % count : Math.Max(0, index - 1);
            int next = closed ? (index + 1) % count : Math.Min(count - 1, index + 1);
            return perpendicular(points[next] - points[previous]);
        }

        private static Vector2 perpendicular(Vector2 direction)
        {
            float lengthSquared = direction.LengthSquared;

            if (lengthSquared < 0.000001f)
                return Vector2.UnitY;

            float inverse = 1f / MathF.Sqrt(lengthSquared);
            return new Vector2(-direction.Y * inverse, direction.X * inverse);
        }
    }
}
