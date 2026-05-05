// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
            int importedSets = 0;
            int importedBeatmaps = 0;

            realm.Write(r =>
            {
                RulesetInfo? managedRuleset = r.All<RulesetInfo>().FirstOrDefault(info => info.ShortName == bmsRulesetInfo.ShortName);

                if (managedRuleset == null)
                    throw new InvalidOperationException("BMS ruleset is not available in realm.");

                foreach (BeatmapSetInfo oldSet in r.All<BeatmapSetInfo>().Where(set => set.Hash.StartsWith(bms_set_hash_prefix)).ToList())
                    removeSet(r, oldSet);

                foreach (BeatmapSetInfo virtualSet in virtualSets)
                {
                    var newSet = new BeatmapSetInfo
                    {
                        ID = virtualSet.ID,
                        DateAdded = virtualSet.DateAdded,
                        Hash = virtualSet.Hash.StartsWith(bms_set_hash_prefix, StringComparison.Ordinal) ? virtualSet.Hash : $"{bms_set_hash_prefix}{virtualSet.ID:N}",
                        Status = BeatmapOnlineStatus.LocallyModified,
                    };

                    HashSet<string> addedHashes = new HashSet<string>(StringComparer.Ordinal);

                    foreach (BeatmapInfo virtualBeatmap in virtualSet.Beatmaps)
                    {
                        if (!sourceMap.TryGetValue(virtualBeatmap.ID, out BMSSourceReference sourceRef))
                            continue;

                        if (!File.Exists(sourceRef.ChartPath))
                            continue;

                        mirrorFolderFiles(storage, r, newSet, sourceRef.FolderPath, addedHashes);

                        string chartFilename = Path.GetFileName(sourceRef.ChartPath);
                        RealmNamedFileUsage? chartFileUsage = newSet.GetFile(chartFilename);

                        if (chartFileUsage == null)
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

            Logger.Log($"[BMS] External library sync finished: {importedSets} sets, {importedBeatmaps} beatmaps imported.");
        }

        private static void mirrorFolderFiles(Storage storage, Realm realm, BeatmapSetInfo set, string folderPath, HashSet<string> addedHashes)
        {
            if (!Directory.Exists(folderPath))
                return;

            foreach (string filePath in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                string relativeName = Path.GetRelativePath(folderPath, filePath).Replace('\\', '/');

                if (string.IsNullOrWhiteSpace(relativeName))
                    continue;

                byte[] bytes = File.ReadAllBytes(filePath);
                string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

                if (!addedHashes.Add(hash))
                    continue;

                string storagePath = toStoragePath(hash);
                string fullStoragePath = storage.GetFullPath(storagePath);
                string? parent = Path.GetDirectoryName(fullStoragePath);

                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);

                if (!File.Exists(fullStoragePath))
                    File.WriteAllBytes(fullStoragePath, bytes);

                RealmFile realmFile = realm.Find<RealmFile>(hash) ?? realm.Add(new RealmFile { Hash = hash }, update: true);
                set.Files.Add(new RealmNamedFileUsage(realmFile, relativeName));
            }
        }

        private static string toStoragePath(string hash) => Path.Combine(hash.Substring(0, 1), hash.Substring(0, 2), hash);

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

