// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Beatmaps
{
    [HeadlessTest]
    public partial class WorkingBeatmapManagerTest : OsuTestScene
    {
        private BeatmapManager beatmaps = null!;

        private BeatmapSetInfo importedSet = null!;

        [BackgroundDependencyLoader]
        private void load(GameHost host, AudioManager audio, RulesetStore rulesets)
        {
            Dependencies.Cache(beatmaps = new BeatmapManager(LocalStorage, Realm, null, audio, Resources, host, Beatmap.Default));
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("import beatmap", () =>
            {
                beatmaps.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();
                importedSet = beatmaps.GetAllUsableBeatmapSets().First();
            });
        }

        [Test]
        public void TestGetWorkingBeatmap() => AddStep("run test", () =>
        {
            Assert.That(beatmaps.GetWorkingBeatmap(importedSet.Beatmaps.First()), Is.Not.Null);
        });

        [Test]
        public void TestCachedRetrievalNoFiles() => AddStep("run test", () =>
        {
            var beatmap = importedSet.Beatmaps.First();

            Assert.That(beatmap.BeatmapSet?.Files, Is.Empty);

            var first = beatmaps.GetWorkingBeatmap(beatmap);
            var second = beatmaps.GetWorkingBeatmap(beatmap);

            Assert.That(first, Is.SameAs(second));
            Assert.That(first.BeatmapInfo.BeatmapSet?.Files, Has.Count.GreaterThan(0));
        });

        [Test]
        public void TestCachedRetrievalWithFiles() => AddStep("run test", () =>
        {
            var beatmap = Realm.Run(r => r.Find<BeatmapInfo>(importedSet.Beatmaps.First().ID)!.Detach());

            Assert.That(beatmap.BeatmapSet?.Files, Has.Count.GreaterThan(0));

            var first = beatmaps.GetWorkingBeatmap(beatmap);
            var second = beatmaps.GetWorkingBeatmap(beatmap);

            Assert.That(first, Is.SameAs(second));
            Assert.That(first.BeatmapInfo.BeatmapSet?.Files, Has.Count.GreaterThan(0));
        });

        [Test]
        public void TestForcedRefetchRetrievalNoFiles() => AddStep("run test", () =>
        {
            var beatmap = importedSet.Beatmaps.First();

            Assert.That(beatmap.BeatmapSet?.Files, Is.Empty);

            var first = beatmaps.GetWorkingBeatmap(beatmap);
            var second = beatmaps.GetWorkingBeatmap(beatmap, true);
            Assert.That(first, Is.Not.SameAs(second));
        });

        [Test]
        public void TestForcedRefetchRetrievalWithFiles() => AddStep("run test", () =>
        {
            var beatmap = Realm.Run(r => r.Find<BeatmapInfo>(importedSet.Beatmaps.First().ID)!.Detach());

            Assert.That(beatmap.BeatmapSet?.Files, Has.Count.GreaterThan(0));

            var first = beatmaps.GetWorkingBeatmap(beatmap);
            var second = beatmaps.GetWorkingBeatmap(beatmap, true);
            Assert.That(first, Is.Not.SameAs(second));
        });

        [Test]
        public void TestSavePreservesCollections() => AddStep("run test", () =>
        {
            var beatmap = Realm.Run(r => r.Find<BeatmapInfo>(importedSet.Beatmaps.First().ID)!.Detach());

            var working = beatmaps.GetWorkingBeatmap(beatmap);

            Assert.That(working.BeatmapInfo.BeatmapSet?.Files, Has.Count.GreaterThan(0));

            string initialHash = working.BeatmapInfo.MD5Hash;

            var preserveCollection = new BeatmapCollection("test contained");
            preserveCollection.BeatmapMD5Hashes.Add(initialHash);

            var noNewCollection = new BeatmapCollection("test not contained");

            Realm.Write(r =>
            {
                r.Add(preserveCollection);
                r.Add(noNewCollection);
            });

            Assert.That(preserveCollection.BeatmapMD5Hashes, Does.Contain(initialHash));
            Assert.That(noNewCollection.BeatmapMD5Hashes, Does.Not.Contain(initialHash));

            beatmaps.Save(working.BeatmapInfo, working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo));

            string finalHash = working.BeatmapInfo.MD5Hash;

            Assert.That(finalHash, Is.Not.SameAs(initialHash));

            Assert.That(preserveCollection.BeatmapMD5Hashes, Does.Not.Contain(initialHash));
            Assert.That(preserveCollection.BeatmapMD5Hashes, Does.Contain(finalHash));
            Assert.That(noNewCollection.BeatmapMD5Hashes, Does.Not.Contain(finalHash));
        });

        [Test]
        public void TestDeleteDifficultiesPartiallyMatchedSet() => AddStep("delete one difficulty", () =>
        {
            BeatmapSetInfo set = createTwoDifficultySet();
            var deletedBeatmap = set.Beatmaps[0];
            var remainingBeatmap = set.Beatmaps[1];
            string deletedFileHash = deletedBeatmap.Hash;
            string sharedFileHash = Realm.Run(r => r.Find<BeatmapSetInfo>(set.ID)!.Files.Single(f => f.Filename == "audio.mp3").File.Hash);

            var result = beatmaps.DeleteDifficulties(new[] { deletedBeatmap.ID, deletedBeatmap.ID });

            Assert.That(result.PermanentlyDeletedDifficulties, Is.EqualTo(1));
            Assert.That(result.SoftDeletedSets, Is.Zero);
            Assert.That(result.SoftDeletedDifficulties, Is.Zero);

            Realm.Run(r =>
            {
                var storedSet = r.Find<BeatmapSetInfo>(set.ID)!;
                Assert.That(storedSet.DeletePending, Is.False);
                Assert.That(storedSet.Beatmaps.Select(b => b.ID), Is.EquivalentTo(new[] { remainingBeatmap.ID }));
                Assert.That(storedSet.Files.Any(f => f.File.Hash == deletedFileHash), Is.False);
                Assert.That(storedSet.Files.Any(f => f.File.Hash == sharedFileHash), Is.True);
            });
        });

        [Test]
        public void TestDeleteDifficultiesFullyMatchedSetIsRestorable() => AddStep("delete all difficulties", () =>
        {
            BeatmapSetInfo set = createTwoDifficultySet();
            var beatmapIds = set.Beatmaps.Select(b => b.ID).ToArray();
            int fileCount = Realm.Run(r => r.Find<BeatmapSetInfo>(set.ID)!.Files.Count);

            var result = beatmaps.DeleteDifficulties(beatmapIds);

            Assert.That(result.PermanentlyDeletedDifficulties, Is.Zero);
            Assert.That(result.SoftDeletedSets, Is.EqualTo(1));
            Assert.That(result.SoftDeletedDifficulties, Is.EqualTo(beatmapIds.Length));

            Realm.Run(r =>
            {
                var storedSet = r.Find<BeatmapSetInfo>(set.ID)!;
                Assert.That(storedSet.DeletePending, Is.True);
                Assert.That(storedSet.Beatmaps, Has.Count.EqualTo(beatmapIds.Length));
                Assert.That(storedSet.Files, Has.Count.EqualTo(fileCount));
            });

            beatmaps.UndeleteAll();

            Realm.Run(r => Assert.That(r.Find<BeatmapSetInfo>(set.ID)!.DeletePending, Is.False));
        });

        private BeatmapSetInfo createTwoDifficultySet()
        {
            var set = new BeatmapSetInfo();

            Realm.Write(r =>
            {
                var fileStore = new RealmFileStore(Realm, LocalStorage);
                var ruleset = r.All<RulesetInfo>().First();
                var firstFile = fileStore.Add(new MemoryStream(new byte[] { 1 }), r);
                var secondFile = fileStore.Add(new MemoryStream(new byte[] { 2 }), r);
                var sharedFile = fileStore.Add(new MemoryStream(new byte[] { 3 }), r);
                var first = new BeatmapInfo(ruleset, metadata: new BeatmapMetadata()) { Hash = firstFile.Hash, DifficultyName = "First", BeatmapSet = set };
                var second = new BeatmapInfo(ruleset, metadata: new BeatmapMetadata()) { Hash = secondFile.Hash, DifficultyName = "Second", BeatmapSet = set };

                set.Beatmaps.Add(first);
                set.Beatmaps.Add(second);
                set.Files.Add(new RealmNamedFileUsage(firstFile, "first.osu"));
                set.Files.Add(new RealmNamedFileUsage(secondFile, "second.osu"));
                set.Files.Add(new RealmNamedFileUsage(sharedFile, "audio.mp3"));
                r.Add(set);
            });

            return Realm.Run(r => r.Find<BeatmapSetInfo>(set.ID)!.Detach());
        }
    }
}
