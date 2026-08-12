// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using osu.Framework.Logging;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Tables
{
    /// <summary>
    /// Reads beatoraja-compatible difficulty table caches (.bmt gzip JSON / plain JSON).
    /// </summary>
    public static class BmsDifficultyTableParser
    {
        private static readonly JsonSerializerOptions json_options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static BmsDifficultyTable? TryLoadFile(string path)
        {
            try
            {
                using var stream = openReadStream(path);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string json = reader.ReadToEnd();
                return ParseTableDataJson(json, path);
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] Failed to load difficulty table '{path}': {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
                return null;
            }
        }

        public static BmsDifficultyTable? ParseTableDataJson(string json, string sourcePath = "")
        {
            var raw = JsonSerializer.Deserialize<TableDataDto>(json, json_options);

            if (raw == null || string.IsNullOrWhiteSpace(raw.Name))
                return null;

            var levels = new List<BmsTableLevel>();

            if (raw.Folder is { Count: > 0 })
            {
                foreach (var folder in raw.Folder)
                {
                    if (folder == null || string.IsNullOrWhiteSpace(folder.Name))
                        continue;

                    var songs = folder.Song ?? (IReadOnlyList<TableSongDto?>)Array.Empty<TableSongDto?>();
                    var entries = songs
                        .Where(s => s != null)
                        .Select(s => new BmsTableEntry
                        {
                            Md5 = (s!.Md5 ?? string.Empty).Trim().ToLowerInvariant(),
                            Sha256 = (s.Sha256 ?? string.Empty).Trim().ToLowerInvariant(),
                            Title = s.Title ?? string.Empty,
                            Artist = s.Artist ?? string.Empty,
                            Level = folder.Name,
                            Url = s.Url ?? string.Empty,
                            AppendUrl = s.AppendUrl ?? string.Empty,
                        })
                        .Where(e => !string.IsNullOrEmpty(e.PreferredHash))
                        .ToList();

                    if (entries.Count == 0)
                        continue;

                    levels.Add(new BmsTableLevel { Name = folder.Name, Entries = entries });
                }
            }

            if (levels.Count == 0)
                return null;

            return new BmsDifficultyTable
            {
                Name = raw.Name.Trim(),
                Url = raw.Url ?? string.Empty,
                Tag = raw.Tag ?? string.Empty,
                SourcePath = sourcePath,
                Levels = levels,
            };
        }

        /// <summary>
        /// Builds a TableData-compatible JSON from a remote difficulty-table header + body.
        /// </summary>
        public static string BuildTableDataJson(string name, string url, string tag, IReadOnlyList<DifficultyTableElementDto> elements, IReadOnlyList<string> levelOrder)
        {
            var folders = new List<object>();

            foreach (string level in levelOrder)
            {
                var songs = elements
                    .Where(e => string.Equals(e.Level, level, StringComparison.Ordinal))
                    .Select(e => new
                    {
                        md5 = (e.Md5 ?? string.Empty).ToLowerInvariant(),
                        sha256 = (e.Sha256 ?? string.Empty).ToLowerInvariant(),
                        title = e.Title ?? string.Empty,
                        artist = e.Artist ?? string.Empty,
                        url = e.Url ?? string.Empty,
                        appendurl = e.AppendUrl ?? string.Empty,
                    })
                    .Where(s => !string.IsNullOrEmpty(s.md5) || !string.IsNullOrEmpty(s.sha256))
                    .ToList();

                if (songs.Count == 0)
                    continue;

                folders.Add(new
                {
                    name = string.IsNullOrEmpty(tag) ? level : tag + level,
                    song = songs,
                });
            }

            var payload = new
            {
                name,
                url,
                tag,
                folder = folders,
            };

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        }

        public static void WriteBmt(string path, string tableDataJson)
        {
            string? dir = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var file = File.Create(path);
            using var gzip = new GZipStream(file, CompressionLevel.Optimal);
            using var writer = new StreamWriter(gzip, Encoding.UTF8);
            writer.Write(tableDataJson);
        }

        private static Stream openReadStream(string path)
        {
            var file = File.OpenRead(path);

            if (path.EndsWith(".bmt", StringComparison.OrdinalIgnoreCase))
                return new GZipStream(file, CompressionMode.Decompress);

            return file;
        }

        private sealed class TableDataDto
        {
            public string? Name { get; set; }
            public string? Url { get; set; }
            public string? Tag { get; set; }
            public List<TableFolderDto?>? Folder { get; set; }
        }

        private sealed class TableFolderDto
        {
            public string? Name { get; set; }

            [JsonPropertyName("song")]
            public List<TableSongDto?>? Song { get; set; }

            // beatoraja Json sometimes uses "songs"
            [JsonPropertyName("songs")]
            public List<TableSongDto?>? Songs
            {
                set
                {
                    if (value != null)
                        Song = value;
                }
            }
        }

        private sealed class TableSongDto
        {
            public string? Md5 { get; set; }
            public string? Sha256 { get; set; }
            public string? Title { get; set; }
            public string? Artist { get; set; }
            public string? Url { get; set; }

            [JsonPropertyName("appendurl")]
            public string? AppendUrl { get; set; }
        }
    }

    public sealed class DifficultyTableHeaderDto
    {
        public string? Name { get; set; }
        public string? Symbol { get; set; }
        public string? DataUrl { get; set; }

        [JsonPropertyName("data_url")]
        public string? DataUrlSnake { get; set; }

        public List<string>? LevelOrder { get; set; }

        [JsonPropertyName("level_order")]
        public List<string>? LevelOrderSnake { get; set; }

        public string ResolveDataUrl() => DataUrl ?? DataUrlSnake ?? string.Empty;

        public IReadOnlyList<string> ResolveLevelOrder()
            => (IReadOnlyList<string>)(LevelOrder ?? LevelOrderSnake ?? (IReadOnlyList<string>)Array.Empty<string>());
    }

    public sealed class DifficultyTableElementDto
    {
        public string? Md5 { get; set; }
        public string? Sha256 { get; set; }
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Level { get; set; }
        public string? Url { get; set; }

        [JsonPropertyName("appendurl")]
        public string? AppendUrl { get; set; }
    }
}
