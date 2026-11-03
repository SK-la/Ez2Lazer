// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class EzSongsBranchContentHashTest
    {
        [Test]
        public void SongsBranchHashStableForSameInputs()
        {
            var collectionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            string[] md5s = { "bbb", "aaa" };

            string first = EzAnalysisPersistentStore.ComputeSongsBranchContentHash(collectionId, 100, 2, md5s, 3, 4);
            string second = EzAnalysisPersistentStore.ComputeSongsBranchContentHash(collectionId, 100, 2, new[] { "AAA", "bbb" }, 3, 4);

            Assert.That(first, Is.EqualTo(second));
        }

        [Test]
        public void SongsBranchHashChangesWhenAlgorithmBumps()
        {
            var collectionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            string[] md5s = { "abc" };

            string baseline = EzAnalysisPersistentStore.ComputeSongsBranchContentHash(collectionId, 1, 1, md5s, 1, 1);
            string bumped = EzAnalysisPersistentStore.ComputeSongsBranchContentHash(collectionId, 1, 1, md5s, 2, 1);

            Assert.That(baseline, Is.Not.EqualTo(bumped));
        }

        [Test]
        public void CollectionHashChangesWhenMembershipChanges()
        {
            string first = EzAnalysisPersistentStore.ComputeCollectionContentHash(10, new[] { "a", "b" });
            string second = EzAnalysisPersistentStore.ComputeCollectionContentHash(10, new[] { "a", "c" });

            Assert.That(first, Is.Not.EqualTo(second));
        }
    }
}
