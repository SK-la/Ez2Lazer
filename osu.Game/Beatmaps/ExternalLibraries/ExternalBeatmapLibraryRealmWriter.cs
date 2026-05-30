// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Rulesets;
using Realms;

namespace osu.Game.Beatmaps.ExternalLibraries
{
    public sealed class ExternalBeatmapLibraryRealmWriter
    {
        private readonly RealmAccess realm;
        private readonly Storage storage;

        public ExternalBeatmapLibraryRealmWriter(RealmAccess realm, Storage storage)
        {
            this.realm = realm;
            this.storage = storage;
        }

        public void UpsertSets(IEnumerable<ExternalBeatmapSetImportModel> sets, string rulesetShortName)
        {
            var setList = sets.ToList();
            var realmFileStore = new RealmFileStore(realm, storage);

            realm.Write(r =>
            {
                RulesetInfo? managedRuleset = r.All<RulesetInfo>().FirstOrDefault(info => info.ShortName == rulesetShortName && info.Available);

                if (managedRuleset == null)
                {
                    Logger.Log($"Skipping external library upsert: ruleset '{rulesetShortName}' not available.", LoggingTarget.Database);
                    return;
                }

                var existingExternal = r.All<BeatmapSetInfo>()
                                        .Where(s => s.HostingKind == BeatmapSetHostingKind.External && s.Beatmaps.Any(b => b.Ruleset.ShortName == rulesetShortName))
                                        .ToDictionary(s => s.ID);

                HashSet<Guid> targetIds = setList.Select(s => s.SetId).ToHashSet();

                foreach (var (id, oldSet) in existingExternal)
                {
                    if (!targetIds.Contains(id))
                        removeSet(r, oldSet);
                }

                foreach (var import in setList)
                {
                    if (existingExternal.TryGetValue(import.SetId, out var existing) && setMatches(existing, import))
                        continue;

                    if (existing != null)
                        removeSet(r, existing);

                    var beatmapSet = new BeatmapSetInfo
                    {
                        ID = import.SetId,
                        DateAdded = import.DateAdded,
                        Hash = import.SetHash,
                        HostingKind = BeatmapSetHostingKind.External,
                        ExternalContentRoot = import.ExternalContentRoot,
                        Status = BeatmapOnlineStatus.LocallyModified,
                    };

                    foreach (var file in import.Files.DistinctBy(f => f.Sha256Hash))
                    {
                        realmFileStore.RegisterExternalHash(file.Sha256Hash, r);
                        beatmapSet.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = file.Sha256Hash }, file.RelativeFilename));
                    }

                    foreach (var difficulty in import.Beatmaps)
                    {
                        var beatmapInfo = new BeatmapInfo(managedRuleset, difficulty.Difficulty, difficulty.Metadata)
                        {
                            DifficultyName = difficulty.DifficultyName,
                            MD5Hash = difficulty.Md5Hash,
                            Hash = difficulty.Sha256Hash,
                            Length = difficulty.Length,
                            BPM = difficulty.BPM,
                        };

                        if (import.Files.All(f => f.Sha256Hash != difficulty.Sha256Hash))
                        {
                            realmFileStore.RegisterExternalHash(difficulty.Sha256Hash, r);
                            beatmapSet.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = difficulty.Sha256Hash }, difficulty.ChartRelativePath));
                        }

                        beatmapInfo.BeatmapSet = beatmapSet;
                        beatmapSet.Beatmaps.Add(beatmapInfo);
                    }

                    if (beatmapSet.Beatmaps.Count == 0)
                        continue;

                    r.Add(beatmapSet, update: true);
                }
            });
        }

        private static bool setMatches(BeatmapSetInfo existing, ExternalBeatmapSetImportModel import)
            => existing.Hash == import.SetHash
               && existing.ExternalContentRoot == import.ExternalContentRoot
               && existing.Beatmaps.Count == import.Beatmaps.Count;

        private static void removeSet(Realm realm, BeatmapSetInfo set)
        {
            foreach (var beatmap in set.Beatmaps.ToArray())
                realm.Remove(beatmap);

            realm.Remove(set);
        }
    }
}
