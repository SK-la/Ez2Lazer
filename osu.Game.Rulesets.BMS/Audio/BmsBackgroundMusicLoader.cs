// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Rulesets.BMS.Audio
{
    /// <summary>
    /// Loads background music/atmosphere from BMS files.
    /// In BMS format, all sounds except note keysounds contribute to the "background" audio.
    ///
    /// Implementation note: we mount the BMS folder as its own <see cref="ITrackStore"/> via
    /// <see cref="NativeStorage"/>+<see cref="StorageBackedResourceStore"/>+<see cref="AudioManager.GetTrackStore"/>.
    /// Calling <c>audioManager.Tracks.Get(absolutePath)</c> directly would route through the global game-data
    /// <see cref="NativeStorage"/>, which rejects external paths with <see cref="ArgumentException"/>
    /// ("traverses outside of …"). The mounted store is the only way to read external BMS audio without
    /// modifying osu.Game.
    /// </summary>
    public static class BmsBackgroundMusicLoader
    {
        /// <summary>
        /// Try to load a background music track from a BMS file.
        /// Looks for common BGM channels like #01 or uses first available WAV.
        /// </summary>
        public static Track? TryLoadBgmTrack(AudioManager audioManager, string bmsFilePath)
        {
            if (!File.Exists(bmsFilePath))
                return null;

            try
            {
                string folderPath = Path.GetDirectoryName(bmsFilePath) ?? string.Empty;
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                    return null;

                var wavDefinitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // Parse BMS file to extract WAV definitions
                using (var reader = new StreamReader(bmsFilePath))
                {
                    string? line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("#WAV", StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Parse #WAVxx filename format
                        int spaceIdx = line.IndexOf(' ');

                        if (spaceIdx > 4)
                        {
                            string index = line.Substring(4, spaceIdx - 4);
                            string filename = line.Substring(spaceIdx + 1).Trim();
                            wavDefinitions[index] = filename;
                        }
                    }
                }

                // Try to find BGM file (typically #01 or #00)
                string? bgmFile = null;

                if (wavDefinitions.TryGetValue("01", out string? f01))
                    bgmFile = f01;
                else if (wavDefinitions.TryGetValue("00", out string? f00))
                    bgmFile = f00;
                else if (wavDefinitions.Count > 0)
                    bgmFile = wavDefinitions.First().Value;

                if (string.IsNullOrEmpty(bgmFile))
                    return null;

                // Resolve a folder-relative reference. We may need to swap extensions or fall back to a probe.
                string? relative = probeRelative(folderPath, bgmFile);

                if (relative == null)
                {
                    Logger.Log($"[BMS] Background music file not found near: {bgmFile}", LoggingTarget.Runtime, LogLevel.Debug);
                    return null;
                }

                var storage = new NativeStorage(folderPath);
                var resourceStore = new StorageBackedResourceStore(storage);
                var trackStore = audioManager.GetTrackStore(resourceStore);

                var track = trackStore.Get(relative);
                Logger.Log($"[BMS] Loaded background music: {relative}", LoggingTarget.Runtime, LogLevel.Debug);
                return track;
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] Failed to load background music: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
                return null;
            }
        }

        private static readonly string[] track_extensions = { ".ogg", ".wav", ".mp3", ".flac" };

        private static string? probeRelative(string folderPath, string filename)
        {
            string normalised = filename.Replace('\\', '/');

            string direct = Path.Combine(folderPath, normalised.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(direct))
                return normalised;

            string directory = Path.GetDirectoryName(normalised) ?? string.Empty;
            string baseName = Path.GetFileNameWithoutExtension(normalised);

            if (string.IsNullOrEmpty(baseName))
                return null;

            foreach (string ext in track_extensions)
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
