// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Audio;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Wraps another <see cref="ISkin"/> (typically the Mania transformer or the legacy beatmap skin)
    /// and falls back to loading samples from the BMS chart folder on disk when the inner skin can't
    /// resolve a lookup.
    ///
    /// This is the seam that lets a standard <see cref="osu.Game.Beatmaps.WorkingBeatmap"/> still hear
    /// keysounds for an external BMS chart at gameplay time, without copying the keysound files into
    /// Realm or replacing the working beatmap type.
    /// </summary>
    public sealed class BMSExternalSampleSkin : ISkin
    {
        private static readonly string[] sample_extensions = { string.Empty, ".wav", ".ogg", ".mp3", ".flac" };
        private static readonly object load_log_gate = new object();
        private static int loadLogCount;

        private readonly ISkin inner;
        private readonly string bmsFolder;
        private readonly Func<AudioManager?> audioProvider;
        private readonly Dictionary<string, ISample?> resolvedSamples = new Dictionary<string, ISample?>(StringComparer.OrdinalIgnoreCase);
        private ISampleStore? folderSampleStore;
        private bool storeInitialised;

        public BMSExternalSampleSkin(ISkin inner, string bmsFolder, Func<AudioManager?> audioProvider)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.bmsFolder = bmsFolder ?? string.Empty;
            this.audioProvider = audioProvider ?? throw new ArgumentNullException(nameof(audioProvider));
        }

        /// <summary>Routed legacy / Mania skin without folder fallback.</summary>
        public ISkin Inner => inner;

        private ISampleStore? ensureStore()
        {
            if (storeInitialised)
                return folderSampleStore;

            var audioManager = audioProvider();

            if (audioManager == null || !Directory.Exists(bmsFolder))
                return null;

            try
            {
                var storage = new NativeStorage(bmsFolder);
                var resourceStore = new StorageBackedResourceStore(storage);
                folderSampleStore = audioManager.GetSampleStore(resourceStore);
                storeInitialised = true;
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] Failed to create external sample store for '{bmsFolder}': {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                folderSampleStore = null;
                storeInitialised = true;
            }

            return folderSampleStore;
        }

        public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => inner.GetDrawableComponent(lookup);

        public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => inner.GetTexture(componentName, wrapModeS, wrapModeT);

        public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            where TLookup : notnull
            where TValue : notnull
            => inner.GetConfig<TLookup, TValue>(lookup);

        public ISample? GetSample(ISampleInfo sampleInfo)
        {
            var fromInner = inner.GetSample(sampleInfo);

            if (fromInner != null)
                return fromInner;

            var store = ensureStore();

            if (store == null)
                return null;

            foreach (string lookupName in sampleInfo.LookupNames)
            {
                if (string.IsNullOrWhiteSpace(lookupName))
                    continue;

                var sample = tryResolve(lookupName, store);

                if (sample != null)
                    return sample;
            }

            return null;
        }

        private ISample? tryResolve(string lookupName, ISampleStore store)
        {
            if (resolvedSamples.TryGetValue(lookupName, out var cached))
                return cached;

            string? foundFilename = probeFilename(lookupName);

            if (foundFilename == null)
            {
                resolvedSamples[lookupName] = null;
                return null;
            }

            try
            {
                var sample = store.Get(foundFilename);
                resolvedSamples[lookupName] = sample;

                lock (load_log_gate)
                {
                    if (loadLogCount < 5)
                    {
                        loadLogCount++;
                        Logger.Log($"[BMS] External sample resolved: {lookupName} -> {foundFilename}", LoggingTarget.Runtime, LogLevel.Debug);
                    }
                }

                return sample;
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] External sample load failed for '{lookupName}': {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                resolvedSamples[lookupName] = null;
                return null;
            }
        }

        private string? probeFilename(string lookupName)
        {
            string normalised = lookupName.Replace('\\', '/');

            string direct = Path.Combine(bmsFolder, normalised.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(direct))
                return normalised;

            string directory = Path.GetDirectoryName(normalised) ?? string.Empty;
            string baseName = Path.GetFileNameWithoutExtension(normalised);

            if (string.IsNullOrEmpty(baseName))
                return null;

            string[] caseVariants = buildCaseVariants(baseName);

            foreach (string ext in sample_extensions)
            {
                foreach (string variant in caseVariants)
                {
                    string candidateRelative = string.IsNullOrEmpty(directory)
                        ? variant + ext
                        : directory + "/" + variant + ext;

                    string candidateFull = Path.Combine(bmsFolder, candidateRelative.Replace('/', Path.DirectorySeparatorChar));

                    if (File.Exists(candidateFull))
                        return candidateRelative;
                }
            }

            return null;
        }

        private static string[] buildCaseVariants(string baseName)
        {
            string lower = baseName.ToLowerInvariant();
            string upper = baseName.ToUpperInvariant();

            return new[] { baseName, lower, upper }
                   .Distinct(StringComparer.Ordinal)
                   .ToArray();
        }
    }
}
