// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Represents cached metadata for a BMS song folder (may contain multiple charts).
    /// </summary>
    [Serializable]
    public class BMSSongCache
    {
        /// <summary>
        /// The absolute path to the song folder.
        /// </summary>
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>
        /// Title of the song (from first chart parsed).
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Artist of the song.
        /// </summary>
        public string Artist { get; set; } = string.Empty;

        /// <summary>
        /// Genre of the song.
        /// </summary>
        public string Genre { get; set; } = string.Empty;

        /// <summary>
        /// List of charts (difficulties) in this song folder.
        /// </summary>
        public List<BMSChartCache> Charts { get; set; } = new();

        /// <summary>
        /// Last modified time of the folder (for detecting changes).
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Path to the banner image (relative to folder).
        /// </summary>
        public string? BannerPath { get; set; }

        /// <summary>
        /// Path to the stage file (relative to folder).
        /// </summary>
        public string? StageFilePath { get; set; }
    }

    /// <summary>
    /// Represents cached metadata for a single BMS chart file.
    /// </summary>
    [Serializable]
    public class BMSChartCache
    {
        /// <summary>
        /// The filename of the BMS file (relative to song folder).
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Full absolute path to the BMS file.
        /// </summary>
        [JsonIgnore]
        public string FullPath => Path.Combine(FolderPath, FileName);

        /// <summary>
        /// Parent folder path.
        /// </summary>
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>
        /// Chart-specific title (may differ from song title).
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Chart-specific subtitle.
        /// </summary>
        public string SubTitle { get; set; } = string.Empty;

        /// <summary>
        /// Chart-specific artist.
        /// </summary>
        public string Artist { get; set; } = string.Empty;

        /// <summary>
        /// Chart-specific subartist.
        /// </summary>
        public string SubArtist { get; set; } = string.Empty;

        /// <summary>
        /// Genre.
        /// </summary>
        public string Genre { get; set; } = string.Empty;

        /// <summary>
        /// Play level (difficulty number).
        /// </summary>
        public int PlayLevel { get; set; }

        /// <summary>
        /// Difficulty rank (JUDGE timing).
        /// </summary>
        public int Rank { get; set; } = 2;

        /// <summary>
        /// Total gauge (health).
        /// </summary>
        public double Total { get; set; } = 100;

        /// <summary>
        /// Initial BPM.
        /// </summary>
        public double Bpm { get; set; } = 130;

        /// <summary>
        /// Minimum BPM (for display).
        /// </summary>
        public double MinBpm { get; set; }

        /// <summary>
        /// Maximum BPM (for display).
        /// </summary>
        public double MaxBpm { get; set; }

        /// <summary>
        /// Number of keys (5, 7, 9, 10, 14, etc.).
        /// </summary>
        public int KeyCount { get; set; } = 7;

        /// <summary>
        /// Whether this chart has scratch lane.
        /// </summary>
        public bool HasScratch { get; set; }

        /// <summary>
        /// Whether this chart has long notes.
        /// </summary>
        public bool HasLongNotes { get; set; }

        /// <summary>
        /// Total note count.
        /// </summary>
        public int TotalNotes { get; set; }

        /// <summary>
        /// Duration of the chart in milliseconds.
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// List of keysound file paths used in this chart (relative to folder).
        /// </summary>
        public List<string> KeysoundFiles { get; set; } = new();

        /// <summary>
        /// MD5 hash of the BMS file for identification.
        /// </summary>
        public string Md5Hash { get; set; } = string.Empty;

        /// <summary>
        /// File size for quick change detection.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Last write time of the file.
        /// </summary>
        public DateTime LastModified { get; set; }
    }

    /// <summary>
    /// The root cache structure that holds all scanned BMS songs.
    /// </summary>
    [Serializable]
    public class BMSLibraryCache
    {
        /// <summary>
        /// Version of the cache format (for migration).
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// The root path that was scanned.
        /// </summary>
        public string RootPath { get; set; } = string.Empty;

        /// <summary>
        /// When the cache was last updated.
        /// </summary>
        public DateTime LastScanTime { get; set; }

        /// <summary>
        /// All songs in the library.
        /// </summary>
        public List<BMSSongCache> Songs { get; set; } = new();

        /// <summary>
        /// Total number of charts.
        /// </summary>
        [JsonIgnore]
        public int TotalCharts
        {
            get
            {
                int count = 0;
                foreach (var song in Songs)
                    count += song.Charts.Count;
                return count;
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        /// <summary>
        /// Save the cache to a file.
        /// </summary>
        public void Save(string filePath)
        {
            string json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Load the cache from a file.
        /// </summary>
        public static BMSLibraryCache? Load(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<BMSLibraryCache>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }
}
