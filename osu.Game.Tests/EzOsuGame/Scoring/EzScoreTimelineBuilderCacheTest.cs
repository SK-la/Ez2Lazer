// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Scoring;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    [TestFixture]
    public class EzScoreTimelineBuilderCacheTest
    {
        [Test]
        public void TestSessionCacheIsolation()
        {
            var cacheA = EzScoreTimelineBuilder.CreateSessionCache();
            var cacheB = EzScoreTimelineBuilder.CreateSessionCache();

            // 两个 Session 独立 cache：A 写一条不应被 B 看到。
            cacheA.Store("key:1", EzScoreTimeline.EMPTY);
            Assert.That(cacheA.TryGet("key:1", out _), Is.True);
            Assert.That(cacheB.TryGet("key:1", out _), Is.False);
        }

        [Test]
        public void TestEmptySentinelStoredAsEmpty()
        {
            // 模拟"无 replay"路径：Store(null) 后第二次读取能拿到 Empty sentinel，
            // 而不是被当作"未缓存"重新走重建。
            var cache = EzScoreTimelineBuilder.CreateSessionCache();
            cache.Store("k", null);

            Assert.That(cache.TryGet("k", out var cached), Is.True);
            Assert.That(cached, Is.SameAs(EzScoreTimeline.EMPTY));
        }

        [Test]
        public void TestSessionDisposeClearsCache()
        {
            var cache = EzScoreTimelineBuilder.CreateSessionCache();
            cache.Store("k", EzScoreTimeline.EMPTY);

            Assert.That(cache.TryGet("k", out _), Is.True);
            cache.Clear();
            Assert.That(cache.TryGet("k", out _), Is.False);
        }

        [Test]
        public void TestNullCacheIsNoop()
        {
            // NullEzScoreTimelineCache 不能抛异常，所有调用都是 no-op（命中 / 写入均不报错）。
            var cache = NullEzScoreTimelineCache.INSTANCE;

            Assert.DoesNotThrow(() => cache.Store("k", null));
            Assert.DoesNotThrow(() => cache.Store("k", EzScoreTimeline.EMPTY));
            Assert.That(cache.TryGet("k", out _), Is.False);
        }

        [Test]
        public void TestEmptyKeyIgnored()
        {
            var cache = EzScoreTimelineBuilder.CreateSessionCache();
            cache.Store(string.Empty, EzScoreTimeline.EMPTY);
            cache.TryGet(string.Empty, out _);

            Assert.That(cache.TryGet(string.Empty, out _), Is.False);
        }
    }
}
