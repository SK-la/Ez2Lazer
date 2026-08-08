// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace osu.Game.Rulesets.BMS.Beatmaps.Persistence
{
    public sealed class BmsLibraryIndexRepository
    {
        private const int schema_version = 2;

        private const string table_meta = "meta";
        private const string table_roots = "roots";
        private const string table_songs = "songs";
        private const string table_charts = "charts";
        private const string table_sync_changes = "sync_changes";

        private readonly string databasePath;
        private readonly object writeLock = new object();
        private bool initialized;

        public BmsLibraryIndexRepository(string databasePath)
        {
            this.databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        }

        public long ScanRevision => readLongMeta("scan_revision");

        public int RealmFileMappingVersion => (int)readLongMeta(BmsRealmSyncConstants.REALM_FILE_MAPPING_META_KEY);

        public int ChartCount => readCount(table_charts);

        public int SongCount => readCount(table_songs);

        public long SyncCursor => readLongMeta("sync_cursor");

        public int PendingSyncSetCount
        {
            get
            {
                ensureInitialized();

                using var connection = openConnection();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"SELECT COUNT(DISTINCT set_id) FROM {table_sync_changes};";
                return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        public IReadOnlyList<string> GetRootPaths()
        {
            ensureInitialized();
            var result = new List<string>();

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT path FROM {table_roots} ORDER BY path;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                result.Add(reader.GetString(0));

            return result;
        }

        public void WriteRealmFileMappingVersion(int version)
        {
            lock (writeLock)
            {
                ensureInitialized();

                using var connection = openConnection();
                writeMeta(connection, BmsRealmSyncConstants.REALM_FILE_MAPPING_META_KEY, version.ToString(CultureInfo.InvariantCulture));
            }
        }

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

        public enum SyncState
        {
            Synchronized,
            Pending,
        }

        public enum SyncChangeKind
        {
            Upsert,
            Delete,
        }

        public record IndexedChart(
            BMSChartCache Chart,
            BmsChartIdentity Identity,
            long SeenGeneration,
            long SyncRevision,
            SyncState SyncState,
            int ParseVersion);

        public record SyncChange(
            long Revision,
            Guid BeatmapId,
            Guid SetId,
            string ChartPath,
            SyncChangeKind Kind);

        public record ScanWriteItem(
            string ChartPath,
            long FileSize,
            long LastModifiedTicks,
            BMSChartCache? Chart,
            BMSSongCache? Song);

        public long BeginScanGeneration()
        {
            lock (writeLock)
            {
                ensureInitialized();

                using var connection = openConnection();
                long generation = readLongMeta(connection, "scan_generation") + 1;
                writeMeta(connection, "scan_generation", generation.ToString(CultureInfo.InvariantCulture));
                return generation;
            }
        }

        public bool TryGetChartSnapshot(string chartPath, out ChartFileSnapshot snapshot)
        {
            snapshot = null!;
            ensureInitialized();

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
SELECT file_size, last_modified_ticks
FROM {table_charts}
WHERE chart_path = $path
LIMIT 1;";
            cmd.Parameters.AddWithValue("$path", chartPath);

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return false;

            snapshot = new ChartFileSnapshot(chartPath, reader.GetInt64(0), reader.GetInt64(1));
            return true;
        }

        public ScanWriter OpenScanWriter(long generation)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(generation);
            ensureInitialized();
            return new ScanWriter(this, generation);
        }

        public long CompleteScanGeneration(long generation, IReadOnlyCollection<string> rootPaths)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(generation);

            if (rootPaths.Count == 0)
                throw new ArgumentException("At least one successfully scanned root is required.", nameof(rootPaths));

            lock (writeLock)
            {
                ensureInitialized();

                using var connection = openConnection();
                using var transaction = connection.BeginTransaction();

                if (readLongMeta(connection, transaction, "scan_generation") != generation)
                    throw new InvalidOperationException($"Scan generation {generation} is no longer current.");

                var staleCharts = new List<(string ChartPath, Guid BeatmapId, Guid SetId)>();

                using (var select = connection.CreateCommand())
                {
                    select.Transaction = transaction;
                    select.CommandText = $@"
SELECT chart_path, beatmap_id, set_id
FROM {table_charts}
WHERE seen_generation <> $generation;";
                    select.Parameters.AddWithValue("$generation", generation);

                    using var reader = select.ExecuteReader();

                    while (reader.Read())
                    {
                        string chartPath = reader.GetString(0);

                        if (rootPaths.Any(root => isPathWithinRoot(chartPath, root)))
                            staleCharts.Add((chartPath, Guid.Parse(reader.GetString(1)), Guid.Parse(reader.GetString(2))));
                    }
                }

                foreach ((string chartPath, Guid beatmapId, Guid setId) in staleCharts)
                {
                    enqueueSyncChange(connection, transaction, beatmapId, setId, chartPath, SyncChangeKind.Delete);

                    using var delete = connection.CreateCommand();
                    delete.Transaction = transaction;
                    delete.CommandText = $"DELETE FROM {table_charts} WHERE chart_path = $path;";
                    delete.Parameters.AddWithValue("$path", chartPath);
                    delete.ExecuteNonQuery();
                }

                pruneOrphanSongs(connection, transaction);
                replaceRoots(connection, transaction, rootPaths);

                long revision = readLongMeta(connection, transaction, "scan_revision") + 1;
                writeMeta(connection, transaction, "scan_revision", revision.ToString(CultureInfo.InvariantCulture));
                writeMeta(connection, transaction, "last_scan_ticks", DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
                transaction.Commit();
                return revision;
            }
        }

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

        public bool TryGetChart(Guid beatmapId, out IndexedChart chart)
        {
            return tryGetIndexedChart("beatmap_id", beatmapId.ToString(), out chart);
        }

        public bool TryGetChartByPathKey(string pathKey, out IndexedChart chart)
        {
            return tryGetIndexedChart("path_key", pathKey, out chart);
        }

        public IReadOnlyList<IndexedChart> GetCharts(int offset, int limit)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

            ensureInitialized();

            var result = new List<IndexedChart>(limit);

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
SELECT *
FROM {table_charts}
ORDER BY chart_path
LIMIT $limit OFFSET $offset;";
            cmd.Parameters.AddWithValue("$limit", limit);
            cmd.Parameters.AddWithValue("$offset", offset);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                result.Add(readIndexedChart(reader));

            return result;
        }

        public IReadOnlyList<BMSSongCache> GetSongs(int offset, int limit)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
            ensureInitialized();
            var result = new List<BMSSongCache>(limit);

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
SELECT folder_path, title, artist, genre, banner_path, stage_path, last_modified_ticks
FROM {table_songs}
ORDER BY folder_path
LIMIT $limit OFFSET $offset;";
            cmd.Parameters.AddWithValue("$limit", limit);
            cmd.Parameters.AddWithValue("$offset", offset);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                result.Add(readSong(reader));

            return result;
        }

        public IReadOnlyList<IndexedChart> GetChartsByFolder(string folderPath)
        {
            ensureInitialized();
            var result = new List<IndexedChart>();

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
SELECT *
FROM {table_charts}
WHERE folder_path = $folder
ORDER BY play_level, file_name;";
            cmd.Parameters.AddWithValue("$folder", folderPath);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                result.Add(readIndexedChart(reader));

            return result;
        }

        public IReadOnlyList<SyncChange> GetPendingSyncChanges(int limit)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

            ensureInitialized();

            var result = new List<SyncChange>(limit);

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
SELECT revision, beatmap_id, set_id, chart_path, change_kind
FROM {table_sync_changes}
ORDER BY revision
LIMIT $limit;";
            cmd.Parameters.AddWithValue("$limit", limit);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                result.Add(new SyncChange(
                    reader.GetInt64(0),
                    Guid.Parse(reader.GetString(1)),
                    Guid.Parse(reader.GetString(2)),
                    reader.GetString(3),
                    (SyncChangeKind)reader.GetInt32(4)));
            }

            return result;
        }

        public IReadOnlyList<SyncChange> GetPendingSyncChangesForSets(int maxSetCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSetCount);
            ensureInitialized();

            var result = new List<SyncChange>();

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
WITH pending_sets AS (
    SELECT set_id, MIN(revision) AS first_revision
    FROM {table_sync_changes}
    GROUP BY set_id
    ORDER BY first_revision
    LIMIT $maxSetCount
)
SELECT changes.revision, changes.beatmap_id, changes.set_id, changes.chart_path, changes.change_kind
FROM {table_sync_changes} changes
INNER JOIN pending_sets ON pending_sets.set_id = changes.set_id
ORDER BY changes.revision;";
            cmd.Parameters.AddWithValue("$maxSetCount", maxSetCount);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                result.Add(new SyncChange(
                    reader.GetInt64(0),
                    Guid.Parse(reader.GetString(1)),
                    Guid.Parse(reader.GetString(2)),
                    reader.GetString(3),
                    (SyncChangeKind)reader.GetInt32(4)));
            }

            return result;
        }

        public void AcknowledgeSyncChanges(IReadOnlyCollection<long> revisions)
        {
            if (revisions.Count == 0)
                return;

            lock (writeLock)
            {
                ensureInitialized();

                using var connection = openConnection();
                using var transaction = connection.BeginTransaction();
                var affectedBeatmapIds = new HashSet<string>(StringComparer.Ordinal);

                using var select = connection.CreateCommand();
                select.Transaction = transaction;
                select.CommandText = $"SELECT beatmap_id FROM {table_sync_changes} WHERE revision = $revision;";
                select.Parameters.Add("$revision", SqliteType.Integer);

                using var delete = connection.CreateCommand();
                delete.Transaction = transaction;
                delete.CommandText = $"DELETE FROM {table_sync_changes} WHERE revision = $revision;";
                delete.Parameters.Add("$revision", SqliteType.Integer);

                foreach (long revision in revisions.Distinct())
                {
                    select.Parameters["$revision"].Value = revision;
                    object? beatmapId = select.ExecuteScalar();

                    if (beatmapId != null && beatmapId != DBNull.Value)
                        affectedBeatmapIds.Add((string)beatmapId);

                    delete.Parameters["$revision"].Value = revision;
                    delete.ExecuteNonQuery();
                }

                foreach (string beatmapId in affectedBeatmapIds)
                {
                    using var update = connection.CreateCommand();
                    update.Transaction = transaction;
                    update.CommandText = $@"
UPDATE {table_charts}
SET sync_state = $synchronized
WHERE beatmap_id = $beatmapId
  AND NOT EXISTS (
      SELECT 1
      FROM {table_sync_changes}
      WHERE beatmap_id = $beatmapId
  );";
                    update.Parameters.AddWithValue("$synchronized", (int)SyncState.Synchronized);
                    update.Parameters.AddWithValue("$beatmapId", beatmapId);
                    update.ExecuteNonQuery();
                }

                using var minPending = connection.CreateCommand();
                minPending.Transaction = transaction;
                minPending.CommandText = $"SELECT MIN(revision) FROM {table_sync_changes};";
                object? nextPending = minPending.ExecuteScalar();
                long cursor;

                if (nextPending == null || nextPending == DBNull.Value)
                {
                    using var latestRevision = connection.CreateCommand();
                    latestRevision.Transaction = transaction;
                    latestRevision.CommandText = "SELECT seq FROM sqlite_sequence WHERE name = $table;";
                    latestRevision.Parameters.AddWithValue("$table", table_sync_changes);
                    object? sequence = latestRevision.ExecuteScalar();
                    long highestIssuedRevision = sequence == null || sequence == DBNull.Value
                        ? revisions.Max()
                        : Convert.ToInt64(sequence, CultureInfo.InvariantCulture);
                    cursor = Math.Max(readLongMeta(connection, transaction, "sync_cursor"), highestIssuedRevision);
                }
                else
                {
                    cursor = Convert.ToInt64(nextPending, CultureInfo.InvariantCulture) - 1;
                }

                writeMeta(connection, transaction, "sync_cursor", cursor.ToString(CultureInfo.InvariantCulture));
                transaction.Commit();
            }
        }

        public void EnqueueAllChartsForSync()
        {
            lock (writeLock)
            {
                ensureInitialized();

                using var connection = openConnection();
                using var transaction = connection.BeginTransaction();

                using (var pending = connection.CreateCommand())
                {
                    pending.Transaction = transaction;
                    pending.CommandText = $"SELECT 1 FROM {table_sync_changes} LIMIT 1;";

                    if (pending.ExecuteScalar() != null)
                        return;
                }

                var charts = new List<(string ChartPath, Guid BeatmapId, Guid SetId)>();

                using (var select = connection.CreateCommand())
                {
                    select.Transaction = transaction;
                    select.CommandText = $"SELECT chart_path, beatmap_id, set_id FROM {table_charts};";

                    using var reader = select.ExecuteReader();

                    while (reader.Read())
                        charts.Add((reader.GetString(0), Guid.Parse(reader.GetString(1)), Guid.Parse(reader.GetString(2))));
                }

                foreach ((string chartPath, Guid beatmapId, Guid setId) in charts)
                    enqueueSyncChange(connection, transaction, beatmapId, setId, chartPath, SyncChangeKind.Upsert);

                transaction.Commit();
            }
        }

        public IReadOnlyList<IndexedChart> GetChartsBySetId(Guid setId)
        {
            ensureInitialized();
            var result = new List<IndexedChart>();

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
SELECT *
FROM {table_charts}
WHERE set_id = $setId
ORDER BY play_level, file_name;";
            cmd.Parameters.AddWithValue("$setId", setId.ToString());

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                result.Add(readIndexedChart(reader));

            return result;
        }

        public bool TryGetSong(string folderPath, out BMSSongCache song)
        {
            song = null!;
            ensureInitialized();

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
SELECT folder_path, title, artist, genre, banner_path, stage_path, last_modified_ticks
FROM {table_songs}
WHERE folder_path = $folder
LIMIT 1;";
            cmd.Parameters.AddWithValue("$folder", folderPath);

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return false;

            song = readSong(reader);
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
                using var transaction = connection.BeginTransaction();
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = $@"
INSERT INTO {table_charts} (
    chart_path, folder_path, file_name, file_size, last_modified_ticks,
    beatmap_id, set_id, path_key, seen_generation, sync_revision, sync_state, parse_version,
    title, sub_title, artist, sub_artist, genre,
    play_level, rank, ln_type, key_count, total_notes,
    bpm, min_bpm, max_bpm, duration, total_gauge,
    preview_time, audio_file, preview_file,
    has_scratch, has_ln, has_stop, has_scroll, has_bga,
    keysound_files_json
) VALUES (
    $chartPath, $folder, $fileName, $size, $modified,
    $beatmapId, $setId, $pathKey, $seenGeneration, 0, 0, $parseVersion,
    $title, $subTitle, $artist, $subArtist, $genre,
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
    set_id = excluded.set_id,
    path_key = excluded.path_key,
    seen_generation = excluded.seen_generation,
    parse_version = excluded.parse_version,
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
                cmd.Parameters.AddWithValue("$setId", BmsChartIdentity.CreateSetId(chart.FolderPath).ToString());
                cmd.Parameters.AddWithValue("$pathKey", pathKey);
                cmd.Parameters.AddWithValue("$seenGeneration", ScanRevision + 1);
                cmd.Parameters.AddWithValue("$parseVersion", 1);
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

                enqueueSyncChange(connection, transaction, beatmapId, BmsChartIdentity.CreateSetId(chart.FolderPath), chartPath, SyncChangeKind.Upsert);
                transaction.Commit();
            }
        }

        public int DeleteChartsNotIn(IReadOnlyCollection<string> chartPaths)
        {
            lock (writeLock)
            {
                ensureInitialized();

                using var conn = openConnection();
                using var transaction = conn.BeginTransaction();

                var existing = new List<(string ChartPath, Guid BeatmapId, Guid SetId)>();

                using (var select = conn.CreateCommand())
                {
                    select.Transaction = transaction;
                    select.CommandText = $"SELECT chart_path, beatmap_id, set_id FROM {table_charts};";

                    using var reader = select.ExecuteReader();

                    while (reader.Read())
                        existing.Add((reader.GetString(0), Guid.Parse(reader.GetString(1)), Guid.Parse(reader.GetString(2))));
                }

                var keep = new HashSet<string>(chartPaths, StringComparer.OrdinalIgnoreCase);
                int deleted = 0;

                foreach ((string path, Guid beatmapId, Guid setId) in existing)
                {
                    if (keep.Contains(path))
                        continue;

                    enqueueSyncChange(conn, transaction, beatmapId, setId, path, SyncChangeKind.Delete);

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
                        Guid beatmapId = BmsChartIdentity.CreateBeatmapId(chartPath);
                        UpsertChart(chart, beatmapId, pathKey);
                    }
                }

                MarkScanComplete(cache.RootPaths.Count > 0 ? cache.RootPaths : new[] { cache.RootPath });
            }
        }

        private bool tryGetIndexedChart(string column, string value, out IndexedChart chart)
        {
            chart = null!;
            ensureInitialized();

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {table_charts} WHERE {column} = $value LIMIT 1;";
            cmd.Parameters.AddWithValue("$value", value);

            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return false;

            chart = readIndexedChart(reader);
            return true;
        }

        private static IndexedChart readIndexedChart(SqliteDataReader reader)
        {
            var identity = new BmsChartIdentity(
                Guid.Parse(reader.GetString(reader.GetOrdinal("beatmap_id"))),
                Guid.Parse(reader.GetString(reader.GetOrdinal("set_id"))),
                reader.GetString(reader.GetOrdinal("path_key")));

            return new IndexedChart(
                readChart(reader),
                identity,
                reader.GetInt64(reader.GetOrdinal("seen_generation")),
                reader.GetInt64(reader.GetOrdinal("sync_revision")),
                (SyncState)reader.GetInt32(reader.GetOrdinal("sync_state")),
                reader.GetInt32(reader.GetOrdinal("parse_version")));
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

        private static BMSSongCache readSong(SqliteDataReader reader)
        {
            return new BMSSongCache
            {
                FolderPath = reader.GetString(0),
                Title = reader.GetString(1),
                Artist = reader.GetString(2),
                Genre = reader.GetString(3),
                BannerPath = reader.IsDBNull(4) ? null : reader.GetString(4),
                StageFilePath = reader.IsDBNull(5) ? null : reader.GetString(5),
                LastModified = new DateTime(reader.GetInt64(6), DateTimeKind.Utc).ToLocalTime(),
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
);";
                    cmd.ExecuteNonQuery();
                }

                int existingVersion = (int)readLongMeta(connection, "schema_version");

                if (existingVersion > schema_version)
                    throw new InvalidOperationException($"BMS library index schema {existingVersion} is newer than supported schema {schema_version}.");

                if (existingVersion == 0)
                    createVersion2Schema(connection);
                else if (existingVersion == 1)
                    migrateVersion1To2(connection);
                else
                    createVersion2Schema(connection);

                writeMeta(connection, "schema_version", schema_version.ToString(CultureInfo.InvariantCulture));
                initialized = true;
            }
        }

        private static void createVersion2Schema(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
CREATE TABLE IF NOT EXISTS {table_charts} (
    chart_path TEXT PRIMARY KEY,
    folder_path TEXT NOT NULL,
    file_name TEXT NOT NULL,
    file_size INTEGER NOT NULL,
    last_modified_ticks INTEGER NOT NULL,
    beatmap_id TEXT NOT NULL,
    set_id TEXT NOT NULL,
    path_key TEXT NOT NULL,
    seen_generation INTEGER NOT NULL DEFAULT 0,
    sync_revision INTEGER NOT NULL DEFAULT 0,
    sync_state INTEGER NOT NULL DEFAULT 0,
    parse_version INTEGER NOT NULL DEFAULT 1,
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

CREATE TABLE IF NOT EXISTS {table_sync_changes} (
    revision INTEGER PRIMARY KEY AUTOINCREMENT,
    beatmap_id TEXT NOT NULL,
    set_id TEXT NOT NULL,
    chart_path TEXT NOT NULL,
    change_kind INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_charts_folder ON {table_charts}(folder_path);
CREATE INDEX IF NOT EXISTS idx_charts_path_key ON {table_charts}(path_key);
CREATE INDEX IF NOT EXISTS idx_charts_beatmap_id ON {table_charts}(beatmap_id);
CREATE INDEX IF NOT EXISTS idx_charts_set_id ON {table_charts}(set_id);";
            cmd.ExecuteNonQuery();
        }

        private static void migrateVersion1To2(SqliteConnection connection)
        {
            using var transaction = connection.BeginTransaction();

            using (var alter = connection.CreateCommand())
            {
                alter.Transaction = transaction;
                alter.CommandText = $@"
ALTER TABLE {table_charts} ADD COLUMN set_id TEXT NOT NULL DEFAULT '';
ALTER TABLE {table_charts} ADD COLUMN seen_generation INTEGER NOT NULL DEFAULT 0;
ALTER TABLE {table_charts} ADD COLUMN sync_revision INTEGER NOT NULL DEFAULT 0;
ALTER TABLE {table_charts} ADD COLUMN sync_state INTEGER NOT NULL DEFAULT 0;
ALTER TABLE {table_charts} ADD COLUMN parse_version INTEGER NOT NULL DEFAULT 1;

CREATE TABLE {table_sync_changes} (
    revision INTEGER PRIMARY KEY AUTOINCREMENT,
    beatmap_id TEXT NOT NULL,
    set_id TEXT NOT NULL,
    chart_path TEXT NOT NULL,
    change_kind INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_charts_folder ON {table_charts}(folder_path);
CREATE INDEX IF NOT EXISTS idx_charts_path_key ON {table_charts}(path_key);
CREATE INDEX IF NOT EXISTS idx_charts_beatmap_id ON {table_charts}(beatmap_id);
CREATE INDEX IF NOT EXISTS idx_charts_set_id ON {table_charts}(set_id);";
                alter.ExecuteNonQuery();
            }

            var charts = new List<(string ChartPath, string FolderPath, Guid BeatmapId)>();

            using (var select = connection.CreateCommand())
            {
                select.Transaction = transaction;
                select.CommandText = $"SELECT chart_path, folder_path, beatmap_id FROM {table_charts};";

                using var reader = select.ExecuteReader();

                while (reader.Read())
                    charts.Add((reader.GetString(0), reader.GetString(1), Guid.Parse(reader.GetString(2))));
            }

            foreach ((string chartPath, string folderPath, Guid beatmapId) in charts)
            {
                Guid setId = BmsChartIdentity.CreateSetId(folderPath);

                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = $"UPDATE {table_charts} SET set_id = $setId WHERE chart_path = $chartPath;";
                    update.Parameters.AddWithValue("$setId", setId.ToString());
                    update.Parameters.AddWithValue("$chartPath", chartPath);
                    update.ExecuteNonQuery();
                }

                enqueueSyncChange(connection, transaction, beatmapId, setId, chartPath, SyncChangeKind.Upsert);
            }

            transaction.Commit();
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

        private int readCount(string table)
        {
            ensureInitialized();

            using var connection = openConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {table};";
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        private static long readLongMeta(SqliteConnection connection, string key)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT value FROM {table_meta} WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            object? result = cmd.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                return 0;

            return long.TryParse(result.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : 0;
        }

        private static long readLongMeta(SqliteConnection connection, SqliteTransaction transaction, string key)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT value FROM {table_meta} WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            object? result = cmd.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                return 0;

            return long.TryParse(result.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : 0;
        }

        private static void enqueueSyncChange(
            SqliteConnection connection,
            SqliteTransaction transaction,
            Guid beatmapId,
            Guid setId,
            string chartPath,
            SyncChangeKind kind)
        {
            long revision;

            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = $@"
INSERT INTO {table_sync_changes} (beatmap_id, set_id, chart_path, change_kind)
VALUES ($beatmapId, $setId, $chartPath, $kind);
SELECT last_insert_rowid();";
                insert.Parameters.AddWithValue("$beatmapId", beatmapId.ToString());
                insert.Parameters.AddWithValue("$setId", setId.ToString());
                insert.Parameters.AddWithValue("$chartPath", chartPath);
                insert.Parameters.AddWithValue("$kind", (int)kind);
                revision = Convert.ToInt64(insert.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = $@"
UPDATE {table_charts}
SET sync_revision = $revision,
    sync_state = $syncState
WHERE beatmap_id = $beatmapId;";
            update.Parameters.AddWithValue("$revision", revision);
            update.Parameters.AddWithValue("$syncState", (int)SyncState.Pending);
            update.Parameters.AddWithValue("$beatmapId", beatmapId.ToString());
            update.ExecuteNonQuery();
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

        private static void writeMeta(SqliteConnection connection, SqliteTransaction transaction, string key, string value)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $@"
INSERT INTO {table_meta} (key, value) VALUES ($key, $value)
ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.ExecuteNonQuery();
        }

        private static void replaceRoots(SqliteConnection connection, SqliteTransaction transaction, IEnumerable<string> rootPaths)
        {
            using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = $"DELETE FROM {table_roots};";
                delete.ExecuteNonQuery();
            }

            foreach (string rootPath in rootPaths)
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = $"INSERT INTO {table_roots} (path) VALUES ($path);";
                insert.Parameters.AddWithValue("$path", rootPath);
                insert.ExecuteNonQuery();
            }
        }

        private static bool isPathWithinRoot(string path, string rootPath)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));

                return fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                       || fullPath.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public sealed class ScanWriter : IDisposable
        {
            private readonly BmsLibraryIndexRepository repository;
            private readonly long generation;
            private readonly SqliteConnection connection;
            private readonly SqliteCommand markSeen;
            private readonly SqliteCommand upsertSong;
            private readonly SqliteCommand upsertChart;
            private readonly SqliteCommand insertChange;
            private readonly SqliteCommand updateSyncState;
            private bool disposed;

            internal ScanWriter(BmsLibraryIndexRepository repository, long generation)
            {
                this.repository = repository;
                this.generation = generation;
                connection = repository.openConnection();

                markSeen = createCommand($@"
UPDATE {table_charts}
SET seen_generation = $generation
WHERE chart_path = $chartPath;", "$generation", "$chartPath");

                upsertSong = createCommand($@"
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
    last_modified_ticks = excluded.last_modified_ticks;",
                    "$folder", "$title", "$artist", "$genre", "$banner", "$stage", "$modified");

                upsertChart = createCommand($@"
INSERT INTO {table_charts} (
    chart_path, folder_path, file_name, file_size, last_modified_ticks,
    beatmap_id, set_id, path_key, seen_generation, sync_revision, sync_state, parse_version,
    title, sub_title, artist, sub_artist, genre,
    play_level, rank, ln_type, key_count, total_notes,
    bpm, min_bpm, max_bpm, duration, total_gauge,
    preview_time, audio_file, preview_file,
    has_scratch, has_ln, has_stop, has_scroll, has_bga,
    keysound_files_json
) VALUES (
    $chartPath, $folder, $fileName, $size, $modified,
    $beatmapId, $setId, $pathKey, $generation, 0, 0, $parseVersion,
    $title, $subTitle, $artist, $subArtist, $genre,
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
    set_id = excluded.set_id,
    path_key = excluded.path_key,
    seen_generation = excluded.seen_generation,
    parse_version = excluded.parse_version,
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
    keysound_files_json = excluded.keysound_files_json;",
                    "$chartPath", "$folder", "$fileName", "$size", "$modified",
                    "$beatmapId", "$setId", "$pathKey", "$generation", "$parseVersion",
                    "$title", "$subTitle", "$artist", "$subArtist", "$genre",
                    "$playLevel", "$rank", "$lnType", "$keyCount", "$totalNotes",
                    "$bpm", "$minBpm", "$maxBpm", "$duration", "$totalGauge",
                    "$previewTime", "$audio", "$preview",
                    "$scratch", "$ln", "$stop", "$scroll", "$bga", "$keysounds");

                insertChange = createCommand($@"
INSERT INTO {table_sync_changes} (beatmap_id, set_id, chart_path, change_kind)
VALUES ($beatmapId, $setId, $chartPath, $kind);
SELECT last_insert_rowid();", "$beatmapId", "$setId", "$chartPath", "$kind");

                updateSyncState = createCommand($@"
UPDATE {table_charts}
SET sync_revision = $revision,
    sync_state = $syncState
WHERE beatmap_id = $beatmapId;", "$revision", "$syncState", "$beatmapId");
            }

            public void WriteBatch(IReadOnlyList<ScanWriteItem> items)
            {
                ObjectDisposedException.ThrowIf(disposed, this);

                if (items.Count == 0)
                    return;

                lock (repository.writeLock)
                {
                    using var transaction = connection.BeginTransaction();
                    setTransaction(transaction);

                    foreach (ScanWriteItem item in items)
                    {
                        if (item.Chart == null)
                        {
                            set(markSeen, "$generation", generation);
                            set(markSeen, "$chartPath", item.ChartPath);
                            markSeen.ExecuteNonQuery();
                            continue;
                        }

                        BMSChartCache chart = item.Chart;
                        BMSSongCache song = item.Song ?? createSong(chart);
                        BmsChartIdentity identity = BmsChartIdentity.Create(item.ChartPath, chart.FolderPath);

                        bindSong(song);
                        upsertSong.ExecuteNonQuery();
                        bindChart(chart, identity);
                        upsertChart.ExecuteNonQuery();

                        set(insertChange, "$beatmapId", identity.BeatmapId.ToString());
                        set(insertChange, "$setId", identity.SetId.ToString());
                        set(insertChange, "$chartPath", item.ChartPath);
                        set(insertChange, "$kind", (int)SyncChangeKind.Upsert);
                        long revision = Convert.ToInt64(insertChange.ExecuteScalar(), CultureInfo.InvariantCulture);

                        set(updateSyncState, "$revision", revision);
                        set(updateSyncState, "$syncState", (int)SyncState.Pending);
                        set(updateSyncState, "$beatmapId", identity.BeatmapId.ToString());
                        updateSyncState.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                markSeen.Dispose();
                upsertSong.Dispose();
                upsertChart.Dispose();
                insertChange.Dispose();
                updateSyncState.Dispose();
                connection.Dispose();
            }

            private SqliteCommand createCommand(string commandText, params string[] parameterNames)
            {
                SqliteCommand command = connection.CreateCommand();
                command.CommandText = commandText;

                foreach (string parameterName in parameterNames)
                    command.Parameters.Add(new SqliteParameter(parameterName, null));

                command.Prepare();
                return command;
            }

            private void setTransaction(SqliteTransaction transaction)
            {
                markSeen.Transaction = transaction;
                upsertSong.Transaction = transaction;
                upsertChart.Transaction = transaction;
                insertChange.Transaction = transaction;
                updateSyncState.Transaction = transaction;
            }

            private void bindSong(BMSSongCache song)
            {
                set(upsertSong, "$folder", song.FolderPath);
                set(upsertSong, "$title", song.Title ?? string.Empty);
                set(upsertSong, "$artist", song.Artist ?? string.Empty);
                set(upsertSong, "$genre", song.Genre ?? string.Empty);
                set(upsertSong, "$banner", (object?)song.BannerPath ?? DBNull.Value);
                set(upsertSong, "$stage", (object?)song.StageFilePath ?? DBNull.Value);
                set(upsertSong, "$modified", song.LastModified.ToUniversalTime().Ticks);
            }

            private void bindChart(BMSChartCache chart, BmsChartIdentity identity)
            {
                set(upsertChart, "$chartPath", chart.FullPath);
                set(upsertChart, "$folder", chart.FolderPath);
                set(upsertChart, "$fileName", chart.FileName);
                set(upsertChart, "$size", chart.FileSize);
                set(upsertChart, "$modified", chart.LastModified.ToUniversalTime().Ticks);
                set(upsertChart, "$beatmapId", identity.BeatmapId.ToString());
                set(upsertChart, "$setId", identity.SetId.ToString());
                set(upsertChart, "$pathKey", identity.PathKey);
                set(upsertChart, "$generation", generation);
                set(upsertChart, "$parseVersion", 1);
                set(upsertChart, "$title", chart.Title ?? string.Empty);
                set(upsertChart, "$subTitle", chart.SubTitle ?? string.Empty);
                set(upsertChart, "$artist", chart.Artist ?? string.Empty);
                set(upsertChart, "$subArtist", chart.SubArtist ?? string.Empty);
                set(upsertChart, "$genre", chart.Genre ?? string.Empty);
                set(upsertChart, "$playLevel", chart.PlayLevel);
                set(upsertChart, "$rank", chart.Rank);
                set(upsertChart, "$lnType", chart.LnType);
                set(upsertChart, "$keyCount", chart.KeyCount);
                set(upsertChart, "$totalNotes", chart.TotalNotes);
                set(upsertChart, "$bpm", chart.Bpm);
                set(upsertChart, "$minBpm", chart.MinBpm);
                set(upsertChart, "$maxBpm", chart.MaxBpm);
                set(upsertChart, "$duration", chart.Duration);
                set(upsertChart, "$totalGauge", chart.Total);
                set(upsertChart, "$previewTime", chart.PreviewTime);
                set(upsertChart, "$audio", (object?)chart.AudioFile ?? DBNull.Value);
                set(upsertChart, "$preview", (object?)chart.PreviewFile ?? DBNull.Value);
                set(upsertChart, "$scratch", chart.HasScratch ? 1 : 0);
                set(upsertChart, "$ln", chart.HasLongNotes ? 1 : 0);
                set(upsertChart, "$stop", chart.HasStopSequence ? 1 : 0);
                set(upsertChart, "$scroll", chart.HasScrollChanges ? 1 : 0);
                set(upsertChart, "$bga", chart.HasBgaLayer ? 1 : 0);
                set(upsertChart, "$keysounds", JsonSerializer.Serialize(chart.KeysoundFiles));
            }

            private static BMSSongCache createSong(BMSChartCache chart)
            {
                return new BMSSongCache
                {
                    FolderPath = chart.FolderPath,
                    Title = chart.Title,
                    Artist = chart.Artist,
                    Genre = chart.Genre,
                    LastModified = chart.LastModified,
                };
            }

            private static void set(SqliteCommand command, string parameterName, object value)
            {
                command.Parameters[parameterName].Value = value;
            }
        }

        private SqliteConnection openConnection()
        {
            var connection = new SqliteConnection($"Data Source={databasePath};Cache=Shared;Mode=ReadWriteCreate");
            connection.Open();
            return connection;
        }
    }
}
