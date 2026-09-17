// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Rulesets;
using osu.Game.Tests.Database;
using Realms;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    /// <summary>
    /// What a play is waiting on is derived from the ledger against the chain's own coverage, so it must stop being
    /// reported exactly when the chain produces the row - and a chart the chain will never rate must never be debt.
    /// </summary>
    [TestFixture]
    public class EzChartChainDebtTest : RealmTest
    {
        private const string settled_chart = "debt-settled";
        private const string waiting_on_csi_chart = "debt-waiting-csi";
        private const string waiting_on_msd_chart = "debt-waiting-msd";
        private const string unsupported_chart = "debt-unsupported-keymode";
        private const string unrateable_with_csi_chart = "debt-unrateable-msd-with-csi";
        private const string osu_chart = "debt-osu-chart";
        private const string deleted_chart = "debt-beatmap-deleted";

        [Test]
        public void Debt_names_only_the_players_whose_plays_the_chain_owes_a_row_for()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);
                var store = new EzSkillStore(realm);

                // The two ends of the chain: fully rated, and rated up to MSD only.
                store.WriteBeatmapMsd(settled_chart, new EzSkillsetVector(10, 1, 2, 3, 4, 5, 6, 7), holdRatio: 0.2);
                store.UpsertChartSkillInfo(settled_chart, new EzChartSkillInfo { Patterns = new[] { "jack" }, KeyCount = 4, DanEligible = true });
                store.WriteBeatmapMsd(waiting_on_csi_chart, new EzSkillsetVector(10, 1, 2, 3, 4, 5, 6, 7), holdRatio: 0.2);

                var debt = EzChartChainDebt.Collect(new[]
                {
                    ("alpha", settled_chart),
                    ("alpha", waiting_on_csi_chart),
                    ("alpha", waiting_on_csi_chart),
                    ("alpha", waiting_on_msd_chart),
                    ("alpha", deleted_chart),
                    ("beta", waiting_on_msd_chart),
                    ("gamma", unsupported_chart),
                    ("gamma", osu_chart),
                }, store);

                Assert.That(debt.HasDebt, Is.True);
                Assert.That(debt.Usernames, Is.EquivalentTo(new[] { "alpha", "beta" }));

                // The same chart played twice is one thing to wait for.
                Assert.That(debt.PlaysFor("alpha"), Is.EqualTo(2));
                Assert.That(debt.WaitingOnCsiByUser["alpha"], Is.EqualTo(1));
                Assert.That(debt.WaitingOnMsdByUser["alpha"], Is.EqualTo(1));
                Assert.That(debt.WaitingOnMsdByUser["beta"], Is.EqualTo(1));
                Assert.That(debt.TotalPlays, Is.EqualTo(3));

                Assert.That(debt.PlaysFor("gamma"), Is.EqualTo(0),
                    "a chart the chain never rates, or whose beatmap is gone, is settled rather than waiting");
            });
        }

        [Test]
        public void Debt_disappears_when_the_chain_catches_up()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);
                var store = new EzSkillStore(realm);

                store.WriteBeatmapMsd(waiting_on_csi_chart, new EzSkillsetVector(10, 1, 2, 3, 4, 5, 6, 7), holdRatio: 0.2);

                var before = EzChartChainDebt.Collect(new[] { ("alpha", waiting_on_csi_chart) }, store);
                Assert.That(before.PlaysFor("alpha"), Is.EqualTo(1));

                store.UpsertChartSkillInfo(waiting_on_csi_chart, new EzChartSkillInfo { Patterns = new[] { "jack" }, KeyCount = 4, DanEligible = true });

                var after = EzChartChainDebt.Collect(new[] { ("alpha", waiting_on_csi_chart) }, store);
                Assert.That(after.HasDebt, Is.False);
                Assert.That(after.Usernames, Is.Empty);
                Assert.That(after.TotalPlays, Is.EqualTo(0));
            });
        }

        [Test]
        public void A_chart_settled_as_unrateable_is_not_debt_even_with_a_leftover_csi_row()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);
                var store = new EzSkillStore(realm);

                // The shape a pass used to leave behind: a full CSI row derived from the stub MSD axis. The chart
                // is settled for the whole chain, so reporting it as waiting is what kept re-queueing the chain.
                store.WriteBeatmapMsdUnrateable(unrateable_with_csi_chart, Guid.NewGuid());
                store.UpsertChartSkillInfo(unrateable_with_csi_chart, new EzChartSkillInfo { Patterns = new[] { "jack" }, KeyCount = 4, DanEligible = false });

                var debt = EzChartChainDebt.Collect(new[]
                {
                    ("alpha", unrateable_with_csi_chart),
                    ("beta", waiting_on_msd_chart),
                }, store);

                Assert.That(debt.PlaysFor("alpha"), Is.EqualTo(0));
                Assert.That(debt.Usernames, Is.EquivalentTo(new[] { "beta" }));
                Assert.That(debt.TotalPlays, Is.EqualTo(1));
            });
        }

        [Test]
        public void No_plays_means_no_debt()
        {
            RunTestWithRealm((realm, _) =>
            {
                seedCharts(realm);

                var debt = EzChartChainDebt.Collect(Array.Empty<(string, string)>(), new EzSkillStore(realm));

                Assert.That(debt.HasDebt, Is.False);
                Assert.That(debt.Usernames, Is.Empty);
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

                addChart(r, settled_chart, maniaRuleset, circleSize: 4);
                addChart(r, waiting_on_csi_chart, maniaRuleset, circleSize: 4);
                addChart(r, waiting_on_msd_chart, maniaRuleset, circleSize: 7);
                addChart(r, unsupported_chart, maniaRuleset, circleSize: 2);
                addChart(r, unrateable_with_csi_chart, maniaRuleset, circleSize: 4);
                addChart(r, osu_chart, osuRuleset, circleSize: 4);
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
