// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using System.Threading.Channels;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Beatmaps.Persistence;
using osu.Game.Rulesets.BMS.Localization;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    public enum BmsRealmSyncChangeKind
    {
        Upsert,
        Delete,
    }

    public readonly record struct BmsRealmSyncChange(
        long Revision,
        Guid BeatmapId,
        Guid SetId,
        string ChartPath,
        BmsRealmSyncChangeKind Kind);

    public readonly record struct BmsRealmSyncChart(BMSChartCache Chart, BmsChartIdentity Identity);

    public sealed record BmsRealmSyncSet(Guid SetId, BMSSongCache Song, IReadOnlyList<BmsRealmSyncChart> Charts);

    /// <summary>
    ///     Manages BMS library scanning, SQLite indexing, and loading.
    /// </summary>
    public class BMSBeatmapManager
    {
        private static readonly object shared_manager_lock = new object();
        private static BMSBeatmapManager? sharedManager;
        private static string? sharedStorageDirectory;

        public Bindable<string> RootPath { get; } = new Bindable<string>(string.Empty);

        public IReadOnlyList<string> RootPaths => rootPaths;

        public BindableDouble ScanProgress { get; } = new BindableDouble();

        public Bindable<string> StatusMessage { get; } = new Bindable<string>(string.Empty);

        public BindableBool IsScanning { get; } = new BindableBool();

        public long LastScanRevision { get; private set; }

        public long LastSynchronizedScanRevision { get; private set; }

        public int LastSynchronizedRealmFileMappingVersion { get; private set; }

        /// <summary>
        /// Tracks whether Realm still needs a catalog pass. Revision equality alone is insufficient
        /// (both zero on a fresh index) and would skip the first sync, leaving the carousel on stale IDs.
        /// </summary>
        private bool realmSyncRequired;

        public bool NeedsRealmSynchronization => realmSyncRequired
                                                 || PendingRealmSyncSetCount > 0
                                                 || LastSynchronizedRealmFileMappingVersion != BmsRealmSyncConstants.FILE_MAPPING_SCHEMA_VERSION;

        public bool HasIndexedCharts => ChartCount > 0;

        public int ChartCount => indexRepository.ChartCount;

        public int SongCount => indexRepository.SongCount;

        private static readonly string[] bms_extensions = { ".bms", ".bme", ".bml", ".pms" };

        private readonly BmsLibraryIndexRepository indexRepository;
        private readonly List<string> rootPaths = new List<string>();

        private CancellationTokenSource? scanCts;

        public static BMSBeatmapManager GetShared(Storage storage)
        {
            string directory = BmsStoragePaths.EnsureInitialized(storage);

            lock (shared_manager_lock)
            {
                if (sharedManager == null || !string.Equals(sharedStorageDirectory, directory, StringComparison.Ordinal))
                {
                    sharedManager = new BMSBeatmapManager(directory);
                    sharedManager.LoadCache();
                    sharedStorageDirectory = directory;
                }

                return sharedManager;
            }
        }

        public BMSBeatmapManager(string storageDirectory)
        {
            Directory.CreateDirectory(storageDirectory);
            indexRepository = new BmsLibraryIndexRepository(Path.Combine(storageDirectory, BmsStoragePaths.INDEX_DATABASE_FILE));
        }

        public void LoadCache()
        {
            try
            {
                LastScanRevision = indexRepository.ScanRevision;
                LastSynchronizedRealmFileMappingVersion = indexRepository.RealmFileMappingVersion;
                SetRootPaths(indexRepository.GetRootPaths());
                StatusMessage.Value = BmsStrings.Scan_LoadedFromIndex(SongCount, ChartCount);
                realmSyncRequired = PendingRealmSyncSetCount > 0;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[BMS] Failed to load library index");
            }
        }

        public IReadOnlyList<string> GetIndexedRootPaths() => indexRepository.GetRootPaths();

        public void SetRootPaths(IEnumerable<string> paths)
        {
            rootPaths.Clear();
            rootPaths.AddRange(normaliseRootPaths(paths));
            RootPath.Value = rootPaths.FirstOrDefault() ?? string.Empty;
        }

        public void MarkRealmSynchronized()
        {
            LastSynchronizedScanRevision = LastScanRevision;
            LastSynchronizedRealmFileMappingVersion = BmsRealmSyncConstants.FILE_MAPPING_SCHEMA_VERSION;
            indexRepository.WriteRealmFileMappingVersion(BmsRealmSyncConstants.FILE_MAPPING_SCHEMA_VERSION);
            realmSyncRequired = false;
        }

        public void RequireRealmSynchronization() => realmSyncRequired = true;

        public long RealmSyncCursor => indexRepository.SyncCursor;

        public int PendingRealmSyncSetCount => indexRepository.PendingSyncSetCount;

        public void PrepareRealmSynchronization()
        {
            if (LastSynchronizedRealmFileMappingVersion != BmsRealmSyncConstants.FILE_MAPPING_SCHEMA_VERSION)
                indexRepository.EnqueueAllChartsForSync();
        }

        public IReadOnlyList<BmsRealmSyncChange> GetPendingRealmSyncChanges(int maxSetCount)
        {
            return indexRepository.GetPendingSyncChangesForSets(maxSetCount)
                                  .Select(change => new BmsRealmSyncChange(
                                      change.Revision,
                                      change.BeatmapId,
                                      change.SetId,
                                      change.ChartPath,
                                      (BmsRealmSyncChangeKind)change.Kind))
                                  .ToList();
        }

        public bool TryGetRealmSyncSet(Guid setId, out BmsRealmSyncSet set)
        {
            IReadOnlyList<BmsLibraryIndexRepository.IndexedChart> indexedCharts = indexRepository.GetChartsBySetId(setId);

            if (indexedCharts.Count == 0)
            {
                set = null!;
                return false;
            }

            string folderPath = indexedCharts[0].Chart.FolderPath;

            if (!indexRepository.TryGetSong(folderPath, out BMSSongCache song))
            {
                BMSChartCache firstChart = indexedCharts[0].Chart;
                song = new BMSSongCache
                {
                    FolderPath = folderPath,
                    Title = firstChart.Title,
                    Artist = firstChart.Artist,
                    Genre = firstChart.Genre,
                    LastModified = firstChart.LastModified,
                };
            }

            set = new BmsRealmSyncSet(
                setId,
                song,
                indexedCharts.Select(indexed => new BmsRealmSyncChart(indexed.Chart, indexed.Identity)).ToList());
            return true;
        }

        public void AcknowledgeRealmSyncChanges(IReadOnlyCollection<long> revisions)
        {
            indexRepository.AcknowledgeSyncChanges(revisions);
        }

        public long FilterSyncCursor => indexRepository.FilterCursor;

        public int PendingFilterSyncChangeCount => indexRepository.PendingFilterChangeCount;

        public IReadOnlyList<BmsRealmSyncChange> GetPendingFilterSyncChanges(int limit)
        {
            return indexRepository.GetPendingFilterSyncChanges(limit)
                                  .Select(change => new BmsRealmSyncChange(
                                      change.Revision,
                                      change.BeatmapId,
                                      change.SetId,
                                      change.ChartPath,
                                      (BmsRealmSyncChangeKind)change.Kind))
                                  .ToList();
        }

        public void AcknowledgeFilterSyncChanges(IReadOnlyCollection<long> revisions)
            => indexRepository.AcknowledgeFilterSyncChanges(revisions);

        public void MarkFilterSynchronizedToCurrent()
            => indexRepository.MarkFilterSynchronizedToCurrent();

        public BeatmapSetInfo BuildRealmSyncTarget(BmsRealmSyncSet syncSet, RulesetInfo bmsRulesetInfo)
        {
            BMSSongCache song = syncSet.Song;
            var beatmapSet = new BeatmapSetInfo
            {
                ID = syncSet.SetId,
                DateAdded = song.LastModified,
                Hash = song.FolderPath,
            };

            foreach (BmsRealmSyncChart indexed in syncSet.Charts)
            {
                BMSChartCache chart = indexed.Chart;
                var metadata = new BeatmapMetadata
                {
                    Title = string.IsNullOrWhiteSpace(chart.Title) ? song.Title : chart.Title,
                    TitleUnicode = string.IsNullOrWhiteSpace(chart.Title) ? song.Title : chart.Title,
                    Artist = string.IsNullOrWhiteSpace(chart.Artist) ? song.Artist : chart.Artist,
                    ArtistUnicode = string.IsNullOrWhiteSpace(chart.Artist) ? song.Artist : chart.Artist,
                    Source = "BMS",
                    Tags = buildTags(chart),
                    AudioFile = string.Empty,
                    BackgroundFile = song.StageFilePath ?? string.Empty,
                    PreviewTime = chart.PreviewTime,
                };

                var beatmapInfo = new BeatmapInfo(bmsRulesetInfo, new BeatmapDifficulty(), metadata)
                {
                    ID = indexed.Identity.BeatmapId,
                    DifficultyName = formatDifficultyName(chart),
                    BPM = chart.Bpm,
                    Length = chart.Duration,
                    Hash = BmsPathKeys.ComputeRealmFileHash(chart.FullPath),
                    MD5Hash = indexed.Identity.PathKey,
                    TotalObjectCount = chart.TotalNotes,
                    EndTimeObjectCount = chart.LongNoteCount,
                    BeatmapSet = beatmapSet,
                    Difficulty =
                    {
                        CircleSize = chart.KeyCount,
                        OverallDifficulty = mapRankToOD(chart.Rank),
                        DrainRate = 7
                    }
                };

                beatmapSet.Beatmaps.Add(beatmapInfo);
            }

            return beatmapSet;
        }

        public void CancelScan()
        {
            lock (this)
            {
                scanCts?.Cancel();
            }
        }

        public Task ScanLibraryAsync(string rootPath, CancellationToken cancellationToken = default) => ScanLibraryAsync(new[] { rootPath }, cancellationToken);

        public async Task ScanLibraryAsync(IEnumerable<string> scanPaths, CancellationToken cancellationToken = default)
        {
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationTokenSource? previous;

            lock (this)
            {
                previous = scanCts;
                scanCts = linked;
            }

            if (previous != null)
            {
                await previous.CancelAsync().ConfigureAwait(false);

                try
                {
                    // Allow the previous scan pipeline to unwind before replacing shared progress state.
                    await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }

                previous.Dispose();
            }

            var token = linked.Token;

            IsScanning.Value = true;
            ScanProgress.Value = 0;
            StatusMessage.Value = BmsStrings.SCAN_SCANNING_FOLDERS.ToString();

            try
            {
                List<string> configuredPaths = normaliseRootPaths(scanPaths);

                if (configuredPaths.Count == 0)
                {
                    // Empty path list is an explicit clear: drop the SQLite index and queue Realm deletes.
                    StatusMessage.Value = BmsStrings.SCAN_CLEARING_LIBRARY.ToString();
                    ScanProgress.Value = 0.25;

                    long clearGeneration = indexRepository.BeginScanGeneration();
                    LastScanRevision = indexRepository.CompleteScanGeneration(clearGeneration, configuredPaths);
                    SetRootPaths(configuredPaths);
                    realmSyncRequired = true;

                    StatusMessage.Value = BmsStrings.Scan_Complete(SongCount, ChartCount);
                    return;
                }

                if (configuredPaths.Any(path => !Directory.Exists(path)))
                {
                    StatusMessage.Value = BmsStrings.SCAN_NO_VALID_PATHS.ToString();
                    return;
                }

                long generation = indexRepository.BeginScanGeneration();
                var pipelineCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                CancellationToken pipelineToken = pipelineCts.Token;
                var files = Channel.CreateBounded<string>(new BoundedChannelOptions(512)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = true,
                });
                var writes = Channel.CreateBounded<BmsLibraryIndexRepository.ScanWriteItem>(new BoundedChannelOptions(256)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = false,
                });
                int discoveredFiles = 0;
                int processedFiles = 0;
                object progressLock = new object();

                Task producer = Task.Run(async () =>
                {
                    try
                    {
                        var extensions = new HashSet<string>(bms_extensions, StringComparer.OrdinalIgnoreCase);

                        foreach (string rootPath in configuredPaths)
                        {
                            pipelineToken.ThrowIfCancellationRequested();

                            try
                            {
                                foreach (string file in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories))
                                {
                                    pipelineToken.ThrowIfCancellationRequested();

                                    if (!extensions.Contains(Path.GetExtension(file)))
                                        continue;

                                    int found = Interlocked.Increment(ref discoveredFiles);

                                    // Discovery can run for a long time before the first write batch;
                                    // surface a count so notifications are not stuck on a spinner with no text change.
                                    if (found == 1 || found % 256 == 0)
                                    {
                                        lock (progressLock)
                                        {
                                            if (Volatile.Read(ref processedFiles) == 0)
                                                StatusMessage.Value = BmsStrings.Scan_FoundFiles(found);
                                        }
                                    }

                                    await files.Writer.WriteAsync(file, pipelineToken).ConfigureAwait(false);
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"[BMS] Failed to enumerate '{rootPath}': {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                                throw;
                            }
                        }

                        files.Writer.TryComplete();
                    }
                    catch (Exception ex)
                    {
                        files.Writer.TryComplete(ex);
                        throw;
                    }
                }, pipelineToken);

                int workerCount = Math.Clamp(Environment.ProcessorCount, 2, 8);
                Task[] parsers = Enumerable.Range(0, workerCount).Select(_ => Task.Run(async () =>
                {
                    await foreach (string filePath in files.Reader.ReadAllAsync(pipelineToken).ConfigureAwait(false))
                    {
                        BmsLibraryIndexRepository.ScanWriteItem? item = createScanWriteItem(filePath, pipelineToken);

                        if (item != null)
                            await writes.Writer.WriteAsync(item, pipelineToken).ConfigureAwait(false);
                    }
                }, pipelineToken)).ToArray();

                Task completeWrites = Task.Run(async () =>
                {
                    try
                    {
                        await Task.WhenAll(parsers).ConfigureAwait(false);
                        writes.Writer.TryComplete();
                    }
                    catch (Exception ex)
                    {
                        writes.Writer.TryComplete(ex);
                        throw;
                    }
                }, pipelineToken);

                Task writer = Task.Run(async () =>
                {
                    using BmsLibraryIndexRepository.ScanWriter scanWriter = indexRepository.OpenScanWriter(generation);
                    var batch = new List<BmsLibraryIndexRepository.ScanWriteItem>(128);

                    await foreach (BmsLibraryIndexRepository.ScanWriteItem item in writes.Reader.ReadAllAsync(pipelineToken).ConfigureAwait(false))
                    {
                        batch.Add(item);

                        if (batch.Count < 128)
                            continue;

                        scanWriter.WriteBatch(batch);
                        batch.Clear();

                        int done = Interlocked.Add(ref processedFiles, 128);

                        lock (progressLock)
                        {
                            int total = Math.Max(done, Volatile.Read(ref discoveredFiles));
                            ScanProgress.Value = total == 0 ? 0 : (double)done / total;
                            StatusMessage.Value = BmsStrings.Scan_ParsingFolders(done, total);
                        }
                    }

                    if (batch.Count > 0)
                    {
                        int count = batch.Count;
                        scanWriter.WriteBatch(batch);
                        int done = Interlocked.Add(ref processedFiles, count);

                        lock (progressLock)
                        {
                            int total = Math.Max(done, Volatile.Read(ref discoveredFiles));
                            ScanProgress.Value = total == 0 ? 0 : (double)done / total;
                            StatusMessage.Value = BmsStrings.Scan_ParsingFolders(done, total);
                        }
                    }
                }, pipelineToken);

                try
                {
                    await Task.WhenAll(producer, completeWrites, writer).ConfigureAwait(false);
                    pipelineToken.ThrowIfCancellationRequested();

                    LastScanRevision = indexRepository.CompleteScanGeneration(generation, configuredPaths);
                    SetRootPaths(configuredPaths);
                    realmSyncRequired = true;

                    StatusMessage.Value = BmsStrings.Scan_Complete(SongCount, ChartCount);
                }
                catch
                {
                    pipelineCts.Cancel();
                    throw;
                }
                finally
                {
                    pipelineCts.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage.Value = BmsStrings.SCAN_CANCELLED.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "BMS library scan failed");
                StatusMessage.Value = BmsStrings.Scan_Error(ex.Message);
            }
            finally
            {
                IsScanning.Value = false;
                ScanProgress.Value = 1;

                lock (this)
                {
                    if (ReferenceEquals(scanCts, linked))
                        scanCts = null;
                }

                linked.Dispose();
            }
        }

        public BMSChartCache? GetChartByHash(string pathKey)
        {
            return TryGetChartByPathKey(pathKey, out BMSChartCache chart) ? chart : null;
        }

        public bool TryGetChart(Guid beatmapId, out BMSChartCache chart)
        {
            if (indexRepository.TryGetChart(beatmapId, out BmsLibraryIndexRepository.IndexedChart indexed))
            {
                chart = indexed.Chart;
                return true;
            }

            chart = null!;
            return false;
        }

        public bool TryGetChartByPathKey(string pathKey, out BMSChartCache chart)
        {
            if (indexRepository.TryGetChartByPathKey(pathKey, out BmsLibraryIndexRepository.IndexedChart indexed))
            {
                chart = indexed.Chart;
                return true;
            }

            chart = null!;
            return false;
        }

        public IReadOnlyList<BMSChartCache> GetChartPage(int offset, int limit)
        {
            return indexRepository.GetCharts(offset, limit).Select(indexed => indexed.Chart).ToList();
        }

        public BmsChartSummaryPage GetChartSummaryPage(BmsChartQuery query, BmsChartPageCursor? after, int limit)
            => indexRepository.GetChartSummaries(query, after, limit);

        public BmsChartSummary? GetRandomChartSummary(BmsChartQuery query)
            => indexRepository.GetRandomChartSummary(query);

        public bool TryGetChartSummaryByPathKey(string pathKey, out BmsChartSummary summary)
            => indexRepository.TryGetChartSummaryByPathKey(pathKey, out summary);

        public bool TryGetChartSummaryByContentHash(string hash, out BmsChartSummary summary)
            => indexRepository.TryGetChartSummaryByContentHash(hash, out summary);

        public bool TryLookupChartByContentHash(string hash, out BMSChartCache chart)
            => indexRepository.TryGetChartByContentHash(hash, out chart);

        public IReadOnlyList<BmsFolderSummary> GetChildFolderPage(string parentPath, string? afterFolderPath, int limit)
            => indexRepository.GetChildFolders(parentPath, afterFolderPath, limit);

        public bool TryGetSourceReference(Guid beatmapId, out BMSSourceReference sourceReference)
            => indexRepository.TryGetSourceReference(beatmapId, out sourceReference);

        public bool TryGetSourceReferenceByHash(string pathKey, out BMSSourceReference sourceReference)
            => indexRepository.TryGetSourceReferenceByPathKey(pathKey, out sourceReference);

        private BmsLibraryIndexRepository.ScanWriteItem? createScanWriteItem(string filePath, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(filePath);

            if (!fileInfo.Exists)
                return null;

            long fileSize = fileInfo.Length;
            long modifiedTicks = fileInfo.LastWriteTimeUtc.Ticks;

            bool hadSnapshot = indexRepository.TryGetChartSnapshot(filePath, out BmsLibraryIndexRepository.ChartFileSnapshot snapshot);

            if (hadSnapshot
                && snapshot.FileSize == fileSize
                && snapshot.LastModifiedTicks == modifiedTicks
                && snapshot.HasContentHash)
            {
                return new BmsLibraryIndexRepository.ScanWriteItem(filePath, fileSize, modifiedTicks, null, null);
            }

            try
            {
                BMSChartCache? chart = parseBmsFileForCache(filePath, token);

                if (chart == null)
                    return hadSnapshot ? new BmsLibraryIndexRepository.ScanWriteItem(filePath, fileSize, modifiedTicks, null, null) : null;

                var song = new BMSSongCache
                {
                    FolderPath = chart.FolderPath,
                    Title = chart.Title,
                    Artist = chart.Artist,
                    Genre = chart.Genre,
                    BannerPath = findImageFile(chart.FolderPath, "banner", "bn"),
                    StageFilePath = findImageFile(chart.FolderPath, "stagefile", "stage", "bg"),
                    LastModified = Directory.GetLastWriteTime(chart.FolderPath),
                };

                return new BmsLibraryIndexRepository.ScanWriteItem(filePath, fileSize, modifiedTicks, chart, song);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to parse BMS file: {filePath} - {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                return hadSnapshot ? new BmsLibraryIndexRepository.ScanWriteItem(filePath, fileSize, modifiedTicks, null, null) : null;
            }
        }

        private static string formatDifficultyName(BMSChartCache chart)
        {
            string label = !string.IsNullOrWhiteSpace(chart.SubTitle)
                ? chart.SubTitle.Trim()
                : Path.GetFileNameWithoutExtension(chart.FileName);

            return chart.PlayLevel > 0
                ? $"★{chart.PlayLevel} {label}".TrimEnd()
                : label;
        }

        private static float mapRankToOD(int bmsRank) => bmsRank switch
        {
            0 => 9f,
            1 => 8f,
            2 => 7f,
            3 => 5f,
            _ => 7f
        };

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

        private static string buildTags(BMSChartCache chart)
        {
            List<string> tags = new List<string> { "bms", $"key{Math.Max(1, chart.KeyCount)}" };

            if (chart.HasScratch) tags.Add("scratch");
            if (chart.HasLongNotes) tags.Add("ln");
            if (chart.HasStopSequence) tags.Add("stop");
            if (chart.HasScrollChanges) tags.Add("scroll");
            if (chart.HasBgaLayer) tags.Add("bga");

            return string.Join(' ', tags);
        }

        private static bool isNoteChannel(string channel)
        {
            if (channel.Length != 2) return false;

            char first = channel[0];
            char second = channel[1];

            if (first == '1' && second >= '1' && second <= '9') return true;
            if (first == '2' && second >= '1' && second <= '9') return true;
            if (first == '5' && second >= '1' && second <= '9') return true;
            if (first == '6' && second >= '1' && second <= '9') return true;

            return false;
        }

        private static bool isScratchChannel(string channel) => channel == "16" || channel == "26" || channel == "56" || channel == "66";

        private static bool isLongNoteChannel(string channel)
        {
            if (channel.Length != 2) return false;

            return channel[0] == '5' || channel[0] == '6';
        }

        private static List<string> normaliseRootPaths(IEnumerable<string> paths)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> result = new List<string>();

            foreach (string path in paths)
            {
                string trimmed = path.Trim();

                if (string.IsNullOrEmpty(trimmed) || !seen.Add(trimmed))
                    continue;

                result.Add(trimmed);
            }

            return result;
        }

        private static bool isBackgroundSoundChannel(string channel)
        {
            if (isNoteChannel(channel))
                return false;

            return channel is not "02" and not "03" and not "04" and not "06" and not "07" and not "08" and not "09";
        }

        private static int countNotes(string data)
        {
            int count = 0;

            for (int i = 0; i + 1 < data.Length; i += 2)
            {
                if (data.Substring(i, 2) != "00")
                    count++;
            }

            return count;
        }

        private static (string Key, double Position)? findFirstObjectKey(string data)
        {
            int objectCount = data.Length / 2;

            if (objectCount <= 0)
                return null;

            for (int i = 0; i + 1 < data.Length; i += 2)
            {
                string key = data.Substring(i, 2);

                if (key == "00")
                    continue;

                return (key, (double)i / 2 / objectCount);
            }

            return null;
        }

        private static int determineKeyCount(HashSet<string> noteChannels)
        {
            bool hasPlayerTwo = noteChannels.Any(c => c[0] is '2' or '6');

            if (hasPlayerTwo)
            {
                int playerOneKeys = noteChannels.Count(c => c[0] is '1' or '5' && c[1] is not '6' and not '7');
                int playerTwoKeys = noteChannels.Count(c => c[0] is '2' or '6' && c[1] is not '6' and not '7');
                return playerOneKeys + playerTwoKeys;
            }

            if (noteChannels.Contains("11") && noteChannels.Contains("12") && noteChannels.Contains("13") && noteChannels.Contains("14") && noteChannels.Contains("15")
                && noteChannels.Contains("18") && noteChannels.Contains("19"))
                return 7;

            return noteChannels.Count(c => c[0] is '1' or '5' && c[1] is not '6' and not '7');
        }

        private static string[] readBmsLines(string filePath)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var shiftJis = Encoding.GetEncoding(932);
                return File.ReadAllLines(filePath, shiftJis);
            }
            catch
            {
                try
                {
                    return File.ReadAllLines(filePath, Encoding.UTF8);
                }
                catch
                {
                    return File.ReadAllLines(filePath);
                }
            }
        }

        private static string? findImageFile(string folderPath, params string[] patterns)
        {
            string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".bmp" };

            foreach (string pattern in patterns)
            {
                foreach (string ext in imageExtensions)
                {
                    string[] files = Directory.GetFiles(folderPath, $"*{pattern}*{ext}", SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                        return Path.GetFileName(files[0]);
                }
            }

            return null;
        }

        private BMSChartCache? parseBmsFileForCache(string filePath, CancellationToken token)
        {
            var fileInfo = new FileInfo(filePath);

            if (!fileInfo.Exists)
                return null;

            var cache = new BMSChartCache
            {
                FileName = fileInfo.Name,
                FolderPath = fileInfo.DirectoryName ?? string.Empty,
                FileSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                Md5Hash = BmsPathKeys.ComputeChartPathKey(filePath),
            };

            try
            {
                BmsContentHashes hashes = BmsContentHash.ComputeFile(filePath);
                cache.ContentMd5 = hashes.Md5;
                cache.ContentSha256 = hashes.Sha256;
            }
            catch
            {
                // Content hash is required for difficulty tables; leave empty and continue metadata parse.
            }

            string[] lines = readBmsLines(filePath);
            var keysoundFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var wavDefinitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var noteChannels = new HashSet<string>();
            var bpmValues = new List<double>();
            bool hasLongNotes = false;
            bool hasScratch = false;
            bool hasStopSequence = false;
            bool hasScrollChanges = false;
            bool hasBgaLayer = false;
            int noteCount = 0;
            int longNoteCount = 0;
            int maxMeasure = 0;
            string? previewAudioFile = null;
            string? explicitPreviewFile = null;
            int previewMeasure = int.MaxValue;
            double previewPosition = double.MaxValue;
            double baseBpm = 130;

            foreach (string line in lines)
            {
                token.ThrowIfCancellationRequested();

                if (!line.StartsWith('#')) continue;

                string upperLine = line.ToUpperInvariant();

                if (upperLine.StartsWith("#TITLE ", StringComparison.Ordinal))
                    cache.Title = line.Substring(7).Trim();
                else if (upperLine.StartsWith("#SUBTITLE ", StringComparison.Ordinal))
                    cache.SubTitle = line.Substring(10).Trim();
                else if (upperLine.StartsWith("#ARTIST ", StringComparison.Ordinal))
                    cache.Artist = line.Substring(8).Trim();
                else if (upperLine.StartsWith("#SUBARTIST ", StringComparison.Ordinal))
                    cache.SubArtist = line.Substring(11).Trim();
                else if (upperLine.StartsWith("#GENRE ", StringComparison.Ordinal))
                    cache.Genre = line.Substring(7).Trim();
                else if (upperLine.StartsWith("#PLAYLEVEL ", StringComparison.Ordinal))
                {
                    if (int.TryParse(line.Substring(11).Trim(), out int level))
                        cache.PlayLevel = level;
                }
                else if (upperLine.StartsWith("#RANK ", StringComparison.Ordinal))
                {
                    if (int.TryParse(line.Substring(6).Trim(), out int rank))
                        cache.Rank = rank;
                }
                else if (upperLine.StartsWith("#TOTAL ", StringComparison.Ordinal))
                {
                    if (double.TryParse(line.Substring(7).Trim(), out double total))
                        cache.Total = total;
                }
                else if (upperLine.StartsWith("#BPM ", StringComparison.Ordinal) && !upperLine.StartsWith("#BPM0", StringComparison.Ordinal))
                {
                    if (double.TryParse(line.Substring(5).Trim(), out double bpm))
                    {
                        baseBpm = bpm;
                        bpmValues.Add(bpm);
                    }
                }
                else if (upperLine.StartsWith("#BPM", StringComparison.Ordinal))
                {
                    int spaceIdx = line.IndexOf(' ');

                    if (spaceIdx > 4 && double.TryParse(line.Substring(spaceIdx + 1).Trim(), out double bpmVal))
                    {
                        string key = line.Substring(4, spaceIdx - 4);
                        bpmValues.Add(bpmVal);
                    }
                }
                else if (upperLine.StartsWith("#WAV", StringComparison.Ordinal))
                {
                    int spaceIdx = line.IndexOf(' ');

                    if (spaceIdx > 4)
                    {
                        string key = line.Substring(4, spaceIdx - 4).Trim();
                        string soundFile = line.Substring(spaceIdx + 1).Trim();

                        if (!string.IsNullOrEmpty(soundFile))
                        {
                            keysoundFiles.Add(soundFile);

                            if (!string.IsNullOrEmpty(key))
                                wavDefinitions[key] = soundFile;
                        }
                    }
                }
                else if (upperLine.StartsWith("#LNTYPE ", StringComparison.Ordinal) || upperLine.StartsWith("#LNOBJ ", StringComparison.Ordinal))
                {
                    hasLongNotes = true;

                    if (upperLine.StartsWith("#LNTYPE ", StringComparison.Ordinal)
                        && int.TryParse(line.Substring(8).Trim(), out int lnType))
                        cache.LnType = lnType;
                }
                else if (upperLine.StartsWith("#PREVIEW ", StringComparison.Ordinal))
                    explicitPreviewFile = line.Substring(9).Trim();
                else if (upperLine.StartsWith("#SCROLL", StringComparison.Ordinal))
                    hasScrollChanges = true;
                else if (line.Length > 6 && line[6] == ':')
                {
                    if (int.TryParse(line.AsSpan(1, 3), out int measureNum) && measureNum > maxMeasure)
                        maxMeasure = measureNum;

                    string channelStr = line.Substring(4, 2);

                    if (isNoteChannel(channelStr))
                    {
                        noteChannels.Add(channelStr);
                        string data = line.Substring(7);
                        int notesInChannel = countNotes(data);
                        noteCount += notesInChannel;

                        if (isScratchChannel(channelStr))
                            hasScratch = true;

                        if (isLongNoteChannel(channelStr))
                        {
                            hasLongNotes = true;
                            longNoteCount += notesInChannel;
                        }
                    }
                    else if (isBackgroundSoundChannel(channelStr))
                    {
                        var firstObject = findFirstObjectKey(line.Substring(7));

                        if (firstObject.HasValue
                            && wavDefinitions.TryGetValue(firstObject.Value.Key, out string? audioFile)
                            && (measureNum < previewMeasure || measureNum == previewMeasure && firstObject.Value.Position < previewPosition))
                        {
                            previewMeasure = measureNum;
                            previewPosition = firstObject.Value.Position;
                            previewAudioFile = audioFile;
                        }
                    }

                    if (channelStr == "09")
                        hasStopSequence = true;
                    else if (channelStr is "04" or "06" or "07")
                        hasBgaLayer = true;
                }
            }

            cache.Bpm = baseBpm;
            cache.MinBpm = bpmValues.Count > 0 ? bpmValues.Min() : baseBpm;
            cache.MaxBpm = bpmValues.Count > 0 ? bpmValues.Max() : baseBpm;
            cache.TotalNotes = noteCount;
            cache.LongNoteCount = longNoteCount;
            cache.HasScratch = hasScratch;
            cache.HasLongNotes = hasLongNotes;
            cache.HasStopSequence = hasStopSequence;
            cache.HasScrollChanges = hasScrollChanges;
            cache.HasBgaLayer = hasBgaLayer;
            cache.KeysoundFiles = keysoundFiles.ToList();
            cache.AudioFile = sanitiseAudioReference(previewAudioFile, cache.FolderPath);
            cache.PreviewFile = sanitiseAudioReference(explicitPreviewFile, cache.FolderPath);

            if (baseBpm > 0 && maxMeasure > 0)
                cache.Duration = (maxMeasure + 1) * 4.0 * 60000.0 / baseBpm;

            if (baseBpm > 0 && previewMeasure != int.MaxValue)
                cache.PreviewTime = (int)Math.Max(0, (previewMeasure * 4 + previewPosition * 4) * 60000.0 / baseBpm);

            cache.KeyCount = Math.Max(1, determineKeyCount(noteChannels));
            return cache;
        }
    }

    public struct BMSSourceReference
    {
        public Guid BeatmapId { get; set; }
        public string FolderPath { get; set; }
        public string ChartPath { get; set; }

        /// <summary>Path-derived key (legacy name).</summary>
        public string Md5Hash { get; set; }

        public string ContentMd5 { get; set; }
        public string ContentSha256 { get; set; }
    }
}
