// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Rulesets;
using Realms;

namespace osu.Game.Beatmaps.ExternalLibraries
{
    /// <summary>
    /// Aligns Realm external sets with a scanned catalog: add shells, update metadata/files in place,
    /// remove missing sets. Never clears analysis fields (<see cref="BeatmapInfo.StarRating"/> etc.).
    /// </summary>
    public sealed class ExternalBeatmapLibraryRealmAligner
    {
        private readonly RealmAccess realm;
        private readonly Storage storage;

        public ExternalBeatmapLibraryRealmAligner(RealmAccess realm, Storage storage)
        {
            this.realm = realm;
            this.storage = storage;
        }

        public ExternalBeatmapLibraryAlignResult Align(IEnumerable<ExternalBeatmapSetImportModel> sets, string rulesetShortName)
        {
            var setList = sets.Where(s => string.Equals(s.Ruleset.ShortName, rulesetShortName, StringComparison.OrdinalIgnoreCase)).ToList();
            var realmFileStore = new RealmFileStore(realm, storage);
            var result = new ExternalBeatmapLibraryAlignResult();

            realm.Write(r =>
            {
                RulesetInfo? managedRuleset = r.All<RulesetInfo>().FirstOrDefault(info => info.ShortName == rulesetShortName && info.Available);

                if (managedRuleset == null)
                {
                    Logger.Log($"Skipping external library align: ruleset '{rulesetShortName}' not available.", LoggingTarget.Database);
                    return;
                }

                int externalKind = (int)BeatmapSetHostingKind.External;

                var existingExternal = r.All<BeatmapSetInfo>()
                                        .Where(s => !s.DeletePending && s.HostingKindInt == externalKind)
                                        .AsEnumerable()
                                        .Where(s => s.Beatmaps.Any(b => b.Ruleset.ShortName == rulesetShortName))
                                        .ToDictionary(s => s.ID);

                HashSet<Guid> targetIds = setList.Select(s => s.SetId).ToHashSet();

                foreach (var (id, oldSet) in existingExternal)
                {
                    if (targetIds.Contains(id))
                        continue;

                    removeSet(r, oldSet);
                    result.RemovedSets++;
                }

                foreach (ExternalBeatmapSetImportModel import in setList)
                {
                    existingExternal.TryGetValue(import.SetId, out BeatmapSetInfo? existing);

                    if (existing != null && catalogMatches(existing, import))
                    {
                        result.UnchangedSets++;
                        continue;
                    }

                    bool isNew = existing == null;
                    applySetDelta(realmFileStore, r, existing, import, managedRuleset);

                    if (isNew)
                        result.AddedSets++;
                    else
                        result.UpdatedSets++;
                }
            });

            Logger.Log(
                $"[ExternalLibrary] Align {rulesetShortName}: +{result.AddedSets} ~{result.UpdatedSets} -{result.RemovedSets} ={result.UnchangedSets}",
                LoggingTarget.Database);

            return result;
        }

        /// <summary>
        /// In-memory delta used by unit tests (no Realm remove/add).
        /// </summary>
        public static BeatmapSetInfo ApplySetDeltaForTesting(
            BeatmapSetInfo? existing,
            ExternalBeatmapSetImportModel import,
            RulesetInfo managedRuleset)
            => applySetDelta(null, null, existing, import, managedRuleset);

        public static bool CatalogMatchesForTesting(BeatmapSetInfo existing, ExternalBeatmapSetImportModel import)
            => catalogMatches(existing, import);

        private static BeatmapSetInfo applySetDelta(
            RealmFileStore? realmFileStore,
            Realm? realm,
            BeatmapSetInfo? existing,
            ExternalBeatmapSetImportModel import,
            RulesetInfo managedRuleset)
        {
            bool isNewSet = existing == null;
            BeatmapSetInfo destinationSet = existing ?? new BeatmapSetInfo { ID = import.SetId };

            if (isNewSet)
                destinationSet.DateAdded = import.DateAdded;

            destinationSet.Hash = import.SetHash;
            destinationSet.ExternalContentRoot = Path.GetFullPath(import.ExternalContentRoot);
            destinationSet.HostingKind = BeatmapSetHostingKind.External;
            destinationSet.Status = BeatmapOnlineStatus.LocallyModified;

            syncFiles(realmFileStore, realm, destinationSet, import);

            var targetByPath = import.Beatmaps
                                     .GroupBy(b => normaliseRelative(b.ChartRelativePath), StringComparer.OrdinalIgnoreCase)
                                     .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var existingByPath = destinationSet.Beatmaps
                                               .Where(b => !string.IsNullOrEmpty(b.Path))
                                               .GroupBy(b => normaliseRelative(b.Path!), StringComparer.OrdinalIgnoreCase)
                                               .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach ((string relativePath, ExternalBeatmapDifficultyImportModel difficulty) in targetByPath)
            {
                if (!existingByPath.TryGetValue(relativePath, out BeatmapInfo? destinationBeatmap))
                {
                    destinationBeatmap = new BeatmapInfo(managedRuleset, cloneDifficulty(difficulty.Difficulty), cloneMetadata(difficulty.Metadata))
                    {
                        BeatmapSet = destinationSet,
                    };
                    destinationSet.Beatmaps.Add(destinationBeatmap);
                }

                applyDifficultyShell(destinationBeatmap, difficulty, managedRuleset);
                destinationBeatmap.BeatmapSet = destinationSet;
            }

            foreach (BeatmapInfo obsolete in destinationSet.Beatmaps
                                                           .Where(b => string.IsNullOrEmpty(b.Path)
                                                                       || !targetByPath.ContainsKey(normaliseRelative(b.Path!)))
                                                           .ToList())
            {
                if (realm != null)
                {
                    realm.Remove(obsolete.Metadata);
                    realm.Remove(obsolete);
                }
                else
                    destinationSet.Beatmaps.Remove(obsolete);
            }

            if (isNewSet && realm != null)
                realm.Add(destinationSet, update: true);

            return destinationSet;
        }

        private static void syncFiles(RealmFileStore? realmFileStore, Realm? realm, BeatmapSetInfo destinationSet, ExternalBeatmapSetImportModel import)
        {
            var desired = import.Files
                                .GroupBy(f => normaliseRelative(f.RelativeFilename), StringComparer.OrdinalIgnoreCase)
                                .ToDictionary(g => g.Key, g => g.First().Sha256Hash, StringComparer.OrdinalIgnoreCase);

            foreach (var difficulty in import.Beatmaps)
            {
                string relative = normaliseRelative(difficulty.ChartRelativePath);

                if (!desired.ContainsKey(relative))
                    desired[relative] = difficulty.Sha256Hash;
            }

            foreach (RealmNamedFileUsage stale in destinationSet.Files
                                                               .Where(f => !desired.ContainsKey(normaliseRelative(f.Filename)))
                                                               .ToList())
                destinationSet.Files.Remove(stale);

            foreach ((string relative, string sha) in desired)
            {
                RealmNamedFileUsage? existingUsage = destinationSet.GetFile(relative);

                if (existingUsage != null)
                {
                    if (string.Equals(existingUsage.File.Hash, sha, StringComparison.OrdinalIgnoreCase))
                        continue;

                    destinationSet.Files.Remove(existingUsage);
                }

                if (realmFileStore != null && realm != null)
                {
                    RealmFile file = realmFileStore.RegisterExternalHash(sha, realm);
                    destinationSet.Files.Add(new RealmNamedFileUsage(file, relative));
                }
                else
                    destinationSet.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = sha }, relative));
            }
        }

        private static void applyDifficultyShell(BeatmapInfo beatmap, ExternalBeatmapDifficultyImportModel difficulty, RulesetInfo managedRuleset)
        {
            beatmap.Ruleset = managedRuleset;
            beatmap.DifficultyName = difficulty.DifficultyName;
            beatmap.MD5Hash = difficulty.Md5Hash;
            beatmap.Hash = difficulty.Sha256Hash;
            beatmap.Length = difficulty.Length;
            beatmap.BPM = difficulty.BPM;
            beatmap.Status = BeatmapOnlineStatus.LocallyModified;

            beatmap.Difficulty.CircleSize = difficulty.Difficulty.CircleSize;
            beatmap.Difficulty.OverallDifficulty = difficulty.Difficulty.OverallDifficulty;
            beatmap.Difficulty.DrainRate = difficulty.Difficulty.DrainRate;
            beatmap.Difficulty.ApproachRate = difficulty.Difficulty.ApproachRate;

            beatmap.Metadata.Title = difficulty.Metadata.Title;
            beatmap.Metadata.TitleUnicode = difficulty.Metadata.TitleUnicode;
            beatmap.Metadata.Artist = difficulty.Metadata.Artist;
            beatmap.Metadata.ArtistUnicode = difficulty.Metadata.ArtistUnicode;
            beatmap.Metadata.Source = difficulty.Metadata.Source;
            beatmap.Metadata.Tags = difficulty.Metadata.Tags;
            beatmap.Metadata.AudioFile = difficulty.Metadata.AudioFile;
            beatmap.Metadata.BackgroundFile = difficulty.Metadata.BackgroundFile;
            beatmap.Metadata.PreviewTime = difficulty.Metadata.PreviewTime;
            // Intentionally never touch StarRating / XxyStarRating / PerformancePoints.
        }

        private static bool catalogMatches(BeatmapSetInfo existing, ExternalBeatmapSetImportModel import)
        {
            if (existing.Hash != import.SetHash)
                return false;

            if (!string.Equals(Path.GetFullPath(existing.ExternalContentRoot), Path.GetFullPath(import.ExternalContentRoot), StringComparison.OrdinalIgnoreCase))
                return false;

            if (existing.Beatmaps.Count != import.Beatmaps.Count)
                return false;

            var existingByPath = existing.Beatmaps
                                         .Where(b => !string.IsNullOrEmpty(b.Path))
                                         .ToDictionary(b => normaliseRelative(b.Path!), StringComparer.OrdinalIgnoreCase);

            foreach (ExternalBeatmapDifficultyImportModel difficulty in import.Beatmaps)
            {
                string relative = normaliseRelative(difficulty.ChartRelativePath);

                if (!existingByPath.TryGetValue(relative, out BeatmapInfo? beatmap))
                    return false;

                if (!string.Equals(beatmap.Hash, difficulty.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!string.Equals(beatmap.MD5Hash, difficulty.Md5Hash, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!string.Equals(beatmap.DifficultyName, difficulty.DifficultyName, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static void removeSet(Realm realm, BeatmapSetInfo set)
        {
            foreach (BeatmapInfo beatmap in set.Beatmaps.ToList())
            {
                realm.Remove(beatmap.Metadata);
                realm.Remove(beatmap);
            }

            realm.Remove(set);
        }

        private static string normaliseRelative(string path) => path.Replace('\\', '/');

        private static BeatmapDifficulty cloneDifficulty(BeatmapDifficulty source) => new BeatmapDifficulty
        {
            CircleSize = source.CircleSize,
            OverallDifficulty = source.OverallDifficulty,
            DrainRate = source.DrainRate,
            ApproachRate = source.ApproachRate,
            SliderMultiplier = source.SliderMultiplier,
            SliderTickRate = source.SliderTickRate,
        };

        private static BeatmapMetadata cloneMetadata(BeatmapMetadata source) => new BeatmapMetadata
        {
            Title = source.Title,
            TitleUnicode = source.TitleUnicode,
            Artist = source.Artist,
            ArtistUnicode = source.ArtistUnicode,
            Source = source.Source,
            Tags = source.Tags,
            AudioFile = source.AudioFile,
            BackgroundFile = source.BackgroundFile,
            PreviewTime = source.PreviewTime,
        };
    }

    public sealed class ExternalBeatmapLibraryAlignResult
    {
        public int AddedSets { get; set; }
        public int UpdatedSets { get; set; }
        public int RemovedSets { get; set; }
        public int UnchangedSets { get; set; }
    }
}
