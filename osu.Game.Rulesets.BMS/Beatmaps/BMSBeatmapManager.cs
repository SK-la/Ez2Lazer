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
using osu.Game.Rulesets.BMS.Configuration;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Manages BMS library scanning, caching, and loading.
    /// </summary>
    public class BMSBeatmapManager
    {
        private const string cache_filename = "bms_library_cache.json";

        /// <summary>
        /// Supported BMS file extensions.
        /// </summary>
        private static readonly string[] bms_extensions = { ".bms", ".bme", ".bml", ".pms" };

        /// <summary>
        /// The current library cache.
        /// </summary>
        public BMSLibraryCache? LibraryCache { get; private set; }

        /// <summary>
        /// Bindable for the BMS root path.
        /// </summary>
        public Bindable<string> RootPath { get; } = new Bindable<string>(string.Empty);

        /// <summary>
        /// Progress of the current scan operation (0-1).
        /// </summary>
        public BindableDouble ScanProgress { get; } = new BindableDouble(0);

        /// <summary>
        /// Current status message.
        /// </summary>
        public Bindable<string> StatusMessage { get; } = new Bindable<string>(string.Empty);

        /// <summary>
        /// Whether a scan is currently in progress.
        /// </summary>
        public BindableBool IsScanning { get; } = new BindableBool(false);

        private readonly string cacheDirectory;
        private CancellationTokenSource? scanCts;

        public BMSBeatmapManager(string cacheDirectory)
        {
            this.cacheDirectory = cacheDirectory;
            Directory.CreateDirectory(cacheDirectory);
        }

        /// <summary>
        /// Get the cache file path.
        /// </summary>
        private string CacheFilePath => Path.Combine(cacheDirectory, cache_filename);

        /// <summary>
        /// Load the library cache from disk.
        /// </summary>
        public void LoadCache()
        {
            LibraryCache = BMSLibraryCache.Load(CacheFilePath);

            if (LibraryCache != null)
            {
                RootPath.Value = LibraryCache.RootPath;
                StatusMessage.Value = $"已加载 {LibraryCache.Songs.Count} 首歌曲, {LibraryCache.TotalCharts} 张谱面";
            }
        }

        /// <summary>
        /// Save the library cache to disk.
        /// </summary>
        public void SaveCache()
        {
            LibraryCache?.Save(CacheFilePath);
        }

        /// <summary>
        /// Cancel any ongoing scan.
        /// </summary>
        public void CancelScan()
        {
            scanCts?.Cancel();
        }

        /// <summary>
        /// Scan the BMS root path and rebuild the cache.
        /// </summary>
        public async Task ScanLibraryAsync(string rootPath, CancellationToken cancellationToken = default)
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
                if (!Directory.Exists(rootPath))
                {
                    StatusMessage.Value = "错误: 路径不存在";
                    return;
                }

                // Find all BMS files
                var bmsFiles = new List<string>();

                foreach (var ext in bms_extensions)
                {
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
                    RootPath = rootPath,
                    LastScanTime = DateTime.Now,
                };

                int processedFolders = 0;

                foreach (var group in folderGroups)
                {
                    token.ThrowIfCancellationRequested();

                    var folderPath = group.Key;
                    var songCache = await ScanSongFolderAsync(folderPath, group.ToList(), token).ConfigureAwait(false);

                    if (songCache != null && songCache.Charts.Count > 0)
                    {
                        cache.Songs.Add(songCache);
                    }

                    processedFolders++;
                    ScanProgress.Value = (double)processedFolders / folderGroups.Count;
                    StatusMessage.Value = $"正在解析... {processedFolders}/{folderGroups.Count} 文件夹";
                }

                LibraryCache = cache;
                RootPath.Value = rootPath;
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
        /// Scan a single song folder.
        /// </summary>
        private async Task<BMSSongCache?> ScanSongFolderAsync(string folderPath, List<string> bmsFiles, CancellationToken token)
        {
            var songCache = new BMSSongCache
            {
                FolderPath = folderPath,
                LastModified = Directory.GetLastWriteTime(folderPath),
            };

            bool firstChart = true;

            foreach (var bmsFile in bmsFiles)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    var chartCache = await ParseBmsFileForCacheAsync(bmsFile, token).ConfigureAwait(false);

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
        /// Parse a BMS file and extract cache information (metadata only, not full parse).
        /// </summary>
        private Task<BMSChartCache?> ParseBmsFileForCacheAsync(string filePath, CancellationToken token)
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
                    Md5Hash = ComputeMd5Hash(filePath),
                };

                // Parse the BMS file for metadata
                var lines = ReadBmsLines(filePath);
                var keysoundFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var bpmValues = new List<double>();
                bool hasNotes = false;
                bool hasLongNotes = false;
                bool hasScratch = false;
                int noteCount = 0;
                int maxMeasure = 0;

                // For BPM calculation
                double baseBpm = 130;
                var bpmDefs = new Dictionary<string, double>();

                foreach (var line in lines)
                {
                    token.ThrowIfCancellationRequested();

                    if (!line.StartsWith('#')) continue;

                    var upperLine = line.ToUpperInvariant();

                    // Parse header commands
                    if (upperLine.StartsWith("#TITLE "))
                        cache.Title = line.Substring(7).Trim();
                    else if (upperLine.StartsWith("#SUBTITLE "))
                        cache.SubTitle = line.Substring(10).Trim();
                    else if (upperLine.StartsWith("#ARTIST "))
                        cache.Artist = line.Substring(8).Trim();
                    else if (upperLine.StartsWith("#SUBARTIST "))
                        cache.SubArtist = line.Substring(11).Trim();
                    else if (upperLine.StartsWith("#GENRE "))
                        cache.Genre = line.Substring(7).Trim();
                    else if (upperLine.StartsWith("#PLAYLEVEL "))
                    {
                        if (int.TryParse(line.Substring(11).Trim(), out int level))
                            cache.PlayLevel = level;
                    }
                    else if (upperLine.StartsWith("#RANK "))
                    {
                        if (int.TryParse(line.Substring(6).Trim(), out int rank))
                            cache.Rank = rank;
                    }
                    else if (upperLine.StartsWith("#TOTAL "))
                    {
                        if (double.TryParse(line.Substring(7).Trim(), out double total))
                            cache.Total = total;
                    }
                    else if (upperLine.StartsWith("#BPM ") && !upperLine.StartsWith("#BPM0"))
                    {
                        if (double.TryParse(line.Substring(5).Trim(), out double bpm))
                        {
                            baseBpm = bpm;
                            bpmValues.Add(bpm);
                        }
                    }
                    else if (upperLine.StartsWith("#BPM"))
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
                    else if (upperLine.StartsWith("#WAV"))
                    {
                        // #WAVxx filename
                        int spaceIdx = line.IndexOf(' ');
                        if (spaceIdx > 4)
                        {
                            string soundFile = line.Substring(spaceIdx + 1).Trim();
                            if (!string.IsNullOrEmpty(soundFile))
                                keysoundFiles.Add(soundFile);
                        }
                    }
                    else if (upperLine.StartsWith("#LNTYPE ") || upperLine.StartsWith("#LNOBJ "))
                    {
                        hasLongNotes = true;
                    }
                    // Parse channel data for note count and max measure
                    else if (line.Length > 6 && line[6] == ':')
                    {
                        // Format: #MMCCC:DATA
                        // Try to get measure number
                        if (int.TryParse(line.Substring(1, 3), out int measureNum))
                        {
                            if (measureNum > maxMeasure)
                                maxMeasure = measureNum;
                        }

                        string channelStr = line.Substring(4, 2);

                        // Note channels
                        if (IsNoteChannel(channelStr))
                        {
                            hasNotes = true;
                            string data = line.Substring(7);
                            int notesInChannel = CountNotes(data);
                            noteCount += notesInChannel;

                            if (IsScratchChannel(channelStr))
                                hasScratch = true;

                            if (IsLongNoteChannel(channelStr))
                                hasLongNotes = true;
                        }
                    }
                }

                cache.Bpm = baseBpm;
                cache.MinBpm = bpmValues.Count > 0 ? bpmValues.Min() : baseBpm;
                cache.MaxBpm = bpmValues.Count > 0 ? bpmValues.Max() : baseBpm;
                cache.TotalNotes = noteCount;
                cache.HasScratch = hasScratch;
                cache.HasLongNotes = hasLongNotes;
                cache.KeysoundFiles = keysoundFiles.ToList();

                // Calculate duration from max measure and BPM
                // Standard: 4 beats per measure, duration = measures * 4 * 60000 / BPM
                if (baseBpm > 0 && maxMeasure > 0)
                {
                    cache.Duration = (maxMeasure + 1) * 4.0 * 60000.0 / baseBpm;
                }

                // Determine key count based on channels used
                // This is simplified - would need full parse for accuracy
                cache.KeyCount = hasScratch ? 8 : 7; // Default to 7K+1 or 7K

                return cache;
            }, token);
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
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static string? FindImageFile(string folderPath, params string[] patterns)
        {
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp" };

            foreach (var pattern in patterns)
            {
                foreach (var ext in imageExtensions)
                {
                    var files = Directory.GetFiles(folderPath, $"*{pattern}*{ext}", SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                        return Path.GetFileName(files[0]);
                }
            }

            return null;
        }

        /// <summary>
        /// Get a chart cache by MD5 hash.
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
        /// Get all songs.
        /// </summary>
        public IEnumerable<BMSSongCache> GetAllSongs()
        {
            return LibraryCache?.Songs ?? Enumerable.Empty<BMSSongCache>();
        }

        /// <summary>
        /// Get all charts.
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
    }
}
