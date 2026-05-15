// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace osu.Game.Rulesets.BMS.Beatmaps.Persistence
{
    public sealed class BmsLibraryIndexRepository
    {
        private const int schema_version = 1;

        private const string table_meta = "meta";
        private const string table_roots = "roots";
        private const string table_songs = "songs";
        private const string table_charts = "charts";

        private readonly string databasePath;
        private readonly object writeLock = new object();
        private bool initialized;

        public BmsLibraryIndexRepository(string databasePath)
        {
            this.databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        }

        public long ScanRevision => readLongMeta("scan_revision");

        public DateTime LastScanTime
        {
            get
            {
                long ticks = readLongMeta("last_scan_ticks");
                return ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.MinValue;
            }
        }

        /// <summary>
        /// Represents a snapshot of a chart file for change detection.
        /// ChartPath is included for data integrity even though the dictionary key already contains the path.
        /// </summary>
        public record ChartFileSnapshot(string ChartPath, long FileSize, long LastModifiedTicks);

        public Dictionary<string, ChartFileSnapshot> GetChartSnapshots()
        {
            ensureInitialized();
            var result = new Dictionary<string, ChartFileSnapshot>(StringComparer.OrdinalIgnoreCase);

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT chart_path, file_size, last_modified_ticks FROM charts;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                result[reader.GetString(0)] = new ChartFileSnapshot(
                    reader.GetString(0),
                    reader.GetInt64(1),
                    reader.GetInt64(2));
            }

            return result;
        }

        public bool TryLoadChart(string chartPath, out BMSChartCache chart)
        {
            chart = null!;

            ensureInitialized();

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM charts WHERE chart_path = $path LIMIT 1;";
            cmd.Parameters.AddWithValue("$path", chartPath);

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return false;

            chart = readChart(reader);
            return true;
        }

        public bool TryGetSourceReference(Guid beatmapId, out BMSSourceReference reference)
        {
            reference = default;
            ensureInitialized();

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT beatmap_id, folder_path, chart_path, path_key
FROM charts
WHERE beatmap_id = $id
LIMIT 1;";
            cmd.Parameters.AddWithValue("$id", beatmapId.ToString());

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return false;

            reference = new BMSSourceReference
            {
                BeatmapId = beatmapId,
                FolderPath = reader.GetString(1),
                ChartPath = reader.GetString(2),
                Md5Hash = reader.GetString(3),
            };

            return true;
        }

        public bool TryGetSourceReferenceByPathKey(string pathKey, out BMSSourceReference reference)
        {
            reference = default;
            ensureInitialized();

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT beatmap_id, folder_path, chart_path, path_key
FROM charts
WHERE path_key = $key
LIMIT 1;";
            cmd.Parameters.AddWithValue("$key", pathKey);

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return false;

            reference = new BMSSourceReference
            {
                BeatmapId = Guid.Parse(reader.GetString(0)),
                FolderPath = reader.GetString(1),
                ChartPath = reader.GetString(2),
                Md5Hash = reader.GetString(3),
            };

            return true;
        }

        public void ReplaceRoots(IEnumerable<string> rootPaths)
        {
            lock (writeLock)
            {
                ensureInitialized();

                using var connection = openConnection();
                using var transaction = connection.BeginTransaction();

                using (var delete = connection.CreateCommand())
                {
                    delete.Transaction = transaction;
                    // Intentionally clear all roots before replacing with new list
                    delete.CommandText = $"DELETE FROM {table_roots};";
                    delete.ExecuteNonQuery();
                }

                foreach (string path in rootPaths)
                {
                    using var insert = connection.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = $"INSERT INTO {table_roots} (path) VALUES ($path);";
                    insert.Parameters.AddWithValue("$path", path);
                    insert.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        public void UpsertSong(BMSSongCache song)
        {
            lock (writeLock)
            {
                ensureInitialized();

                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $@"
INSERT INTO {table_songs} (
    folder_path, title, artist, genre, banner_path, stage_path, last_modified_ticks
) VALUES (
    $folder, $title, $artist, $genre, $banner, $stage, $modified
)
ON CONFLICT(folder_path) DO UPDATE SET
    title = excluded.title,
    artist = excluded.artist,
    genre = excluded.genre,
    banner_path = excluded.banner_path,
    stage_path = excluded.stage_path,
    last_modified_ticks = excluded.last_modified_ticks;";

                cmd.Parameters.AddWithValue("$folder", song.FolderPath);
                cmd.Parameters.AddWithValue("$title", song.Title ?? string.Empty);
                cmd.Parameters.AddWithValue("$artist", song.Artist ?? string.Empty);
                cmd.Parameters.AddWithValue("$genre", song.Genre ?? string.Empty);
                cmd.Parameters.AddWithValue("$banner", (object?)song.BannerPath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$stage", (object?)song.StageFilePath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$modified", song.LastModified.ToUniversalTime().Ticks);
                cmd.ExecuteNonQuery();
            }
        }

        public void UpsertChart(BMSChartCache chart, Guid beatmapId, string pathKey)
        {
            lock (writeLock)
            {
                ensureInitialized();

                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $@"
INSERT INTO {table_charts} (
    chart_path, folder_path, file_name, file_size, last_modified_ticks,
    beatmap_id, path_key, title, sub_title, artist, sub_artist, genre,
    play_level, rank, ln_type, key_count, total_notes,
    bpm, min_bpm, max_bpm, duration, total_gauge,
    preview_time, audio_file, preview_file,
    has_scratch, has_ln, has_stop, has_scroll, has_bga,
    keysound_files_json
) VALUES (
    $chartPath, $folder, $fileName, $size, $modified,
    $beatmapId, $pathKey, $title, $subTitle, $artist, $subArtist, $genre,
    $playLevel, $rank, $lnType, $keyCount, $totalNotes,
    $bpm, $minBpm, $maxBpm, $duration, $totalGauge,
    $previewTime, $audio, $preview,
    $scratch, $ln, $stop, $scroll, $bga,
    $keysounds
)
ON CONFLICT(chart_path) DO UPDATE SET
    folder_path = excluded.folder_path,
    file_name = excluded.file_name,
    file_size = excluded.file_size,
    last_modified_ticks = excluded.last_modified_ticks,
    beatmap_id = excluded.beatmap_id,
    path_key = excluded.path_key,
    title = excluded.title,
    sub_title = excluded.sub_title,
    artist = excluded.artist,
    sub_artist = excluded.sub_artist,
    genre = excluded.genre,
    play_level = excluded.play_level,
    rank = excluded.rank,
    ln_type = excluded.ln_type,
    key_count = excluded.key_count,
    total_notes = excluded.total_notes,
    bpm = excluded.bpm,
    min_bpm = excluded.min_bpm,
    max_bpm = excluded.max_bpm,
    duration = excluded.duration,
    total_gauge = excluded.total_gauge,
    preview_time = excluded.preview_time,
    audio_file = excluded.audio_file,
    preview_file = excluded.preview_file,
    has_scratch = excluded.has_scratch,
    has_ln = excluded.has_ln,
    has_stop = excluded.has_stop,
    has_scroll = excluded.has_scroll,
    has_bga = excluded.has_bga,
    keysound_files_json = excluded.keysound_files_json;";

                string chartPath = chart.FullPath;
                cmd.Parameters.AddWithValue("$chartPath", chartPath);
                cmd.Parameters.AddWithValue("$folder", chart.FolderPath);
                cmd.Parameters.AddWithValue("$fileName", chart.FileName);
                cmd.Parameters.AddWithValue("$size", chart.FileSize);
                cmd.Parameters.AddWithValue("$modified", chart.LastModified.ToUniversalTime().Ticks);
                cmd.Parameters.AddWithValue("$beatmapId", beatmapId.ToString());
                cmd.Parameters.AddWithValue("$pathKey", pathKey);
                cmd.Parameters.AddWithValue("$title", chart.Title ?? string.Empty);
                cmd.Parameters.AddWithValue("$subTitle", chart.SubTitle ?? string.Empty);
                cmd.Parameters.AddWithValue("$artist", chart.Artist ?? string.Empty);
                cmd.Parameters.AddWithValue("$subArtist", chart.SubArtist ?? string.Empty);
                cmd.Parameters.AddWithValue("$genre", chart.Genre ?? string.Empty);
                cmd.Parameters.AddWithValue("$playLevel", chart.PlayLevel);
                cmd.Parameters.AddWithValue("$rank", chart.Rank);
                cmd.Parameters.AddWithValue("$lnType", chart.LnType);
                cmd.Parameters.AddWithValue("$keyCount", chart.KeyCount);
                cmd.Parameters.AddWithValue("$totalNotes", chart.TotalNotes);
                cmd.Parameters.AddWithValue("$bpm", chart.Bpm);
                cmd.Parameters.AddWithValue("$minBpm", chart.MinBpm);
                cmd.Parameters.AddWithValue("$maxBpm", chart.MaxBpm);
                cmd.Parameters.AddWithValue("$duration", chart.Duration);
                cmd.Parameters.AddWithValue("$totalGauge", chart.Total);
                cmd.Parameters.AddWithValue("$previewTime", chart.PreviewTime);
                cmd.Parameters.AddWithValue("$audio", (object?)chart.AudioFile ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$preview", (object?)chart.PreviewFile ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$scratch", chart.HasScratch ? 1 : 0);
                cmd.Parameters.AddWithValue("$ln", chart.HasLongNotes ? 1 : 0);
                cmd.Parameters.AddWithValue("$stop", chart.HasStopSequence ? 1 : 0);
                cmd.Parameters.AddWithValue("$scroll", chart.HasScrollChanges ? 1 : 0);
                cmd.Parameters.AddWithValue("$bga", chart.HasBgaLayer ? 1 : 0);
                cmd.Parameters.AddWithValue("$keysounds", JsonSerializer.Serialize(chart.KeysoundFiles));
                cmd.ExecuteNonQuery();
            }
        }

        public int DeleteChartsNotIn(IReadOnlyCollection<string> chartPaths)
        {
            lock (writeLock)
            {
                ensureInitialized();

                if (chartPaths.Count == 0)
                {
                    using var connection = openConnection();
                    using var wipe = connection.CreateCommand();
                    wipe.CommandText = $"DELETE FROM {table_charts};";
                    return wipe.ExecuteNonQuery();
                }

                using var conn = openConnection();
                using var transaction = conn.BeginTransaction();

                var existing = new List<string>();

                using (var select = conn.CreateCommand())
                {
                    select.Transaction = transaction;
                    select.CommandText = "SELECT chart_path FROM charts;";

                    using var reader = select.ExecuteReader();

                    while (reader.Read())
                        existing.Add(reader.GetString(0));
                }

                var keep = new HashSet<string>(chartPaths, StringComparer.OrdinalIgnoreCase);
                int deleted = 0;

                foreach (string path in existing)
                {
                    if (keep.Contains(path))
                        continue;

                    using var delete = conn.CreateCommand();
                    delete.Transaction = transaction;
                    delete.CommandText = "DELETE FROM charts WHERE chart_path = $path;";
                    delete.Parameters.AddWithValue("$path", path);
                    deleted += delete.ExecuteNonQuery();
                }

                pruneOrphanSongs(conn, transaction);
                transaction.Commit();
                return deleted;
            }
        }

        public long MarkScanComplete(IEnumerable<string> rootPaths)
        {
            lock (writeLock)
            {
                ensureInitialized();
                ReplaceRoots(rootPaths);

                long revision = ScanRevision + 1;
                DateTime now = DateTime.UtcNow;

                using var connection = openConnection();
                writeMeta(connection, "scan_revision", revision.ToString(CultureInfo.InvariantCulture));
                writeMeta(connection, "last_scan_ticks", now.Ticks.ToString(CultureInfo.InvariantCulture));

                return revision;
            }
        }

        public BMSLibraryCache LoadLibraryCache()
        {
            ensureInitialized();

            var cache = new BMSLibraryCache
            {
                Version = schema_version,
                LastScanTime = LastScanTime.ToLocalTime(),
            };

            using var connection = openConnection();

            using (var rootsCmd = connection.CreateCommand())
            {
                rootsCmd.CommandText = $"SELECT path FROM {table_roots} ORDER BY path;";
                using var reader = rootsCmd.ExecuteReader();

                while (reader.Read())
                    cache.RootPaths.Add(reader.GetString(0));
            }

            cache.RootPath = cache.RootPaths.FirstOrDefault() ?? string.Empty;

            var songsByFolder = new Dictionary<string, BMSSongCache>(StringComparer.OrdinalIgnoreCase);

            using (var songsCmd = connection.CreateCommand())
            {
                songsCmd.CommandText = $"SELECT folder_path, title, artist, genre, banner_path, stage_path, last_modified_ticks FROM {table_songs};";
                using var reader = songsCmd.ExecuteReader();

                while (reader.Read())
                {
                    var song = new BMSSongCache
                    {
                        FolderPath = reader.GetString(0),
                        Title = reader.GetString(1),
                        Artist = reader.GetString(2),
                        Genre = reader.GetString(3),
                        BannerPath = reader.IsDBNull(4) ? null : reader.GetString(4),
                        StageFilePath = reader.IsDBNull(5) ? null : reader.GetString(5),
                        LastModified = new DateTime(reader.GetInt64(6), DateTimeKind.Utc).ToLocalTime(),
                    };

                    songsByFolder[song.FolderPath] = song;
                    cache.Songs.Add(song);
                }
            }

            using (var chartsCmd = connection.CreateCommand())
            {
                chartsCmd.CommandText = "SELECT * FROM charts ORDER BY folder_path, file_name;";
                using var reader = chartsCmd.ExecuteReader();

                while (reader.Read())
                {
                    var chart = readChart(reader);

                    if (!songsByFolder.TryGetValue(chart.FolderPath, out var song))
                    {
                        song = new BMSSongCache
                        {
                            FolderPath = chart.FolderPath,
                            Title = chart.Title,
                            Artist = chart.Artist,
                            Genre = chart.Genre,
                            LastModified = chart.LastModified,
                        };
                        songsByFolder[song.FolderPath] = song;
                        cache.Songs.Add(song);
                    }

                    chart.Md5Hash = reader.GetString(reader.GetOrdinal("path_key"));
                    song.Charts.Add(chart);
                }
            }

            return cache;
        }

        public void ImportFromLibraryCache(BMSLibraryCache cache)
        {
            lock (writeLock)
            {
                ensureInitialized();
                ReplaceRoots(cache.RootPaths.Count > 0 ? cache.RootPaths : new[] { cache.RootPath });

                foreach (var song in cache.Songs)
                {
                    UpsertSong(song);

                    foreach (var chart in song.Charts)
                    {
                        string chartPath = chart.FullPath;
                        string pathKey = string.IsNullOrEmpty(chart.Md5Hash)
                            ? BmsPathKeys.ComputeChartPathKey(chartPath)
                            : chart.Md5Hash;
                        Guid beatmapId = createDeterministicGuid($"bms:chart:{chartPath}");
                        UpsertChart(chart, beatmapId, pathKey);
                    }
                }

                MarkScanComplete(cache.RootPaths.Count > 0 ? cache.RootPaths : new[] { cache.RootPath });
            }
        }

        private static Guid createDeterministicGuid(string input)
        {
            byte[] bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
            return new Guid(bytes);
        }

        private static BMSChartCache readChart(SqliteDataReader reader)
        {
            return new BMSChartCache
            {
                FolderPath = reader.GetString(reader.GetOrdinal("folder_path")),
                FileName = reader.GetString(reader.GetOrdinal("file_name")),
                FileSize = reader.GetInt64(reader.GetOrdinal("file_size")),
                LastModified = new DateTime(reader.GetInt64(reader.GetOrdinal("last_modified_ticks")), DateTimeKind.Utc).ToLocalTime(),
                Title = reader.GetString(reader.GetOrdinal("title")),
                SubTitle = reader.GetString(reader.GetOrdinal("sub_title")),
                Artist = reader.GetString(reader.GetOrdinal("artist")),
                SubArtist = reader.GetString(reader.GetOrdinal("sub_artist")),
                Genre = reader.GetString(reader.GetOrdinal("genre")),
                PlayLevel = reader.GetInt32(reader.GetOrdinal("play_level")),
                Rank = reader.GetInt32(reader.GetOrdinal("rank")),
                LnType = reader.GetInt32(reader.GetOrdinal("ln_type")),
                KeyCount = reader.GetInt32(reader.GetOrdinal("key_count")),
                TotalNotes = reader.GetInt32(reader.GetOrdinal("total_notes")),
                Bpm = reader.GetDouble(reader.GetOrdinal("bpm")),
                MinBpm = reader.GetDouble(reader.GetOrdinal("min_bpm")),
                MaxBpm = reader.GetDouble(reader.GetOrdinal("max_bpm")),
                Duration = reader.GetDouble(reader.GetOrdinal("duration")),
                Total = reader.GetDouble(reader.GetOrdinal("total_gauge")),
                PreviewTime = reader.GetInt32(reader.GetOrdinal("preview_time")),
                AudioFile = reader.IsDBNull(reader.GetOrdinal("audio_file")) ? null : reader.GetString(reader.GetOrdinal("audio_file")),
                PreviewFile = reader.IsDBNull(reader.GetOrdinal("preview_file")) ? null : reader.GetString(reader.GetOrdinal("preview_file")),
                HasScratch = reader.GetInt32(reader.GetOrdinal("has_scratch")) != 0,
                HasLongNotes = reader.GetInt32(reader.GetOrdinal("has_ln")) != 0,
                HasStopSequence = reader.GetInt32(reader.GetOrdinal("has_stop")) != 0,
                HasScrollChanges = reader.GetInt32(reader.GetOrdinal("has_scroll")) != 0,
                HasBgaLayer = reader.GetInt32(reader.GetOrdinal("has_bga")) != 0,
                KeysoundFiles = JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("keysound_files_json"))) ?? new List<string>(),
                Md5Hash = reader.GetString(reader.GetOrdinal("path_key")),
            };
        }

        private static void pruneOrphanSongs(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $@"
DELETE FROM {table_songs}
WHERE folder_path NOT IN (SELECT DISTINCT folder_path FROM {table_charts});";
            cmd.ExecuteNonQuery();
        }

        private void ensureInitialized()
        {
            lock (writeLock)
            {
                if (initialized)
                    return;

                string? directory = Path.GetDirectoryName(databasePath);

                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using var connection = openConnection();

                using (var pragma = connection.CreateCommand())
                {
                    pragma.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA temp_store=MEMORY;";
                    pragma.ExecuteNonQuery();
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $@"
CREATE TABLE IF NOT EXISTS {table_meta} (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS {table_roots} (
    path TEXT PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS {table_songs} (
    folder_path TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    artist TEXT NOT NULL,
    genre TEXT NOT NULL,
    banner_path TEXT,
    stage_path TEXT,
    last_modified_ticks INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS {table_charts} (
    chart_path TEXT PRIMARY KEY,
    folder_path TEXT NOT NULL,
    file_name TEXT NOT NULL,
    file_size INTEGER NOT NULL,
    last_modified_ticks INTEGER NOT NULL,
    beatmap_id TEXT NOT NULL,
    path_key TEXT NOT NULL,
    title TEXT NOT NULL,
    sub_title TEXT NOT NULL,
    artist TEXT NOT NULL,
    sub_artist TEXT NOT NULL,
    genre TEXT NOT NULL,
    play_level INTEGER NOT NULL,
    rank INTEGER NOT NULL,
    ln_type INTEGER NOT NULL,
    key_count INTEGER NOT NULL,
    total_notes INTEGER NOT NULL,
    bpm REAL NOT NULL,
    min_bpm REAL NOT NULL,
    max_bpm REAL NOT NULL,
    duration REAL NOT NULL,
    total_gauge REAL NOT NULL,
    preview_time INTEGER NOT NULL,
    audio_file TEXT,
    preview_file TEXT,
    has_scratch INTEGER NOT NULL,
    has_ln INTEGER NOT NULL,
    has_stop INTEGER NOT NULL,
    has_scroll INTEGER NOT NULL,
    has_bga INTEGER NOT NULL,
    keysound_files_json TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_charts_folder ON {table_charts}(folder_path);
CREATE INDEX IF NOT EXISTS idx_charts_path_key ON {table_charts}(path_key);
CREATE INDEX IF NOT EXISTS idx_charts_beatmap_id ON {table_charts}(beatmap_id);";
                    cmd.ExecuteNonQuery();
                }

                writeMeta(connection, "schema_version", schema_version.ToString(CultureInfo.InvariantCulture));
                initialized = true;
            }
        }

        private long readLongMeta(string key)
        {
            ensureInitialized();

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT value FROM {table_meta} WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            object? result = cmd.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                return 0;

            return long.TryParse(result.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : 0;
        }

        private static void writeMeta(SqliteConnection connection, string key, string value)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
INSERT INTO {table_meta} (key, value) VALUES ($key, $value)
ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.ExecuteNonQuery();
        }

        private SqliteConnection openConnection()
        {
            var connection = new SqliteConnection($"Data Source={databasePath};Cache=Shared;Mode=ReadWriteCreate");
            connection.Open();
            return connection;
        }
    }
}
