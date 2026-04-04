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
        private static int xxySrBranchComputeFailCount;
        private static readonly IReadOnlyDictionary<Guid, double> empty_xxy_sr_values = new Dictionary<Guid, double>();

        private readonly EzAnalysisPersistentStore persistentStore;
        private readonly BeatmapManager beatmapManager;
        private readonly IBindable<bool> sqliteAnalysisEnabled;
        private readonly Bindable<string?> activeXxySrBranchDisplayName = new Bindable<string?>();
        private readonly Bindable<string?> activeXxySrBranchPathBindable = new Bindable<string?>();
        private readonly Bindable<int> activeXxySrBranchVersion = new Bindable<int>();

        private string? activeXxySrBranchPath;
        private string? activeXxySrBranchModsFingerprint;
        private int? activeXxySrBranchRulesetOnlineId;

        public readonly record struct XxySrBranchBuildResult(
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

        public IBindable<string?> ActiveXxySrBranchDisplayName => activeXxySrBranchDisplayName;

        public IBindable<string?> ActiveXxySrBranchPath => activeXxySrBranchPathBindable;

        public IBindable<int> ActiveXxySrBranchVersion => activeXxySrBranchVersion;

        public bool HasActiveXxySrBranch => !string.IsNullOrEmpty(activeXxySrBranchPath);

        public bool HasActiveXxySrBranchFor(IRulesetInfo? rulesetInfo)
            => rulesetInfo?.OnlineID == 3
               && !string.IsNullOrEmpty(activeXxySrBranchPath)
               && activeXxySrBranchRulesetOnlineId == rulesetInfo.OnlineID;

        public IReadOnlyDictionary<Guid, double> GetActiveXxySrBranchValues(IEnumerable<BeatmapInfo> beatmaps, IRulesetInfo? rulesetInfo)
        {
            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled || !HasActiveXxySrBranchFor(rulesetInfo) || activeXxySrBranchPath == null)
                return empty_xxy_sr_values;

            return persistentStore.GetXxySrBranchValues(activeXxySrBranchPath, beatmaps);
        }

        public IReadOnlyDictionary<Guid, double> GetStoredXxySrValues(IEnumerable<BeatmapInfo> beatmaps, IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods = null)
        {
            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled)
                return empty_xxy_sr_values;

            var beatmapList = beatmaps.Distinct().ToList();

            if (beatmapList.Count == 0)
                return empty_xxy_sr_values;

            var resolvedValues = new Dictionary<Guid, double>(beatmapList.Count);

            if (HasActiveXxySrBranchFor(rulesetInfo) && activeXxySrBranchPath != null)
            {
                foreach (var kvp in persistentStore.GetXxySrBranchValues(activeXxySrBranchPath, beatmapList))
                    resolvedValues[kvp.Key] = kvp.Value;
            }

            var eligibleBeatmaps = beatmapList.Where(b => !resolvedValues.ContainsKey(b.ID) && CanUseStoredAnalysis(b, rulesetInfo, mods: null)).ToList();

            if (eligibleBeatmaps.Count == 0)
                return resolvedValues.Count == 0 ? empty_xxy_sr_values : resolvedValues;

            foreach (var kvp in persistentStore.GetStoredXxySrValues(eligibleBeatmaps))
                resolvedValues[kvp.Key] = kvp.Value;

            return resolvedValues.Count == 0 ? empty_xxy_sr_values : resolvedValues;
        }

        public bool IsActiveXxySrBranchFor(IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods)
        {
            if (rulesetInfo?.OnlineID != 3 || string.IsNullOrEmpty(activeXxySrBranchPath) || activeXxySrBranchRulesetOnlineId != rulesetInfo.OnlineID)
                return false;

            return string.Equals(activeXxySrBranchModsFingerprint, createModsProfileFingerprint(mods), StringComparison.Ordinal);
        }

        public void ActivateXxySrBranch(string databasePath, IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods, int beatmapCount = 0, string? displayName = null)
        {
            activateXxySrBranch(databasePath, new EzAnalysisPersistentStore.XxySrBranchMetadata(
                rulesetInfo?.OnlineID ?? 0,
                rulesetInfo?.ShortName ?? "ruleset",
                createModsProfileFingerprint(mods),
                createModsDisplay(mods),
                beatmapCount,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                displayName ?? createBranchDisplayName(rulesetInfo, mods, beatmapCount),
                createModsJson(mods)));
        }

        public void DeactivateXxySrBranch()
        {
            activeXxySrBranchPath = null;
            activeXxySrBranchRulesetOnlineId = null;
            activeXxySrBranchModsFingerprint = null;
            activeXxySrBranchDisplayName.Value = null;
            activeXxySrBranchPathBindable.Value = null;
            activeXxySrBranchVersion.Value++;
        }

        public Task<XxySrBranchBuildResult> CreateAndActivateXxySrBranchAsync(
            IEnumerable<BeatmapInfo> beatmaps, IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods,
            Action<int, int>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled)
                return Task.FromResult(new XxySrBranchBuildResult(false, XxySrBranchStrings.SQLITE_DISABLED, null, null, 0, 0));

            if (rulesetInfo is not RulesetInfo localRulesetInfo || localRulesetInfo.OnlineID != 3)
                return Task.FromResult(new XxySrBranchBuildResult(false, XxySrBranchStrings.MANIA_ONLY, null, null, 0, 0));

            var beatmapList = beatmaps.Distinct().ToList();

            if (beatmapList.Count == 0)
                return Task.FromResult(new XxySrBranchBuildResult(false, XxySrBranchStrings.EMPTY_FILTER_RESULT, null, null, 0, 0));

            return Task.Run(() =>
            {
                string modsFingerprint = createModsProfileFingerprint(mods);
                string modsDisplay = createModsDisplay(mods);
                string displayName = createBranchDisplayName(localRulesetInfo, mods, beatmapList.Count);
                long lastProgressReportAt = Environment.TickCount64;
                var metadata = new EzAnalysisPersistentStore.XxySrBranchMetadata(
                    localRulesetInfo.OnlineID,
                    localRulesetInfo.ShortName,
                    modsFingerprint,
                    modsDisplay,
                    beatmapList.Count,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    displayName,
                    createModsJson(mods));
                string databasePath = persistentStore.CreateXxySrBranchDatabasePath(metadata);
                var rows = new List<EzAnalysisPersistentStore.XxySrBranchRow>(beatmapList.Count);

                for (int i = 0; i < beatmapList.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var beatmap = beatmapList[i];
                    var lookup = new EzAnalysisLookupCache(beatmap, localRulesetInfo, mods);

                    try
                    {
                        if (EzAnalysisComputation.TryComputeXxySr(beatmapManager, lookup, cancellationToken, out double xxySr))
                            rows.Add(new EzAnalysisPersistentStore.XxySrBranchRow(beatmap.ID, beatmap.Hash, beatmap.MD5Hash, xxySr));
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
                        if (Interlocked.Increment(ref xxySrBranchComputeFailCount) <= 10)
                        {
                            Logger.Error(ex,
                                $"[EzAnalysisDatabase] CreateAndActivateXxySrBranchAsync failed. beatmapId={beatmap.ID} diff=\"{beatmap.DifficultyName}\" ruleset={localRulesetInfo.ShortName} mods={modsDisplay}",
                                Ez2ConfigManager.LOGGER_NAME);
                        }
                    }

                    long now = Environment.TickCount64;

                    if (i == 0 || i + 1 == beatmapList.Count || now - lastProgressReportAt >= 100)
                    {
                        lastProgressReportAt = now;
                        progress?.Invoke(i + 1, beatmapList.Count);
                    }
                }

                if (rows.Count == 0)
                    return new XxySrBranchBuildResult(false, XxySrBranchStrings.NO_WRITABLE_RESULTS, null, null, beatmapList.Count, 0);

                persistentStore.StoreXxySrBranch(databasePath, metadata, rows);
                activateXxySrBranch(databasePath, metadata);

                return new XxySrBranchBuildResult(true, XxySrBranchStrings.GENERATED_AND_ACTIVATED, databasePath, displayName, beatmapList.Count, rows.Count);
            }, cancellationToken);
        }

        public IReadOnlyList<EzAnalysisPersistentStore.XxySrBranchDescriptor> GetAvailableXxySrBranches(IRulesetInfo? rulesetInfo = null, IReadOnlyList<Mod>? mods = null, int maxCount = 0)
        {
            var branches = persistentStore.GetAvailableXxySrBranches();

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

        public bool TryActivateXxySrBranch(string databasePath, out LocalisableString message)
        {
            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled)
            {
                message = XxySrBranchStrings.SQLITE_DISABLED;
                return false;
            }

            if (!persistentStore.TryGetXxySrBranchDescriptor(databasePath, out var branch))
            {
                message = XxySrBranchStrings.INVALID_BRANCH;
                return false;
            }

            activateXxySrBranch(branch.DatabasePath, branch.Metadata);
            message = LocalisableString.Format(XxySrBranchStrings.ACTIVATED_BRANCH, branch.Metadata.DisplayName);
            return true;
        }

        public bool TryRenameXxySrBranch(string databasePath, string newDisplayName, out LocalisableString message, out EzAnalysisPersistentStore.XxySrBranchDescriptor renamedBranch)
        {
            renamedBranch = default;

            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled)
            {
                message = XxySrBranchStrings.SQLITE_DISABLED;
                return false;
            }

            if (string.IsNullOrWhiteSpace(newDisplayName))
            {
                message = XxySrBranchStrings.EMPTY_BRANCH_NAME;
                return false;
            }

            if (!persistentStore.TryRenameXxySrBranch(databasePath, newDisplayName, out renamedBranch))
            {
                message = XxySrBranchStrings.RENAME_BRANCH_FAILED;
                return false;
            }

            if (isActiveXxySrBranch(databasePath))
                activateXxySrBranch(renamedBranch.DatabasePath, renamedBranch.Metadata);

            message = LocalisableString.Format(XxySrBranchStrings.RENAMED_BRANCH, renamedBranch.Metadata.DisplayName);
            return true;
        }

        public bool TryDeleteXxySrBranch(string databasePath, out LocalisableString message)
        {
            if (!sqliteAnalysisEnabled.Value || !EzAnalysisPersistentStore.Enabled)
            {
                message = XxySrBranchStrings.SQLITE_DISABLED;
                return false;
            }

            if (!persistentStore.TryGetXxySrBranchDescriptor(databasePath, out var branch))
            {
                message = XxySrBranchStrings.INVALID_BRANCH;
                return false;
            }

            if (!persistentStore.DeleteXxySrBranch(branch.DatabasePath))
            {
                message = XxySrBranchStrings.DELETE_BRANCH_FAILED;
                return false;
            }

            if (isActiveXxySrBranch(branch.DatabasePath))
                DeactivateXxySrBranch();

            message = LocalisableString.Format(XxySrBranchStrings.DELETED_BRANCH, branch.Metadata.DisplayName);
            return true;
        }

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

        private static string createBranchDisplayName(IRulesetInfo? rulesetInfo, IReadOnlyList<Mod>? mods, int beatmapCount)
            => $"xxySR | {beatmapCount:#,0}diff | {createModsDisplay(mods)}";

        private void activateXxySrBranch(string databasePath, EzAnalysisPersistentStore.XxySrBranchMetadata metadata)
        {
            activeXxySrBranchPath = Path.GetFullPath(databasePath);
            activeXxySrBranchRulesetOnlineId = metadata.RulesetOnlineId;
            activeXxySrBranchModsFingerprint = metadata.ModsFingerprint;
            activeXxySrBranchDisplayName.Value = metadata.DisplayName;
            activeXxySrBranchPathBindable.Value = activeXxySrBranchPath;
            activeXxySrBranchVersion.Value++;
        }

        private bool isActiveXxySrBranch(string databasePath)
            => !string.IsNullOrEmpty(activeXxySrBranchPath)
               && string.Equals(Path.GetFullPath(databasePath), activeXxySrBranchPath, StringComparison.OrdinalIgnoreCase);

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

        private static class XxySrBranchStrings
        {
            internal static readonly EzLocalizationManager.EzLocalisableString SQLITE_DISABLED = new EzLocalizationManager.EzLocalisableString("Ez analysis sqlite 未启用。", "Ez analysis sqlite is disabled.");
            internal static readonly EzLocalizationManager.EzLocalisableString MANIA_ONLY = new EzLocalizationManager.EzLocalisableString("当前仅支持 mania xxySR 分支库。", "Only mania xxySR branch sqlite is supported.");
            internal static readonly EzLocalizationManager.EzLocalisableString EMPTY_FILTER_RESULT = new EzLocalizationManager.EzLocalisableString("当前筛选结果为空，未生成分支库。", "The current filtered result is empty. No branch sqlite was generated.");
            internal static readonly EzLocalizationManager.EzLocalisableString NO_WRITABLE_RESULTS = new EzLocalizationManager.EzLocalisableString("当前条件下没有可写入的 xxySR 结果。", "No writable xxySR results were produced for the current conditions.");
            internal static readonly EzLocalizationManager.EzLocalisableString GENERATED_AND_ACTIVATED = new EzLocalizationManager.EzLocalisableString("xxySR 分支库已生成并启用。", "The xxySR branch sqlite has been generated and activated.");
            internal static readonly EzLocalizationManager.EzLocalisableString INVALID_BRANCH = new EzLocalizationManager.EzLocalisableString("所选 xxySR 分支库无效。", "The selected xxySR branch sqlite is invalid.");
            internal static readonly EzLocalizationManager.EzLocalisableString ACTIVATED_BRANCH = new EzLocalizationManager.EzLocalisableString("已启用 xxySR 分支库：{0}", "Activated xxySR branch sqlite: {0}");
            internal static readonly EzLocalizationManager.EzLocalisableString EMPTY_BRANCH_NAME = new EzLocalizationManager.EzLocalisableString("分支库名称不能为空。", "Branch sqlite name cannot be empty.");
            internal static readonly EzLocalizationManager.EzLocalisableString RENAMED_BRANCH = new EzLocalizationManager.EzLocalisableString("已重命名 xxySR 分支库：{0}", "Renamed xxySR branch sqlite: {0}");
            internal static readonly EzLocalizationManager.EzLocalisableString RENAME_BRANCH_FAILED = new EzLocalizationManager.EzLocalisableString("重命名 xxySR 分支库失败。", "Failed to rename xxySR branch sqlite.");
            internal static readonly EzLocalizationManager.EzLocalisableString DELETED_BRANCH = new EzLocalizationManager.EzLocalisableString("已删除 xxySR 分支库：{0}", "Deleted xxySR branch sqlite: {0}");
            internal static readonly EzLocalizationManager.EzLocalisableString DELETE_BRANCH_FAILED = new EzLocalizationManager.EzLocalisableString("删除 xxySR 分支库失败。", "Failed to delete xxySR branch sqlite.");
        }
    }
}
