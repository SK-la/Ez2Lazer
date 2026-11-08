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

            var result = new List<EzRulesetAnalysisStatus>(chartsByRuleset.Count);

            foreach (int rulesetId in EzAnalysisChannels.ALL_RULESETS)
            {
                if (!chartsByRuleset.TryGetValue(rulesetId, out var charts) || charts.Count == 0)
                    continue;

                var facets = new List<EzFacetStatus>();

                if (rulesetId == EzLocalProfileConstants.MANIA_RULESET_ID)
                    facets.AddRange(chain.Facets.Select(EzDataStateChecker.Count));

                if (pppFacetOf(rulesetId, charts, skillRows) is { } ppp)
                    facets.Add(ppp);

                facets.AddRange(ratingFacetsOf(rulesetId, charts));

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
        private static EzFacetStatus? pppFacetOf(
            int rulesetOnlineId,
            IReadOnlyList<BeatmapInfo> charts,
            IReadOnlyDictionary<(string SystemId, int RulesetOnlineId), Dictionary<string, List<EzBeatmapSkillValue>>> skillRows)
        {
            var specs = EzPPPSkillCatalog.MetricsFor(rulesetOnlineId);

            if (specs.Count == 0)
                return null;

            int revision = EzAnalysisRevision.VERSION;
            var perHash = skillRows.GetValueOrDefault((EzSkillSystems.BEATMAP_PPP, rulesetOnlineId));
            var states = new Dictionary<string, EzFacetRowState>(charts.Count, StringComparer.Ordinal);

            if (perHash != null)
            {
                foreach (var (hash, stored) in perHash)
                {
                    var values = new HashSet<string>(StringComparer.Ordinal);

                    foreach (var row in stored)
                    {
                        if (row.AlgorithmVersion == revision && row.State == (int)EzSkillRowState.Current)
                            values.Add(row.SkillId);
                    }

                    bool complete = specs.All(spec => values.Contains(spec.ChartSkillId));
                    states[hash] = new EzFacetRowState(complete ? revision : 0, false);
                }
            }

            return EzDataStateChecker.Count(new EzDataStateFacet
            {
                Id = PPP_FACET,
                CurrentRevision = revision,
                UniverseCount = charts.Count,
                Rows = states,
            });
        }

        /// <summary>
        /// The stored star / pp columns of one ruleset. These carry no revision, so a facet is either filled in or
        /// absent: that is the question the read path asks of them.
        /// </summary>
        private static IEnumerable<EzFacetStatus> ratingFacetsOf(int rulesetOnlineId, IReadOnlyList<BeatmapInfo> charts)
        {
            yield return columnFacet(OFFICIAL_FACET, charts, static b => b.StarRating);

            if (rulesetOnlineId != EzLocalProfileConstants.MANIA_RULESET_ID)
                yield break;

            yield return columnFacet(SUNNY_FACET, charts, static b => b.EzRatingData.SunnySR);
        }

        private static EzFacetStatus columnFacet(string id, IReadOnlyList<BeatmapInfo> charts, Func<BeatmapInfo, double> valueOf)
        {
            var filled = new Dictionary<string, EzFacetRowState>(charts.Count, StringComparer.Ordinal);

            foreach (var chart in charts)
            {
                if (valueOf(chart) >= 0)
                    filled[chart.Hash] = new EzFacetRowState(0, false);
            }

            return EzDataStateChecker.Count(new EzDataStateFacet
            {
                Id = id,
                CurrentRevision = 0,
                UniverseCount = charts.Count,
                Rows = filled,
            });
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
