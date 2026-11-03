// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
        public static void Draw(IRenderer renderer, Texture texture, ReadOnlySpan<Vector2> points, float thickness, Color4 colour, bool closed)
        {
            int count = points.Length;

            if (count < 2 || thickness <= 0)
                return;

            float half = thickness * 0.5f;
            var edge = new Color4(colour.R, colour.G, colour.B, 0);
            int segments = closed ? count : count - 1;

            for (int i = 0; i < segments; i++)
            {
                int next = closed ? (i + 1) % count : i + 1;
                Vector2 start = points[i];
                Vector2 end = points[next];
                Vector2 delta = end - start;

                if (delta.LengthSquared < 0.000001f)
                    continue;

                Vector2 side = perpendicular(delta);
                Vector2 startOffset = miterOffset(points, i, side, half, closed);
                Vector2 endOffset = miterOffset(points, next, side, half, closed);

                drawHalf(renderer, texture, start, end, startOffset, endOffset, colour, edge);
                drawHalf(renderer, texture, start, end, -startOffset, -endOffset, colour, edge);
            }
        }

        private static void drawHalf(IRenderer renderer, Texture texture, Vector2 start, Vector2 end, Vector2 startOffset, Vector2 endOffset, Color4 core, Color4 edge)
        {
            var quad = new Quad(start + startOffset, end + endOffset, start, end);
            var colour = new ColourInfo
            {
                TopLeft = edge,
                TopRight = edge,
                BottomLeft = core,
                BottomRight = core,
            };

            renderer.DrawQuad(texture, quad, colour);
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
