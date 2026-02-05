// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Logging;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    ///     Manages BMS library scanning, caching, and loading.
    /// </summary>
    public class BMSBeatmapManager
    {
        private static readonly object shared_manager_lock = new object();
        private static BMSBeatmapManager? sharedManager;
        private static string? sharedCacheDirectory;

        /// <summary>
        ///     Bindable for the BMS root path.
        /// </summary>
        public Bindable<string> RootPath { get; } = new Bindable<string>(string.Empty);

        public IReadOnlyList<string> RootPaths => rootPaths;

        /// <summary>
        ///     Progress of the current scan operation (0-1).
        /// </summary>
        public BindableDouble ScanProgress { get; } = new BindableDouble();

        /// <summary>
        ///     Current status message.
        /// </summary>
        public Bindable<string> StatusMessage { get; } = new Bindable<string>(string.Empty);

        /// <summary>
        ///     Whether a scan is currently in progress.
        /// </summary>
        public BindableBool IsScanning { get; } = new BindableBool();

        /// <summary>
        ///     The current library cache.
        /// </summary>
        public BMSLibraryCache? LibraryCache { get; private set; }

        private const string cache_filename = "bms_library_cache.json";

        /// <summary>
        ///     Supported BMS file extensions.
        /// </summary>
        private static readonly string[] bms_extensions = { ".bms", ".bme", ".bml", ".pms" };

        private readonly string cacheDirectory;
        private readonly List<string> rootPaths = new List<string>();

        /// <summary>
        ///     Get the cache file path.
        /// </summary>
        private string cacheFilePath => Path.Combine(cacheDirectory, cache_filename);

        private CancellationTokenSource? scanCts;

        public static BMSBeatmapManager GetShared(string cacheDirectory)
        {
            lock (shared_manager_lock)
            {
                if (sharedManager == null || !string.Equals(sharedCacheDirectory, cacheDirectory, StringComparison.Ordinal))
                {
                    sharedManager = new BMSBeatmapManager(cacheDirectory);
                    sharedManager.LoadCache();
                    sharedCacheDirectory = cacheDirectory;
                }

                return sharedManager;
            }
        }

        public BMSBeatmapManager(string cacheDirectory)
        {
            this.cacheDirectory = cacheDirectory;
            Directory.CreateDirectory(cacheDirectory);
        }

        /// <summary>
        ///     Load the library cache from disk.
        /// </summary>
        public void LoadCache()
        {
            LibraryCache = BMSLibraryCache.Load(cacheFilePath);

            if (LibraryCache != null)
            {
                SetRootPaths(LibraryCache.RootPaths.Count > 0 ? LibraryCache.RootPaths : new[] { LibraryCache.RootPath });
                StatusMessage.Value = $"已加载 {LibraryCache.Songs.Count} 首歌曲, {LibraryCache.TotalCharts} 张谱面";
            }
        }

        public void SetRootPaths(IEnumerable<string> paths)
        {
            rootPaths.Clear();
            rootPaths.AddRange(normaliseRootPaths(paths));
            RootPath.Value = rootPaths.FirstOrDefault() ?? string.Empty;
        }

        /// <summary>
        ///     Save the library cache to disk.
        /// </summary>
        public void SaveCache()
        {
            LibraryCache?.Save(cacheFilePath);
        }

        /// <summary>
        ///     Cancel any ongoing scan.
        /// </summary>
        public void CancelScan()
        {
            scanCts?.Cancel();
        }

        /// <summary>
        ///     Scan the BMS root path and rebuild the cache.
        /// </summary>
        public async Task ScanLibraryAsync(string rootPath, CancellationToken cancellationToken = default)
            => await ScanLibraryAsync(new[] { rootPath }, cancellationToken).ConfigureAwait(false);

        public async Task ScanLibraryAsync(IEnumerable<string> scanPaths, CancellationToken cancellationToken = default)
        {
            if (IsScanning.Value)
            {
                CancelScan();
                // Wait a bit for previous scan to stop
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

                // Find all BMS files
                var bmsFiles = new List<string>();

                foreach (string rootPath in existingPaths)
                {
                    foreach (string ext in bms_extensions)
                        bmsFiles.AddRange(Directory.GetFiles(rootPath, $"*{ext}", SearchOption.AllDirectories));
                }

                if (bmsFiles.Count == 0)
                {
                    StatusMessage.Value = "未找到 BMS 文件";
                    return;
                }

                StatusMessage.Value = $"找到 {bmsFiles.Count} 个 BMS 文件，正在解析...";

                // Group by folder (each folder is a "song")
                var folderGroups = bmsFiles.GroupBy(f => Path.GetDirectoryName(f) ?? string.Empty).ToList();

                var cache = new BMSLibraryCache
                {
                    RootPath = existingPaths.FirstOrDefault() ?? string.Empty,
                    RootPaths = existingPaths,
                    LastScanTime = DateTime.Now
                };

                int processedFolders = 0;

                foreach (var group in folderGroups)
                {
                    token.ThrowIfCancellationRequested();

                    string folderPath = group.Key;
                    var songCache = await scanSongFolderAsync(folderPath, group.ToList(), token).ConfigureAwait(false);

                    if (songCache != null && songCache.Charts.Count > 0) cache.Songs.Add(songCache);

                    processedFolders++;
                    ScanProgress.Value = (double)processedFolders / folderGroups.Count;
                    StatusMessage.Value = $"正在解析... {processedFolders}/{folderGroups.Count} 文件夹";
                }

                LibraryCache = cache;
                SetRootPaths(existingPaths);
                SaveCache();

                StatusMessage.Value = $"扫描完成: {cache.Songs.Count} 首歌曲, {cache.TotalCharts} 张谱面";
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

        /// <summary>
        ///     Get a chart cache by MD5 hash.
        /// </summary>
        public BMSChartCache? GetChartByHash(string md5Hash)
        {
            if (LibraryCache == null) return null;

            foreach (var song in LibraryCache.Songs)
            {
                foreach (var chart in song.Charts)
                {
                    if (chart.Md5Hash.Equals(md5Hash, StringComparison.OrdinalIgnoreCase))
                        return chart;
                }
            }

            return null;
        }

        /// <summary>
        ///     Get all songs.
        /// </summary>
        public IEnumerable<BMSSongCache> GetAllSongs()
        {
            return LibraryCache?.Songs ?? Enumerable.Empty<BMSSongCache>();
        }

        /// <summary>
        ///     Get all charts.
        /// </summary>
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

        private static bool IsNoteChannel(string channel)
        {
            // 1P visible: 11-19, 2P visible: 21-29
            // 1P LN: 51-59, 2P LN: 61-69
            if (channel.Length != 2) return false;

            char first = channel[0];
            char second = channel[1];

            if (first == '1' && second >= '1' && second <= '9') return true;
            if (first == '2' && second >= '1' && second <= '9') return true;
            if (first == '5' && second >= '1' && second <= '9') return true;
            if (first == '6' && second >= '1' && second <= '9') return true;

            return false;
        }

        private static bool IsScratchChannel(string channel)
        {
            return channel == "16" || channel == "26" || channel == "56" || channel == "66";
        }

        private static bool IsLongNoteChannel(string channel)
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

        private static bool IsBackgroundSoundChannel(string channel)
        {
            if (IsNoteChannel(channel))
                return false;

            return channel is not "02" and not "03" and not "04" and not "06" and not "07" and not "08" and not "09";
        }

        private static int CountNotes(string data)
        {
            // Each note is 2 characters, "00" means no note
            int count = 0;

            for (int i = 0; i + 1 < data.Length; i += 2)
            {
                if (data.Substring(i, 2) != "00")
                    count++;
            }

            return count;
        }

        private static (string Key, double Position)? FindFirstObjectKey(string data)
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

        private static string[] ReadBmsLines(string filePath)
        {
            // Try different encodings
            try
            {
                // Try Shift-JIS first (common for Japanese BMS)
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

        private static string ComputeMd5Hash(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static string? FindImageFile(string folderPath, params string[] patterns)
        {
            string[] imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp" };

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

        /// <summary>
        ///     Scan a single song folder.
        /// </summary>
        private async Task<BMSSongCache?> scanSongFolderAsync(string folderPath, List<string> bmsFiles, CancellationToken token)
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
                    var chartCache = await parseBmsFileForCacheAsync(bmsFile, token).ConfigureAwait(false);

                    if (chartCache != null)
                    {
                        songCache.Charts.Add(chartCache);

                        // Use first chart's metadata for song-level info
                        if (firstChart)
                        {
                            songCache.Title = chartCache.Title;
                            songCache.Artist = chartCache.Artist;
                            songCache.Genre = chartCache.Genre;
                            firstChart = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to parse BMS file: {bmsFile} - {ex.Message}", LoggingTarget.Runtime, LogLevel.Debug);
                }
            }

            // Look for banner/stagefile
            songCache.BannerPath = FindImageFile(folderPath, "banner", "bn");
            songCache.StageFilePath = FindImageFile(folderPath, "stagefile", "stage", "bg");

            return songCache;
        }

        /// <summary>
        ///     Parse a BMS file and extract cache information (metadata only, not full parse).
        /// </summary>
        private Task<BMSChartCache?> parseBmsFileForCacheAsync(string filePath, CancellationToken token)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists) return null;

                var cache = new BMSChartCache
                {
                    FileName = fileInfo.Name,
                    FolderPath = fileInfo.DirectoryName ?? string.Empty,
                    FileSize = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    Md5Hash = ComputeMd5Hash(filePath)
                };

                // Parse the BMS file for metadata
                string[] lines = ReadBmsLines(filePath);
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
                int maxMeasure = 0;
                string? previewAudioFile = null;
                string? explicitPreviewFile = null;
                int previewMeasure = int.MaxValue;
                double previewPosition = double.MaxValue;

                // For BPM calculation
                double baseBpm = 130;
                var bpmDefs = new Dictionary<string, double>();

                foreach (string line in lines)
                {
                    token.ThrowIfCancellationRequested();

                    if (!line.StartsWith('#')) continue;

                    string upperLine = line.ToUpperInvariant();

                    // Parse header commands
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
                        // #BPMxx definitions
                        int spaceIdx = line.IndexOf(' ');

                        if (spaceIdx > 4)
                        {
                            string key = line.Substring(4, spaceIdx - 4);

                            if (double.TryParse(line.Substring(spaceIdx + 1).Trim(), out double bpmVal))
                            {
                                bpmDefs[key.ToUpperInvariant()] = bpmVal;
                                bpmValues.Add(bpmVal);
                            }
                        }
                    }
                    else if (upperLine.StartsWith("#WAV", StringComparison.Ordinal))
                    {
                        // #WAVxx filename
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
                    {
                        explicitPreviewFile = line.Substring(9).Trim();
                    }
                    else if (upperLine.StartsWith("#SCROLL", StringComparison.Ordinal))
                    {
                        hasScrollChanges = true;
                    }
                    // Parse channel data for note count and max measure
                    else if (line.Length > 6 && line[6] == ':')
                    {
                        // Format: #MMCCC:DATA
                        // Try to get measure number
                        if (int.TryParse(line.AsSpan(1, 3), out int measureNum))
                        {
                            if (measureNum > maxMeasure)
                                maxMeasure = measureNum;
                        }

                        string channelStr = line.Substring(4, 2);

                        // Note channels
                        if (IsNoteChannel(channelStr))
                        {
                            noteChannels.Add(channelStr);
                            string data = line.Substring(7);
                            int notesInChannel = CountNotes(data);
                            noteCount += notesInChannel;

                            if (IsScratchChannel(channelStr))
                                hasScratch = true;

                            if (IsLongNoteChannel(channelStr))
                                hasLongNotes = true;
                        }
                        else if (IsBackgroundSoundChannel(channelStr))
                        {
                            var firstObject = FindFirstObjectKey(line.Substring(7));

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
                cache.HasScratch = hasScratch;
                cache.HasLongNotes = hasLongNotes;
                cache.HasStopSequence = hasStopSequence;
                cache.HasScrollChanges = hasScrollChanges;
                cache.HasBgaLayer = hasBgaLayer;
                cache.KeysoundFiles = keysoundFiles.ToList();
                cache.AudioFile = previewAudioFile;
                cache.PreviewFile = explicitPreviewFile;

                // Calculate duration from max measure and BPM
                // Standard: 4 beats per measure, duration = measures * 4 * 60000 / BPM
                if (baseBpm > 0 && maxMeasure > 0) cache.Duration = (maxMeasure + 1) * 4.0 * 60000.0 / baseBpm;

                if (baseBpm > 0 && previewMeasure != int.MaxValue)
                    cache.PreviewTime = (int)Math.Max(0, ((previewMeasure * 4) + (previewPosition * 4)) * 60000.0 / baseBpm);

                cache.KeyCount = Math.Max(1, determineKeyCount(noteChannels));

                return cache;
            }, token);
        }
    }
}
