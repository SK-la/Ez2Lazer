// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    /// <summary>
    /// The generic state kernel is what makes the same counts work for a Realm table, a SQLite table or any other
    /// facet a store declares, so it is exercised with no store behind it at all.
    /// </summary>
    [TestFixture]
    public class EzDataStateTest
    {
        [Test]
        public void Counts_ready_settled_and_missing_from_row_states()
        {
            var facet = new EzDataStateFacet
            {
                Id = "Test",
                CurrentRevision = 7,
                UniverseCount = 6,
                Rows = new Dictionary<string, EzFacetRowState>
                {
                    ["ready"] = new EzFacetRowState(false),
                    ["stub"] = new EzFacetRowState(true),
                    ["ready-too"] = new EzFacetRowState(false),
                    // A row an upstream facet settled: it must not read as a result.
                    ["settled-upstream"] = new EzFacetRowState(false),
                },
                SettledWithoutRow = new[] { "settled-upstream", "keyless" },
            };

            var status = EzDataStateChecker.Count(facet);

            Assert.That(status.Id, Is.EqualTo("Test"));
            Assert.That(status.Ready, Is.EqualTo(2));
            Assert.That(status.Settled, Is.EqualTo(3), "stub + upstream-settled row + the key that never gets a row");
            Assert.That(status.Missing, Is.EqualTo(1));
            Assert.That(status.Pending, Is.EqualTo(1));
        }

        [Test]
        public void An_out_of_date_facet_owes_every_key()
        {
            var facet = new EzDataStateFacet
            {
                Id = "Test",
                CurrentRevision = 7,
                UniverseCount = 6,
                IsStale = true,
                Rows = new Dictionary<string, EzFacetRowState>
                {
                    ["ready"] = new EzFacetRowState(false),
                },
                SettledWithoutRow = new[] { "keyless" },
            };

            var status = EzDataStateChecker.Count(facet);

            Assert.That(status.Ready, Is.Zero);
            Assert.That(status.Settled, Is.EqualTo(1), "a key no pass can hold a row for stays settled");
            Assert.That(status.Missing, Is.EqualTo(5));
            Assert.That(status.IsStale, Is.True);
        }

        [Test]
        public void Report_indexes_facets_by_id_rather_than_fixed_properties()
        {
            var report = new EzDataStateReport
            {
                UniverseCount = 4,
                Facets = new[]
                {
                    new EzFacetStatus { Id = "Mina", CurrentRevision = 1, Ready = 4, Settled = 0, Missing = 0 },
                    // Any facet a store declares shows up here, named by itself.
                    new EzFacetStatus { Id = "KPS", CurrentRevision = 3, Ready = 1, Settled = 1, Missing = 1 },
                },
                MeasuredAt = DateTimeOffset.UnixEpoch,
            };

            Assert.That(report.HasWorkToDo, Is.True);
            Assert.That(report.TotalPending, Is.EqualTo(1));
            Assert.That(report["KPS"].CurrentRevision, Is.EqualTo(3));
            Assert.That(report.Describe(), Does.Contain("KPS v3: ready 1, settled 1, missing 1"));
            Assert.That(report.Describe(), Does.Contain("Mina v1: ready 4, settled 0, missing 0"));
            Assert.That(() => report["sunnySR"], Throws.TypeOf<KeyNotFoundException>());
        }
    }
}
