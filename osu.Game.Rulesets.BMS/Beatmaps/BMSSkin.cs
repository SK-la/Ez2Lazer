// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
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
    /// A skin that provides keysound samples from a BMS folder on disk.
    ///
    /// Implementation note: this class MUST NOT call AudioManager.Samples or AudioManager.Tracks directly.
    /// osu-framework's NativeStorage wraps the global game data folder and rejects any absolute path that
    /// "traverses outside of" that root with ArgumentException, which silently breaks both song-select preview
    /// and in-game keysound playback for external BMS folders.
    ///
    /// Instead we mount the BMS folder as its own ISampleStore via NativeStorage + StorageBackedResourceStore
    /// + AudioManager.GetSampleStore, and look up samples by paths relative to that mount.
    /// </summary>
    public class BMSSkin : ISkin
    {
        private static readonly string[] sample_extensions = { ".wav", ".ogg", ".mp3", ".flac" };

        private readonly string folderPath;
        private readonly AudioManager audioManager;
        private readonly Dictionary<string, ISample?> resolvedSamples = new Dictionary<string, ISample?>(StringComparer.OrdinalIgnoreCase);
        private ISampleStore? folderSampleStore;
        private bool storeInitialised;

        public BMSSkin(string folderPath, AudioManager audioManager)
        {
            this.folderPath = folderPath ?? string.Empty;
            this.audioManager = audioManager;
        }

        public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;

        public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

        public ISample? GetSample(ISampleInfo sampleInfo)
        {
            ISampleStore? store = ensureStore();

            if (store == null)
                return null;

            foreach (string lookupName in sampleInfo.LookupNames)
            {
                if (string.IsNullOrWhiteSpace(lookupName))
                    continue;

                ISample? sample = tryResolve(lookupName, store);

                if (sample != null)
                    return sample;
            }

            return null;
        }

        public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            where TLookup : notnull
            where TValue : notnull
        {
            return null;
        }

        private ISampleStore? ensureStore()
        {
            if (storeInitialised)
                return folderSampleStore;

            if (audioManager == null || !Directory.Exists(folderPath))
            {
                storeInitialised = true;
                return null;
            }

            try
            {
                var storage = new NativeStorage(folderPath);
                var resourceStore = new StorageBackedResourceStore(storage);
                folderSampleStore = audioManager.GetSampleStore(resourceStore);
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] BMSSkin failed to mount sample store for '{folderPath}': {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
                folderSampleStore = null;
            }
            finally
            {
                storeInitialised = true;
            }

            return folderSampleStore;
        }

        private ISample? tryResolve(string lookupName, ISampleStore store)
        {
            if (resolvedSamples.TryGetValue(lookupName, out ISample? cached))
                return cached;

            string? probedRelative = probeRelativePath(lookupName);

            if (probedRelative == null)
            {
                resolvedSamples[lookupName] = null;
                return null;
            }

            try
            {
                ISample? sample = store.Get(probedRelative);
                resolvedSamples[lookupName] = sample;
                return sample;
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] BMSSkin sample load failed for '{lookupName}': {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                resolvedSamples[lookupName] = null;
                return null;
            }
        }

        private string? probeRelativePath(string lookupName)
        {
            string normalised = lookupName.Replace('\\', '/');
            string direct = Path.Combine(folderPath, normalised.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(direct))
                return normalised;

            string directory = Path.GetDirectoryName(normalised) ?? string.Empty;
            string baseName = Path.GetFileNameWithoutExtension(normalised);

            if (string.IsNullOrEmpty(baseName))
                return null;

            foreach (string ext in sample_extensions)
            {
                string candidateRelative = string.IsNullOrEmpty(directory) ? baseName + ext : directory + "/" + baseName + ext;
                string candidateFull = Path.Combine(folderPath, candidateRelative.Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(candidateFull))
                    return candidateRelative;
            }

            return null;
        }
    }
}
