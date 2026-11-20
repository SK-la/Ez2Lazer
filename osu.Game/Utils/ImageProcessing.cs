// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Imaging;

namespace osu.Game.Utils
{
    public enum ResizeMode
    {
        Stretch,
        Max
    }

    public static class ImageProcessing
    {
        public static RgbaImage Resize(RgbaImage source, int width, int height, ResizeMode mode)
        {
            if (mode == ResizeMode.Stretch)
                return source.Resize(width, height);

            double scale = Math.Min(width / (double)source.Width, height / (double)source.Height);
            int resizedWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            int resizedHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
            return source.Resize(resizedWidth, resizedHeight);
        }

        public static void DrawImage(RgbaImage destination, RgbaImage source, int x, int y)
        {
            int left = Math.Max(0, x);
            int top = Math.Max(0, y);
            int right = Math.Min(destination.Width, x + source.Width);
            int bottom = Math.Min(destination.Height, y + source.Height);

            for (int destinationY = top; destinationY < bottom; destinationY++)
            {
                int sourceY = destinationY - y;

                for (int destinationX = left; destinationX < right; destinationX++)
                {
                    int sourceX = destinationX - x;
                    destination[destinationX, destinationY] = sourceOver(destination[destinationX, destinationY], source[sourceX, sourceY]);
                }
            }
        }

        private static Rgba32 sourceOver(Rgba32 destination, Rgba32 source)
        {
            float sourceAlpha = source.A / 255f;
            if (sourceAlpha <= 0)
                return destination;
            if (sourceAlpha >= 1 || destination.A == 0)
                return source;

            float destinationAlpha = destination.A / 255f;
            float outputAlpha = sourceAlpha + destinationAlpha * (1 - sourceAlpha);
            float destinationWeight = destinationAlpha * (1 - sourceAlpha) / outputAlpha;

            return new Rgba32(
                toByte(source.R * (1 - destinationWeight) + destination.R * destinationWeight),
                toByte(source.G * (1 - destinationWeight) + destination.G * destinationWeight),
                toByte(source.B * (1 - destinationWeight) + destination.B * destinationWeight),
                toByte(outputAlpha * 255));
        }

        private static byte toByte(float value) => (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
    }
}
