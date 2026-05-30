// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets;

namespace osu.Game.Beatmaps.ExternalLibraries
{
    /// <summary>
    /// Scans osu!-style folder layouts (<c>Songs/set/*.osu</c> or a single set folder) for external library indexing.
    /// </summary>
    public static class OsuFolderExternalLibraryScanner
    {
        public static IEnumerable<ExternalBeatmapSetImportModel> Scan(string configuredPath, IReadOnlyDictionary<string, RulesetInfo> rulesetsByShortName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(configuredPath) || !Directory.Exists(configuredPath))
                yield break;

            string songsRoot = resolveSongsRoot(configuredPath);

            if (!Directory.Exists(songsRoot))
                yield break;

            IEnumerable<string> setFolders = Directory.GetDirectories(songsRoot);

            // If the path itself is a single beatmap folder, scan only that folder.
            if (Directory.GetFiles(songsRoot, "*.osu", SearchOption.TopDirectoryOnly).Any())
                setFolders = new[] { songsRoot };

            foreach (string setFolder in setFolders)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var model in scanSetFolder(setFolder, rulesetsByShortName, cancellationToken))
                    yield return model;
            }
        }

        private static string resolveSongsRoot(string configuredPath)
        {
            string fullPath = Path.GetFullPath(configuredPath);

            if (Directory.GetFiles(fullPath, "*.osu", SearchOption.TopDirectoryOnly).Any())
                return fullPath;

            string songsSub = Path.Combine(fullPath, "Songs");

            if (Directory.Exists(songsSub))
                return songsSub;

            return fullPath;
        }

        private static IEnumerable<ExternalBeatmapSetImportModel> scanSetFolder(string setFolder, IReadOnlyDictionary<string, RulesetInfo> rulesetsByShortName, CancellationToken cancellationToken)
        {
            string contentRoot = Path.GetFullPath(setFolder);
            var chartPaths = Directory.GetFiles(contentRoot, "*.osu", SearchOption.TopDirectoryOnly);

            if (chartPaths.Length == 0)
                yield break;

            var files = new List<ExternalBeatmapFileEntry>();

            foreach (string filePath in Directory.EnumerateFiles(contentRoot, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relative = Path.GetRelativePath(contentRoot, filePath).Replace('\\', '/');

                if (string.IsNullOrEmpty(relative) || relative.StartsWith("..", StringComparison.Ordinal))
                    continue;

                try
                {
                    using var stream = File.OpenRead(filePath);
                    string sha2 = stream.ComputeSHA2Hash();
                    files.Add(new ExternalBeatmapFileEntry(relative, sha2));
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to hash external file {filePath}: {e.Message}", LoggingTarget.Database);
                }
            }

            var beatmapsByRuleset = new Dictionary<string, List<ExternalBeatmapDifficultyImportModel>>();
            var dateAddedByRuleset = new Dictionary<string, DateTimeOffset>();

            foreach (string chartPath in chartPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relativeChart = Path.GetFileName(chartPath);

                try
                {
                    using var stream = File.OpenRead(chartPath);
                    string md5 = stream.ComputeMD5Hash();
                    stream.Seek(0, SeekOrigin.Begin);
                    string sha2 = stream.ComputeSHA2Hash();
                    stream.Seek(0, SeekOrigin.Begin);

                    using var reader = new LineBufferedReader(stream);
                    var decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
                    string rulesetShortName = decoded.BeatmapInfo.Ruleset.ShortName;

                    if (!rulesetsByShortName.TryGetValue(rulesetShortName, out var targetRuleset))
                        continue;

                    if (!beatmapsByRuleset.TryGetValue(rulesetShortName, out var beatmaps))
                    {
                        beatmaps = new List<ExternalBeatmapDifficultyImportModel>();
                        beatmapsByRuleset[rulesetShortName] = beatmaps;
                        dateAddedByRuleset[rulesetShortName] = DateTimeOffset.UtcNow;
                    }

                    beatmaps.Add(new ExternalBeatmapDifficultyImportModel
                    {
                        ChartRelativePath = relativeChart,
                        Md5Hash = md5,
                        Sha256Hash = sha2,
                        Difficulty = decoded.BeatmapInfo.Difficulty,
                        Metadata = decoded.Metadata,
                        Ruleset = targetRuleset,
                        DifficultyName = decoded.BeatmapInfo.DifficultyName,
                        Length = decoded.BeatmapInfo.Length,
                        BPM = decoded.BeatmapInfo.BPM,
                    });

                    var writeTime = File.GetLastWriteTimeUtc(chartPath);

                    if (writeTime < dateAddedByRuleset[rulesetShortName].UtcDateTime)
                        dateAddedByRuleset[rulesetShortName] = writeTime;
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to decode external chart {chartPath}: {e.Message}", LoggingTarget.Database);
                }
            }

            foreach (var (rulesetShortName, beatmaps) in beatmapsByRuleset)
            {
                if (beatmaps.Count == 0)
                    continue;

                yield return new ExternalBeatmapSetImportModel
                {
                    SetId = deterministicGuidForFolder(contentRoot, rulesetShortName),
                    SetHash = ExternalBeatmapPathEncoding.Encode(contentRoot),
                    ExternalContentRoot = contentRoot,
                    Ruleset = rulesetsByShortName[rulesetShortName],
                    DateAdded = dateAddedByRuleset[rulesetShortName],
                    Files = files,
                    Beatmaps = beatmaps,
                };
            }
        }

        private static Guid deterministicGuidForFolder(string folder, string rulesetShortName)
        {
            string key = $"{rulesetShortName}:{Path.GetFullPath(folder).ToLowerInvariant()}";
            byte[] hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(key));
            return new Guid(hash);
        }
    }
}
