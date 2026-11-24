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
        private const double lanczos_radius = 3;

        public static RgbaImage Resize(RgbaImage source, int width, int height, ResizeMode mode)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(width <= 0 ? nameof(width) : nameof(height));

            if (mode == ResizeMode.Stretch)
                return resize(source, width, height);

            double scale = Math.Min(width / (double)source.Width, height / (double)source.Height);
            int resizedWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            int resizedHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
            return resize(source, resizedWidth, resizedHeight);
        }

        private static RgbaImage resize(RgbaImage source, int width, int height)
        {
            var result = new RgbaImage(width, height);
            double horizontalScale = source.Width / (double)width;
            double horizontalFilterScale = Math.Max(1, horizontalScale);
            double horizontalRadius = lanczos_radius * horizontalFilterScale;
            double verticalScale = source.Height / (double)height;
            double verticalFilterScale = Math.Max(1, verticalScale);
            double verticalRadius = lanczos_radius * verticalFilterScale;
            int rowStride = checked(width * 4);
            int rowCapacity = checked((int)Math.Ceiling(verticalRadius * 2) + 2);
            float[] horizontalRows = new float[checked(rowCapacity * rowStride)];
            int[] cachedSourceRows = new int[rowCapacity];
            Array.Fill(cachedSourceRows, -1);

            int[] horizontalFirst = new int[width];
            int[] horizontalWeightOffsets = new int[width + 1];
            double[] horizontalWeightSums = new double[width];

            for (int x = 0; x < width; x++)
            {
                double centre = (x + 0.5) * horizontalScale - 0.5;
                int first = Math.Max(0, (int)Math.Ceiling(centre - horizontalRadius));
                int last = Math.Min(source.Width - 1, (int)Math.Floor(centre + horizontalRadius));
                horizontalFirst[x] = first;
                horizontalWeightOffsets[x + 1] = checked(horizontalWeightOffsets[x] + last - first + 1);
            }

            double[] horizontalWeights = new double[horizontalWeightOffsets[width]];

            for (int x = 0; x < width; x++)
            {
                double centre = (x + 0.5) * horizontalScale - 0.5;
                int first = horizontalFirst[x];
                int last = first + horizontalWeightOffsets[x + 1] - horizontalWeightOffsets[x] - 1;
                int weightOffset = horizontalWeightOffsets[x];

                for (int sourceX = first; sourceX <= last; sourceX++)
                {
                    double weight = lanczos((centre - sourceX) / horizontalFilterScale);
                    horizontalWeights[weightOffset++] = weight;
                    horizontalWeightSums[x] += weight;
                }
            }

            double[] verticalWeights = new double[rowCapacity];

            for (int y = 0; y < height; y++)
            {
                double centre = (y + 0.5) * verticalScale - 0.5;
                int first = Math.Max(0, (int)Math.Ceiling(centre - verticalRadius));
                int last = Math.Min(source.Height - 1, (int)Math.Floor(centre + verticalRadius));
                int taps = last - first + 1;
                double verticalWeightSum = 0;

                for (int i = 0; i < taps; i++)
                {
                    double weight = lanczos((centre - (first + i)) / verticalFilterScale);
                    verticalWeights[i] = weight;
                    verticalWeightSum += weight;
                }

                for (int sourceY = first; sourceY <= last; sourceY++)
                {
                    int rowIndex = sourceY % rowCapacity;

                    if (cachedSourceRows[rowIndex] != sourceY)
                    {
                        Span<float> row = horizontalRows.AsSpan(rowIndex * rowStride, rowStride);
                        resizeHorizontalRow(source, sourceY, width, horizontalFirst, horizontalWeightOffsets, horizontalWeights, horizontalWeightSums, row);
                        cachedSourceRows[rowIndex] = sourceY;
                    }
                }

                for (int x = 0; x < width; x++)
                {
                    double red = 0;
                    double green = 0;
                    double blue = 0;
                    double alpha = 0;

                    for (int i = 0; i < taps; i++)
                    {
                        int sourceY = first + i;
                        double weight = verticalWeights[i];
                        int rowIndex = sourceY % rowCapacity;
                        int index = rowIndex * rowStride + x * 4;
                        red += horizontalRows[index] * weight;
                        green += horizontalRows[index + 1] * weight;
                        blue += horizontalRows[index + 2] * weight;
                        alpha += horizontalRows[index + 3] * weight;
                    }

                    red /= verticalWeightSum;
                    green /= verticalWeightSum;
                    blue /= verticalWeightSum;
                    alpha /= verticalWeightSum;

                    double alphaUnit = Math.Clamp(alpha / 255, 0, 1);
                    result[x, y] = alphaUnit == 0
                        ? default
                        : new Rgba32(toByte((float)(red / (alpha / 255))), toByte((float)(green / (alpha / 255))), toByte((float)(blue / (alpha / 255))), toByte((float)(alphaUnit * 255)));
                }
            }

            return result;
        }

        private static void resizeHorizontalRow(RgbaImage source, int sourceY, int width, int[] firstValues, int[] weightOffsets, double[] weights, double[] weightSums, Span<float> destination)
        {
            // Filter premultiplied channels to avoid colour fringes around transparent pixels.
            for (int x = 0; x < width; x++)
            {
                int first = firstValues[x];
                int last = first + weightOffsets[x + 1] - weightOffsets[x] - 1;
                int weightOffset = weightOffsets[x];
                double red = 0;
                double green = 0;
                double blue = 0;
                double alpha = 0;

                for (int sourceX = first; sourceX <= last; sourceX++)
                {
                    double weight = weights[weightOffset++];
                    Rgba32 pixel = source[sourceX, sourceY];
                    double pixelAlpha = pixel.A / 255d;
                    red += pixel.R * pixelAlpha * weight;
                    green += pixel.G * pixelAlpha * weight;
                    blue += pixel.B * pixelAlpha * weight;
                    alpha += pixel.A * weight;
                }

                int index = x * 4;
                destination[index] = (float)(red / weightSums[x]);
                destination[index + 1] = (float)(green / weightSums[x]);
                destination[index + 2] = (float)(blue / weightSums[x]);
                destination[index + 3] = (float)(alpha / weightSums[x]);
            }
        }

        private static double lanczos(double value)
        {
            value = Math.Abs(value);
            if (value < 1e-8)
                return 1;
            if (value >= lanczos_radius)
                return 0;

            double angle = Math.PI * value;
            return Math.Sin(angle) / angle * (Math.Sin(angle / lanczos_radius) / (angle / lanczos_radius));
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
