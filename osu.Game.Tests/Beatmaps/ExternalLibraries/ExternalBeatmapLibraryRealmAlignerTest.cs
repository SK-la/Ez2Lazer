// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ExternalLibraries;
using osu.Game.Models;
using osu.Game.Rulesets;

namespace osu.Game.Tests.Beatmaps.ExternalLibraries
{
    [TestFixture]
    public class ExternalBeatmapLibraryRealmAlignerTest
    {
        private const string content_root = @"E:\songs\test-set";

        [Test]
        public void ApplySetDelta_preserves_analysis_fields_on_existing_difficulty()
        {
            var ruleset = new RulesetInfo { ShortName = "mania", Name = "osu!mania", OnlineID = 3 };
            var existing = buildExistingSet(ruleset, "chart.osu", starRating: 4.5, xxy: 5.5, pp: 120);

            var import = buildImport(ruleset, "chart.osu", title: "Updated Title", sha: existing.Beatmaps[0].Hash, md5: existing.Beatmaps[0].MD5Hash);

            BeatmapSetInfo aligned = ExternalBeatmapLibraryRealmAligner.ApplySetDeltaForTesting(existing, import, ruleset);
            BeatmapInfo beatmap = aligned.Beatmaps[0];

            Assert.Multiple(() =>
            {
                Assert.That(aligned.ID, Is.EqualTo(existing.ID));
                Assert.That(beatmap.StarRating, Is.EqualTo(4.5).Within(0.001));
                Assert.That(beatmap.XxyStarRating, Is.EqualTo(5.5).Within(0.001));
                Assert.That(beatmap.PerformancePoints, Is.EqualTo(120).Within(0.001));
                Assert.That(beatmap.Metadata.Title, Is.EqualTo("Updated Title"));
            });
        }

        [Test]
        public void ApplySetDelta_new_set_leaves_analysis_at_defaults()
        {
            var ruleset = new RulesetInfo { ShortName = "osu", Name = "osu!", OnlineID = 0 };
            var import = buildImport(ruleset, "Normal.osu", title: "New Map", sha: "a".PadLeft(64, 'a'), md5: "b".PadLeft(32, 'b'));

            BeatmapSetInfo aligned = ExternalBeatmapLibraryRealmAligner.ApplySetDeltaForTesting(null, import, ruleset);
            BeatmapInfo beatmap = aligned.Beatmaps[0];

            Assert.Multiple(() =>
            {
                Assert.That(aligned.ID, Is.EqualTo(import.SetId));
                Assert.That(aligned.HostingKind, Is.EqualTo(BeatmapSetHostingKind.External));
                Assert.That(beatmap.StarRating, Is.EqualTo(-1));
                Assert.That(beatmap.XxyStarRating, Is.EqualTo(-1));
                Assert.That(beatmap.PerformancePoints, Is.EqualTo(-1));
            });
        }

        [Test]
        public void ApplySetDelta_removes_difficulty_missing_from_catalog()
        {
            var ruleset = new RulesetInfo { ShortName = "taiko", Name = "osu!taiko", OnlineID = 1 };
            var existing = buildExistingSet(ruleset, "keep.osu", starRating: 3, xxy: 3, pp: 50);
            Guid keepId = existing.Beatmaps[0].ID;

            var obsolete = new BeatmapInfo(ruleset, new BeatmapDifficulty(), new BeatmapMetadata { Title = "Gone" })
            {
                ID = Guid.NewGuid(),
                Hash = "c".PadLeft(64, 'c'),
                MD5Hash = "d".PadLeft(32, 'd'),
                DifficultyName = "Gone",
                BeatmapSet = existing,
                StarRating = 9,
            };
            existing.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = obsolete.Hash }, "gone.osu"));
            existing.Beatmaps.Add(obsolete);

            var import = buildImport(ruleset, "keep.osu", title: "Keep", sha: existing.Beatmaps[0].Hash, md5: existing.Beatmaps[0].MD5Hash);
            BeatmapSetInfo aligned = ExternalBeatmapLibraryRealmAligner.ApplySetDeltaForTesting(existing, import, ruleset);

            Assert.That(aligned.Beatmaps.Count, Is.EqualTo(1));
            Assert.That(aligned.Beatmaps[0].ID, Is.EqualTo(keepId));
            Assert.That(aligned.Beatmaps[0].StarRating, Is.EqualTo(3).Within(0.001));
        }

        [Test]
        public void CatalogMatches_true_when_paths_and_hashes_align()
        {
            var ruleset = new RulesetInfo { ShortName = "catch", Name = "osu!catch", OnlineID = 2 };
            var existing = buildExistingSet(ruleset, "chart.osu", starRating: 1, xxy: 1, pp: 1);
            var import = buildImport(ruleset, "chart.osu", title: existing.Beatmaps[0].Metadata.Title,
                sha: existing.Beatmaps[0].Hash, md5: existing.Beatmaps[0].MD5Hash,
                difficultyName: existing.Beatmaps[0].DifficultyName);

            Assert.That(ExternalBeatmapLibraryRealmAligner.CatalogMatchesForTesting(existing, import), Is.True);
        }

        [Test]
        public void DeterministicGuid_stable_for_folder_and_ruleset()
        {
            Guid a = OsuFolderExternalLibraryScanner.DeterministicGuidForFolder(content_root, "mania");
            Guid b = OsuFolderExternalLibraryScanner.DeterministicGuidForFolder(content_root, "mania");
            Guid c = OsuFolderExternalLibraryScanner.DeterministicGuidForFolder(content_root, "osu");

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Is.Not.EqualTo(c));
        }

        private static BeatmapSetInfo buildExistingSet(RulesetInfo ruleset, string relativeChart, double starRating, double xxy, double pp)
        {
            string sha = "1".PadLeft(64, '1');
            string md5 = "2".PadLeft(32, '2');
            Guid setId = OsuFolderExternalLibraryScanner.DeterministicGuidForFolder(content_root, ruleset.ShortName);

            var set = new BeatmapSetInfo
            {
                ID = setId,
                Hash = ExternalBeatmapPathEncoding.Encode(content_root),
                ExternalContentRoot = content_root,
                HostingKind = BeatmapSetHostingKind.External,
                Status = BeatmapOnlineStatus.LocallyModified,
            };

            set.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = sha }, relativeChart));

            var beatmap = new BeatmapInfo(ruleset, new BeatmapDifficulty { CircleSize = 4 }, new BeatmapMetadata { Title = "Original" })
            {
                ID = Guid.NewGuid(),
                DifficultyName = "Normal",
                Hash = sha,
                MD5Hash = md5,
                BeatmapSet = set,
                StarRating = starRating,
                XxyStarRating = xxy,
                PerformancePoints = pp,
            };
            set.Beatmaps.Add(beatmap);
            return set;
        }

        private static ExternalBeatmapSetImportModel buildImport(
            RulesetInfo ruleset,
            string relativeChart,
            string title,
            string sha,
            string md5,
            string difficultyName = "Normal")
        {
            return new ExternalBeatmapSetImportModel
            {
                SetId = OsuFolderExternalLibraryScanner.DeterministicGuidForFolder(content_root, ruleset.ShortName),
                SetHash = ExternalBeatmapPathEncoding.Encode(content_root),
                ExternalContentRoot = content_root,
                Ruleset = ruleset,
                Files = new List<ExternalBeatmapFileEntry>
                {
                    new ExternalBeatmapFileEntry(relativeChart, sha),
                },
                Beatmaps = new List<ExternalBeatmapDifficultyImportModel>
                {
                    new ExternalBeatmapDifficultyImportModel
                    {
                        ChartRelativePath = relativeChart,
                        Md5Hash = md5,
                        Sha256Hash = sha,
                        Difficulty = new BeatmapDifficulty { CircleSize = 4 },
                        Metadata = new BeatmapMetadata { Title = title },
                        Ruleset = ruleset,
                        DifficultyName = difficultyName,
                        Length = 120,
                        BPM = 180,
                    },
                },
            };
        }
    }
}
