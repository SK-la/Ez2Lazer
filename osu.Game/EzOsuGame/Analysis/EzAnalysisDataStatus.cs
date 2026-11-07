// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.Rulesets;
using Realms;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// Stored-data coverage of one ruleset, as the facets its analysis systems declare.
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
    /// Coverage of every analysis system, grouped by the ruleset that reads it out.
    /// </summary>
    /// <remarks>
    /// Facets come from the chart producer catalog rather than from a hand-written list, so a system added there shows
    /// up in the readout with no change here. Where a system's value lives is decided the same way the read path
    /// decides it: the skill-value table when the producer owns a storage namespace, the chart chain when the chain
    /// derives it, otherwise the flattened column on <see cref="BeatmapInfo"/>.
    /// <para>
    /// A column facet has no revision to compare against (nothing stamps those columns), so it reports two states
    /// only: filled in ("ready") or not ("missing"). That is the question the read path asks of them - a column is
    /// either a usable value or absent.
    /// </para>
    /// </remarks>
    public static class EzAnalysisDataStatus
    {
        public static IReadOnlyList<EzRulesetAnalysisStatus> Collect(Realm realm, DateTimeOffset measuredAt)
        {
            ArgumentNullException.ThrowIfNull(realm);

            var chain = EzChartChainState.Build(realm);
            var resolver = EzMetricRegistries.Current;

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

            foreach (int rulesetId in EzMetricRulesets.All)
            {
                if (!chartsByRuleset.TryGetValue(rulesetId, out var charts) || charts.Count == 0)
                    continue;

                var hashes = charts.Select(static c => c.Hash).ToHashSet(StringComparer.Ordinal);
                var facets = new List<EzFacetStatus>();

                foreach (var producer in chartProducersFor(resolver, rulesetId))
                {
                    if (facetOf(producer, rulesetId, charts, hashes, chain, skillRows, resolver) is { } facet)
                        facets.Add(facet);
                }

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

        /// <summary>Producers that apply to <paramref name="rulesetOnlineId"/>, in the catalog's stable order.</summary>
        private static IEnumerable<EzMetricProducer> chartProducersFor(EzMetricRevisionResolver resolver, int rulesetOnlineId)
            => resolver.ChartRegistry.All
                      .Where(p => p.AppliesToRuleset(rulesetOnlineId))
                      .OrderBy(static p => p.Tier)
                      .ThenBy(static p => p.Id.Id, StringComparer.Ordinal);

        private static EzFacetStatus? facetOf(
            EzMetricProducer producer,
            int rulesetOnlineId,
            IReadOnlyList<BeatmapInfo> charts,
            IReadOnlySet<string> hashes,
            EzChartChainState chain,
            IReadOnlyDictionary<(string SystemId, int RulesetOnlineId), Dictionary<string, List<EzBeatmapSkillValue>>> skillRows,
            EzMetricRevisionResolver resolver)
        {
            string id = producer.Id.Id;

            // The chain derives these three from the same rows, including the verdicts that settle a chart without a
            // row; re-counting them here would restate its rules.
            if (chainFacetIdOf(producer.Id) is { } chainFacet)
                return chain.Facets.FirstOrDefault(f => f.Id == chainFacet) is { } source ? withId(EzDataStateChecker.Count(source), id) : null;

            int revision = resolver.RevisionOf(producer.Id, rulesetOnlineId);

            if (!string.IsNullOrEmpty(producer.SystemId) && revision > 0)
            {
                var perHash = skillRows.GetValueOrDefault((producer.SystemId, rulesetOnlineId));
                var states = new Dictionary<string, EzFacetRowState>(charts.Count, StringComparer.Ordinal);

                if (perHash != null)
                {
                    foreach (var (hash, stored) in perHash)
                    {
                        if (!hashes.Contains(hash))
                            continue;

                        var values = new Dictionary<string, double>(StringComparer.Ordinal);
                        bool stub = false;

                        foreach (var row in stored)
                        {
                            if (row.AlgorithmVersion != revision)
                                continue;

                            if (row.State == (int)EzMetricState.Unrateable)
                            {
                                stub = true;
                                continue;
                            }

                            values[row.SkillId] = row.Value;
                        }

                        // A partial axis set is work, not a result: the same completeness rule the write path uses
                        // decides whether the rows describe the system. The producer declares that rule.
                        bool complete = producer.IsCompleteFor(EzSkillSystems.UNSLICED_KEY, rulesetOnlineId, values);

                        states[hash] = new EzFacetRowState(complete || stub ? revision : 0, stub);
                    }
                }

                return EzDataStateChecker.Count(new EzDataStateFacet
                {
                    Id = id,
                    Facet = declaredFacetOf(producer),
                    CurrentRevision = revision,
                    UniverseCount = charts.Count,
                    Rows = states,
                });
            }

            if (!EzMetricVariantReadout.HasColumn(producer.Id))
                return null;

            // Columns carry no stamp: a filled column is the answer, an empty one is the work.
            var filled = new Dictionary<string, EzFacetRowState>(charts.Count, StringComparer.Ordinal);

            foreach (var chart in charts)
            {
                if (EzMetricVariantReadout.ColumnValue(chart, producer.Id) >= 0)
                    filled[chart.Hash] = new EzFacetRowState(0, false);
            }

            return EzDataStateChecker.Count(new EzDataStateFacet
            {
                Id = id,
                Facet = declaredFacetOf(producer),
                CurrentRevision = 0,
                UniverseCount = charts.Count,
                Rows = filled,
            });
        }

        /// <summary>Chart-chain facet a producer's rows are counted as, or null when it owns its own storage.</summary>
        private static string? chainFacetIdOf(EzMetricProducerId producer)
            => producer == EzChartMetricProducer.Mina.Id() ? "MSD"
               : producer == EzChartMetricProducer.Csi.Id() ? "CSI"
               : producer == EzChartMetricProducer.Dan.Id() ? "Dan"
               : null;

        private static EzFacetStatus withId(EzFacetStatus source, string id)
            => new EzFacetStatus
            {
                Id = id,
                CurrentRevision = source.CurrentRevision,
                Ready = source.Ready,
                Settled = source.Settled,
                Stale = source.Stale,
                Missing = source.Missing,
            };

        private static EzAnalysisFacet declaredFacetOf(EzMetricProducer producer)
            => producer.Id == EzChartMetricProducer.Csi.Id() ? EzAnalysisFacet.ChartSkillInfo
               : producer.Id == EzChartMetricProducer.Dan.Id() ? EzAnalysisFacet.ChartDan
               : producer.Id == EzChartMetricProducer.Mina.Id() ? EzAnalysisFacet.Msd
               : producer.Id == EzChartMetricProducer.Ppp.Id() ? EzAnalysisFacet.PPP
               : EzAnalysisFacet.None;

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
