// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;
using osu.Game.Screens.Select;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// Feeds BMS <c>analytics.sqlite</c> rows into osu.Game panel EZ widgets.
    /// </summary>
    public sealed class BmsPanelEzAnalysisProvider : IPanelEzAnalysisProvider
    {
        private const string bms_ruleset_short_name = "bms";

        private BMSBeatmapManager? beatmapManager;
        private readonly Dictionary<string, BmsAnalyticsRecord> byPathKey = new Dictionary<string, BmsAnalyticsRecord>(StringComparer.OrdinalIgnoreCase);

        private BmsAnalyticsSqliteRepository? repository;

        /// <summary>
        /// Must be constructed before child drawables load so <c>[Cached]</c> can register a non-null instance.
        /// Call <see cref="BindBeatmapManager"/> from <c>BackgroundDependencyLoader</c> once <see cref="Storage"/> is available.
        /// </summary>
        public BmsPanelEzAnalysisProvider()
        {
        }

        public void BindBeatmapManager(BMSBeatmapManager manager) => beatmapManager = manager;

        public void AttachRepository(BmsAnalyticsSqliteRepository repository)
        {
            this.repository = repository;
            reloadFromRepository();
        }

        public void ReloadFromRepository() => reloadFromRepository();

        public bool SupportsEzDisplay(RulesetInfo ruleset)
            => string.Equals(ruleset.ShortName, bms_ruleset_short_name, StringComparison.OrdinalIgnoreCase);

        public IBindable<EzAnalysisResult> GetBindableAnalysis(BeatmapInfo beatmap, CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            var bindable = new Bindable<EzAnalysisResult>(default);

            if (TryGetStoredAnalysis(beatmap, out var result))
                bindable.Value = result;

            return bindable;
        }

        public bool TryGetStoredAnalysis(BeatmapInfo beatmap, out EzAnalysisResult result)
        {
            result = default;

            if (!SupportsEzDisplay(beatmap.Ruleset))
                return false;

            if (!tryResolvePathKey(beatmap, out string pathKey))
                return false;

            if (!byPathKey.TryGetValue(pathKey, out BmsAnalyticsRecord? record))
                return false;

            if (BmsAnalyticsEzConverter.ToEzAnalysisResult(record) is not EzAnalysisResult converted)
                return false;

            result = converted;
            return true;
        }

        private bool tryResolvePathKey(BeatmapInfo beatmap, out string pathKey)
        {
            foreach (string candidate in enumeratePathKeyCandidates(beatmap))
            {
                if (byPathKey.ContainsKey(candidate))
                {
                    pathKey = candidate;
                    return true;
                }
            }

            pathKey = string.Empty;
            return false;
        }

        private IEnumerable<string> enumeratePathKeyCandidates(BeatmapInfo beatmap)
        {
            if (!string.IsNullOrEmpty(beatmap.MD5Hash))
                yield return beatmap.MD5Hash;

            if (!string.IsNullOrEmpty(beatmap.Hash) && !string.Equals(beatmap.Hash, beatmap.MD5Hash, StringComparison.OrdinalIgnoreCase))
                yield return beatmap.Hash;

            if (beatmapManager != null && beatmapManager.TryGetSourceReference(beatmap.ID, out BMSSourceReference sourceRef))
            {
                if (!string.IsNullOrEmpty(sourceRef.Md5Hash))
                    yield return sourceRef.Md5Hash;

                if (!string.IsNullOrEmpty(sourceRef.ChartPath))
                    yield return BmsPathKeys.ComputeChartPathKey(sourceRef.ChartPath);
            }
        }

        private void reloadFromRepository()
        {
            byPathKey.Clear();

            if (repository == null)
                return;

            try
            {
                foreach (var (key, record) in repository.LoadAll())
                    byPathKey[key] = record;

                Logger.Log($"[BMS] Panel EZ analytics loaded {byPathKey.Count} row(s).", LoggingTarget.Database, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Log($"[BMS] Panel EZ analytics load failed: {ex.Message}", LoggingTarget.Database, LogLevel.Important);
            }
        }
    }
}
