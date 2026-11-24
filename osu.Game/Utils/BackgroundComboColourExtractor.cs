// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Imaging;

namespace osu.Game.Utils
{
    /// <summary>
    /// Extracts prominent combo colours from a beatmap background.
    /// </summary>
    public static class BackgroundComboColourExtractor
    {
        public const int MAX_COLOUR_COUNT = 8;

        // Resolution to downscale to before quantizing.
        private const int max_dimension = 128;

        // Ranking criteria: avoid colours with lightness <50 or >220.
        private const float min_lightness = 50 / 255f;
        private const float max_lightness = 220 / 255f;

        public static Colour4[] Extract(Stream imageStream)
        {
            using var image = ImageCodec.Decode(imageStream);
            using var resized = ImageProcessing.Resize(image, max_dimension, max_dimension, ResizeMode.Max);
            return quantize(resized);
        }

        private static Colour4[] quantize(RgbaImage image)
        {
            const int channel_bits = 5;
            const int channel_size = 1 << channel_bits;
            const int green_shift = channel_bits;
            const int red_shift = channel_bits * 2;

            var bins = new ColourBin[channel_size * channel_size * channel_size];
            var populatedBins = new List<int>();

            foreach (Rgba32 pixel in image.PixelSpan)
            {
                if (pixel.A < 128)
                    continue;

                int index = (pixel.R >> (8 - channel_bits) << red_shift)
                            | (pixel.G >> (8 - channel_bits) << green_shift)
                            | pixel.B >> (8 - channel_bits);

                if (bins[index].Count == 0)
                    populatedBins.Add(index);

                bins[index].Add(pixel);
            }

            if (populatedBins.Count == 0)
                return Array.Empty<Colour4>();

            var boxes = new List<ColourBox> { new ColourBox(populatedBins) };

            while (boxes.Count < MAX_COLOUR_COUNT)
            {
                int splitIndex = findSplittableBox(boxes, bins);
                if (splitIndex < 0)
                    break;

                ColourBox box = boxes[splitIndex];
                int axis = box.LongestAxis(bins);
                List<int> orderedBins = box.Bins;
                orderedBins.Sort((first, second) => channel(first, axis).CompareTo(channel(second, axis)));

                int targetCount = box.Count(bins) / 2;
                int accumulatedCount = 0;
                int splitAt = 0;
                while (splitAt < orderedBins.Count - 1 && accumulatedCount < targetCount)
                    accumulatedCount += bins[orderedBins[splitAt++]].Count;

                boxes[splitIndex] = new ColourBox(orderedBins.GetRange(0, splitAt));
                boxes.Insert(splitIndex + 1, new ColourBox(orderedBins.GetRange(splitAt, orderedBins.Count - splitAt)));
            }

            var result = new Colour4[boxes.Count];

            for (int i = 0; i < boxes.Count; i++)
            {
                ColourBox box = boxes[i];
                long red = 0;
                long green = 0;
                long blue = 0;
                long count = 0;

                foreach (int index in box.Bins)
                {
                    red += bins[index].Red;
                    green += bins[index].Green;
                    blue += bins[index].Blue;
                    count += bins[index].Count;
                }

                result[i] = clampLightness(new Colour4(red / (count * 255f), green / (count * 255f), blue / (count * 255f), 1));
            }

            return result;
        }

        private static int findSplittableBox(List<ColourBox> boxes, ColourBin[] bins)
        {
            int splitIndex = -1;
            long bestScore = 0;

            for (int i = 0; i < boxes.Count; i++)
            {
                ColourBox box = boxes[i];
                if (box.Bins.Count < 2)
                    continue;

                long score = (long)box.LongestRange(bins) * box.Count(bins);

                if (score > bestScore)
                {
                    bestScore = score;
                    splitIndex = i;
                }
            }

            return splitIndex;
        }

        private static int channel(int index, int axis) => axis switch
        {
            0 => index >> 10,
            1 => index >> 5 & 31,
            _ => index & 31,
        };

        private struct ColourBin
        {
            public int Count;
            public long Red;
            public long Green;
            public long Blue;

            public void Add(Rgba32 pixel)
            {
                Count++;
                Red += pixel.R;
                Green += pixel.G;
                Blue += pixel.B;
            }
        }

        private sealed class ColourBox
        {
            public List<int> Bins { get; }

            public ColourBox(List<int> bins)
            {
                Bins = bins;
            }

            public int Count(ColourBin[] bins)
            {
                int result = 0;
                foreach (int index in Bins)
                    result += bins[index].Count;
                return result;
            }

            public int LongestAxis(ColourBin[] bins)
            {
                int redRange = 0;
                int greenRange = 0;
                int blueRange = 0;
                int minRed = int.MaxValue;
                int minGreen = int.MaxValue;
                int minBlue = int.MaxValue;
                int maxRed = int.MinValue;
                int maxGreen = int.MinValue;
                int maxBlue = int.MinValue;

                foreach (int index in Bins)
                {
                    int red = channel(index, 0);
                    int green = channel(index, 1);
                    int blue = channel(index, 2);
                    minRed = Math.Min(minRed, red);
                    minGreen = Math.Min(minGreen, green);
                    minBlue = Math.Min(minBlue, blue);
                    maxRed = Math.Max(maxRed, red);
                    maxGreen = Math.Max(maxGreen, green);
                    maxBlue = Math.Max(maxBlue, blue);
                }

                redRange = maxRed - minRed;
                greenRange = maxGreen - minGreen;
                blueRange = maxBlue - minBlue;

                return redRange >= greenRange && redRange >= blueRange ? 0 : greenRange >= blueRange ? 1 : 2;
            }

            public int LongestRange(ColourBin[] bins)
            {
                int redRange = 0;
                int greenRange = 0;
                int blueRange = 0;
                int minRed = int.MaxValue;
                int minGreen = int.MaxValue;
                int minBlue = int.MaxValue;
                int maxRed = int.MinValue;
                int maxGreen = int.MinValue;
                int maxBlue = int.MinValue;

                foreach (int index in Bins)
                {
                    int red = channel(index, 0);
                    int green = channel(index, 1);
                    int blue = channel(index, 2);
                    minRed = Math.Min(minRed, red);
                    minGreen = Math.Min(minGreen, green);
                    minBlue = Math.Min(minBlue, blue);
                    maxRed = Math.Max(maxRed, red);
                    maxGreen = Math.Max(maxGreen, green);
                    maxBlue = Math.Max(maxBlue, blue);
                }

                redRange = maxRed - minRed;
                greenRange = maxGreen - minGreen;
                blueRange = maxBlue - minBlue;
                return Math.Max(redRange, Math.Max(greenRange, blueRange));
            }
        }

        private static Colour4 clampLightness(Colour4 colour)
        {
            var hsl = colour.ToHSL();
            return Colour4.FromHSL(hsl.X, hsl.Y, Math.Clamp(hsl.Z, min_lightness, max_lightness));
        }
    }
}
