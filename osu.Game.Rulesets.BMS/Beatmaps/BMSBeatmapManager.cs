// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Beatmaps.Persistence;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
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

        public BMSLibraryCache? LibraryCache { get; private set; }

        public long LastScanRevision { get; private set; }

        public long LastSynchronizedScanRevision { get; private set; }

        /// <summary>
        /// Tracks whether Realm still needs a catalog pass. Revision equality alone is insufficient
        /// (both zero on a fresh index) and would skip the first sync, leaving the carousel on stale IDs.
        /// </summary>
        private bool realmSyncRequired = true;

        public bool NeedsRealmSynchronization => realmSyncRequired || LastScanRevision != LastSynchronizedScanRevision;

        private static readonly string[] bms_extensions = { ".bms", ".bme", ".bml", ".pms" };

        private readonly string storageDirectory;
        private readonly BmsLibraryIndexRepository indexRepository;
        private readonly List<string> rootPaths = new List<string>();
        private readonly Dictionary<Guid, BMSSourceReference> beatmapSourceMap = new Dictionary<Guid, BMSSourceReference>();
        private readonly object sourceMapLock = new object();

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
            this.storageDirectory = storageDirectory;
            Directory.CreateDirectory(storageDirectory);
            indexRepository = new BmsLibraryIndexRepository(Path.Combine(storageDirectory, BmsStoragePaths.INDEX_DATABASE_FILE));
        }

        public void LoadCache()
        {
            try
            {
                LibraryCache = indexRepository.LoadLibraryCache();
                LastScanRevision = indexRepository.ScanRevision;
                rebuildSourceMapFromIndex();

                if (LibraryCache.RootPaths.Count > 0)
                    SetRootPaths(LibraryCache.RootPaths);
                else if (!string.IsNullOrEmpty(LibraryCache.RootPath))
                    SetRootPaths(new[] { LibraryCache.RootPath });

                StatusMessage.Value = $"已加载 {LibraryCache.Songs.Count} 首歌曲, {LibraryCache.TotalCharts} 张谱面";

                if (LibraryCache.TotalCharts > 0)
                    realmSyncRequired = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[BMS] Failed to load library index");
                LibraryCache = new BMSLibraryCache();
            }
        }

        public void SetRootPaths(IEnumerable<string> paths)
        {
            rootPaths.Clear();
            rootPaths.AddRange(normaliseRootPaths(paths));
            RootPath.Value = rootPaths.FirstOrDefault() ?? string.Empty;
        }

        public void MarkRealmSynchronized()
        {
            LastSynchronizedScanRevision = LastScanRevision;
            realmSyncRequired = false;
        }

        public void RequireRealmSynchronization() => realmSyncRequired = true;

        public void CancelScan() => scanCts?.Cancel();

        public Task ScanLibraryAsync(string rootPath, CancellationToken cancellationToken = default)
            => ScanLibraryAsync(new[] { rootPath }, cancellationToken);

        public async Task ScanLibraryAsync(IEnumerable<string> scanPaths, CancellationToken cancellationToken = default)
        {
            if (IsScanning.Value)
            {
                CancelScan();
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = scanCts.Token;

            IsScanning.Value = true;
            ScanProgress.Value = 0;
            StatusMessage.Value = "正在扫描文件夹...";

            try
            {
                List<string> configuredPaths = normaliseRootPaths(scanPaths);
                List<string> existingPaths = configuredPaths.Where(Directory.Exists).ToList();

                if (existingPaths.Count == 0)
                {
                    StatusMessage.Value = "错误: 没有可用的路径";
                    return;
                }

                var bmsFiles = enumerateBmsFiles(existingPaths).ToList();

                if (bmsFiles.Count == 0)
                {
                    StatusMessage.Value = "未找到 BMS 文件";
                    return;
                }

                StatusMessage.Value = $"找到 {bmsFiles.Count} 个 BMS 文件，正在解析...";

                var folderGroups = bmsFiles
                                   .GroupBy(f => Path.GetDirectoryName(f) ?? string.Empty)
                                   .ToList();

                var snapshots = indexRepository.GetChartSnapshots();
                var seenChartPaths = new ConcurrentBag<string>();
                int processedFolders = 0;
                int totalFolders = folderGroups.Count;
                object progressLock = new object();

                await Task.Run(() =>
                {
                    Parallel.ForEach(
                        folderGroups,
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 2, 8),
                            CancellationToken = token,
                        },
                        group =>
                        {
                            token.ThrowIfCancellationRequested();

                            string folderPath = group.Key;
                            var songCache = scanSongFolder(folderPath, group.ToList(), snapshots, token);

                            if (songCache == null || songCache.Charts.Count == 0)
                                return;

                            indexRepository.UpsertSong(songCache);

                            foreach (BMSChartCache chart in songCache.Charts)
                            {
                                string chartPath = chart.FullPath;
                                seenChartPaths.Add(chartPath);

                                string pathKey = BmsPathKeys.ComputeChartPathKey(chartPath);
                                chart.Md5Hash = pathKey;
                                Guid beatmapId = createDeterministicGuid($"bms:chart:{chartPath}");

                                indexRepository.UpsertChart(chart, beatmapId, pathKey);

                                lock (sourceMapLock)
                                {
                                    beatmapSourceMap[beatmapId] = new BMSSourceReference
                                    {
                                        BeatmapId = beatmapId,
                                        FolderPath = chart.FolderPath,
                                        ChartPath = chartPath,
                                        Md5Hash = pathKey,
                                    };
                                }
                            }

                            int done = Interlocked.Increment(ref processedFolders);

                            lock (progressLock)
                            {
                                ScanProgress.Value = (double)done / totalFolders;
                                StatusMessage.Value = $"正在解析... {done}/{totalFolders} 文件夹";
                            }
                        });
                }, token).ConfigureAwait(false);

                indexRepository.DeleteChartsNotIn(seenChartPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
                LastScanRevision = indexRepository.MarkScanComplete(existingPaths);
                LibraryCache = indexRepository.LoadLibraryCache();
                SetRootPaths(existingPaths);
                rebuildSourceMapFromIndex();
                realmSyncRequired = true;

                StatusMessage.Value = $"扫描完成: {LibraryCache.Songs.Count} 首歌曲, {LibraryCache.TotalCharts} 张谱面";
            }
            catch (OperationCanceledException)
            {
                StatusMessage.Value = "扫描已取消";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "BMS library scan failed");
                StatusMessage.Value = $"扫描错误: {ex.Message}";
            }
            finally
            {
                IsScanning.Value = false;
                ScanProgress.Value = 1;
            }
        }

        public BMSChartCache? GetChartByHash(string pathKey)
        {
            if (indexRepository.TryGetSourceReferenceByPathKey(pathKey, out BMSSourceReference reference)
                && indexRepository.TryLoadChart(reference.ChartPath, out BMSChartCache chart))
                return chart;

            if (LibraryCache == null)
                return null;

            foreach (var song in LibraryCache.Songs)
            {
                foreach (var cached in song.Charts)
                {
                    if (cached.Md5Hash.Equals(pathKey, StringComparison.OrdinalIgnoreCase))
                        return cached;
                }
            }

            return null;
        }

        public IEnumerable<BMSSongCache> GetAllSongs() => LibraryCache?.Songs ?? Enumerable.Empty<BMSSongCache>();

        public IEnumerable<BMSChartCache> GetAllCharts()
        {
            if (LibraryCache == null)
                yield break;

            foreach (var song in LibraryCache.Songs)
            {
                foreach (var chart in song.Charts)
                    yield return chart;
            }
        }

        public IReadOnlyList<BeatmapSetInfo> BuildVirtualBeatmapCatalog(RulesetInfo bmsRulesetInfo)
        {
            List<BeatmapSetInfo> result = new List<BeatmapSetInfo>();

            if (LibraryCache == null)
                return result;

            foreach (BMSSongCache song in LibraryCache.Songs)
            {
                if (song.Charts.Count == 0)
                    continue;

                var beatmapSet = new BeatmapSetInfo
                {
                    ID = createDeterministicGuid($"bms:set:{song.FolderPath}"),
                    DateAdded = song.LastModified,
                    Hash = song.FolderPath,
                };

                foreach (BMSChartCache chart in song.Charts.OrderBy(c => c.PlayLevel).ThenBy(c => c.FileName, StringComparer.OrdinalIgnoreCase))
                {
                    string chartPath = chart.FullPath;
                    string pathKey = string.IsNullOrEmpty(chart.Md5Hash)
                        ? BmsPathKeys.ComputeChartPathKey(chartPath)
                        : chart.Md5Hash;
                    string realmHash = BmsPathKeys.ComputeRealmFileHash(chartPath);

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
                        ID = createDeterministicGuid($"bms:chart:{chartPath}"),
                        DifficultyName = formatDifficultyName(chart),
                        BPM = chart.Bpm,
                        Length = chart.Duration,
                        Hash = realmHash,
                        MD5Hash = pathKey,
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

                result.Add(beatmapSet);
            }

            return result;
        }

        public bool TryGetSourceReference(Guid beatmapId, out BMSSourceReference sourceReference)
        {
            if (tryGetSourceReferenceCore(beatmapId, out sourceReference))
                return true;

            if (indexRepository.TryGetSourceReference(beatmapId, out sourceReference))
            {
                cacheSourceReference(sourceReference);
                return true;
            }

            sourceReference = default;
            return false;
        }

        public bool TryGetSourceReferenceByHash(string pathKey, out BMSSourceReference sourceReference)
        {
            if (tryGetSourceReferenceByHashCore(pathKey, out sourceReference))
                return true;

            if (indexRepository.TryGetSourceReferenceByPathKey(pathKey, out sourceReference))
            {
                cacheSourceReference(sourceReference);
                return true;
            }

            sourceReference = default;
            return false;
        }

        public Dictionary<Guid, BMSSourceReference> GetCurrentSourceMap()
        {
            lock (sourceMapLock)
                return new Dictionary<Guid, BMSSourceReference>(beatmapSourceMap);
        }

        private void rebuildSourceMapFromIndex()
        {
            var map = new Dictionary<Guid, BMSSourceReference>();

            if (LibraryCache == null)
            {
                replaceSourceMap(map);
                return;
            }

            foreach (var song in LibraryCache.Songs)
            {
                foreach (var chart in song.Charts)
                {
                    string chartPath = chart.FullPath;
                    string pathKey = string.IsNullOrEmpty(chart.Md5Hash)
                        ? BmsPathKeys.ComputeChartPathKey(chartPath)
                        : chart.Md5Hash;
                    Guid beatmapId = createDeterministicGuid($"bms:chart:{chartPath}");

                    map[beatmapId] = new BMSSourceReference
                    {
                        BeatmapId = beatmapId,
                        FolderPath = chart.FolderPath,
                        ChartPath = chartPath,
                        Md5Hash = pathKey,
                    };
                }
            }

            replaceSourceMap(map);
        }

        private void cacheSourceReference(BMSSourceReference reference)
        {
            if (reference.BeatmapId == Guid.Empty)
                return;

            lock (sourceMapLock)
                beatmapSourceMap[reference.BeatmapId] = reference;
        }

        private void replaceSourceMap(Dictionary<Guid, BMSSourceReference> newMap)
        {
            lock (sourceMapLock)
            {
                beatmapSourceMap.Clear();

                foreach ((Guid key, BMSSourceReference value) in newMap)
                    beatmapSourceMap[key] = value;
            }
        }

        private bool tryGetSourceReferenceCore(Guid beatmapId, out BMSSourceReference sourceReference)
        {
            lock (sourceMapLock)
                return beatmapSourceMap.TryGetValue(beatmapId, out sourceReference);
        }

        private bool tryGetSourceReferenceByHashCore(string pathKey, out BMSSourceReference sourceReference)
        {
            lock (sourceMapLock)
            {
                foreach (BMSSourceReference reference in beatmapSourceMap.Values)
                {
                    if (string.Equals(reference.Md5Hash, pathKey, StringComparison.OrdinalIgnoreCase))
                    {
                        sourceReference = reference;
                        return true;
                    }
                }
            }

            sourceReference = default;
            return false;
        }

        private static IEnumerable<string> enumerateBmsFiles(IEnumerable<string> rootPaths)
        {
            var extensions = new HashSet<string>(bms_extensions, StringComparer.OrdinalIgnoreCase);

            foreach (string rootPath in rootPaths)
            {
                IEnumerable<string> files;

                try
                {
                    files = Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[BMS] Failed to enumerate '{rootPath}': {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                    continue;
                }

                foreach (string file in files)
                {
                    if (extensions.Contains(Path.GetExtension(file)))
                        yield return file;
                }
            }
        }

        private BMSSongCache? scanSongFolder(
            string folderPath,
            List<string> bmsFiles,
            Dictionary<string, BmsLibraryIndexRepository.ChartFileSnapshot> snapshots,
            CancellationToken token)
        {
            var songCache = new BMSSongCache
            {
                FolderPath = folderPath,
                LastModified = Directory.GetLastWriteTime(folderPath)
            };

            bool firstChart = true;

            foreach (string bmsFile in bmsFiles)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    var chartCache = parseOrLoadChart(bmsFile, snapshots, token);

                    if (chartCache == null)
                        continue;

                    songCache.Charts.Add(chartCache);

                    if (firstChart)
                    {
                        songCache.Title = chartCache.Title;
                        songCache.Artist = chartCache.Artist;
                        songCache.Genre = chartCache.Genre;
                        firstChart = false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to parse BMS file: {bmsFile} - {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                }
            }

            songCache.BannerPath = findImageFile(folderPath, "banner", "bn");
            songCache.StageFilePath = findImageFile(folderPath, "stagefile", "stage", "bg");

            return songCache;
        }

        private BMSChartCache? parseOrLoadChart(string filePath, Dictionary<string, BmsLibraryIndexRepository.ChartFileSnapshot> snapshots, CancellationToken token)
        {
            var fileInfo = new FileInfo(filePath);

            if (!fileInfo.Exists)
                return null;

            long ticks = fileInfo.LastWriteTimeUtc.Ticks;

            if (snapshots.TryGetValue(filePath, out var snapshot)
                && snapshot.FileSize == fileInfo.Length
                && snapshot.LastModifiedTicks == ticks
                && indexRepository.TryLoadChart(filePath, out BMSChartCache? cached))
                return cached;

            return parseBmsFileForCache(filePath, token);
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

        private static float mapRankToOD(int bmsRank)
            => bmsRank switch
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

        private static Guid createDeterministicGuid(string input)
        {
            byte[] bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
            return new Guid(bytes);
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

        private static bool isScratchChannel(string channel)
            => channel == "16" || channel == "26" || channel == "56" || channel == "66";

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
                string trimmed = path?.Trim() ?? string.Empty;

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

                return (key, ((double)i / 2) / objectCount);
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
                cache.PreviewTime = (int)Math.Max(0, ((previewMeasure * 4) + (previewPosition * 4)) * 60000.0 / baseBpm);

            cache.KeyCount = Math.Max(1, determineKeyCount(noteChannels));
            return cache;
        }
    }

    public struct BMSSourceReference
    {
        public Guid BeatmapId { get; set; }
        public string FolderPath { get; set; }
        public string ChartPath { get; set; }
        public string Md5Hash { get; set; }
    }
}
