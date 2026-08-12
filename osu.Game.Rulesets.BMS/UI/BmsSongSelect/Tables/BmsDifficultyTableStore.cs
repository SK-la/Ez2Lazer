// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Tables
{
    public sealed class BmsDifficultyTableStore
    {
        private static readonly JsonSerializerOptions json_options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private readonly string tablesDirectory;
        private IReadOnlyList<BmsDifficultyTable> cached = Array.Empty<BmsDifficultyTable>();

        public BmsDifficultyTableStore(Storage storage)
        {
            BmsStoragePaths.EnsureInitialized(storage);
            tablesDirectory = BmsStoragePaths.GetTablesDirectoryPath(storage);
            Directory.CreateDirectory(tablesDirectory);
        }

        public string TablesDirectory => tablesDirectory;

        public IReadOnlyList<BmsDifficultyTable> GetTables(bool forceReload = false)
        {
            if (!forceReload && cached.Count > 0)
                return cached;

            var tables = new List<BmsDifficultyTable>();

            foreach (string path in Directory.EnumerateFiles(tablesDirectory)
                         .Where(p => p.EndsWith(".bmt", StringComparison.OrdinalIgnoreCase)
                                     || p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                // Skip Qwilight-style header companions named "#foo.json"
                if (Path.GetFileName(path).StartsWith('#'))
                    continue;

                var table = BmsDifficultyTableParser.TryLoadFile(path);

                if (table != null)
                    tables.Add(table);
            }

            cached = tables;
            return cached;
        }

        public void Invalidate() => cached = Array.Empty<BmsDifficultyTable>();

        public BmsDifficultyTable? ImportLocalFile(string sourcePath)
        {
            if (!File.Exists(sourcePath))
                return null;

            string ext = Path.GetExtension(sourcePath);
            string destName = Path.GetFileName(sourcePath);

            if (string.IsNullOrEmpty(destName))
                return null;

            string dest = Path.Combine(tablesDirectory, destName);

            if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                File.Copy(sourcePath, dest, overwrite: true);

            Invalidate();
            return BmsDifficultyTableParser.TryLoadFile(dest);
        }

        public async Task<BmsDifficultyTable?> ImportFromUrlAsync(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            url = url.Trim();

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                string headerJson = await http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);

                // Direct TableData JSON (already expanded)
                if (headerJson.Contains("\"folder\"", StringComparison.OrdinalIgnoreCase)
                    && headerJson.Contains("\"name\"", StringComparison.OrdinalIgnoreCase)
                    && !headerJson.Contains("\"data_url\"", StringComparison.OrdinalIgnoreCase)
                    && !headerJson.Contains("\"dataUrl\"", StringComparison.OrdinalIgnoreCase))
                {
                    var direct = BmsDifficultyTableParser.ParseTableDataJson(headerJson, url);

                    if (direct == null)
                        return null;

                    string directPath = Path.Combine(tablesDirectory, fileNameForUrl(url) + ".bmt");
                    BmsDifficultyTableParser.WriteBmt(directPath, headerJson);
                    Invalidate();
                    return BmsDifficultyTableParser.TryLoadFile(directPath);
                }

                var header = JsonSerializer.Deserialize<DifficultyTableHeaderDto>(headerJson, json_options);

                if (header == null || string.IsNullOrWhiteSpace(header.Name))
                {
                    Logger.Log($"[BMS] Difficulty table header missing name: {url}", LoggingTarget.Network, LogLevel.Important);
                    return null;
                }

                string dataUrl = header.ResolveDataUrl();

                if (string.IsNullOrWhiteSpace(dataUrl))
                {
                    // Header URL itself may point at body when ends with .json body list
                    if (url.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        var elementsOnly = JsonSerializer.Deserialize<List<DifficultyTableElementDto>>(headerJson, json_options);

                        if (elementsOnly is { Count: > 0 })
                        {
                            var levels = elementsOnly
                                .Select(e => e.Level ?? string.Empty)
                                .Where(l => !string.IsNullOrEmpty(l))
                                .Distinct(StringComparer.Ordinal)
                                .ToList();
                            string built = BmsDifficultyTableParser.BuildTableDataJson(
                                header.Name ?? "Table",
                                url,
                                header.Symbol ?? string.Empty,
                                elementsOnly,
                                levels);
                            string path = Path.Combine(tablesDirectory, fileNameForUrl(url) + ".bmt");
                            BmsDifficultyTableParser.WriteBmt(path, built);
                            Invalidate();
                            return BmsDifficultyTableParser.TryLoadFile(path);
                        }
                    }

                    Logger.Log($"[BMS] Difficulty table header missing data_url: {url}", LoggingTarget.Network, LogLevel.Important);
                    return null;
                }

                if (!Uri.TryCreate(dataUrl, UriKind.Absolute, out _))
                {
                    if (Uri.TryCreate(url, UriKind.Absolute, out var baseUri))
                        dataUrl = new Uri(baseUri, dataUrl).ToString();
                }

                string bodyJson = await http.GetStringAsync(dataUrl, cancellationToken).ConfigureAwait(false);
                var elements = JsonSerializer.Deserialize<List<DifficultyTableElementDto>>(bodyJson, json_options) ?? new List<DifficultyTableElementDto>();
                var levelOrder = header.ResolveLevelOrder().ToList();

                if (levelOrder.Count == 0)
                {
                    levelOrder = elements
                        .Select(e => e.Level ?? string.Empty)
                        .Where(l => !string.IsNullOrEmpty(l))
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                }

                string tableJson = BmsDifficultyTableParser.BuildTableDataJson(
                    header.Name,
                    url,
                    header.Symbol ?? string.Empty,
                    elements,
                    levelOrder);

                string destPath = Path.Combine(tablesDirectory, fileNameForUrl(url) + ".bmt");
                BmsDifficultyTableParser.WriteBmt(destPath, tableJson);
                Invalidate();
                return BmsDifficultyTableParser.TryLoadFile(destPath);
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] Difficulty table URL import failed ({url}): {ex.Message}", LoggingTarget.Network, LogLevel.Important);
                return null;
            }
        }

        private static string fileNameForUrl(string url)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
