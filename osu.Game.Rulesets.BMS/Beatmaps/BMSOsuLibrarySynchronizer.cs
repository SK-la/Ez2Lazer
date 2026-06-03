// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ExternalLibraries;
using osu.Game.Database;
using osu.Game.Models;
using Realms;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    public static class BMSOsuLibrarySynchronizer
    {
        public static void Synchronize(BMSBeatmapManager manager, Storage storage, RealmAccess realm, RulesetInfo bmsRulesetInfo)
        {
            if (manager.LibraryCache == null)
                return;

            if (!manager.NeedsRealmSynchronization)
                return;

            IReadOnlyList<BeatmapSetInfo> virtualSets = manager.BuildVirtualBeatmapCatalog(bmsRulesetInfo);

            if (virtualSets.Count == 0)
            {
                Logger.Log("[BMS] Library index has no charts; skipping Realm sync to avoid clearing the carousel.", LoggingTarget.Database);
                return;
            }

            Dictionary<Guid, BMSSourceReference> sourceMap = manager.GetCurrentSourceMap();

            var realmFileStore = new RealmFileStore(realm, storage);
            int importedSets = 0;
            int importedBeatmaps = 0;
            int removedSets = 0;
            int skippedSets = 0;

            realm.Write(r =>
            {
                RulesetInfo? managedRuleset = r.All<RulesetInfo>().FirstOrDefault(info => info.ShortName == bmsRulesetInfo.ShortName);

                if (managedRuleset == null)
                    throw new InvalidOperationException("BMS ruleset is not available in realm.");

                // Realm cannot translate HostingKind enum or custom helpers; query persisted HostingKindInt + hash prefixes only.
                const int external_hosting_kind = (int)BeatmapSetHostingKind.External;

                List<BeatmapSetInfo> candidateSets = r.All<BeatmapSetInfo>()
                                                      .Where(set => set.HostingKindInt == external_hosting_kind
                                                                    || set.Hash.StartsWith(BMSExternalPath.LEGACY_HASH_PREFIX)
                                                                    || set.Hash.StartsWith(ExternalBeatmapPathEncoding.HASH_PREFIX))
                                                      .ToList();

                Dictionary<Guid, BeatmapSetInfo> existingSets = candidateSets
                                                                .Where(set => isManagedExternalBmsSet(set, bmsRulesetInfo.ShortName))
                                                                .ToDictionary(set => set.ID);
                HashSet<Guid> targetSetIds = virtualSets.Select(set => set.ID).ToHashSet();

                foreach ((Guid setId, BeatmapSetInfo oldSet) in existingSets)
                {
                    if (!targetSetIds.Contains(setId))
                    {
                        removeSet(r, oldSet);
                        removedSets++;
                    }
                }

                foreach (BeatmapSetInfo virtualSet in virtualSets)
                {
                    if (existingSets.TryGetValue(virtualSet.ID, out BeatmapSetInfo? existingSet)
                        && setMatches(existingSet, virtualSet, sourceMap))
                    {
                        Logger.Log($"[BMS] Skipping unchanged external set {virtualSet.ID} (setMatches).", LoggingTarget.Database, LogLevel.Debug);
                        skippedSets++;
                        continue;
                    }

                    Dictionary<Guid, PersistedSongSelectBaseline>? preservedBaseline = null;

                    if (existingSet != null)
                    {
                        preservedBaseline = capturePersistedSongSelectBaseline(existingSet);
                        removeSet(r, existingSet);
                        removedSets++;
                    }

                    string contentRoot = virtualSet.Hash;

                    var newSet = new BeatmapSetInfo
                    {
                        ID = virtualSet.ID,
                        DateAdded = virtualSet.DateAdded,
                        Hash = ExternalBeatmapPathEncoding.Encode(contentRoot),
                        ExternalContentRoot = Path.GetFullPath(contentRoot),
                        HostingKind = BeatmapSetHostingKind.External,
                        Status = BeatmapOnlineStatus.LocallyModified,
                    };

                    foreach (BeatmapInfo virtualBeatmap in virtualSet.Beatmaps)
                    {
                        if (!sourceMap.TryGetValue(virtualBeatmap.ID, out BMSSourceReference sourceRef))
                            continue;

                        if (!File.Exists(sourceRef.ChartPath))
                            continue;

                        string relativeChartPath = registerExternalChartFile(realmFileStore, r, newSet, sourceRef.ChartPath, contentRoot);

                        if (string.IsNullOrEmpty(relativeChartPath))
                            continue;

                        RealmNamedFileUsage? chartFileUsage = newSet.GetFile(relativeChartPath);

                        if (chartFileUsage is null)
                            continue;

                        var beatmap = new BeatmapInfo(managedRuleset, virtualBeatmap.Difficulty.Clone(), virtualBeatmap.Metadata.DeepClone())
                        {
                            ID = virtualBeatmap.ID,
                            DifficultyName = virtualBeatmap.DifficultyName,
                            Hash = chartFileUsage.File.Hash,
                            MD5Hash = virtualBeatmap.MD5Hash,
                            Status = BeatmapOnlineStatus.LocallyModified,
                            BeatmapSet = newSet,
                        };

                        applyVirtualBeatmapToRealm(beatmap, virtualBeatmap);
                        applyPersistedSongSelectBaseline(beatmap, preservedBaseline);

                        newSet.Beatmaps.Add(beatmap);
                        importedBeatmaps++;
                    }

                    if (newSet.Beatmaps.Count > 0)
                    {
                        r.Add(newSet, update: true);
                        importedSets++;
                    }
                }
            });

            manager.MarkRealmSynchronized();

            Logger.Log($"[BMS] External library sync finished: imported {importedSets} sets/{importedBeatmaps} beatmaps, removed {removedSets} sets, skipped {skippedSets} unchanged sets.");
        }

        /// <summary>
        /// Song-select baseline fields written by <see cref="UI.BmsSongSelect.Analytics.BmsAnalyticsRealmWriteback"/>.
        /// </summary>
        public readonly record struct PersistedSongSelectBaseline(double StarRating, double XxyStarRating, double PerformancePoints);

        /// <summary>
        /// Exposed for unit tests validating Realm re-import retains song-select baseline.
        /// </summary>
        public static IReadOnlyDictionary<Guid, PersistedSongSelectBaseline> CapturePersistedSongSelectBaselineForTesting(BeatmapSetInfo existingSet)
            => capturePersistedSongSelectBaseline(existingSet);

        /// <summary>
        /// Exposed for unit tests validating Realm re-import retains song-select baseline.
        /// </summary>
        public static void ApplyPersistedSongSelectBaselineForTesting(BeatmapInfo beatmap, IReadOnlyDictionary<Guid, PersistedSongSelectBaseline>? preservedBaseline)
            => applyPersistedSongSelectBaseline(beatmap, preservedBaseline);

        private static Dictionary<Guid, PersistedSongSelectBaseline> capturePersistedSongSelectBaseline(BeatmapSetInfo existingSet)
        {
            var result = new Dictionary<Guid, PersistedSongSelectBaseline>();

            foreach (var beatmap in existingSet.Beatmaps)
            {
                result[beatmap.ID] = new PersistedSongSelectBaseline(beatmap.StarRating, beatmap.XxyStarRating, beatmap.PerformancePoints);
            }

            return result;
        }

        private static void applyPersistedSongSelectBaseline(BeatmapInfo beatmap, IReadOnlyDictionary<Guid, PersistedSongSelectBaseline>? preservedBaseline)
        {
            if (preservedBaseline == null || !preservedBaseline.TryGetValue(beatmap.ID, out PersistedSongSelectBaseline persisted))
                return;

            if (persisted.StarRating >= 0)
                beatmap.StarRating = persisted.StarRating;

            if (persisted.XxyStarRating >= 0)
                beatmap.XxyStarRating = persisted.XxyStarRating;

            if (persisted.PerformancePoints >= 0)
                beatmap.PerformancePoints = persisted.PerformancePoints;
        }

        private static void applyVirtualBeatmapToRealm(BeatmapInfo beatmap, BeatmapInfo virtualBeatmap)
        {
            beatmap.BPM = virtualBeatmap.BPM;
            beatmap.Length = virtualBeatmap.Length;
            beatmap.TotalObjectCount = virtualBeatmap.TotalObjectCount;
            beatmap.EndTimeObjectCount = virtualBeatmap.EndTimeObjectCount;

            beatmap.Difficulty.CircleSize = virtualBeatmap.Difficulty.CircleSize;
            beatmap.Difficulty.OverallDifficulty = virtualBeatmap.Difficulty.OverallDifficulty;
            beatmap.Difficulty.DrainRate = virtualBeatmap.Difficulty.DrainRate;
            beatmap.Difficulty.ApproachRate = virtualBeatmap.Difficulty.ApproachRate;

            beatmap.Metadata.Title = virtualBeatmap.Metadata.Title;
            beatmap.Metadata.TitleUnicode = virtualBeatmap.Metadata.TitleUnicode;
            beatmap.Metadata.Artist = virtualBeatmap.Metadata.Artist;
            beatmap.Metadata.ArtistUnicode = virtualBeatmap.Metadata.ArtistUnicode;
            beatmap.Metadata.Source = virtualBeatmap.Metadata.Source;
            beatmap.Metadata.Tags = virtualBeatmap.Metadata.Tags;
            beatmap.Metadata.AudioFile = virtualBeatmap.Metadata.AudioFile;
            beatmap.Metadata.BackgroundFile = virtualBeatmap.Metadata.BackgroundFile;
            beatmap.Metadata.PreviewTime = virtualBeatmap.Metadata.PreviewTime;
        }

        private static bool isManagedExternalBmsSet(BeatmapSetInfo set, string bmsShortName)
        {
            if (!set.IsExternallyHosted)
                return BMSExternalPath.TryDecodeLegacyHash(set.Hash, out _);

            return set.Beatmaps.Any(b => string.Equals(b.Ruleset.ShortName, bmsShortName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Exposed for unit tests validating Realm skip/re-import decisions.
        /// </summary>
        public static bool SetMatchesForTesting(BeatmapSetInfo existingSet, BeatmapSetInfo targetSet, IReadOnlyDictionary<Guid, BMSSourceReference> sourceMap)
            => setMatches(existingSet, targetSet, sourceMap);

        /// <summary>
        /// Computes the <see cref="RealmNamedFileUsage.Filename"/> stored for an external chart (relative to the song folder).
        /// </summary>
        public static string ComputeRelativeChartFilename(string chartPath, string contentRoot)
        {
            string relativeFilename = Path.GetRelativePath(contentRoot, chartPath);

            if (relativeFilename.StartsWith("..", StringComparison.Ordinal))
                relativeFilename = Path.GetFileName(chartPath);

            return relativeFilename.Replace('\\', '/');
        }

        private static bool setMatches(BeatmapSetInfo existingSet, BeatmapSetInfo targetSet, IReadOnlyDictionary<Guid, BMSSourceReference> sourceMap)
        {
            string contentRoot = targetSet.Hash;

            if (!existingSet.IsExternallyHosted)
                return false;

            if (string.IsNullOrWhiteSpace(existingSet.ExternalContentRoot))
                return false;

            if (!existingSet.Hash.StartsWith(ExternalBeatmapPathEncoding.HASH_PREFIX, StringComparison.Ordinal))
                return false;

            if (!string.Equals(existingSet.Hash, ExternalBeatmapPathEncoding.Encode(contentRoot), StringComparison.Ordinal))
                return false;

            if (existingSet.Beatmaps.Count != targetSet.Beatmaps.Count)
                return false;

            Dictionary<Guid, BeatmapInfo> existingBeatmaps = existingSet.Beatmaps.ToDictionary(beatmap => beatmap.ID);

            foreach (BeatmapInfo targetBeatmap in targetSet.Beatmaps)
            {
                if (!existingBeatmaps.TryGetValue(targetBeatmap.ID, out BeatmapInfo? existingBeatmap))
                    return false;

                if (!sourceMap.TryGetValue(targetBeatmap.ID, out BMSSourceReference sourceRef))
                    return false;

                string expectedRelative = ComputeRelativeChartFilename(sourceRef.ChartPath, contentRoot);

                if (string.IsNullOrEmpty(expectedRelative))
                    return false;

                if (!string.Equals(existingBeatmap.Path, expectedRelative, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (existingSet.GetFile(expectedRelative) == null)
                    return false;

                if (!string.Equals(existingBeatmap.Hash, targetBeatmap.Hash, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!string.Equals(existingBeatmap.MD5Hash, targetBeatmap.MD5Hash, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!string.Equals(existingBeatmap.DifficultyName, targetBeatmap.DifficultyName, StringComparison.Ordinal))
                    return false;

                if (Path.IsPathRooted(existingBeatmap.Metadata.AudioFile))
                    return false;

                if (!string.Equals(existingBeatmap.Metadata.AudioFile, targetBeatmap.Metadata.AudioFile, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!string.Equals(existingBeatmap.Metadata.BackgroundFile, targetBeatmap.Metadata.BackgroundFile, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!string.Equals(existingBeatmap.Metadata.Tags, targetBeatmap.Metadata.Tags, StringComparison.Ordinal))
                    return false;

                if (!difficultyMatches(existingBeatmap, targetBeatmap))
                    return false;

                if (!floatEquals(existingBeatmap.BPM, targetBeatmap.BPM))
                    return false;

                if (!floatEquals(existingBeatmap.Length, targetBeatmap.Length))
                    return false;

                if (existingBeatmap.TotalObjectCount != targetBeatmap.TotalObjectCount)
                    return false;

                if (existingBeatmap.EndTimeObjectCount != targetBeatmap.EndTimeObjectCount)
                    return false;
            }

            return true;
        }

        private static bool difficultyMatches(BeatmapInfo existing, BeatmapInfo target) => floatEquals(existing.Difficulty.CircleSize, target.Difficulty.CircleSize)
                                                                                           && floatEquals(existing.Difficulty.OverallDifficulty, target.Difficulty.OverallDifficulty)
                                                                                           && floatEquals(existing.Difficulty.DrainRate, target.Difficulty.DrainRate);

        private static bool floatEquals(double a, double b) => Math.Abs(a - b) < 0.01;

        private static string registerExternalChartFile(RealmFileStore realmFileStore, Realm realm, BeatmapSetInfo set, string chartPath, string contentRoot)
        {
            string relativeFilename = ComputeRelativeChartFilename(chartPath, contentRoot);

            if (string.IsNullOrEmpty(relativeFilename))
                return string.Empty;

            if (set.GetFile(relativeFilename) != null)
                return relativeFilename;

            string syntheticHash = BmsPathKeys.ComputeRealmFileHash(chartPath);
            RealmFile file = realmFileStore.RegisterExternalHash(syntheticHash, realm);
            set.Files.Add(new RealmNamedFileUsage(file, relativeFilename));
            return relativeFilename;
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
    }
}
