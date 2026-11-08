// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.Rulesets;
using osu.Game.Tests.Database;
using Realms;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    /// <summary>
    /// The status readout covers every ruleset's analysis systems, so a system's coverage is counted where that
    /// system actually stores it: the value table, the chart chain, or the beatmap column.
    /// </summary>
    [TestFixture]
    public class EzAnalysisDataStatusTest : RealmTest
    {
        private const string mania_hash = "status-mania-chart";
        private const string osu_hash = "status-osu-chart";

        [Test]
        public void Reports_one_section_per_ruleset_that_has_charts()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);
                var statuses = new EzChartSkillStore(realm).CollectAnalysisStatus();

                Assert.That(statuses.Select(s => s.RulesetOnlineId), Is.EqualTo(new[] { 0, 3 }));
                Assert.That(statuses.Select(s => s.RulesetName), Is.EqualTo(new[] { "osu", "mania" }));
                Assert.That(statuses.Select(s => s.ChartCount), Is.EqualTo(new[] { 1, 1 }));
            });
        }

        [Test]
        public void Each_ruleset_reports_only_the_systems_it_reads_out_under()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);
                var statuses = new EzChartSkillStore(realm).CollectAnalysisStatus();

                string[] osuFacets = statuses.Single(s => s.RulesetOnlineId == 0).Report.Facets.Select(f => f.Id).ToArray();
                string[] maniaFacets = statuses.Single(s => s.RulesetOnlineId == 3).Report.Facets.Select(f => f.Id).ToArray();

                // One facet per system: a system holds its star and its pp in one slot, so there is no separate
                // performance facet here.
                Assert.That(osuFacets, Is.EquivalentTo(new[] { "Official", "PPP" }));
                Assert.That(maniaFacets, Is.EquivalentTo(new[] { "Mina", "CSI", "Dan", "PPP", "Official", "Sunny" }));
            });
        }

        [Test]
        public void A_column_without_a_revision_only_distinguishes_filled_from_unfilled()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);

                realm.Write(r =>
                {
                    var beatmap = r.All<BeatmapInfo>().Single(b => b.Hash == mania_hash);
                    beatmap.EzRatingData.SunnySR = 6.5;
                });

                var mania = new EzChartSkillStore(realm).CollectAnalysisStatus().Single(s => s.RulesetOnlineId == 3);
                var sunny = mania.Report["Sunny"];

                Assert.That(sunny.Ready, Is.EqualTo(1));
                Assert.That(sunny.Missing, Is.Zero);
                Assert.That(sunny.CurrentRevision, Is.Zero);
                Assert.That(sunny.Pending, Is.Zero);

                Assert.That(mania.Report["Official"].Missing, Is.EqualTo(1));
                Assert.That(mania.Report["PPP"].Missing, Is.EqualTo(1));
            });
        }

        [Test]
        public void Value_table_rows_are_counted_under_their_own_ruleset()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);

                realm.Write(r =>
                {
                    foreach (var row in pppRows(osu_hash, 0))
                        r.Add(row);
                });

                var statuses = new EzChartSkillStore(realm).CollectAnalysisStatus();

                var osuPpp = statuses.Single(s => s.RulesetOnlineId == 0).Report["PPP"];
                var maniaPpp = statuses.Single(s => s.RulesetOnlineId == 3).Report["PPP"];

                Assert.That(osuPpp.Ready, Is.EqualTo(1));
                Assert.That(osuPpp.CurrentRevision, Is.EqualTo(EzPPPEngine.VERSION));
                Assert.That(maniaPpp.Ready, Is.Zero);
                Assert.That(maniaPpp.Missing, Is.EqualTo(1));
            });
        }

        [Test]
        public void A_partial_axis_set_is_work_not_a_result()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);

                realm.Write(r =>
                {
                    // One axis of a multi-axis vector: the write path's completeness rule still owes the rest.
                    var rows = pppRows(osu_hash, 0);
                    r.Add(rows[0]);
                });

                var osu = new EzChartSkillStore(realm).CollectAnalysisStatus().Single(s => s.RulesetOnlineId == 0);

                Assert.That(osu.Report["PPP"].Ready, Is.Zero);
                Assert.That(osu.Report["PPP"].Stale, Is.EqualTo(1));
                Assert.That(osu.Report["PPP"].Pending, Is.EqualTo(1));
            });
        }

        [Test]
        public void Chain_facets_report_under_the_producer_that_owns_them()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);

                realm.Write(r => r.Add(new EzChartDan
                {
                    BeatmapHash = mania_hash,
                    AlgorithmVersion = EzDanEngine.VERSION,
                    KeyCount = 4,
                    HoldRatio = 0.1,
                    OverallMsd = 10,
                    RcRawDan = 5,
                    RcLabel = "Shodan",
                    LnRawDan = -1,
                    ComputedAt = DateTimeOffset.UtcNow,
                }));

                var mania = new EzChartSkillStore(realm).CollectAnalysisStatus().Single(s => s.RulesetOnlineId == 3);

                Assert.That(mania.Report["Dan"].Ready, Is.EqualTo(1));
                Assert.That(mania.Report["Dan"].CurrentRevision, Is.EqualTo(EzDanEngine.VERSION));
                Assert.That(mania.Report["Mina"].Missing, Is.EqualTo(1));
            });
        }

        private static IReadOnlyList<EzBeatmapSkillValue> pppRows(string hash, int rulesetOnlineId)
        {
            var metrics = EzPPPSkillCatalog.MetricsFor(rulesetOnlineId);
            var rows = new List<EzBeatmapSkillValue>(metrics.Count);

            foreach (var metric in metrics)
            {
                rows.Add(new EzBeatmapSkillValue
                {
                    BeatmapHash = hash,
                    SystemId = EzAnalysisSystem.PPP.ToStorageId(),
                    RulesetOnlineId = rulesetOnlineId,
                    SkillId = EzPPPSkillCatalog.ChartSkillId(metric.MetricId),
                    Value = 4.2,
                    State = (int)EzSkillRowState.Current,
                    AlgorithmVersion = EzPPPEngine.VERSION,
                    ComputedAt = DateTimeOffset.UtcNow,
                });
            }

            return rows;
        }

        private static void seedCharts(RealmAccess realm)
        {
            realm.Write(r =>
            {
                var maniaRuleset = new RulesetInfo { OnlineID = 3, ShortName = "mania", Available = true };
                var osuRuleset = new RulesetInfo { OnlineID = 0, ShortName = "osu", Available = true };
                r.Add(maniaRuleset);
                r.Add(osuRuleset);

                addChart(r, mania_hash, maniaRuleset, circleSize: 4);
                addChart(r, osu_hash, osuRuleset, circleSize: 4);
            });
        }

        private static void addChart(Realm r, string hash, RulesetInfo ruleset, float circleSize)
        {
            var set = new BeatmapSetInfo();
            r.Add(set);

            r.Add(new BeatmapInfo
            {
                Hash = hash,
                BeatmapSet = set,
                Ruleset = ruleset,
                DifficultyName = hash,
                Difficulty = new BeatmapDifficulty { CircleSize = circleSize },
            });
        }
    }
}
