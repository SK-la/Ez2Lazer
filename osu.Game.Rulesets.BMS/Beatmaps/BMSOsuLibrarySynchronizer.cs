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
            => Synchronize(manager, storage, realm, bmsRulesetInfo, CancellationToken.None);

        public static void Synchronize(
            BMSBeatmapManager manager,
            Storage storage,
            RealmAccess realm,
            RulesetInfo bmsRulesetInfo,
            CancellationToken cancellationToken,
            Action<double>? reportProgress = null)
        {
            if (!manager.NeedsRealmSynchronization)
                return;

            manager.PrepareRealmSynchronization();
            var realmFileStore = new RealmFileStore(realm, storage);
            int updatedSets = 0;
            int updatedBeatmaps = 0;
            int removedSets = 0;
            int processedSets = 0;
            int totalSets = manager.PendingRealmSyncSetCount;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<BmsRealmSyncChange> changes = manager.GetPendingRealmSyncChanges(200);

                if (changes.Count == 0)
                    break;

                List<IGrouping<Guid, BmsRealmSyncChange>> changesBySet = changes.GroupBy(change => change.SetId).ToList();

                realm.Write(r =>
                {
                    RulesetInfo? managedRuleset = r.All<RulesetInfo>().FirstOrDefault(info => info.ShortName == bmsRulesetInfo.ShortName);

                    if (managedRuleset == null)
                        throw new InvalidOperationException("BMS ruleset is not available in realm.");

                    foreach (IGrouping<Guid, BmsRealmSyncChange> setChanges in changesBySet)
                    {
                        BeatmapSetInfo? existingSet = r.Find<BeatmapSetInfo>(setChanges.Key);

                        if (!manager.TryGetRealmSyncSet(setChanges.Key, out BmsRealmSyncSet syncSet))
                        {
                            if (existingSet != null && setChanges.Any(change => change.Kind == BmsRealmSyncChangeKind.Delete))
                            {
                                removeSet(r, existingSet);
                                removedSets++;
                            }

                            continue;
                        }

                        BeatmapSetInfo targetSet = manager.BuildRealmSyncTarget(syncSet, managedRuleset);
                        applySetDelta(realmFileStore, r, existingSet, targetSet, syncSet, setChanges, ref updatedBeatmaps);
                        updatedSets++;
                    }
                });

                manager.AcknowledgeRealmSyncChanges(changes.Select(change => change.Revision).ToList());
                processedSets += changesBySet.Count;
                reportProgress?.Invoke(totalSets == 0 ? 1 : Math.Min(1, (double)processedSets / totalSets));
            }

            manager.MarkRealmSynchronized();
            reportProgress?.Invoke(1);

            Logger.Log($"[BMS] External library delta sync finished: updated {updatedSets} sets/{updatedBeatmaps} beatmaps, removed {removedSets} sets.");
        }

        private static void applySetDelta(
            RealmFileStore realmFileStore,
            Realm realm,
            BeatmapSetInfo? existingSet,
            BeatmapSetInfo targetSet,
            BmsRealmSyncSet syncSet,
            IEnumerable<BmsRealmSyncChange> changes,
            ref int updatedBeatmaps)
        {
            bool isNewSet = existingSet == null;
            BeatmapSetInfo destinationSet = existingSet ?? new BeatmapSetInfo { ID = targetSet.ID };
            string contentRoot = syncSet.Song.FolderPath;

            destinationSet.DateAdded = targetSet.DateAdded;
            destinationSet.Hash = ExternalBeatmapPathEncoding.Encode(contentRoot);
            destinationSet.ExternalContentRoot = Path.GetFullPath(contentRoot);
            destinationSet.HostingKind = BeatmapSetHostingKind.External;
            destinationSet.Status = BeatmapOnlineStatus.LocallyModified;

            var targetBeatmaps = targetSet.Beatmaps.ToDictionary(beatmap => beatmap.ID);
            var chartsByBeatmapId = syncSet.Charts.ToDictionary(chart => chart.Identity.BeatmapId);

            foreach ((Guid beatmapId, BeatmapInfo targetBeatmap) in targetBeatmaps)
            {
                BmsRealmSyncChart indexed = chartsByBeatmapId[beatmapId];
                string chartPath = indexed.Chart.FullPath;

                if (!File.Exists(chartPath))
                    throw new FileNotFoundException("BMS chart disappeared during Realm synchronization.", chartPath);

                string expectedRelative = ComputeRelativeChartFilename(chartPath, contentRoot);
                BeatmapInfo? destinationBeatmap = destinationSet.Beatmaps.FirstOrDefault(beatmap => beatmap.ID == beatmapId);

                if (destinationBeatmap?.Path is string previousPath
                    && !string.Equals(previousPath, expectedRelative, StringComparison.OrdinalIgnoreCase))
                {
                    RealmNamedFileUsage? previousUsage = destinationSet.GetFile(previousPath);

                    if (previousUsage != null)
                        destinationSet.Files.Remove(previousUsage);
                }

                string relativeChartPath = registerExternalChartFile(realmFileStore, realm, destinationSet, chartPath, contentRoot);
                RealmNamedFileUsage chartFileUsage = destinationSet.GetFile(relativeChartPath)
                                                     ?? throw new InvalidOperationException($"Failed to register external BMS chart '{chartPath}'.");

                if (destinationBeatmap == null)
                {
                    destinationBeatmap = new BeatmapInfo(targetBeatmap.Ruleset, new BeatmapDifficulty(), new BeatmapMetadata())
                    {
                        ID = beatmapId,
                        BeatmapSet = destinationSet,
                    };
                    destinationSet.Beatmaps.Add(destinationBeatmap);
                }

                destinationBeatmap.DifficultyName = targetBeatmap.DifficultyName;
                destinationBeatmap.Ruleset = targetBeatmap.Ruleset;
                destinationBeatmap.Hash = chartFileUsage.File.Hash;
                destinationBeatmap.MD5Hash = targetBeatmap.MD5Hash;
                destinationBeatmap.Status = BeatmapOnlineStatus.LocallyModified;
                destinationBeatmap.BeatmapSet = destinationSet;
                applyVirtualBeatmapToRealm(destinationBeatmap, targetBeatmap);
                updatedBeatmaps++;
            }

            HashSet<Guid> deletedBeatmapIds = changes.Where(change => change.Kind == BmsRealmSyncChangeKind.Delete)
                                                    .Select(change => change.BeatmapId)
                                                    .Where(id => !targetBeatmaps.ContainsKey(id))
                                                    .ToHashSet();

            foreach (BeatmapInfo deletedBeatmap in destinationSet.Beatmaps.Where(beatmap => deletedBeatmapIds.Contains(beatmap.ID)).ToList())
            {
                RealmNamedFileUsage? fileUsage = deletedBeatmap.File;
                realm.Remove(deletedBeatmap.Metadata);
                realm.Remove(deletedBeatmap);

                if (fileUsage != null && destinationSet.Beatmaps.All(beatmap => !string.Equals(beatmap.Hash, fileUsage.File.Hash, StringComparison.Ordinal)))
                    destinationSet.Files.Remove(fileUsage);
            }

            if (isNewSet)
                realm.Add(destinationSet, update: true);
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

        public static void ApplyVirtualBeatmapFieldsForTesting(BeatmapInfo beatmap, BeatmapInfo virtualBeatmap)
            => applyVirtualBeatmapToRealm(beatmap, virtualBeatmap);

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
