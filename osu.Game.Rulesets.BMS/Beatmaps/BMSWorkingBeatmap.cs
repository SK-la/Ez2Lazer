// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.IO;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// A WorkingBeatmap that loads directly from BMS files on disk,
    /// without importing into the osu! database.
    /// </summary>
    public class BMSWorkingBeatmap : WorkingBeatmap
    {
        private readonly string bmsFilePath;
        private readonly string folderPath;
        private readonly AudioManager audioManager;
        private readonly TextureStore? textures;

        private BMSBeatmap? cachedBeatmap;
        private readonly Dictionary<string, Track> keysoundCache = new();

        /// <summary>
        /// Gets the folder path containing the BMS file.
        /// </summary>
        public string FolderPath => folderPath;

        public BMSWorkingBeatmap(
            string bmsFilePath,
            AudioManager audioManager,
            TextureStore? textures = null)
            : base(CreateBeatmapInfo(bmsFilePath), audioManager)
        {
            this.bmsFilePath = bmsFilePath;
            this.folderPath = Path.GetDirectoryName(bmsFilePath) ?? string.Empty;
            this.audioManager = audioManager;
            this.textures = textures;
        }

        /// <summary>
        /// Create a BeatmapInfo from BMS file path (minimal info for display).
        /// Full parsing happens in GetBeatmap().
        /// </summary>
        private static BeatmapInfo CreateBeatmapInfo(string bmsFilePath)
        {
            var fileInfo = new System.IO.FileInfo(bmsFilePath);

            return new BeatmapInfo
            {
                Metadata = new BeatmapMetadata
                {
                    Title = fileInfo.Name,
                    Artist = "BMS",
                    Source = "BMS Import",
                },
                DifficultyName = Path.GetFileNameWithoutExtension(bmsFilePath),
                BeatmapSet = new BeatmapSetInfo
                {
                    OnlineID = -1,
                },
            };
        }

        protected override IBeatmap GetBeatmap()
        {
            if (cachedBeatmap != null)
                return cachedBeatmap;

            try
            {
                using var stream = File.OpenRead(bmsFilePath);
                using var reader = new LineBufferedReader(stream);
                var decoder = new BMSBeatmapDecoder();
                var result = decoder.Decode(reader);

                if (result is BMSBeatmap bmsBeatmap)
                {
                    cachedBeatmap = bmsBeatmap;
                    // Update BeatmapInfo with parsed metadata
                    BeatmapInfo.Metadata.Title = cachedBeatmap.BeatmapInfo.Metadata.Title;
                    BeatmapInfo.Metadata.Artist = cachedBeatmap.BeatmapInfo.Metadata.Artist;
                    BeatmapInfo.Metadata.Source = "BMS Import";
                    BeatmapInfo.BPM = cachedBeatmap.BeatmapInfo.BPM;
                }
                else
                {
                    cachedBeatmap = new BMSBeatmap();
                }

                return cachedBeatmap;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to parse BMS file: {bmsFilePath}");
                return new BMSBeatmap();
            }
        }

        public override Texture? GetBackground()
        {
            // For now, return null - background loading requires more complex texture loading
            // TODO: Implement proper background loading from BMS folder
            return null;
        }

        protected override Track GetBeatmapTrack()
        {
            // BMS doesn't have a main track - keysounds ARE the track
            // We need to create a virtual track of the appropriate length
            var beatmap = GetBeatmap();
            double length = 0;

            if (beatmap.HitObjects.Count > 0)
            {
                var lastObject = beatmap.HitObjects.MaxBy(h => h.GetEndTime());
                if (lastObject != null)
                    length = lastObject.GetEndTime() + 2000; // Add 2 seconds padding
            }

            return audioManager.Tracks.GetVirtual(Math.Max(length, 60000));
        }

        protected override ISkin GetSkin()
        {
            // Return a skin that can load keysounds from the BMS folder
            return new BMSSkin(folderPath, audioManager);
        }

        public override Stream? GetStream(string storagePath)
        {
            // Try to load from the BMS folder
            string fullPath = Path.Combine(folderPath, storagePath);
            if (File.Exists(fullPath))
                return File.OpenRead(fullPath);

            return null;
        }

        /// <summary>
        /// Preload all keysounds for this beatmap.
        /// Call this during loading screen to avoid hitches during gameplay.
        /// </summary>
        public void PreloadKeysounds()
        {
            var beatmap = GetBeatmap() as BMSBeatmap;
            if (beatmap == null) return;

            // Collect all unique keysound files
            var keysoundFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hitObject in beatmap.HitObjects)
            {
                foreach (var sample in hitObject.Samples)
                {
                    if (sample is ConvertHitObjectParser.FileHitSampleInfo fileSample)
                    {
                        keysoundFiles.Add(fileSample.Filename);
                    }
                }
            }

            Logger.Log($"Preloading {keysoundFiles.Count} keysounds for {Path.GetFileName(bmsFilePath)}");

            foreach (var filename in keysoundFiles)
            {
                try
                {
                    LoadKeysound(filename);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to preload keysound {filename}: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                }
            }
        }

        /// <summary>
        /// Load a keysound from the BMS folder.
        /// </summary>
        public Track? LoadKeysound(string filename)
        {
            if (keysoundCache.TryGetValue(filename, out var cached))
                return cached;

            string fullPath = Path.Combine(folderPath, filename);

            // Try with different extensions if file doesn't exist
            if (!File.Exists(fullPath))
            {
                var extensions = new[] { ".wav", ".ogg", ".mp3", ".flac" };
                string baseName = Path.GetFileNameWithoutExtension(filename);

                foreach (var ext in extensions)
                {
                    string testPath = Path.Combine(folderPath, baseName + ext);
                    if (File.Exists(testPath))
                    {
                        fullPath = testPath;
                        break;
                    }
                }
            }

            if (!File.Exists(fullPath))
            {
                Logger.Log($"Keysound not found: {filename}", LoggingTarget.Runtime, LogLevel.Debug);
                return null;
            }

            try
            {
                var track = audioManager.Tracks.Get(fullPath);
                keysoundCache[filename] = track;
                return track;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to load keysound {filename}: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                return null;
            }
        }

        /// <summary>
        /// Dispose all cached keysounds.
        /// </summary>
        public void DisposeKeysounds()
        {
            foreach (var track in keysoundCache.Values)
            {
                track?.Dispose();
            }
            keysoundCache.Clear();
        }
    }

    /// <summary>
    /// A skin that provides keysound samples from the BMS folder.
    /// </summary>
    public class BMSSkin : ISkin
    {
        private readonly string folderPath;
        private readonly AudioManager audioManager;

        public BMSSkin(string folderPath, AudioManager audioManager)
        {
            this.folderPath = folderPath;
            this.audioManager = audioManager;
        }

        public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;

        public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

        public ISample? GetSample(ISampleInfo sampleInfo)
        {
            if (sampleInfo is not ConvertHitObjectParser.FileHitSampleInfo fileSample)
                return null;

            string filename = fileSample.Filename;
            string fullPath = Path.Combine(folderPath, filename);

            // Try different extensions
            if (!File.Exists(fullPath))
            {
                var extensions = new[] { ".wav", ".ogg", ".mp3", ".flac" };
                string baseName = Path.GetFileNameWithoutExtension(filename);

                foreach (var ext in extensions)
                {
                    string testPath = Path.Combine(folderPath, baseName + ext);
                    if (File.Exists(testPath))
                    {
                        fullPath = testPath;
                        break;
                    }
                }
            }

            if (!File.Exists(fullPath))
                return null;

            try
            {
                return audioManager.Samples.Get(fullPath);
            }
            catch
            {
                return null;
            }
        }

        public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            where TLookup : notnull
            where TValue : notnull
        {
            return null;
        }
    }
}
