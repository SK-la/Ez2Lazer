// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Logging;

namespace osu.Game.Rulesets.BMS.Audio
{
    /// <summary>
    /// Loads background music/atmosphere from BMS files.
    /// In BMS format, all sounds except note keysounds contribute to the "background" audio.
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
                var folderPath = Path.GetDirectoryName(bmsFilePath) ?? string.Empty;
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
                if (wavDefinitions.TryGetValue("01", out var f01))
                    bgmFile = f01;
                else if (wavDefinitions.TryGetValue("00", out var f00))
                    bgmFile = f00;
                else if (wavDefinitions.Count > 0)
                    bgmFile = wavDefinitions.First().Value;

                if (string.IsNullOrEmpty(bgmFile))
                    return null;

                // Try to load the BGM file
                string fullPath = Path.Combine(folderPath, bgmFile);
                if (File.Exists(fullPath))
                {
                    var track = audioManager.Tracks.Get(fullPath);
                    Logger.Log($"[BMS] Loaded background music: {bgmFile}", LoggingTarget.Runtime, LogLevel.Debug);
                    return track;
                }

                Logger.Log($"[BMS] Background music file not found: {bgmFile}", LoggingTarget.Runtime, LogLevel.Debug);
                return null;
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] Failed to load background music: {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                return null;
            }
        }
    }
}
