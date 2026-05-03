// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Skinning
{
    public class LegacyTextureLoaderStore : IResourceStore<TextureUpload>
    {
        public const string MANIA_TIMING_COLOUR_TEXTURE_PREFIX = "__mania-timing-colour-note__/";

        private readonly IResourceStore<TextureUpload>? wrappedStore;
        private readonly object maniaTimingColourLock = new object();
        private readonly Dictionary<string, ManiaTimingColourTextureGroup> maniaTimingColourTextureGroups = new Dictionary<string, ManiaTimingColourTextureGroup>(StringComparer.OrdinalIgnoreCase);

        public LegacyTextureLoaderStore(IResourceStore<TextureUpload>? wrappedStore)
        {
            this.wrappedStore = wrappedStore;
        }

        public TextureUpload Get(string name)
        {
            if (tryGetManiaTimingColourTexture(name, out var processedTexture))
                return processedTexture;

            var textureUpload = wrappedStore?.Get(name);

            if (textureUpload == null)
                return null!;

            return shouldConvertToGrayscale(name)
                ? convertToGrayscale(textureUpload)
                : textureUpload;
        }

        public async Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = new CancellationToken())
        {
            if (tryGetManiaTimingColourTexture(name, out var processedTexture))
                return processedTexture;

            if (wrappedStore == null)
                return null!;

            var textureUpload = await wrappedStore.GetAsync(name, cancellationToken).ConfigureAwait(false);

            if (textureUpload == null)
                return null!;

            return shouldConvertToGrayscale(name)
                ? await Task.Run(() => convertToGrayscale(textureUpload), cancellationToken).ConfigureAwait(false)
                : textureUpload;
        }

        private bool tryGetManiaTimingColourTexture(string name, out TextureUpload textureUpload)
        {
            textureUpload = null!;

            if (!TryParseManiaTimingColourTextureName(name, out string groupName, out string sourceName))
                return false;

            ManiaTimingColourTextureGroup? group;

            lock (maniaTimingColourLock)
            {
                if (!maniaTimingColourTextureGroups.TryGetValue(groupName, out group))
                    return false;
            }

            var processedTexture = group.GetProcessedTexture(sourceName, wrappedStore);

            if (processedTexture == null)
                return false;

            textureUpload = processedTexture;
            return true;
        }

        public static string CreateManiaTimingColourTextureName(string groupName, string sourceName)
            => $"{MANIA_TIMING_COLOUR_TEXTURE_PREFIX}{groupName}/{sourceName}";

        public static bool TryParseManiaTimingColourTextureName(string name, out string groupName, out string sourceName)
        {
            groupName = string.Empty;
            sourceName = string.Empty;

            if (!name.StartsWith(MANIA_TIMING_COLOUR_TEXTURE_PREFIX, StringComparison.OrdinalIgnoreCase))
                return false;

            string rest = name.Substring(MANIA_TIMING_COLOUR_TEXTURE_PREFIX.Length);
            int slashIndex = rest.IndexOf('/');

            if (slashIndex <= 0 || slashIndex == rest.Length - 1)
                return false;

            groupName = rest.Substring(0, slashIndex);
            sourceName = rest.Substring(slashIndex + 1);
            return true;
        }

        public void RegisterManiaTimingColourTextureGroup(string groupName, IEnumerable<IEnumerable<string>> sourceTextureNameFallbacks)
        {
            lock (maniaTimingColourLock)
            {
                if (maniaTimingColourTextureGroups.ContainsKey(groupName))
                    return;
            }

            var group = new ManiaTimingColourTextureGroup(sourceTextureNameFallbacks, wrappedStore);

            lock (maniaTimingColourLock)
            {
                if (!maniaTimingColourTextureGroups.ContainsKey(groupName))
                    maniaTimingColourTextureGroups[groupName] = group;
            }
        }

        // https://github.com/peppy/osu-stable-reference/blob/013c3010a9d495e3471a9c59518de17006f9ad89/osu!/Graphics/Textures/TextureManager.cs#L91-L96
        private static readonly string[] grayscale_sprites =
        {
            @"taiko-bar-right",
            @"taikobigcircle",
            @"taikohitcircle",
            @"taikohitcircleoverlay"
        };

        private bool shouldConvertToGrayscale(string name)
        {
            foreach (string grayscaleSprite in grayscale_sprites)
            {
                // unfortunately at this level of lookup we can encounter `@2x` scale suffixes in the name,
                // so straight equality cannot be used.
                if (name.Equals(grayscaleSprite, StringComparison.OrdinalIgnoreCase)
                    || name.Equals($@"{grayscaleSprite}@2x", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private TextureUpload convertToGrayscale(TextureUpload textureUpload)
        {
            var image = Image.LoadPixelData(textureUpload.Data, textureUpload.Width, textureUpload.Height);

            // stable uses `0.299 * r + 0.587 * g + 0.114 * b`
            // (https://github.com/peppy/osu-stable-reference/blob/013c3010a9d495e3471a9c59518de17006f9ad89/osu!/Graphics/Textures/pTexture.cs#L138-L153)
            // which matches mode BT.601 (https://en.wikipedia.org/wiki/Grayscale#Luma_coding_in_video_systems)
            image.Mutate(i => i.Grayscale(GrayscaleMode.Bt601));

            return new TextureUpload(image);
        }

        private class ManiaTimingColourTextureGroup
        {
            private const byte alpha_threshold = 16;
            private const int histogram_bins = 256;
            private const float min_non_extreme_luminance = 0.08f;
            private const float max_non_extreme_luminance = 0.92f;

            private readonly Dictionary<string, TextureInfo> textureInfos = new Dictionary<string, TextureInfo>(StringComparer.OrdinalIgnoreCase);
            private readonly float targetBaseLuminance;

            public ManiaTimingColourTextureGroup(IEnumerable<IEnumerable<string>> sourceTextureNameFallbacks, IResourceStore<TextureUpload>? wrappedStore)
            {
                var availableResources = new HashSet<string>(wrappedStore?.GetAvailableResources() ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                var analysedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var fallbackNames in sourceTextureNameFallbacks)
                {
                    foreach (string sourceTextureName in getExistingTextureNames(fallbackNames, availableResources))
                    {
                        if (!analysedNames.Add(sourceTextureName))
                            continue;

                        if (!tryGetUploadForAnalysis(sourceTextureName, wrappedStore, out string actualSourceName, out var upload))
                            continue;

                        using var image = Image.LoadPixelData(upload.Data, upload.Width, upload.Height);
                        var info = analyseTexture(image);

                        addTextureInfo(sourceTextureName, info);
                        addTextureInfo(actualSourceName, info);
                        addTextureInfo(stripHighResolutionSuffix(sourceTextureName), info);
                        addTextureInfo(stripHighResolutionSuffix(actualSourceName), info);
                        upload.Dispose();
                    }
                }

                targetBaseLuminance = 0;

                foreach (var info in textureInfos.Values)
                    targetBaseLuminance = Math.Max(targetBaseLuminance, info.BaseLuminance);
            }

            public TextureUpload? GetProcessedTexture(string sourceName, IResourceStore<TextureUpload>? wrappedStore)
            {
                if (targetBaseLuminance <= 0)
                    return null;

                if (!textureInfos.TryGetValue(sourceName, out var info) && !textureInfos.TryGetValue(stripHighResolutionSuffix(sourceName), out info))
                    return null;

                if (!tryGetUploadForAnalysis(sourceName, wrappedStore, out _, out var upload))
                    return null;

                using var image = Image.LoadPixelData(upload.Data, upload.Width, upload.Height);
                upload.Dispose();

                return processTexture(image, info.BaseLuminance, targetBaseLuminance);
            }

            private static IEnumerable<string> getExistingTextureNames(IEnumerable<string> fallbackNames, HashSet<string> availableResources)
            {
                foreach (string fallbackName in fallbackNames.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var frameNames = getExistingFrameNames(fallbackName, availableResources).ToArray();

                    if (frameNames.Length > 0)
                    {
                        foreach (string frameName in frameNames)
                            yield return frameName;

                        yield break;
                    }

                    if (hasResource(fallbackName, availableResources))
                    {
                        yield return fallbackName;
                        yield break;
                    }
                }
            }

            private static IEnumerable<string> getExistingFrameNames(string sourceName, HashSet<string> availableResources)
            {
                for (int i = 0; ; i++)
                {
                    string frameName = $"{sourceName}-{i}";

                    if (!hasResource(frameName, availableResources))
                        yield break;

                    yield return frameName;
                }
            }

            private static bool hasResource(string sourceName, HashSet<string> availableResources)
            {
                if (availableResources.Contains(sourceName) || availableResources.Contains(getHighResolutionName(sourceName)))
                    return true;

                string sourceNameWithoutExtension = Path.ChangeExtension(sourceName, null);
                return availableResources.Any(resource =>
                    string.Equals(Path.ChangeExtension(resource, null), sourceNameWithoutExtension, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.ChangeExtension(stripHighResolutionSuffix(resource), null), sourceNameWithoutExtension, StringComparison.OrdinalIgnoreCase));
            }

            private static bool tryGetUploadForAnalysis(string sourceName, IResourceStore<TextureUpload>? wrappedStore, out string actualSourceName, out TextureUpload upload)
            {
                actualSourceName = sourceName;
                upload = null!;

                if (wrappedStore == null)
                    return false;

                string highResolutionName = getHighResolutionName(sourceName);

                if (!sourceName.Equals(highResolutionName, StringComparison.OrdinalIgnoreCase))
                {
                    upload = wrappedStore.Get(highResolutionName);

                    if (upload != null)
                    {
                        actualSourceName = highResolutionName;
                        return true;
                    }
                }

                upload = wrappedStore.Get(sourceName);
                return upload != null;
            }

            private void addTextureInfo(string name, TextureInfo info)
            {
                if (!string.IsNullOrEmpty(name))
                    textureInfos[name] = info;
            }

            private static TextureInfo analyseTexture(Image<Rgba32> image)
            {
                int[] histogram = new int[histogram_bins];
                int opaquePixels = 0;

                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        Span<Rgba32> row = accessor.GetRowSpan(y);

                        for (int x = 0; x < row.Length; x++)
                        {
                            Rgba32 pixel = row[x];

                            if (pixel.A < alpha_threshold)
                                continue;

                            int bin = (int)Math.Clamp(Math.Round(getLuminance(pixel) * (histogram_bins - 1)), 0, histogram_bins - 1);
                            histogram[bin]++;
                            opaquePixels++;
                        }
                    }
                });

                if (opaquePixels == 0)
                    return new TextureInfo(0.5f);

                int bestBin = -1;
                int bestCount = -1;
                int minBin = (int)MathF.Round(min_non_extreme_luminance * (histogram_bins - 1));
                int maxBin = (int)MathF.Round(max_non_extreme_luminance * (histogram_bins - 1));

                for (int i = minBin; i <= maxBin; i++)
                {
                    int count = histogram[i];

                    if (count > bestCount)
                    {
                        bestCount = count;
                        bestBin = i;
                    }
                }

                if (bestCount <= 0)
                    bestBin = getPercentileBin(histogram, opaquePixels, 0.5f);

                return new TextureInfo(bestBin / (float)(histogram_bins - 1));
            }

            private static int getPercentileBin(int[] histogram, int total, float percentile)
            {
                int target = (int)MathF.Round(total * percentile);
                int accumulated = 0;

                for (int i = 0; i < histogram.Length; i++)
                {
                    accumulated += histogram[i];

                    if (accumulated >= target)
                        return i;
                }

                return histogram.Length - 1;
            }

            private static TextureUpload processTexture(Image<Rgba32> image, float sourceBaseLuminance, float targetBaseLuminance)
            {
                float detailStrength = computeDetailStrength(image, sourceBaseLuminance, targetBaseLuminance);

                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        Span<Rgba32> row = accessor.GetRowSpan(y);

                        for (int x = 0; x < row.Length; x++)
                        {
                            Rgba32 pixel = row[x];
                            float luminance = getLuminance(pixel);
                            float adjusted = Math.Clamp(targetBaseLuminance + (luminance - sourceBaseLuminance) * detailStrength, 0, 1);
                            byte channel = (byte)Math.Clamp(MathF.Round(adjusted * 255), 0, 255);

                            row[x] = new Rgba32(channel, channel, channel, pixel.A);
                        }
                    }
                });

                return new TextureUpload(image.Clone());
            }

            private static float computeDetailStrength(Image<Rgba32> image, float sourceBaseLuminance, float targetBaseLuminance)
            {
                float minStrength = 1f;

                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        Span<Rgba32> row = accessor.GetRowSpan(y);

                        for (int x = 0; x < row.Length; x++)
                        {
                            Rgba32 pixel = row[x];

                            if (pixel.A < alpha_threshold)
                                continue;

                            float detail = getLuminance(pixel) - sourceBaseLuminance;

                            if (detail > 0 && targetBaseLuminance + detail > 1)
                                minStrength = Math.Min(minStrength, (1 - targetBaseLuminance) / detail);
                            else if (detail < 0 && targetBaseLuminance + detail < 0)
                                minStrength = Math.Min(minStrength, targetBaseLuminance / -detail);
                        }
                    }
                });

                return Math.Clamp(minStrength, 0.25f, 1f);
            }

            private static float getLuminance(Rgba32 pixel)
                => pixel.R / 255f * 0.299f
                   + pixel.G / 255f * 0.587f
                   + pixel.B / 255f * 0.114f;

            private static string stripHighResolutionSuffix(string name)
                => name.Replace("@2x", string.Empty, StringComparison.OrdinalIgnoreCase);

            private static string getHighResolutionName(string name)
            {
                string nameWithoutHighResolutionSuffix = stripHighResolutionSuffix(name);
                return $"{Path.ChangeExtension(nameWithoutHighResolutionSuffix, null)}@2x{Path.GetExtension(nameWithoutHighResolutionSuffix)}";
            }

            private readonly record struct TextureInfo(float BaseLuminance);
        }

        public Stream? GetStream(string name) => wrappedStore?.GetStream(name);

        public IEnumerable<string> GetAvailableResources() => wrappedStore?.GetAvailableResources() ?? Array.Empty<string>();

        public void Dispose()
        {
            wrappedStore?.Dispose();
        }
    }
}
