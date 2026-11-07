// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class EzAnalysisLiveCacheTest
    {
        [Test]
        public void EvictionKeepsTheMostRecentlyUsedEntries()
        {
            var cache = new EzAnalysisLiveCache();

            for (int i = 0; i < 512; i++)
                cache.Set(keyOf(i), result());

            // Touch the oldest key so recency, not insertion order, decides what is dropped.
            Assert.That(cache.Get(keyOf(0)), Is.Not.Null);

            cache.Set(keyOf(512), result());

            Assert.That(cache.Get(keyOf(0)), Is.Not.Null, "recently read entry was evicted");
            Assert.That(cache.Get(keyOf(1)), Is.Null, "oldest untouched entry survived");
            Assert.That(cache.Get(keyOf(512)), Is.Not.Null, "new entry was not stored");
        }

        [Test]
        public void EvictionNeverDropsTheWholeTable()
        {
            var cache = new EzAnalysisLiveCache();

            for (int i = 0; i < 512; i++)
                cache.Set(keyOf(i), result());

            cache.Set(keyOf(512), result());

            int survivors = 0;

            for (int i = 0; i <= 512; i++)
            {
                if (cache.Get(keyOf(i)) != null)
                    survivors++;
            }

            Assert.That(survivors, Is.GreaterThan(256), "eviction should drop only the oldest quarter");
        }

        [Test]
        public void InvalidateSubjectDropsOnlyMatchingHash()
        {
            var cache = new EzAnalysisLiveCache();

            cache.Set(new EzAnalysisLiveCache.AnalysisKey("a", 3, 0), result());
            cache.Set(new EzAnalysisLiveCache.AnalysisKey("a", 3, 1), result());
            cache.Set(new EzAnalysisLiveCache.AnalysisKey("b", 3, 0), result());

            cache.InvalidateSubject("a");

            Assert.That(cache.Get(new EzAnalysisLiveCache.AnalysisKey("a", 3, 0)), Is.Null);
            Assert.That(cache.Get(new EzAnalysisLiveCache.AnalysisKey("a", 3, 1)), Is.Null);
            Assert.That(cache.Get(new EzAnalysisLiveCache.AnalysisKey("b", 3, 0)), Is.Not.Null);
        }

        private static EzAnalysisLiveCache.AnalysisKey keyOf(int index) => new EzAnalysisLiveCache.AnalysisKey($"hash-{index}", 3, 0);

        private static EzAnalysisResult result() => new EzAnalysisResult(new KpsSummary(1, 2, new[] { 1.0, 2.0 }));
    }
}
