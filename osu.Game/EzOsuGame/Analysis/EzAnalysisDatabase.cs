// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 选歌分析数据库入口。
    ///
    /// 职责对齐官方选歌读取链路：
    /// - SQLite 持久化结果作为选歌基线数据源。
    /// - 运行时分析缓存仅负责当前 ruleset/mods 的动态修正。
    /// - 启动预热与运行期补算由外部后台处理器驱动，不再挂在 cache 上。
    /// </summary>
    public class EzAnalysisDatabase
    {
        private static int computeFailCount;
        private static int songsBranchComputeFailCount;
        private static readonly IReadOnlyDictionary<Guid, double> empty_xxy_sr_values = new Dictionary<Guid, double>();

        private readonly EzAnalysisPersistentStore persistentStore;
        private readonly BeatmapManager beatmapManager;
        private readonly IBindable<bool> sqliteAnalysisEnabled;
        private readonly Bindable<string?> activeSongsBranchDisplayName = new Bindable<string?>();
        private readonly Bindable<int> activeSongsBranchVersion = new Bindable<int>();
        private readonly object activeSongsBranchStateLock = new object();
        private readonly List<ActiveSongsBranchState> activeSongsBranches = new List<ActiveSongsBranchState>();

        private readonly record struct ActiveSongsBranchState(string DatabasePath, string DisplayName, int RulesetOnlineId, string ModsFingerprint);

        public readonly record struct SongsBranchBuildResult(
            bool Success,
            LocalisableString Message,
            string? DatabasePath,
            string? DisplayName,
            int RequestedBeatmapCount,
            int StoredBeatmapCount);

        public EzAnalysisDatabase(EzAnalysisPersistentStore persistentStore, BeatmapManager beatmapManager, Ez2ConfigManager ezConfig)
        {
            this.persistentStore = persistentStore;
            this.beatmapManager = beatmapManager;

            sqliteAnalysisEnabled = ezConfig.GetBindable<bool>(Ez2Setting.EzAnalysisSqliteEnabled);
        }

        public bool TryGetStoredAnalysis(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo, out EzAnalysisResult result)
        {
            result = default;

            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled)
                return false;

            if (!tryCreateStoredLookup(beatmapInfo, rulesetInfo, mods: null, out var lookup))
                return false;

            return persistentStore.TryGet(lookup.BeatmapInfo, out result);
        }

        public bool TryGetXxySr(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo, out double xxySr)
        {
            xxySr = 0;

            if (!TryGetStoredAnalysis(beatmapInfo, rulesetInfo, out var result))
                return false;

            if (result.ManiaAttributes?.XxySr is not double storedXxySr)
                return false;

            xxySr = storedXxySr;
            return true;
        }

        public IBindable<string?> ActiveSongsBranchDisplayName => activeSongsBranchDisplayName;

        public IBindable<int> ActiveSongsBranchVersion => activeSongsBranchVersion;

        public bool HasActiveSongsBranch
        {
            get
            {
                lock (activeSongsBranchStateLock)
                    return activeSongsBranches.Count > 0;
            }
        }

        public bool HasActiveSongsBranchFor(IRulesetInfo? rulesetInfo)
        {
            int? rulesetOnlineId = rulesetInfo?.OnlineID;

            if (!rulesetOnlineId.HasValue)
                return false;

            lock (activeSongsBranchStateLock)
                return activeSongsBranches.Any(branch => branch.RulesetOnlineId == rulesetOnlineId.Value);
        }

        public IReadOnlyDictionary<Guid, double> GetActiveSongsBranchValues(IEnumerable<BeatmapInfo> beatmaps, IRulesetInfo? rulesetInfo)
        {
            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled || !HasActiveSongsBranchFor(rulesetInfo))
                return empty_xxy_sr_values;

            return getResolvedActiveBranchValues(beatmaps, rulesetInfo);
        }

        public IReadOnlyDictionary<Guid, double> GetStoredXxySrValues(IEnumerable<BeatmapInfo> beatmaps, IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods = null)
        {
            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled)
                return empty_xxy_sr_values;

            var beatmapList = beatmaps.Distinct().ToList();

            if (beatmapList.Count == 0)
                return empty_xxy_sr_values;

            var resolvedValues = new Dictionary<Guid, double>(beatmapList.Count);

            if (HasActiveSongsBranchFor(rulesetInfo))
            {
                foreach (var kvp in getResolvedActiveBranchValues(beatmapList, rulesetInfo))
                    resolvedValues[kvp.Key] = kvp.Value;
            }

            var eligibleBeatmaps = beatmapList.Where(b => !resolvedValues.ContainsKey(b.ID) && CanUseStoredAnalysis(b, rulesetInfo, mods: null)).ToList();

            if (eligibleBeatmaps.Count == 0)
                return resolvedValues.Count == 0 ? empty_xxy_sr_values : resolvedValues;

            foreach (var kvp in persistentStore.GetStoredXxySrValues(eligibleBeatmaps))
                resolvedValues[kvp.Key] = kvp.Value;

            return resolvedValues.Count == 0 ? empty_xxy_sr_values : resolvedValues;
        }

        public bool IsActiveSongsBranchFor(IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods)
        {
            int? rulesetOnlineId = rulesetInfo?.OnlineID;

            if (!rulesetOnlineId.HasValue)
                return false;

            string modsFingerprint = createModsProfileFingerprint(mods);

            lock (activeSongsBranchStateLock)
            {
                return activeSongsBranches.Any(branch => branch.RulesetOnlineId == rulesetOnlineId.Value
                                                         && string.Equals(branch.ModsFingerprint, modsFingerprint, StringComparison.Ordinal));
            }
        }

        public bool IsSongsBranchActive(string databasePath)
        {
            string fullPath = Path.GetFullPath(databasePath);

            lock (activeSongsBranchStateLock)
                return activeSongsBranches.Any(branch => string.Equals(branch.DatabasePath, fullPath, StringComparison.OrdinalIgnoreCase));
        }

        public void ActivateSongsBranch(string databasePath, IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods, int beatmapCount = 0, string? displayName = null)
        {
            activateSongsBranch(databasePath, new EzAnalysisPersistentStore.SongsBranchMetadata(
                rulesetInfo?.OnlineID ?? 0,
                rulesetInfo?.ShortName ?? "ruleset",
                createModsProfileFingerprint(mods),
                createModsDisplay(mods),
                beatmapCount,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                displayName ?? createBranchDisplayName(null, mods),
                createModsJson(mods)));
        }

        public void DeactivateSongsBranch()
        {
            lock (activeSongsBranchStateLock)
            {
                activeSongsBranches.Clear();
                updateActiveBranchBindablesLocked();
                activeSongsBranchVersion.Value++;
            }
        }

        public Task<SongsBranchBuildResult> CreateAndActivateSongsBranchAsync(
            IEnumerable<BeatmapInfo> beatmaps, EzAnalysisPersistentStore.SourceCollectionSnapshot? sourceCollection, IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods,
            bool activateAfterCreate = true, Action<int, int>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled)
                return Task.FromResult(new SongsBranchBuildResult(false, SongsBranchStrings.SQLITE_DISABLED, null, null, 0, 0));

            if (rulesetInfo is not RulesetInfo localRulesetInfo || localRulesetInfo.OnlineID != 3)
                return Task.FromResult(new SongsBranchBuildResult(false, SongsBranchStrings.MANIA_ONLY, null, null, 0, 0));

            var requestedBeatmaps = beatmaps.Distinct().ToList();

            if (requestedBeatmaps.Count == 0)
                return Task.FromResult(new SongsBranchBuildResult(false, SongsBranchStrings.EMPTY_FILTER_RESULT, null, null, 0, 0));

            return Task.Run(() =>
            {
                var beatmapList = requestedBeatmaps.Where(beatmap => beatmap.Ruleset.OnlineID == localRulesetInfo.OnlineID).ToList();

                if (beatmapList.Count == 0)
                    return new SongsBranchBuildResult(false, SongsBranchStrings.NO_WRITABLE_RESULTS, null, null, requestedBeatmaps.Count, 0);

                string modsFingerprint = createModsProfileFingerprint(mods);
                string modsDisplay = createModsDisplay(mods);
                Guid resolvedSourceCollectionId = sourceCollection?.CollectionId ?? Guid.Empty;
                string resolvedSourceCollectionName = sourceCollection?.Name.Trim() ?? string.Empty;
                string displayName = createBranchDisplayName(resolvedSourceCollectionName, mods);
                long lastProgressReportAt = Environment.TickCount64;
                var metadata = new EzAnalysisPersistentStore.SongsBranchMetadata(
                    localRulesetInfo.OnlineID,
                    localRulesetInfo.ShortName,
                    modsFingerprint,
                    modsDisplay,
                    beatmapList.Count,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    displayName,
                    createModsJson(mods),
                    SourceCollectionId: resolvedSourceCollectionId,
                    SourceCollectionName: resolvedSourceCollectionName,
                    SourceCollectionLastModifiedUnixMilliseconds: sourceCollection?.LastModifiedUnixMilliseconds ?? 0,
                    SourceCollectionBeatmapCount: sourceCollection?.BeatmapMd5Hashes.Count ?? beatmapList.Count);
                string databasePath = persistentStore.CreateSongsBranchDatabasePath(metadata);
                var rows = new List<EzAnalysisPersistentStore.SongsBranchRow>(beatmapList.Count);

                for (int i = 0; i < beatmapList.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var beatmap = beatmapList[i];
                    double xxySr = 0;

                    try
                    {
                        var lookup = new EzAnalysisLookupCache(beatmap, localRulesetInfo, mods);

                        if (EzAnalysisComputation.TryComputeXxySr(beatmapManager, lookup, cancellationToken, out double computedXxySr))
                            xxySr = computedXxySr;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (BeatmapInvalidForRulesetException)
                    {
                    }
                    catch (Exception ex)
                    {
                        if (Interlocked.Increment(ref songsBranchComputeFailCount) <= 10)
                        {
                            Logger.Error(ex,
                                $"[EzAnalysisDatabase] CreateAndActivateSongsBranchAsync failed. beatmapId={beatmap.ID} diff=\"{beatmap.DifficultyName}\" ruleset={localRulesetInfo.ShortName} mods={modsDisplay}",
                                Ez2ConfigManager.LOGGER_NAME);
                        }
                    }

                    rows.Add(new EzAnalysisPersistentStore.SongsBranchRow(beatmap.ID, beatmap.Hash, beatmap.MD5Hash, xxySr));

                    long now = Environment.TickCount64;

                    if (i == 0 || i + 1 == beatmapList.Count || now - lastProgressReportAt >= 100)
                    {
                        lastProgressReportAt = now;
                        progress?.Invoke(i + 1, beatmapList.Count);
                    }
                }

                persistentStore.StoreSongsBranch(databasePath, metadata, rows, sourceCollection);
                deleteBranchesForSourceCollection(resolvedSourceCollectionId, databasePath);

                if (activateAfterCreate)
                {
                    activateSongsBranch(databasePath, metadata);
                    return new SongsBranchBuildResult(true, SongsBranchStrings.GENERATED_AND_ACTIVATED, databasePath, displayName, beatmapList.Count, rows.Count);
                }

                return new SongsBranchBuildResult(true, SongsBranchStrings.GENERATED_ONLY, databasePath, displayName, beatmapList.Count, rows.Count);
            }, cancellationToken);
        }

        public IReadOnlyList<EzAnalysisPersistentStore.SongsBranchDescriptor> GetAvailableSongsBranches(IRulesetInfo? rulesetInfo = null, IReadOnlyList<Mod>? mods = null, int maxCount = 0)
        {
            var branches = persistentStore.GetAvailableSongsBranches();

            if (branches.Count == 0)
                return branches;

            int? currentRulesetOnlineId = rulesetInfo?.OnlineID;
            string currentModsFingerprint = createModsProfileFingerprint(mods);

            var ordered = branches
                          .OrderByDescending(branch => currentRulesetOnlineId.HasValue && branch.Metadata.RulesetOnlineId == currentRulesetOnlineId.Value && string.Equals(branch.Metadata.ModsFingerprint, currentModsFingerprint, StringComparison.Ordinal))
                          .ThenByDescending(branch => currentRulesetOnlineId.HasValue && branch.Metadata.RulesetOnlineId == currentRulesetOnlineId.Value)
                          .ThenByDescending(branch => branch.Metadata.CreatedAtUnixMilliseconds)
                          .ToList();

            if (maxCount > 0 && ordered.Count > maxCount)
                ordered = ordered.Take(maxCount).ToList();

            return ordered;
        }

        public bool TryActivateSongsBranch(string databasePath, out LocalisableString message)
        {
            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled)
            {
                message = SongsBranchStrings.SQLITE_DISABLED;
                return false;
            }

            if (!persistentStore.TryGetSongsBranchDescriptor(databasePath, out var branch))
            {
                message = SongsBranchStrings.INVALID_BRANCH;
                return false;
            }

            if (IsSongsBranchActive(branch.DatabasePath))
            {
                message = LocalisableString.Format(SongsBranchStrings.ALREADY_ACTIVE_BRANCH, branch.Metadata.DisplayName);
                return true;
            }

            activateSongsBranch(branch.DatabasePath, branch.Metadata);
            message = LocalisableString.Format(SongsBranchStrings.ACTIVATED_BRANCH, branch.Metadata.DisplayName);
            return true;
        }

        public bool TryToggleSongsBranchActivation(string databasePath, out LocalisableString message)
        {
            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled)
            {
                message = SongsBranchStrings.SQLITE_DISABLED;
                return false;
            }

            if (!persistentStore.TryGetSongsBranchDescriptor(databasePath, out var branch))
            {
                message = SongsBranchStrings.INVALID_BRANCH;
                return false;
            }

            if (removeActiveSongsBranch(branch.DatabasePath))
            {
                message = LocalisableString.Format(SongsBranchStrings.DEACTIVATED_BRANCH, branch.Metadata.DisplayName);
                return true;
            }

            activateSongsBranch(branch.DatabasePath, branch.Metadata);
            message = LocalisableString.Format(SongsBranchStrings.ACTIVATED_BRANCH, branch.Metadata.DisplayName);
            return true;
        }

        public bool TryDeleteSongsBranch(string databasePath, out LocalisableString message)
        {
            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled)
            {
                message = SongsBranchStrings.SQLITE_DISABLED;
                return false;
            }

            if (!persistentStore.TryGetSongsBranchDescriptor(databasePath, out var branch))
            {
                message = SongsBranchStrings.INVALID_BRANCH;
                return false;
            }

            bool wasActive = removeActiveSongsBranch(branch.DatabasePath);

            if (!persistentStore.DeleteSongsBranch(branch.DatabasePath))
            {
                if (wasActive)
                    activateSongsBranch(branch.DatabasePath, branch.Metadata);

                message = SongsBranchStrings.DELETE_BRANCH_FAILED;
                return false;
            }

            message = LocalisableString.Format(SongsBranchStrings.DELETED_BRANCH, branch.Metadata.DisplayName);
            return true;
        }

        public bool TryToggleSongsBranchHidden(string databasePath, out LocalisableString message, out IReadOnlyList<BeatmapSetInfo> nonHideableBeatmapSets)
        {
            nonHideableBeatmapSets = Array.Empty<BeatmapSetInfo>();

            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled)
            {
                message = SongsBranchStrings.SQLITE_DISABLED;
                return false;
            }

            if (!persistentStore.TryGetSongsBranchDescriptor(databasePath, out var branch))
            {
                message = SongsBranchStrings.INVALID_BRANCH;
                return false;
            }

            List<BeatmapInfo> localBeatmaps = beatmapManager.GetAllUsableBeatmapSets().SelectMany(set => set.Beatmaps).Distinct().ToList();
            IReadOnlyDictionary<string, List<BeatmapInfo>> localBeatmapsByMd5 = createLocalBeatmapsByMd5Lookup(localBeatmaps);
            List<BeatmapInfo> branchBeatmaps = getLocalBeatmapsForBranchCollection(branch.DatabasePath, localBeatmapsByMd5);

            if (branchBeatmaps.Count == 0)
            {
                message = LocalisableString.Format(SongsBranchStrings.BRANCH_HIDE_NO_LOCAL_BEATMAPS, branch.Metadata.DisplayName);
                return false;
            }

            if (branch.Metadata.HiddenApplied)
                return tryRestoreBranchHiddenState(branch, localBeatmapsByMd5, branchBeatmaps, out message);

            return tryApplyBranchHiddenState(branch, localBeatmapsByMd5, branchBeatmaps, out message, out nonHideableBeatmapSets);
        }

        public bool TryToggleCollectionHidden(Guid collectionId, string collectionName, IEnumerable<string> beatmapMd5Hashes, out LocalisableString message,
                                              out IReadOnlyList<BeatmapSetInfo> nonHideableBeatmapSets)
        {
            nonHideableBeatmapSets = Array.Empty<BeatmapSetInfo>();

            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled)
            {
                message = SongsBranchStrings.SQLITE_DISABLED;
                return false;
            }

            if (collectionId == Guid.Empty)
            {
                message = SongsBranchStrings.INVALID_COLLECTION;
                return false;
            }

            List<BeatmapInfo> localBeatmaps = beatmapManager.GetAllUsableBeatmapSets().SelectMany(set => set.Beatmaps).Distinct().ToList();
            IReadOnlyDictionary<string, List<BeatmapInfo>> localBeatmapsByMd5 = createLocalBeatmapsByMd5Lookup(localBeatmaps);
            List<BeatmapInfo> collectionBeatmaps = getLocalBeatmapsForCollection(beatmapMd5Hashes, localBeatmapsByMd5);
            IReadOnlySet<Guid> hiddenCollectionIds = persistentStore.GetHiddenCollectionIds();
            bool hiddenApplied = hiddenCollectionIds.Contains(collectionId);

            if (hiddenApplied)
                return tryRestoreCollectionHiddenState(collectionId, collectionName, localBeatmapsByMd5, collectionBeatmaps, out message);

            return tryApplyCollectionHiddenState(collectionId, collectionName, beatmapMd5Hashes, localBeatmapsByMd5, collectionBeatmaps, out message, out nonHideableBeatmapSets);
        }

        public IReadOnlySet<Guid> GetHiddenCollectionIds() => persistentStore.GetHiddenCollectionIds();

        public IReadOnlyList<Guid> GetBeatmapsNeedingRecompute(IEnumerable<(Guid id, string hash)> beatmaps)
        {
            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled)
                return Array.Empty<Guid>();

            return persistentStore.GetBeatmapsNeedingRecompute(beatmaps);
        }

        public Task<EzAnalysisResult?> RecomputeStoredAnalysisAsync(BeatmapInfo beatmapInfo, CancellationToken cancellationToken = default)
        {
            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled)
                return Task.FromResult<EzAnalysisResult?>(null);

            if (!tryCreateStoredLookup(beatmapInfo, beatmapInfo.Ruleset, mods: null, out var lookup))
                return Task.FromResult<EzAnalysisResult?>(null);

            return Task.Run(() => computeAndStore(beatmapInfo, lookup, cancellationToken), cancellationToken);
        }

        internal static bool CanUseStoredAnalysis(BeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo, IEnumerable<Mod>? mods)
        {
            var localRulesetInfo = (rulesetInfo ?? beatmapInfo.Ruleset) as RulesetInfo;

            if (localRulesetInfo == null)
                return false;

            if (!localRulesetInfo.Equals(beatmapInfo.Ruleset))
                return false;

            return mods == null || !mods.Any();
        }

        private EzAnalysisResult? computeAndStore(BeatmapInfo beatmapInfo, EzAnalysisLookupCache lookup, CancellationToken cancellationToken)
        {
            try
            {
                var result = EzAnalysisComputation.Compute(beatmapManager, lookup, cancellationToken);
                persistentStore.StoreIfDifferent(beatmapInfo, result);
                return result;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (BeatmapInvalidForRulesetException invalidForRuleset)
            {
                if (lookup.Ruleset.Equals(lookup.BeatmapInfo.Ruleset))
                {
                    Logger.Error(invalidForRuleset, $"[EzAnalysisDatabase] Failed to convert {lookup.BeatmapInfo.OnlineID} to the beatmap's default ruleset ({lookup.BeatmapInfo.Ruleset}).",
                        Ez2ConfigManager.LOGGER_NAME);
                }

                return null;
            }
            catch (Exception ex)
            {
                if (Interlocked.Increment(ref computeFailCount) <= 10)
                {
                    string mods = lookup.OrderedMods.Length == 0 ? "(none)" : string.Join(',', lookup.OrderedMods.Select(m => m.Acronym));
                    Logger.Error(ex,
                        $"[EzAnalysisDatabase] computeAndStore failed. beatmapId={lookup.BeatmapInfo.ID} diff=\"{lookup.BeatmapInfo.DifficultyName}\" ruleset={lookup.Ruleset.ShortName} mods={mods}",
                        Ez2ConfigManager.LOGGER_NAME);
                }

                return null;
            }
        }

        private static bool tryCreateStoredLookup(IBeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo, IEnumerable<Mod>? mods, out EzAnalysisLookupCache lookup)
        {
            lookup = default;

            if (beatmapInfo is not BeatmapInfo localBeatmapInfo)
                return false;

            if (!CanUseStoredAnalysis(localBeatmapInfo, rulesetInfo, mods))
                return false;

            var localRulesetInfo = (rulesetInfo ?? localBeatmapInfo.Ruleset) as RulesetInfo;
            if (localRulesetInfo == null)
                return false;

            lookup = new EzAnalysisLookupCache(localBeatmapInfo, localRulesetInfo, mods: null);
            return true;
        }

        private static string createBranchDisplayName(string? sourceCollectionName, IReadOnlyList<Mod>? mods)
            => string.IsNullOrWhiteSpace(sourceCollectionName)
                ? $"songs | {createModsDisplay(mods)}"
                : $"{sourceCollectionName.Trim()} | {createModsDisplay(mods)}";

        private IReadOnlyDictionary<Guid, double> getResolvedActiveBranchValues(IEnumerable<BeatmapInfo> beatmaps, IRulesetInfo? rulesetInfo)
        {
            int? rulesetOnlineId = rulesetInfo?.OnlineID;

            if (!rulesetOnlineId.HasValue)
                return empty_xxy_sr_values;

            var beatmapList = beatmaps.Distinct().ToList();

            if (beatmapList.Count == 0)
                return empty_xxy_sr_values;

            List<ActiveSongsBranchState> activeBranches;

            lock (activeSongsBranchStateLock)
            {
                activeBranches = activeSongsBranches
                                 .Where(branch => branch.RulesetOnlineId == rulesetOnlineId.Value)
                                 .ToList();
            }

            if (activeBranches.Count == 0)
                return empty_xxy_sr_values;

            var resolvedValues = new Dictionary<Guid, double>(beatmapList.Count);

            foreach (ActiveSongsBranchState activeBranch in activeBranches)
            {
                IReadOnlyDictionary<Guid, double> branchValues = persistentStore.GetSongsBranchValues(activeBranch.DatabasePath, beatmapList);

                List<BeatmapInfo>? unresolvedBeatmaps = null;

                foreach (BeatmapInfo beatmap in beatmapList)
                {
                    if (beatmap.Ruleset.OnlineID != rulesetOnlineId.Value)
                        continue;

                    if (branchValues.TryGetValue(beatmap.ID, out double branchXxySr))
                    {
                        resolvedValues[beatmap.ID] = branchXxySr;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(beatmap.MD5Hash))
                        continue;

                    unresolvedBeatmaps ??= new List<BeatmapInfo>();
                    unresolvedBeatmaps.Add(beatmap);
                }

                if (unresolvedBeatmaps == null || unresolvedBeatmaps.Count == 0)
                    continue;

                IReadOnlySet<string> matchedMd5Hashes = persistentStore.GetSongsBranchCollectionMatchingMd5Hashes(activeBranch.DatabasePath,
                    unresolvedBeatmaps.Select(b => b.MD5Hash).Where(h => !string.IsNullOrWhiteSpace(h)));

                if (matchedMd5Hashes.Count == 0)
                    continue;

                foreach (BeatmapInfo beatmap in unresolvedBeatmaps)
                {
                    if (!matchedMd5Hashes.Contains(beatmap.MD5Hash))
                        continue;

                    resolvedValues[beatmap.ID] = 0;
                }
            }

            return resolvedValues.Count == 0 ? empty_xxy_sr_values : resolvedValues;
        }

        private bool tryApplyBranchHiddenState(EzAnalysisPersistentStore.SongsBranchDescriptor branch, IReadOnlyDictionary<string, List<BeatmapInfo>> localBeatmapsByMd5,
                                               IReadOnlyList<BeatmapInfo> branchBeatmaps, out LocalisableString message,
                                               out IReadOnlyList<BeatmapSetInfo> nonHideableBeatmapSets)
        {
            HashSet<Guid> hiddenByOtherBranches = getHiddenBeatmapIdsFromOtherSources(localBeatmapsByMd5, excludedBranchDatabasePath: branch.DatabasePath);
            var preexistingHiddenBeatmapIds = new HashSet<Guid>();
            var nonHideableBeatmapSetIds = new HashSet<Guid>();
            var nonHideableBeatmapSetList = new List<BeatmapSetInfo>();
            int hiddenCount = 0;
            int skippedCount = 0;

            foreach (BeatmapInfo beatmap in branchBeatmaps)
            {
                if (beatmap.Hidden)
                {
                    if (!hiddenByOtherBranches.Contains(beatmap.ID))
                        preexistingHiddenBeatmapIds.Add(beatmap.ID);

                    continue;
                }

                if (beatmapManager.Hide(beatmap))
                    hiddenCount++;
                else
                {
                    skippedCount++;

                    if (beatmap.BeatmapSet != null && nonHideableBeatmapSetIds.Add(beatmap.BeatmapSet.ID))
                        nonHideableBeatmapSetList.Add(beatmap.BeatmapSet);
                }
            }

            nonHideableBeatmapSets = nonHideableBeatmapSetList;

            if (!persistentStore.TrySetSongsBranchHideState(branch.DatabasePath, true, preexistingHiddenBeatmapIds))
            {
                nonHideableBeatmapSets = Array.Empty<BeatmapSetInfo>();
                message = SongsBranchStrings.HIDE_BRANCH_FAILED;
                return false;
            }

            message = skippedCount > 0
                ? LocalisableString.Format(SongsBranchStrings.HIDDEN_BRANCH_WITH_SKIPS, branch.Metadata.DisplayName, hiddenCount, skippedCount)
                : LocalisableString.Format(SongsBranchStrings.HIDDEN_BRANCH, branch.Metadata.DisplayName, hiddenCount);

            return true;
        }

        private bool tryRestoreBranchHiddenState(EzAnalysisPersistentStore.SongsBranchDescriptor branch, IReadOnlyDictionary<string, List<BeatmapInfo>> localBeatmapsByMd5,
                                                 IReadOnlyList<BeatmapInfo> branchBeatmaps, out LocalisableString message)
        {
            HashSet<Guid> keepHiddenBeatmapIds = getHiddenBeatmapIdsFromOtherSources(localBeatmapsByMd5, excludedBranchDatabasePath: branch.DatabasePath);

            foreach (Guid beatmapId in getPersistedPreexistingHiddenBeatmapIds())
                keepHiddenBeatmapIds.Add(beatmapId);

            int restoredCount = 0;

            foreach (BeatmapInfo beatmap in branchBeatmaps)
            {
                if (keepHiddenBeatmapIds.Contains(beatmap.ID) || !beatmap.Hidden)
                    continue;

                beatmapManager.Restore(beatmap);
                restoredCount++;
            }

            if (!persistentStore.TrySetSongsBranchHideState(branch.DatabasePath, false,
                    persistentStore.GetSongsBranchPreexistingHiddenBeatmapIds(branch.DatabasePath)))
            {
                message = SongsBranchStrings.RESTORE_BRANCH_HIDE_FAILED;
                return false;
            }

            message = LocalisableString.Format(SongsBranchStrings.RESTORED_BRANCH_HIDDEN, branch.Metadata.DisplayName, restoredCount);
            return true;
        }

        private bool tryApplyCollectionHiddenState(Guid collectionId, string collectionName, IEnumerable<string> beatmapMd5Hashes,
                                                   IReadOnlyDictionary<string, List<BeatmapInfo>> localBeatmapsByMd5, IReadOnlyList<BeatmapInfo> collectionBeatmaps,
                                                   out LocalisableString message, out IReadOnlyList<BeatmapSetInfo> nonHideableBeatmapSets)
        {
            HashSet<Guid> hiddenByOtherSources = getHiddenBeatmapIdsFromOtherSources(localBeatmapsByMd5, excludedCollectionId: collectionId);
            var preexistingHiddenBeatmapIds = new HashSet<Guid>();
            var nonHideableBeatmapSetIds = new HashSet<Guid>();
            var nonHideableBeatmapSetList = new List<BeatmapSetInfo>();
            int hiddenCount = 0;
            int skippedCount = 0;

            foreach (BeatmapInfo beatmap in collectionBeatmaps)
            {
                if (beatmap.Hidden)
                {
                    if (!hiddenByOtherSources.Contains(beatmap.ID))
                        preexistingHiddenBeatmapIds.Add(beatmap.ID);

                    continue;
                }

                if (beatmapManager.Hide(beatmap))
                    hiddenCount++;
                else
                {
                    skippedCount++;

                    if (beatmap.BeatmapSet != null && nonHideableBeatmapSetIds.Add(beatmap.BeatmapSet.ID))
                        nonHideableBeatmapSetList.Add(beatmap.BeatmapSet);
                }
            }

            nonHideableBeatmapSets = nonHideableBeatmapSetList;

            if (!persistentStore.TrySetCollectionHideState(collectionId, true, preexistingHiddenBeatmapIds, beatmapMd5Hashes))
            {
                nonHideableBeatmapSets = Array.Empty<BeatmapSetInfo>();
                message = SongsBranchStrings.HIDE_COLLECTION_FAILED;
                return false;
            }

            message = skippedCount > 0
                ? LocalisableString.Format(SongsBranchStrings.HIDDEN_COLLECTION_WITH_SKIPS, collectionName, hiddenCount, skippedCount)
                : LocalisableString.Format(SongsBranchStrings.HIDDEN_COLLECTION, collectionName, hiddenCount);

            return true;
        }

        private bool tryRestoreCollectionHiddenState(Guid collectionId, string collectionName, IReadOnlyDictionary<string, List<BeatmapInfo>> localBeatmapsByMd5,
                                                     IReadOnlyList<BeatmapInfo> collectionBeatmaps, out LocalisableString message)
        {
            HashSet<Guid> keepHiddenBeatmapIds = getHiddenBeatmapIdsFromOtherSources(localBeatmapsByMd5, excludedCollectionId: collectionId);

            foreach (Guid beatmapId in getPersistedPreexistingHiddenBeatmapIds())
                keepHiddenBeatmapIds.Add(beatmapId);

            int restoredCount = 0;

            foreach (BeatmapInfo beatmap in collectionBeatmaps)
            {
                if (keepHiddenBeatmapIds.Contains(beatmap.ID) || !beatmap.Hidden)
                    continue;

                beatmapManager.Restore(beatmap);
                restoredCount++;
            }

            if (!persistentStore.TrySetCollectionHideState(collectionId, false, persistentStore.GetCollectionPreexistingHiddenBeatmapIds(collectionId), Array.Empty<string>()))
            {
                message = SongsBranchStrings.RESTORE_COLLECTION_HIDE_FAILED;
                return false;
            }

            message = LocalisableString.Format(SongsBranchStrings.RESTORED_COLLECTION_HIDDEN, collectionName, restoredCount);
            return true;
        }

        private static IReadOnlyDictionary<string, List<BeatmapInfo>> createLocalBeatmapsByMd5Lookup(IEnumerable<BeatmapInfo> localBeatmaps)
        {
            var beatmapsByMd5 = new Dictionary<string, List<BeatmapInfo>>(StringComparer.OrdinalIgnoreCase);

            foreach (BeatmapInfo beatmap in localBeatmaps)
            {
                if (string.IsNullOrWhiteSpace(beatmap.MD5Hash))
                    continue;

                if (!beatmapsByMd5.TryGetValue(beatmap.MD5Hash, out List<BeatmapInfo>? beatmaps))
                {
                    beatmaps = new List<BeatmapInfo>();
                    beatmapsByMd5.Add(beatmap.MD5Hash, beatmaps);
                }

                if (beatmaps.All(existingBeatmap => existingBeatmap.ID != beatmap.ID))
                    beatmaps.Add(beatmap);
            }

            return beatmapsByMd5;
        }

        private List<BeatmapInfo> getLocalBeatmapsForBranchCollection(string databasePath, IReadOnlyDictionary<string, List<BeatmapInfo>> localBeatmapsByMd5)
        {
            IReadOnlySet<string> beatmapMd5Hashes = persistentStore.GetSongsBranchCollectionBeatmapMd5Hashes(databasePath);
            return getLocalBeatmapsForCollection(beatmapMd5Hashes, localBeatmapsByMd5);
        }

        private List<BeatmapInfo> getLocalBeatmapsForCollection(IEnumerable<string> beatmapMd5Hashes, IReadOnlyDictionary<string, List<BeatmapInfo>> localBeatmapsByMd5)
        {
            var branchBeatmaps = new Dictionary<Guid, BeatmapInfo>();

            foreach (string beatmapMd5 in beatmapMd5Hashes)
            {
                if (!localBeatmapsByMd5.TryGetValue(beatmapMd5, out List<BeatmapInfo>? beatmaps))
                    continue;

                foreach (BeatmapInfo beatmap in beatmaps)
                    branchBeatmaps.TryAdd(beatmap.ID, beatmap);
            }

            return branchBeatmaps.Values.ToList();
        }

        private HashSet<Guid> getHiddenBeatmapIdsFromOtherSources(IReadOnlyDictionary<string, List<BeatmapInfo>> localBeatmapsByMd5, string? excludedBranchDatabasePath = null,
                                                                  Guid? excludedCollectionId = null)
        {
            var hiddenBeatmapIds = new HashSet<Guid>();

            foreach (var branch in persistentStore.GetAvailableSongsBranches())
            {
                if (!branch.Metadata.HiddenApplied || string.Equals(branch.DatabasePath, excludedBranchDatabasePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (BeatmapInfo beatmap in getLocalBeatmapsForBranchCollection(branch.DatabasePath, localBeatmapsByMd5))
                    hiddenBeatmapIds.Add(beatmap.ID);
            }

            foreach (Guid collectionId in persistentStore.GetHiddenCollectionIds())
            {
                if (excludedCollectionId == collectionId)
                    continue;

                foreach (BeatmapInfo beatmap in getLocalBeatmapsForCollection(persistentStore.GetHiddenCollectionBeatmapMd5Hashes(collectionId), localBeatmapsByMd5))
                    hiddenBeatmapIds.Add(beatmap.ID);
            }

            return hiddenBeatmapIds;
        }

        private HashSet<Guid> getPersistedPreexistingHiddenBeatmapIds()
        {
            var hiddenBeatmapIds = new HashSet<Guid>();

            foreach (var branch in persistentStore.GetAvailableSongsBranches())
            {
                foreach (Guid beatmapId in persistentStore.GetSongsBranchPreexistingHiddenBeatmapIds(branch.DatabasePath))
                    hiddenBeatmapIds.Add(beatmapId);
            }

            foreach (Guid collectionId in persistentStore.GetHiddenCollectionIds())
            {
                foreach (Guid beatmapId in persistentStore.GetCollectionPreexistingHiddenBeatmapIds(collectionId))
                    hiddenBeatmapIds.Add(beatmapId);
            }

            return hiddenBeatmapIds;
        }

        private void activateSongsBranch(string databasePath, EzAnalysisPersistentStore.SongsBranchMetadata metadata)
        {
            string fullPath = Path.GetFullPath(databasePath);

            lock (activeSongsBranchStateLock)
            {
                activeSongsBranches.RemoveAll(branch => string.Equals(branch.DatabasePath, fullPath, StringComparison.OrdinalIgnoreCase));
                activeSongsBranches.Add(new ActiveSongsBranchState(fullPath, metadata.DisplayName, metadata.RulesetOnlineId, metadata.ModsFingerprint));

                updateActiveBranchBindablesLocked();
                activeSongsBranchVersion.Value++;
            }
        }

        private bool isActiveSongsBranch(string databasePath)
            => IsSongsBranchActive(databasePath);

        private bool removeActiveSongsBranch(string databasePath)
        {
            string fullPath = Path.GetFullPath(databasePath);

            lock (activeSongsBranchStateLock)
            {
                int removedCount = activeSongsBranches.RemoveAll(branch => string.Equals(branch.DatabasePath, fullPath, StringComparison.OrdinalIgnoreCase));

                if (removedCount <= 0)
                    return false;

                updateActiveBranchBindablesLocked();
                activeSongsBranchVersion.Value++;
                return true;
            }
        }

        private void updateActiveBranchBindablesLocked()
        {
            if (activeSongsBranches.Count == 0)
            {
                activeSongsBranchDisplayName.Value = null;
                return;
            }

            ActiveSongsBranchState lastActiveBranch = activeSongsBranches[^1];
            activeSongsBranchDisplayName.Value = activeSongsBranches.Count == 1
                ? lastActiveBranch.DisplayName
                : $"{lastActiveBranch.DisplayName} +{activeSongsBranches.Count - 1}";
        }

        private void deleteBranchesForSourceCollection(Guid sourceCollectionId, string keepDatabasePath)
        {
            if (sourceCollectionId == Guid.Empty)
                return;

            foreach (var existingBranch in persistentStore.GetAvailableSongsBranches())
            {
                if (existingBranch.Metadata.SourceCollectionId != sourceCollectionId
                    || string.Equals(existingBranch.DatabasePath, keepDatabasePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool wasActive = removeActiveSongsBranch(existingBranch.DatabasePath);

                if (persistentStore.DeleteSongsBranch(existingBranch.DatabasePath))
                    continue;

                if (wasActive)
                    activateSongsBranch(existingBranch.DatabasePath, existingBranch.Metadata);
            }
        }

        private static string createModsDisplay(IReadOnlyList<Mod>? mods)
        {
            var relevantMods = getXxySrRelevantMods(mods).ToList();
            return relevantMods.Count == 0 ? "NoMod" : string.Join('+', relevantMods.Select(m => m.Acronym));
        }

        private static string createModsJson(IReadOnlyList<Mod>? mods)
        {
            var apiMods = (mods ?? Array.Empty<Mod>()).Select(mod => new APIMod(mod)).ToArray();
            return JsonConvert.SerializeObject(apiMods);
        }

        private static string createModsProfileFingerprint(IEnumerable<Mod>? mods)
        {
            var relevantMods = getXxySrRelevantMods(mods).ToList();

            if (relevantMods.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();

            foreach (var mod in relevantMods.OrderBy(m => m.GetType().FullName, StringComparer.Ordinal))
            {
                builder.Append(mod.GetType().FullName);

                foreach (var setting in mod.SettingsBindables)
                {
                    builder.Append('|');
                    builder.Append(System.Text.Json.JsonSerializer.Serialize(setting.GetUnderlyingSettingValue()));
                }

                builder.Append(';');
            }

            return builder.ToString();
        }

        private static IEnumerable<Mod> getXxySrRelevantMods(IEnumerable<Mod>? mods)
        {
            if (mods == null)
                yield break;

            foreach (var mod in mods)
            {
                if (modAffectsXxySr(mod))
                    yield return mod;
            }
        }

        private static bool modAffectsXxySr(Mod mod)
            => mod is IApplicableToRate
                      or IApplicableToBeatmapConverter
                      or IApplicableAfterBeatmapConversion
                      or IApplicableToDifficulty
                      or IApplicableToBeatmapProcessor
                      or IApplicableToHitObject
                      or IApplicableToBeatmap;

        private static class SongsBranchStrings
        {
            internal static readonly EzLocalizationManager.EzLocalisableString SQLITE_DISABLED = new EzLocalizationManager.EzLocalisableString("Ez analysis sqlite 未启用。", "Ez analysis sqlite is disabled.");
            internal static readonly EzLocalizationManager.EzLocalisableString MANIA_ONLY = new EzLocalizationManager.EzLocalisableString("当前仅支持在 mania 下生成分支曲库。", "Generating branch libraries is currently only supported under mania.");
            internal static readonly EzLocalizationManager.EzLocalisableString EMPTY_FILTER_RESULT = new EzLocalizationManager.EzLocalisableString("当前筛选结果为空，未生成分支库。", "The current filtered result is empty. No branch sqlite was generated.");
            internal static readonly EzLocalizationManager.EzLocalisableString NO_WRITABLE_RESULTS = new EzLocalizationManager.EzLocalisableString("当前收藏夹里没有可写入分支曲库的 mania 谱面。", "The selected collection does not contain any mania beatmaps that can be written into the branch library.");
            internal static readonly EzLocalizationManager.EzLocalisableString GENERATED_AND_ACTIVATED = new EzLocalizationManager.EzLocalisableString("分支曲库已生成并启用。", "The branch library has been generated and activated.");
            internal static readonly EzLocalizationManager.EzLocalisableString GENERATED_ONLY = new EzLocalizationManager.EzLocalisableString("分支曲库已生成。", "The branch library has been generated.");
            internal static readonly EzLocalizationManager.EzLocalisableString INVALID_BRANCH = new EzLocalizationManager.EzLocalisableString("所选分支曲库无效。", "The selected branch library is invalid.");
            internal static readonly EzLocalizationManager.EzLocalisableString INVALID_COLLECTION = new EzLocalizationManager.EzLocalisableString("所选收藏夹无效。", "The selected collection is invalid.");
            internal static readonly EzLocalizationManager.EzLocalisableString ALREADY_ACTIVE_BRANCH = new EzLocalizationManager.EzLocalisableString("分支曲库已在启用列表中：{0}", "Branch library is already active: {0}");
            internal static readonly EzLocalizationManager.EzLocalisableString ACTIVATED_BRANCH = new EzLocalizationManager.EzLocalisableString("已启用分支曲库：{0}", "Activated branch library: {0}");
            internal static readonly EzLocalizationManager.EzLocalisableString DEACTIVATED_BRANCH = new EzLocalizationManager.EzLocalisableString("已停用分支曲库：{0}", "Deactivated branch library: {0}");
            internal static readonly EzLocalizationManager.EzLocalisableString DELETED_BRANCH = new EzLocalizationManager.EzLocalisableString("已删除分支曲库：{0}", "Deleted branch library: {0}");
            internal static readonly EzLocalizationManager.EzLocalisableString DELETE_BRANCH_FAILED = new EzLocalizationManager.EzLocalisableString("删除分支曲库失败。", "Failed to delete the branch library.");
            internal static readonly EzLocalizationManager.EzLocalisableString BRANCH_HIDE_NO_LOCAL_BEATMAPS = new EzLocalizationManager.EzLocalisableString("分支曲库“{0}”当前没有可处理的本地谱面。", "Branch library \"{0}\" currently has no local beatmaps to process.");
            internal static readonly EzLocalizationManager.EzLocalisableString HIDDEN_BRANCH = new EzLocalizationManager.EzLocalisableString("已对分支曲库“{0}”应用隐藏，隐藏 {1:#,0} 张。", "Applied hide for branch library \"{0}\". Hid {1:#,0} beatmaps.");
            internal static readonly EzLocalizationManager.EzLocalisableString HIDDEN_BRANCH_WITH_SKIPS = new EzLocalizationManager.EzLocalisableString("已对分支曲库“{0}”应用隐藏，隐藏 {1:#,0} 张，跳过 {2:#,0} 张。", "Applied hide for branch library \"{0}\". Hid {1:#,0} beatmaps and skipped {2:#,0}.");
            internal static readonly EzLocalizationManager.EzLocalisableString HIDE_BRANCH_FAILED = new EzLocalizationManager.EzLocalisableString("应用分支曲库隐藏失败。", "Failed to apply branch library hide.");
            internal static readonly EzLocalizationManager.EzLocalisableString RESTORED_BRANCH_HIDDEN = new EzLocalizationManager.EzLocalisableString("已取消分支曲库“{0}”的隐藏，恢复 {1:#,0} 张。", "Removed branch library hide for \"{0}\". Restored {1:#,0} beatmaps.");
            internal static readonly EzLocalizationManager.EzLocalisableString RESTORE_BRANCH_HIDE_FAILED = new EzLocalizationManager.EzLocalisableString("取消分支曲库隐藏失败。", "Failed to remove branch library hide.");
            internal static readonly EzLocalizationManager.EzLocalisableString HIDDEN_COLLECTION = new EzLocalizationManager.EzLocalisableString("已对收藏夹“{0}”应用隐藏，隐藏 {1:#,0} 张。", "Applied hide for collection \"{0}\". Hid {1:#,0} beatmaps.");
            internal static readonly EzLocalizationManager.EzLocalisableString HIDDEN_COLLECTION_WITH_SKIPS = new EzLocalizationManager.EzLocalisableString("已对收藏夹“{0}”应用隐藏，隐藏 {1:#,0} 张，跳过 {2:#,0} 张。", "Applied hide for collection \"{0}\". Hid {1:#,0} beatmaps and skipped {2:#,0}.");
            internal static readonly EzLocalizationManager.EzLocalisableString HIDE_COLLECTION_FAILED = new EzLocalizationManager.EzLocalisableString("应用收藏夹隐藏失败。", "Failed to apply collection hide.");
            internal static readonly EzLocalizationManager.EzLocalisableString RESTORED_COLLECTION_HIDDEN = new EzLocalizationManager.EzLocalisableString("已取消收藏夹“{0}”的隐藏，恢复 {1:#,0} 张。", "Removed collection hide for \"{0}\". Restored {1:#,0} beatmaps.");
            internal static readonly EzLocalizationManager.EzLocalisableString RESTORE_COLLECTION_HIDE_FAILED = new EzLocalizationManager.EzLocalisableString("取消收藏夹隐藏失败。", "Failed to remove collection hide.");
        }
    }
}
