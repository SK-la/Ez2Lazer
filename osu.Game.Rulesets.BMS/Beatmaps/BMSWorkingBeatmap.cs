// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.IO;
using osu.Game.Rulesets.Objects;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using FileInfo = System.IO.FileInfo;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// A WorkingBeatmap that loads directly from BMS files on disk,
    /// without importing into the osu! database.
    /// </summary>
    public class BMSWorkingBeatmap : WorkingBeatmap
    {
        private readonly string bmsFilePath;
        private readonly AudioManager audioManager;
        private readonly TextureStore? textures;
        private readonly IResourceStore<TextureUpload>? backgroundTextureLoader;
        private readonly BMSChartCache? chartCache;

        private BMSBeatmap? cachedBeatmap;

        /// <summary>
        /// Gets the folder path containing the BMS file.
        /// </summary>
        public string FolderPath { get; }

        public BMSWorkingBeatmap(
            string bmsFilePath,
            AudioManager audioManager,
            TextureStore? textures = null,
            BMSChartCache? chartCache = null)
            : base(createBeatmapInfo(bmsFilePath, chartCache), audioManager)
        {
            this.bmsFilePath = bmsFilePath;
            FolderPath = Path.GetDirectoryName(bmsFilePath) ?? string.Empty;
            this.audioManager = audioManager;
            this.textures = textures;
            this.chartCache = chartCache;

            if (this.textures != null && Directory.Exists(FolderPath))
            {
                var storage = new NativeStorage(FolderPath);
                backgroundTextureLoader = new TextureLoaderStore(new StorageBackedResourceStore(storage));
                this.textures.AddTextureSource(backgroundTextureLoader);
            }
        }

        /// <summary>
        /// Create a BeatmapInfo from BMS file path (minimal info for display).
        /// Full parsing happens in GetBeatmap().
        /// </summary>
        private static BeatmapInfo createBeatmapInfo(string bmsFilePath, BMSChartCache? chartCache)
        {
            var fileInfo = new FileInfo(bmsFilePath);

            string? title = chartCache?.Title;
            if (string.IsNullOrWhiteSpace(title))
                title = fileInfo.Name;

            string? artist = chartCache?.Artist;
            if (string.IsNullOrWhiteSpace(artist))
                artist = "BMS";

            string? difficultyName = chartCache?.SubTitle;
            if (string.IsNullOrWhiteSpace(difficultyName))
                difficultyName = Path.GetFileNameWithoutExtension(bmsFilePath);

            var beatmapInfo = new BeatmapInfo(new BMSRuleset().RulesetInfo)
            {
                Metadata = new BeatmapMetadata
                {
                    Title = title,
                    Artist = artist,
                    Source = "BMS Import",
                    AudioFile = sanitiseAudioReference(chartCache?.PreviewFile ?? chartCache?.AudioFile, chartCache?.FolderPath ?? fileInfo.DirectoryName) ?? string.Empty,
                },
                DifficultyName = difficultyName,
                Difficulty = new BeatmapDifficulty
                {
                    CircleSize = chartCache?.KeyCount ?? 7,
                },
                BPM = chartCache?.Bpm ?? 0,
                Length = chartCache?.Duration ?? 0,
                MD5Hash = chartCache?.Md5Hash ?? string.Empty,
                Hash = chartCache?.Md5Hash ?? string.Empty,
                TotalObjectCount = chartCache?.TotalNotes ?? -1,
                EndTimeObjectCount = chartCache?.TotalNotes ?? -1,
                BeatmapSet = new BeatmapSetInfo
                {
                    OnlineID = -1,
                },
            };

            if (chartCache != null)
            {
                beatmapInfo.Metadata.PreviewTime = chartCache.PreviewTime;
                beatmapInfo.Metadata.BackgroundFile = string.Empty;
            }

            return beatmapInfo;
        }

        private static string? sanitiseAudioReference(string? raw, string? baseFolder)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            string trimmed = raw.Trim();
            trimmed = trimmed.Replace('\\', '/');

            if (!Path.IsPathRooted(trimmed))
                return trimmed;

            if (!string.IsNullOrWhiteSpace(baseFolder))
            {
                try
                {
                    string fullBase = Path.GetFullPath(baseFolder);
                    string fullPath = Path.GetFullPath(trimmed);
                    string relative = Path.GetRelativePath(fullBase, fullPath);

                    if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
                        return relative.Replace('\\', '/');
                }
                catch
                {
                    // Fall back to file name below.
                }
            }

            return Path.GetFileName(trimmed);
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
                    BeatmapInfo.Ruleset = cachedBeatmap.BeatmapInfo.Ruleset;
                    BeatmapInfo.Metadata.Title = cachedBeatmap.BeatmapInfo.Metadata.Title;
                    BeatmapInfo.Metadata.Artist = cachedBeatmap.BeatmapInfo.Metadata.Artist;
                    BeatmapInfo.Metadata.Source = "BMS Import";
                    BeatmapInfo.Metadata.AudioFile = sanitiseAudioReference(cachedBeatmap.BeatmapInfo.Metadata.AudioFile, FolderPath) ?? string.Empty;
                    BeatmapInfo.Metadata.BackgroundFile = cachedBeatmap.BeatmapInfo.Metadata.BackgroundFile;
                    BeatmapInfo.Metadata.PreviewTime = cachedBeatmap.BeatmapInfo.Metadata.PreviewTime;
                    BeatmapInfo.DifficultyName = cachedBeatmap.BeatmapInfo.DifficultyName;
                    BeatmapInfo.Difficulty = cachedBeatmap.BeatmapInfo.Difficulty.Clone();
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
            if (textures == null)
                return null;

            string backgroundFile = BeatmapInfo.Metadata.BackgroundFile;
            if (string.IsNullOrEmpty(backgroundFile))
                return null;

            var texture = textures.Get(backgroundFile);

            if (texture == null)
            {
                Logger.Log($"BMS background not found: {backgroundFile}", LoggingTarget.Runtime, LogLevel.Debug);
                return null;
            }

            return texture;
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

        protected override Storyboard GetStoryboard()
        {
            var storyboard = base.GetStoryboard();

            if (GetBeatmap() is not BMSBeatmap beatmap || beatmap.BackgroundSoundEvents.Count == 0)
                return storyboard;

            BmsStoryboardPreviewAugment.Augment(storyboard, beatmap.BackgroundSoundEvents);
            return storyboard;
        }

        protected override ISkin GetSkin()
        {
            // Return a skin that can load keysounds from the BMS folder
            return new BMSSkin(FolderPath, audioManager);
        }

        public override Stream? GetStream(string storagePath)
        {
            // Try to load from the BMS folder
            string fullPath = Path.Combine(FolderPath, storagePath);
            if (File.Exists(fullPath))
                return File.OpenRead(fullPath);

            return null;
        }

        internal static string? ResolveAudioPath(string folderPath, string? relativePath)
        {
            if (!string.IsNullOrWhiteSpace(relativePath))
            {
                string normalisedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
                string directPath = Path.Combine(folderPath, normalisedRelativePath);

                if (File.Exists(directPath))
                    return directPath;

                string directory = Path.GetDirectoryName(normalisedRelativePath) ?? string.Empty;
                string baseName = Path.GetFileNameWithoutExtension(normalisedRelativePath);

                foreach (string extension in new[] { ".wav", ".ogg", ".mp3", ".flac" })
                {
                    string candidate = Path.Combine(folderPath, directory, baseName + extension);

                    if (File.Exists(candidate))
                        return candidate;
                }
            }

            foreach (string extension in new[] { "*.ogg", "*.mp3", "*.wav", "*.flac" })
            {
                string? fallback = Directory.GetFiles(folderPath, extension, SearchOption.TopDirectoryOnly)
                                            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                                            .FirstOrDefault();

                if (fallback != null)
                    return fallback;
            }

            return null;
        }
    }
}
