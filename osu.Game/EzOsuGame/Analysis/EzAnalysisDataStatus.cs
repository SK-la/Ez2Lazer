// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.EzOsuGame.Skills.SSR;
using osu.Game.Rulesets;
using Realms;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// Stored-data coverage of one ruleset.
    /// </summary>
    public sealed class EzRulesetAnalysisStatus
    {
        public required int RulesetOnlineId { get; init; }

        /// <summary>Display name from the ruleset store, or the id when the ruleset is unavailable.</summary>
        public required string RulesetName { get; init; }

        public required EzDataStateReport Report { get; init; }

        /// <summary>Charts of this ruleset the facets were counted over.</summary>
        public int ChartCount => Report.UniverseCount;
    }

    /// <summary>
    /// Coverage of each analysis system, grouped by ruleset.
    /// </summary>
    /// <remarks>
    /// Every facet here is a direct read of one store: the chart chain's three typed tables, the PPP skill rows, or
    /// a flattened rating column. There is no declaration layer between the store and this readout, so a system's
    /// presence is decided by the code that counts it.
    /// </remarks>
    public static class EzAnalysisDataStatus
    {
        private const string PPP_FACET = "PPP";
        private const string TAGS_FACET = "Tags";
        private const string OFFICIAL_FACET = "Official";
        private const string SUNNY_FACET = "Sunny";

        public static IReadOnlyList<EzRulesetAnalysisStatus> Collect(Realm realm, DateTimeOffset measuredAt)
        {
            ArgumentNullException.ThrowIfNull(realm);

            var chain = EzChartChainState.Build(realm);

            var rulesetNames = new Dictionary<int, string>();

            foreach (var ruleset in realm.All<RulesetInfo>().AsEnumerable())
                rulesetNames[ruleset.OnlineID] = string.IsNullOrWhiteSpace(ruleset.Name) ? ruleset.ShortName : ruleset.Name;

            var chartsByRuleset = new Dictionary<int, List<BeatmapInfo>>();

            foreach (var beatmap in realm.All<BeatmapInfo>().AsEnumerable())
            {
                if (beatmap.BeatmapSet == null || string.IsNullOrEmpty(beatmap.Hash))
                    continue;

                int rulesetId = beatmap.Ruleset.OnlineID;

                if (!chartsByRuleset.TryGetValue(rulesetId, out var charts))
                    chartsByRuleset[rulesetId] = charts = new List<BeatmapInfo>();

                charts.Add(beatmap);
            }

            if (chartsByRuleset.Count == 0)
                return Array.Empty<EzRulesetAnalysisStatus>();

            var skillRows = readSkillRows(realm, chartsByRuleset);
            var expired = readExpiredSystems(realm);

            var result = new List<EzRulesetAnalysisStatus>(chartsByRuleset.Count);

            foreach (int rulesetId in EzAxisIds.ALL_RULESETS)
            {
                if (!chartsByRuleset.TryGetValue(rulesetId, out var charts) || charts.Count == 0)
                    continue;

                var facets = new List<EzFacetStatus>();

                if (rulesetId == EzLocalProfileConstants.MANIA_RULESET_ID)
                    facets.AddRange(chain.Facets.Select(EzDataStateChecker.Count));

                if (pppFacetOf(rulesetId, charts, skillRows, expired.Contains(EzAnalysisSystem.PPP)) is { } ppp)
                    facets.Add(ppp);

                if (tagsFacetOf(charts) is { } tags)
                    facets.Add(tags);

                facets.AddRange(ratingFacetsOf(rulesetId, charts, expired));

                result.Add(new EzRulesetAnalysisStatus
                {
                    RulesetOnlineId = rulesetId,
                    RulesetName = rulesetNames.GetValueOrDefault(rulesetId, rulesetId.ToString()),
                    Report = new EzDataStateReport
                    {
                        UniverseCount = charts.Count,
                        Facets = facets,
                        MeasuredAt = measuredAt,
                    },
                });
            }

            return result;
        }

        /// <summary>PP+ rows of one ruleset, counted against the axis set that ruleset's catalog declares.</summary>
        /// <summary>
        /// The metadata tag columns of one ruleset. They carry no revision, so the only question the read path asks of
        /// them is whether they have been computed, and null is that question's answer.
        /// </summary>
        private static EzFacetStatus? tagsFacetOf(IReadOnlyList<BeatmapInfo> charts)
        {
            var filled = new Dictionary<string, EzFacetRowState>(charts.Count, StringComparer.Ordinal);

            foreach (var chart in charts)
            {
                if (chart.HasVideo.HasValue && chart.HasStoryboard.HasValue)
                    filled[chart.Hash] = new EzFacetRowState(false);
            }

            return EzDataStateChecker.Count(new EzDataStateFacet
            {
                Id = TAGS_FACET,
                CurrentRevision = 0,
                UniverseCount = charts.Count,
                Rows = filled,
            });
        }

        private static EzFacetStatus? pppFacetOf(
            int rulesetOnlineId, IReadOnlyList<BeatmapInfo> charts, IReadOnlyDictionary<(string SystemId, int RulesetOnlineId)
              , Dictionary<string, List<EzBeatmapSkillValue>>> skillRows, bool isStale)
        {
            var specs = EzPPPSkillCatalog.MetricsFor(rulesetOnlineId);

            if (specs.Count == 0)
                return null;

            var perHash = skillRows.GetValueOrDefault((EzAnalysisSystem.PPP.ToStorageId(), rulesetOnlineId));
            var states = new Dictionary<string, EzFacetRowState>(charts.Count, StringComparer.Ordinal);

            if (perHash != null)
            {
                foreach (var (hash, stored) in perHash)
                {
                    var values = new HashSet<string>(StringComparer.Ordinal);

                    foreach (var row in stored)
                    {
                        if ((EzSkillRowState)row.State == EzSkillRowState.Current)
                            values.Add(row.SkillId);
                    }

                    // A partial axis set is not a result: leaving the key out of the table makes it work to do.
                    if (specs.All(spec => values.Contains(spec.ChartSkillId)))
                        states[hash] = new EzFacetRowState(false);
                }
            }

            return EzDataStateChecker.Count(new EzDataStateFacet
            {
                Id = PPP_FACET,
                CurrentRevision = EzPPPEngine.VERSION,
                UniverseCount = charts.Count,
                Rows = states,
                IsStale = isStale,
            });
        }

        /// <summary>
        /// The stored star / pp columns of one ruleset. These carry no revision, so a facet is either filled in or
        /// absent: that is the question the read path asks of them.
        /// </summary>
        private static IEnumerable<EzFacetStatus> ratingFacetsOf(int rulesetOnlineId, IReadOnlyList<BeatmapInfo> charts, IReadOnlySet<EzAnalysisSystem> expired)
        {
            yield return columnFacet(OFFICIAL_FACET, charts, static b => b.StarRating, expired.Contains(EzAnalysisSystem.Official));

            if (rulesetOnlineId != EzLocalProfileConstants.MANIA_RULESET_ID)
                yield break;

            yield return columnFacet(SUNNY_FACET, charts, static b => b.EzRatingData.SunnySR, expired.Contains(EzAnalysisSystem.SSR));
        }

        private static EzFacetStatus columnFacet(string id, IReadOnlyList<BeatmapInfo> charts, Func<BeatmapInfo, double> valueOf, bool isStale)
        {
            var filled = new Dictionary<string, EzFacetRowState>(charts.Count, StringComparer.Ordinal);

            foreach (var chart in charts)
            {
                if (valueOf(chart) >= 0)
                    filled[chart.Hash] = new EzFacetRowState(false);
            }

            return EzDataStateChecker.Count(new EzDataStateFacet
            {
                Id = id,
                CurrentRevision = 0,
                UniverseCount = charts.Count,
                Rows = filled,
                IsStale = isStale,
            });
        }

        /// <summary>Systems whose applied revision is behind the one their algorithm declares.</summary>
        private static HashSet<EzAnalysisSystem> readExpiredSystems(Realm realm)
        {
            var expired = new HashSet<EzAnalysisSystem>();

            foreach (var system in new[] { EzAnalysisSystem.Official, EzAnalysisSystem.SSR, EzAnalysisSystem.PPP, EzAnalysisSystem.Mina, EzAnalysisSystem.CSI, EzAnalysisSystem.Dan })
            {
                int applied = realm.Find<EzAnalysisState>(system.ToStorageId())?.AppliedVersion ?? 0;

                if (applied != system.CurrentVersion())
                    expired.Add(system);
            }

            // The Sunny coefficients are part of its algorithm even though editing one never bumps the revision, so
            // the readout has to ask the same question the pass does. The mania chart-chain facets answer this for
            // themselves (the chain settles its own staleness), which is why only the rating column needs it here.
            if (EzSSRTuningSnapshot.IsPending)
                expired.Add(EzAnalysisSystem.SSR);

            return expired;
        }

        /// <summary>All skill-value rows of the scanned charts, grouped by system, ruleset and then chart.</summary>
        private static Dictionary<(string SystemId, int RulesetOnlineId), Dictionary<string, List<EzBeatmapSkillValue>>> readSkillRows(
            Realm realm,
            IReadOnlyDictionary<int, List<BeatmapInfo>> chartsByRuleset)
        {
            var hashes = new HashSet<string>(StringComparer.Ordinal);

            foreach (var charts in chartsByRuleset.Values)
            {
                foreach (var chart in charts)
                    hashes.Add(chart.Hash);
            }

            var rows = new Dictionary<(string SystemId, int RulesetOnlineId), Dictionary<string, List<EzBeatmapSkillValue>>>();

            foreach (var row in realm.All<EzBeatmapSkillValue>().AsEnumerable())
            {
                if (!hashes.Contains(row.BeatmapHash))
                    continue;

                var key = (row.SystemId, row.RulesetOnlineId);

                if (!rows.TryGetValue(key, out var byHash))
                    rows[key] = byHash = new Dictionary<string, List<EzBeatmapSkillValue>>(StringComparer.Ordinal);

                if (!byHash.TryGetValue(row.BeatmapHash, out var list))
                    byHash[row.BeatmapHash] = list = new List<EzBeatmapSkillValue>();

                list.Add(row);
            }

            return rows;
        }
    }
}
