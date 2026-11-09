// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    /// <summary>
    /// A songs branch mirrors every analysis system, not only the Sunny star and the official pp. These pin that the
    /// mirrored values and the axis rows survive a write and a re-read, and that each carries the revision it was
    /// produced at - which is what lets a reader tell a current value from one written by an older algorithm.
    /// </summary>
    [TestFixture]
    public class EzSongsBranchMirrorTest
    {
        private static readonly Guid beatmap_a = Guid.NewGuid();
        private static readonly Guid beatmap_b = Guid.NewGuid();

        [Test]
        public void Scalar_ratings_round_trip_through_a_branch()
        {
            withBranch((store, path) =>
            {
                store.StoreSongsBranch(path, metadata(), new[]
                {
                    row(beatmap_a, "hash-a", sunnySr: 6.5, pp: 120,
                        officialSr: 4.75, minaSr: 7.25, minaPp: 310, pppSr: 5.5, pppPp: 205),
                    row(beatmap_b, "hash-b", sunnySr: 3.25, pp: null),
                });

                var rows = store.ReadAllSongsBranchRows(path);

                Assert.That(rows, Has.Count.EqualTo(2));

                var first = rows.Single(r => r.BeatmapId == beatmap_a);
                Assert.That(first.SunnySR, Is.EqualTo(6.5).Within(1e-9));
                Assert.That(first.Pp, Is.EqualTo(120).Within(1e-9));
                Assert.That(first.OfficialSr, Is.EqualTo(4.75).Within(1e-9));
                Assert.That(first.MinaSr, Is.EqualTo(7.25).Within(1e-9));
                Assert.That(first.MinaPp, Is.EqualTo(310).Within(1e-9));
                Assert.That(first.PppSr, Is.EqualTo(5.5).Within(1e-9));
                Assert.That(first.PppPp, Is.EqualTo(205).Within(1e-9));

                // A chart with no value keeps the column empty, which is how a reader asks "has this been computed".
                var second = rows.Single(r => r.BeatmapId == beatmap_b);
                Assert.That(second.Pp, Is.Null);
                Assert.That(second.MinaSr, Is.Null);
                Assert.That(second.PppPp, Is.Null);
            });
        }

        [Test]
        public void Axis_rows_round_trip_and_are_keyed_by_system()
        {
            withBranch((store, path) =>
            {
                store.StoreSongsBranch(path, metadata(), new[] { row(beatmap_a, "hash-a", 6.5, 120) },
                    new[]
                    {
                        new EzAnalysisPersistentStore.SongsBranchSkillRow(beatmap_a, EzAnalysisSystem.Mina.ToStorageId(), 3, "stream", 18.5, 20240101),
                        new EzAnalysisPersistentStore.SongsBranchSkillRow(beatmap_a, EzAnalysisSystem.CSI.ToStorageId(), 3, "ln_ratio", 0.42, 20240201),
                        // Dan's label is text, which is why the axis table carries a text column at all.
                        new EzAnalysisPersistentStore.SongsBranchSkillRow(beatmap_a, EzAnalysisSystem.Dan.ToStorageId(), 3, "rc.raw_dan", 9.5, 20240301),
                        new EzAnalysisPersistentStore.SongsBranchSkillRow(beatmap_a, EzAnalysisSystem.Dan.ToStorageId(), 3, "rc.label", 0, 20240301, "Shodan"),
                    });

                var rows = store.ReadSongsBranchSkillRows(path);

                Assert.That(rows.Select(r => r.SkillId), Is.EquivalentTo(new[] { "stream", "ln_ratio", "rc.raw_dan", "rc.label" }));

                var stream = rows.Single(r => r.SkillId == "stream");
                Assert.That(stream.SystemId, Is.EqualTo(EzAnalysisSystem.Mina.ToStorageId()));
                Assert.That(stream.Value, Is.EqualTo(18.5).Within(1e-9));
                Assert.That(stream.AlgorithmVersion, Is.EqualTo(20240101));

                var label = rows.Single(r => r.SkillId == "rc.label");
                Assert.That(label.TextValue, Is.EqualTo("Shodan"));
            });
        }

        [Test]
        public void Rewriting_axis_rows_replaces_the_previous_set()
        {
            withBranch((store, path) =>
            {
                store.StoreSongsBranch(path, metadata(), new[] { row(beatmap_a, "hash-a", 6.5, 120) },
                    new[] { new EzAnalysisPersistentStore.SongsBranchSkillRow(beatmap_a, EzAnalysisSystem.Mina.ToStorageId(), 3, "stream", 18.5, 20240101) });

                // An axis the system no longer produces must not survive as if it were current.
                store.UpdateSongsBranchRows(path, new[] { row(beatmap_a, "hash-a", 6.5, 120) }, null, null,
                    new[] { new EzAnalysisPersistentStore.SongsBranchSkillRow(beatmap_a, EzAnalysisSystem.Mina.ToStorageId(), 3, "jumpstream", 12.5, 20240101) });

                var rows = store.ReadSongsBranchSkillRows(path);

                Assert.That(rows.Select(r => r.SkillId), Is.EqualTo(new[] { "jumpstream" }));
            });
        }

        [Test]
        public void Mirrored_system_revisions_are_stamped_per_system()
        {
            withBranch((store, path) =>
            {
                store.StoreSongsBranch(path, metadata(), new[] { row(beatmap_a, "hash-a", 6.5, 120) });

                var versions = store.GetSongsBranchMirroredSystemVersions(path);

                foreach (var system in new[] { EzAnalysisSystem.Mina, EzAnalysisSystem.CSI, EzAnalysisSystem.Dan, EzAnalysisSystem.PPP })
                {
                    Assert.That(versions.ContainsKey(system.ToStorageId()), Is.True, system.ToStorageId());
                    Assert.That(versions[system.ToStorageId()], Is.EqualTo(system.CurrentVersion()));
                }
            });
        }

        [Test]
        public void An_updated_row_keeps_values_it_was_not_asked_to_change()
        {
            withBranch((store, path) =>
            {
                store.StoreSongsBranch(path, metadata(), new[] { row(beatmap_a, "hash-a", 6.5, 120, officialSr: 4.75, minaSr: 7.25) });

                // A refresh that only recomputed the Sunny star must not blank the mirrored columns.
                store.UpdateSongsBranchRows(path, new[] { row(beatmap_a, "hash-a", 7.5, 120) }, 20300101, null);

                var updated = store.ReadAllSongsBranchRows(path).Single();

                Assert.That(updated.SunnySR, Is.EqualTo(7.5).Within(1e-9));
                Assert.That(updated.OfficialSr, Is.EqualTo(4.75).Within(1e-9));
                Assert.That(updated.MinaSr, Is.EqualTo(7.25).Within(1e-9));
            });
        }

        private static void withBranch(Action<EzAnalysisPersistentStore, string> test)
        {
            using var storage = new TemporaryNativeStorage($"ez-songs-branch-mirror-{Guid.NewGuid()}");

            bool previousEnabled = EzAnalysisPersistentStore.Enabled;
            EzAnalysisPersistentStore.Enabled = true;

            try
            {
                using var store = new EzAnalysisPersistentStore(storage);
                string path = store.CreateSongsBranchDatabasePath("mania");
                test(store, path);
            }
            finally
            {
                EzAnalysisPersistentStore.Enabled = previousEnabled;
            }
        }

        private static EzAnalysisPersistentStore.SongsBranchMetadata metadata() => new EzAnalysisPersistentStore.SongsBranchMetadata(
            EzLocalProfileConstants.MANIA_RULESET_ID,
            "mania",
            "mods-fingerprint",
            "NoMod",
            2,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            "mirror test");

        private static EzAnalysisPersistentStore.SongsBranchRow row(Guid id, string hash, double sunnySr, double? pp,
                                                                    double? officialSr = null, double? minaSr = null, double? minaPp = null,
                                                                    double? pppSr = null, double? pppPp = null)
            => new EzAnalysisPersistentStore.SongsBranchRow(id, hash, "md5-" + hash, sunnySr, pp, officialSr, minaSr, minaPp, pppSr, pppPp);
    }
}
