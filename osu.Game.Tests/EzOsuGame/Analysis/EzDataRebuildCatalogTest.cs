// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    /// <summary>
    /// The maintenance dialog's spacing is not hand-tuned: it reads the declared dependency edges. These lock the
    /// graph the layout encodes, so a new node cannot silently claim to be independent.
    /// </summary>
    [TestFixture]
    public class EzDataRebuildCatalogTest
    {
        [Test]
        public void The_chart_chain_is_dependent_and_its_input_leads()
        {
            var realm = EzDataRebuildCatalog.Realm;

            int msd = indexOf(realm, EzDataRebuildAction.RealmChartMsd);
            int csi = indexOf(realm, EzDataRebuildAction.RealmChartCsi);
            int dan = indexOf(realm, EzDataRebuildAction.RealmChartDan);

            Assert.That(msd, Is.LessThan(csi));
            Assert.That(csi, Is.LessThan(dan));

            Assert.That(realm[csi].ContinuesChain, Is.True);
            Assert.That(realm[dan].ContinuesChain, Is.True);
            Assert.That(realm[msd].ContinuesChain, Is.False);
        }

        [Test]
        public void Sunny_stands_alone_so_it_gets_the_wide_gap()
        {
            var realm = EzDataRebuildCatalog.Realm;
            var sunny = realm.Single(n => n.Action == EzDataRebuildAction.RealmSunny);

            Assert.That(sunny.ContinuesChain, Is.False);
            Assert.That(EzDataRebuildCatalog.GapBefore(sunny), Is.EqualTo(EzDataRebuildCatalog.INDEPENDENT_GAP));
            Assert.That(EzDataRebuildCatalog.INDEPENDENT_GAP, Is.GreaterThan(EzDataRebuildCatalog.CHAIN_GAP));
        }

        [Test]
        public void Songs_branches_continue_the_sqlite_chain()
        {
            var sqlite = EzDataRebuildCatalog.Sqlite;

            Assert.That(sqlite.Select(n => n.Action), Is.EqualTo(new[]
            {
                EzDataRebuildAction.SqliteMain,
                EzDataRebuildAction.SqliteSongsBranches,
            }));

            Assert.That(sqlite[0].ContinuesChain, Is.False);
            Assert.That(sqlite[1].ContinuesChain, Is.True);
            Assert.That(EzDataRebuildCatalog.GapBefore(sqlite[1]), Is.EqualTo(EzDataRebuildCatalog.CHAIN_GAP));
        }

        [Test]
        public void Every_action_has_exactly_one_node()
        {
            var actions = Enum.GetValues<EzDataRebuildAction>();

            Assert.That(EzDataRebuildCatalog.All.Count, Is.EqualTo(actions.Length));

            foreach (var action in actions)
                Assert.That(EzDataRebuildCatalog.Find(action)?.Action, Is.EqualTo(action), action.ToString());
        }

        [Test]
        public void Realm_nodes_declare_the_realm_side_and_sqlite_nodes_the_other()
        {
            foreach (var node in EzDataRebuildCatalog.Realm)
                Assert.That(node.Side, Is.EqualTo(EzDataRebuildSide.Realm));

            foreach (var node in EzDataRebuildCatalog.Sqlite)
                Assert.That(node.Side, Is.EqualTo(EzDataRebuildSide.Sqlite));
        }

        [Test]
        public void A_continuing_node_always_has_a_predecessor()
        {
            // Spacing is read off ContinuesChain alone, so the head of a chain must not claim to continue one.
            assertChainHeads(EzDataRebuildCatalog.Realm);
            assertChainHeads(EzDataRebuildCatalog.Sqlite);
        }

        private static void assertChainHeads(IReadOnlyList<EzDataRebuildNode> nodes)
            => Assert.That(nodes[0].ContinuesChain, Is.False, "the first node of a side has nothing above it");

        private static int indexOf(IReadOnlyList<EzDataRebuildNode> nodes, EzDataRebuildAction action)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Action == action)
                    return i;
            }

            Assert.Fail($"{action} is not in the chain.");
            return -1;
        }
    }
}
