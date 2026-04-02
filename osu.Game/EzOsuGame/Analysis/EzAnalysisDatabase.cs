// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
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

        private readonly EzAnalysisPersistentStore persistentStore;
        private readonly BeatmapManager beatmapManager;
        private readonly IBindable<bool> sqliteAnalysisEnabled;

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
    }
}
