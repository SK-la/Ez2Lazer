// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.EzOsuGame.Skills.SSR;
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
                var store = new EzChartSkillStore(realm);

                var status = store.GetSkillDataStatus();

                Assert.That(status.UniverseCount, Is.EqualTo(3));
                Assert.That(status["Mina"].Missing, Is.EqualTo(3));
                Assert.That(status["CSI"].Missing, Is.EqualTo(3));
                Assert.That(status["Dan"].Missing, Is.EqualTo(3));
                Assert.That(status.HasWorkToDo, Is.True);
                Assert.That(status["PPP"].Missing, Is.EqualTo(4));
                Assert.That(status.TotalPending, Is.EqualTo(13));
            });
        }

        [Test]
        public void Counts_ready_unrateable_stale_and_missing_per_facet()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);
                var store = new EzChartSkillStore(realm);

                // Mina: a complete, b settled-unrateable, c missing.
                store.WriteBeatmapMsd(mania_a, new EzMinaSkillAxisVector(10, 1, 2, 3, 4, 5, 6, 7));
                store.WriteBeatmapMsdUnrateable(mania_b, Guid.NewGuid());

                // CSI: a complete, b settled stub, c missing.
                store.UpsertChartSkillInfo(mania_a, new EzChartSkillInfo { Patterns = new[] { "jack" }, KeyCount = 4, DanEligible = true });
                store.WriteChartSkillInfoUnavailable(mania_b, Guid.NewGuid());

                // Dan: a and b have a row, c has none.
                store.UpsertChartDan(chartDan(mania_a));
                store.UpsertChartDan(chartDan(mania_b));

                // Rows only count once the systems have run at their current revision. SSR is part of that set: dan is
                // derived from the Sunny rating, so without it the dan rows are not results yet.
                markApplied(store, EzAnalysisSystem.Mina, EzAnalysisSystem.CSI, EzAnalysisSystem.Dan, EzAnalysisSystem.PPP, EzAnalysisSystem.SSR);
                EzSSRTuningSnapshot.Record();

                var status = store.GetSkillDataStatus();

                Assert.That(status.UniverseCount, Is.EqualTo(3));

                Assert.That(status["Mina"].Ready, Is.EqualTo(1));
                Assert.That(status["Mina"].Settled, Is.EqualTo(1));
                Assert.That(status["Mina"].Missing, Is.EqualTo(1));
                Assert.That(status["Mina"].CurrentRevision, Is.EqualTo(EzMinaEngine.VERSION));

                Assert.That(status["CSI"].Ready, Is.EqualTo(1));
                Assert.That(status["CSI"].Settled, Is.EqualTo(1));
                Assert.That(status["CSI"].Missing, Is.EqualTo(1));
                Assert.That(status["CSI"].CurrentRevision, Is.EqualTo(EzCSIEngine.VERSION));

                // Dan: a is ready, b is settled by its unrateable Mina (the row written for it is not read as a
                // result), c has no row.
                Assert.That(status["Dan"].Ready, Is.EqualTo(1));
                Assert.That(status["Dan"].Settled, Is.EqualTo(1));
                Assert.That(status["Dan"].Missing, Is.EqualTo(1));
                Assert.That(status["Dan"].CurrentRevision, Is.EqualTo(EzDanEngine.VERSION));

                // a is complete everywhere; b has Mina+CSI settled and a current Dan; c has only a stale Dan row.
                Assert.That(status["Mina"].Pending, Is.EqualTo(1));
                Assert.That(status["CSI"].Pending, Is.EqualTo(1));
                Assert.That(status["Dan"].Pending, Is.EqualTo(1));
                Assert.That(status["PPP"].Missing, Is.EqualTo(4));
                Assert.That(status.TotalPending, Is.EqualTo(7));

                string described = status.Describe();
                Assert.That(described, Does.Contain("Mina v"));
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
                var store = new EzChartSkillStore(realm);

                // A non-mania chart with a full set of rows must not inflate the report either way.
                store.WriteBeatmapMsd(osu_hash, new EzMinaSkillAxisVector(10, 1, 2, 3, 4, 5, 6, 7));
                store.UpsertChartSkillInfo(osu_hash, new EzChartSkillInfo { Patterns = new[] { "jack" }, KeyCount = 4, DanEligible = true });

                var status = store.GetSkillDataStatus();

                Assert.That(status.UniverseCount, Is.EqualTo(3));
                Assert.That(status["Mina"].Ready, Is.EqualTo(0));
                Assert.That(status["Mina"].Missing, Is.EqualTo(3));
                Assert.That(status["CSI"].Ready, Is.EqualTo(0));
                Assert.That(status["CSI"].Missing, Is.EqualTo(3));
            });
        }

        private static void markApplied(EzChartSkillStore store, params EzAnalysisSystem[] systems)
        {
            foreach (var system in systems)
                store.SetAppliedVersion(system, system.CurrentVersion());
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
        /// The chain drops keymodes its engine cannot rate before Mina, so those charts are never work for it and
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

                var store = new EzChartSkillStore(realm);
                var status = store.GetSkillDataStatus();

                Assert.That(status.UniverseCount, Is.EqualTo(3));
                Assert.That(status["Mina"].Missing, Is.EqualTo(3));
                Assert.That(status["PPP"].Missing, Is.EqualTo(6));
                Assert.That(status.TotalPending, Is.EqualTo(15));
            });
        }

        /// <summary>
        /// A chart whose Mina settled as unrateable can never produce a ChartDan, so it is settled for that facet
        /// too rather than permanently pending.
        /// </summary>
        [Test]
        public void Msd_unrateable_chart_is_settled_for_dan()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);
                var store = new EzChartSkillStore(realm);

                store.WriteBeatmapMsdUnrateable(mania_b, Guid.NewGuid());

                var status = store.GetSkillDataStatus();

                Assert.That(status["Mina"].Settled, Is.EqualTo(1));
                Assert.That(status["Dan"].Ready, Is.EqualTo(0));
                Assert.That(status["Dan"].Settled, Is.EqualTo(1));
                Assert.That(status["Dan"].Missing, Is.EqualTo(2));
                Assert.That(status.HasWorkToDo, Is.True);
            });
        }

        /// <summary>
        /// Player staleness is per player, derived from the drill ledger; a manual compute uses the same list. A pass
        /// that covered a player must therefore be able to clear it wholesale: a row the pass could
        /// not re-derive (SunnySR values for a keymode whose plays are gone, say) has no later pass to pick it up, so
        /// leaving it set would keep the status readout warning forever.
        /// </summary>
        [Test]
        public void Staleness_is_derived_from_the_ledger_not_stored()
        {
            RunTestWithRealm((_, storage) =>
            {
                var store = new EzPlayerSkillStore(storage);

                store.WritePlayerSsr("alpha", 4, new EzMinaSkillAxisVector(10, 1, 2, 3, 4, 5, 6, 7), analyzedPlays: 5);

                // No ledger rows behind the slice: the rows cannot be behind anything, so nothing reports stale.
                Assert.That(store.GetStalePlayerSkillUsernames(), Is.Empty);

                // The flag is gone; a write through it is a no-op rather than a second source of truth.
                Assert.That(store.SetPlayerSkillStale("alpha", true), Is.Zero);
                Assert.That(store.GetStalePlayerSkillUsernames(), Is.Empty);

                var snapshot = store.GetPlayerSsrSnapshot("alpha", 4);
                Assert.That(snapshot.Stale, Is.False);
                Assert.That(snapshot.Values, Is.Not.Empty);
                Assert.That(snapshot.Values.Values.Max(), Is.EqualTo(10).Within(1e-9));
            });
        }

        /// <summary>
        /// The readout lists the stale players with what the flag actually means: how much of their row set is flagged
        /// and when the values it is showing were written. A count alone cannot tell "behind by one play" from "values
        /// from a week ago", and the note has to be able to say which.
        /// </summary>
        [Test]
        public void Stale_player_details_carry_the_row_count_and_newest_write_time()
        {
            RunTestWithRealm((_, storage) =>
            {
                var store = new EzPlayerSkillStore(storage);

                store.WritePlayerSsr("alpha", 4, new EzMinaSkillAxisVector(10, 1, 2, 3, 4, 5, 6, 7), analyzedPlays: 5);
                store.WritePlayerSsr("beta", 4, new EzMinaSkillAxisVector(11, 1, 2, 3, 4, 5, 6, 7), analyzedPlays: 5);

                // Staleness is derived from the ledger, so a slice nobody has played since its write is current.
                Assert.That(store.GetStalePlayerSkillDetails(), Is.Empty);
            });
        }

        /// <summary>
        /// A CSI row written for a chart whose Mina later settled as unrateable came from the stub axis rather than
        /// from real Mina, so it must stop reading as a result the moment the verdict lands: otherwise the facet
        /// reports "ready" while Mina reports the same chart as unrateable, and the chain never revisits it.
        /// </summary>
        [Test]
        public void Csi_row_that_an_unrateable_msd_invalidates_is_settled_not_ready()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);
                var store = new EzChartSkillStore(realm);

                store.WriteBeatmapMsdUnrateable(mania_b, Guid.NewGuid());
                store.UpsertChartSkillInfo(mania_b, new EzChartSkillInfo { Patterns = new[] { "jack" }, KeyCount = 4, DanEligible = true });

                var status = store.GetSkillDataStatus();

                Assert.That(status["CSI"].Ready, Is.EqualTo(0));
                Assert.That(status["CSI"].Settled, Is.EqualTo(1));
                Assert.That(status["CSI"].Missing, Is.EqualTo(2));
                Assert.That(status["CSI"].Pending, Is.EqualTo(2));
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

            // The seeded library has been through a pass at the current revisions, which is what lets a row count
            // as a result rather than as work.
            markApplied(new EzChartSkillStore(realm), EzAnalysisSystem.Mina, EzAnalysisSystem.CSI, EzAnalysisSystem.Dan, EzAnalysisSystem.PPP);
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
