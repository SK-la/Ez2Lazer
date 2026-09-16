// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Rulesets;
using osu.Game.Tests.Database;
using Realms;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    /// <summary>
    /// The status report must agree with what the backfill filters on: it is counted from the same
    /// <see cref="EzAnalysisRevision"/> stamps, so a bump has to show up as "stale".
    /// </summary>
    [TestFixture]
    public class EzSkillDataStatusTest : RealmTest
    {
        private const string mania_a = "status-mania-a";
        private const string mania_b = "status-mania-b";
        private const string mania_c = "status-mania-c";
        private const string osu_hash = "status-osu-chart";

        [Test]
        public void Empty_library_reports_everything_missing()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);
                var store = new EzSkillStore(realm);

                var status = store.GetSkillDataStatus();

                Assert.That(status.TotalCharts, Is.EqualTo(3));
                Assert.That(status.Msd.Missing, Is.EqualTo(3));
                Assert.That(status.ChartSkillInfo.Missing, Is.EqualTo(3));
                Assert.That(status.ChartDan.Missing, Is.EqualTo(3));
                Assert.That(status.HasWorkToDo, Is.True);
                Assert.That(status.TotalPending, Is.EqualTo(9));
            });
        }

        [Test]
        public void Counts_ready_unrateable_stale_and_missing_per_facet()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);
                var store = new EzSkillStore(realm);

                // MSD: a complete, b settled-unrateable, c missing.
                store.WriteBeatmapMsd(mania_a, new EzSkillsetVector(10, 1, 2, 3, 4, 5, 6, 7), holdRatio: 0.2);
                store.WriteBeatmapMsdUnrateable(mania_b, Guid.NewGuid());

                // CSI: a complete, b settled stub, c missing.
                store.UpsertChartSkillInfo(mania_a, new EzChartSkillInfo { Patterns = new[] { "jack" }, KeyCount = 4, DanEligible = true });
                store.WriteChartSkillInfoUnavailable(mania_b, Guid.NewGuid());

                // Dan: a and b current, c left behind by an older revision.
                store.UpsertChartDan(chartDan(mania_a));
                store.UpsertChartDan(chartDan(mania_b));

                realm.Write(r => r.Add(new EzBeatmapChartDan
                {
                    BeatmapHash = mania_c,
                    AlgorithmVersion = EzAnalysisRevision.ChartDan - 1,
                    KeyCount = 4,
                    HoldRatio = 0.1,
                    OverallMsd = 10,
                    RcRawDan = 5,
                    RcLabel = "Shodan",
                    LnRawDan = -1,
                    ComputedAt = DateTimeOffset.UtcNow,
                }));

                var status = store.GetSkillDataStatus();

                Assert.That(status.TotalCharts, Is.EqualTo(3));

                Assert.That(status.Msd.Ready, Is.EqualTo(1));
                Assert.That(status.Msd.Unrateable, Is.EqualTo(1));
                Assert.That(status.Msd.Stale, Is.EqualTo(0));
                Assert.That(status.Msd.Missing, Is.EqualTo(1));
                Assert.That(status.Msd.CurrentRevision, Is.EqualTo(EzAnalysisRevision.Msd));

                Assert.That(status.ChartSkillInfo.Ready, Is.EqualTo(1));
                Assert.That(status.ChartSkillInfo.Unrateable, Is.EqualTo(1));
                Assert.That(status.ChartSkillInfo.Missing, Is.EqualTo(1));
                Assert.That(status.ChartSkillInfo.CurrentRevision, Is.EqualTo(EzAnalysisRevision.ChartSkillInfo));

                Assert.That(status.ChartDan.Ready, Is.EqualTo(2));
                Assert.That(status.ChartDan.Unrateable, Is.EqualTo(0));
                Assert.That(status.ChartDan.Stale, Is.EqualTo(1));
                Assert.That(status.ChartDan.Missing, Is.EqualTo(0));
                Assert.That(status.ChartDan.CurrentRevision, Is.EqualTo(EzAnalysisRevision.ChartDan));

                // a is complete everywhere; b has MSD+CSI settled and a current Dan; c has only a stale Dan row.
                Assert.That(status.Msd.Pending, Is.EqualTo(1));
                Assert.That(status.ChartSkillInfo.Pending, Is.EqualTo(1));
                Assert.That(status.ChartDan.Pending, Is.EqualTo(1));
                Assert.That(status.TotalPending, Is.EqualTo(3));

                string described = status.Describe();
                Assert.That(described, Does.Contain("MSD v"));
                Assert.That(described, Does.Contain("CSI v"));
                Assert.That(described, Does.Contain("Dan v"));
            });
        }

        [Test]
        public void Non_mania_charts_and_their_rows_are_ignored()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);
                var store = new EzSkillStore(realm);

                // A non-mania chart with a full set of rows must not inflate the report either way.
                store.WriteBeatmapMsd(osu_hash, new EzSkillsetVector(10, 1, 2, 3, 4, 5, 6, 7), holdRatio: 0.2);
                store.UpsertChartSkillInfo(osu_hash, new EzChartSkillInfo { Patterns = new[] { "jack" }, KeyCount = 4, DanEligible = true });

                var status = store.GetSkillDataStatus();

                Assert.That(status.TotalCharts, Is.EqualTo(3));
                Assert.That(status.Msd.Ready, Is.EqualTo(0));
                Assert.That(status.Msd.Missing, Is.EqualTo(3));
                Assert.That(status.ChartSkillInfo.Ready, Is.EqualTo(0));
                Assert.That(status.ChartSkillInfo.Missing, Is.EqualTo(3));
            });
        }

        private static EzPersistedChartDan chartDan(string hash) => new EzPersistedChartDan
        {
            BeatmapHash = hash,
            KeyCount = 4,
            HoldRatio = 0.1,
            OverallMsd = 10,
            RcRawDan = 5,
            RcLabel = "Shodan",
            LnRawDan = -1,
            ComputedAt = DateTimeOffset.UtcNow,
        };

        /// <summary>
        /// The chain drops keymodes its engine cannot rate before MSD, so those charts are never work for it and
        /// must not be counted as missing - otherwise "all current" can never be reached.
        /// </summary>
        [Test]
        public void Charts_outside_the_engine_keymode_range_are_not_counted()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);

                realm.Write(r =>
                {
                    var maniaRuleset = r.All<RulesetInfo>().First(s => s.OnlineID == 3);
                    addChart(r, "status-mania-2k", maniaRuleset, circleSize: 2);
                    addChart(r, "status-mania-20k", maniaRuleset, circleSize: 20);
                });

                var store = new EzSkillStore(realm);
                var status = store.GetSkillDataStatus();

                Assert.That(status.TotalCharts, Is.EqualTo(3));
                Assert.That(status.Msd.Missing, Is.EqualTo(3));
                Assert.That(status.TotalPending, Is.EqualTo(9));
            });
        }

        /// <summary>
        /// A chart whose MSD settled as unrateable can never produce a ChartDan, so it is settled for that facet
        /// too rather than permanently pending.
        /// </summary>
        [Test]
        public void Msd_unrateable_chart_is_settled_for_dan()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);
                var store = new EzSkillStore(realm);

                store.WriteBeatmapMsdUnrateable(mania_b, Guid.NewGuid());

                var status = store.GetSkillDataStatus();

                Assert.That(status.Msd.Unrateable, Is.EqualTo(1));
                Assert.That(status.ChartDan.Ready, Is.EqualTo(0));
                Assert.That(status.ChartDan.Unrateable, Is.EqualTo(1));
                Assert.That(status.ChartDan.Missing, Is.EqualTo(2));
                Assert.That(status.HasWorkToDo, Is.True);
            });
        }

        /// <summary>
        /// The player-chain flag is per player, and it is what a manual compute reads to decide whose Realm rows to
        /// re-derive. A pass that covered a player must therefore be able to clear it wholesale: a row the pass could
        /// not re-derive (SSR values for a keymode whose plays are gone, say) has no later pass to pick it up, so
        /// leaving it set would keep the status readout warning forever.
        /// </summary>
        [Test]
        public void Stale_player_skill_flag_is_cleared_per_player_without_touching_values()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);

                store.WritePlayerSsr("alpha", 4, new EzSkillsetVector(10, 1, 2, 3, 4, 5, 6, 7), analyzedPlays: 5);
                store.WritePlayerSsr("beta", 4, new EzSkillsetVector(11, 1, 2, 3, 4, 5, 6, 7), analyzedPlays: 5);

                Assert.That(store.GetStalePlayerSkillUsernames(), Is.Empty);

                Assert.That(store.MarkPlayerSkillStale("alpha"), Is.GreaterThan(0));
                Assert.That(store.MarkPlayerSkillStale("beta"), Is.GreaterThan(0));
                Assert.That(store.GetStalePlayerSkillUsernames(), Is.EquivalentTo(new[] { "alpha", "beta" }));

                // One player's pass must not un-flag another's rows.
                Assert.That(store.ClearPlayerSkillStale("alpha"), Is.GreaterThan(0));
                Assert.That(store.GetStalePlayerSkillUsernames(), Is.EquivalentTo(new[] { "beta" }));

                // Flag already gone: nothing to write.
                Assert.That(store.ClearPlayerSkillStale("alpha"), Is.EqualTo(0));

                var snapshot = store.GetPlayerSsrSnapshot("alpha", 4);
                Assert.That(snapshot.Stale, Is.False);
                Assert.That(snapshot.Values, Is.Not.Empty);
                Assert.That(snapshot.Values.Values.Max(), Is.EqualTo(10).Within(1e-9));
            });
        }

        private static void seedCharts(RealmAccess realm)
        {
            realm.Write(r =>
            {
                var maniaRuleset = new RulesetInfo { OnlineID = 3, ShortName = "mania", Available = true };
                var osuRuleset = new RulesetInfo { OnlineID = 0, ShortName = "osu", Available = true };
                r.Add(maniaRuleset);
                r.Add(osuRuleset);

                addChart(r, mania_a, maniaRuleset, circleSize: 4);
                addChart(r, mania_b, maniaRuleset, circleSize: 4);
                addChart(r, mania_c, maniaRuleset, circleSize: 7);
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
