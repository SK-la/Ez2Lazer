// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Models;
using Realms;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    public static class BMSOsuLibrarySynchronizer
    {
        private const string bms_set_hash_prefix = "bms-ext:set:";

        public static void Synchronize(BMSBeatmapManager manager, Storage storage, RealmAccess realm, RulesetInfo bmsRulesetInfo)
        {
            if (manager.LibraryCache == null)
                return;

            IReadOnlyList<BeatmapSetInfo> virtualSets = manager.BuildVirtualBeatmapCatalog(bmsRulesetInfo);
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

                Dictionary<Guid, BeatmapSetInfo> existingSets = r.All<BeatmapSetInfo>()
                                                                 .Where(set => set.Hash.StartsWith(bms_set_hash_prefix))
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
                        && setMatches(existingSet, virtualSet))
                    {
                        skippedSets++;
                        continue;
                    }

                    if (existingSet != null)
                    {
                        removeSet(r, existingSet);
                        removedSets++;
                    }

                    var newSet = new BeatmapSetInfo
                    {
                        ID = virtualSet.ID,
                        DateAdded = virtualSet.DateAdded,
                        Hash = createExternalHash(virtualSet.Hash),
                        Status = BeatmapOnlineStatus.LocallyModified,
                    };

                    foreach (BeatmapInfo virtualBeatmap in virtualSet.Beatmaps)
                    {
                        if (!sourceMap.TryGetValue(virtualBeatmap.ID, out BMSSourceReference sourceRef))
                            continue;

                        if (!File.Exists(sourceRef.ChartPath))
                            continue;

                        registerExternalChartFile(realmFileStore, r, newSet, sourceRef.ChartPath);

                        string chartFilename = Path.GetFileName(sourceRef.ChartPath);
                        RealmNamedFileUsage? chartFileUsage = newSet.GetFile(chartFilename);

                        if (chartFileUsage is null)
                            continue;

                        var beatmap = new BeatmapInfo(managedRuleset, virtualBeatmap.Difficulty.Clone(), virtualBeatmap.Metadata.DeepClone())
                        {
                            ID = virtualBeatmap.ID,
                            DifficultyName = virtualBeatmap.DifficultyName,
                            BPM = virtualBeatmap.BPM,
                            Length = virtualBeatmap.Length,
                            Hash = chartFileUsage.File.Hash,
                            MD5Hash = virtualBeatmap.MD5Hash,
                            TotalObjectCount = virtualBeatmap.TotalObjectCount,
                            EndTimeObjectCount = virtualBeatmap.EndTimeObjectCount,
                            Status = BeatmapOnlineStatus.LocallyModified,
                            BeatmapSet = newSet,
                        };

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

            Logger.Log($"[BMS] External library sync finished: imported {importedSets} sets/{importedBeatmaps} beatmaps, removed {removedSets} sets, skipped {skippedSets} unchanged sets.");
        }

        private static bool setMatches(BeatmapSetInfo existingSet, BeatmapSetInfo targetSet)
        {
            if (!string.Equals(existingSet.Hash, createExternalHash(targetSet.Hash), StringComparison.Ordinal))
                return false;

            // Legacy sync registered every file in the folder; re-import so Files only lists chart entries.
            if (existingSet.Hash.StartsWith(bms_set_hash_prefix, StringComparison.Ordinal)
                && existingSet.Files.Count > existingSet.Beatmaps.Count)
                return false;

            if (existingSet.Beatmaps.Count != targetSet.Beatmaps.Count)
                return false;

            Dictionary<Guid, BeatmapInfo> existingBeatmaps = existingSet.Beatmaps.ToDictionary(beatmap => beatmap.ID);

            foreach (BeatmapInfo targetBeatmap in targetSet.Beatmaps)
            {
                if (!existingBeatmaps.TryGetValue(targetBeatmap.ID, out BeatmapInfo? existingBeatmap))
                    return false;

                if (!string.Equals(existingBeatmap.MD5Hash, targetBeatmap.MD5Hash, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!string.Equals(existingBeatmap.DifficultyName, targetBeatmap.DifficultyName, StringComparison.Ordinal))
                    return false;

                // Force reimport of legacy entries that stored absolute audio paths.
                if (Path.IsPathRooted(existingBeatmap.Metadata.AudioFile))
                    return false;

                // Keep metadata in sync so audio path fixes (including subdirectories) are propagated.
                if (!string.Equals(existingBeatmap.Metadata.AudioFile, targetBeatmap.Metadata.AudioFile, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private static string createExternalHash(string folderPath)
        {
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(folderPath));
            return bms_set_hash_prefix + encoded;
        }

        /// <summary>
        /// Only the chart file is registered in Realm: <see cref="BeatmapInfo"/> links via <see cref="BeatmapInfo.Hash"/> / <see cref="BeatmapInfo.File"/>.
        /// Audio, BGA and keysounds stay on disk and are resolved through <c>bms-ext:set:</c> + original folder in working beatmap.
        /// </summary>
        private static void registerExternalChartFile(RealmFileStore realmFileStore, Realm realm, BeatmapSetInfo set, string chartPath)
        {
            string chartFilename = Path.GetFileName(chartPath);

            if (string.IsNullOrEmpty(chartFilename))
                return;

            if (set.GetFile(chartFilename) != null)
                return;

            using (Stream stream = File.OpenRead(chartPath))
            {
                RealmFile file = realmFileStore.RegisterExternalHash(stream.ComputeSHA2Hash(), realm);
                set.Files.Add(new RealmNamedFileUsage(file, chartFilename));
            }
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

