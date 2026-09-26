// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.Mania;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class EzAnalysisPersistentStoreMemoTest
    {
        /// <summary>
        /// The song-select panel path memoises misses, so the memoised negative must not shadow a result that only
        /// exists as a pending (unflushed) write yet.
        /// </summary>
        [Test]
        public void MemoisedNegative_is_overlaid_by_a_later_pending_write()
        {
            withEnabledStore((store, beatmap) =>
            {
                Assert.That(store.TryGetMemoised(beatmap, out _), Is.False);

                store.Store(beatmap, createSlice(1.5, 3));

                Assert.That(store.TryGetMemoised(beatmap, out var stored), Is.True);
                Assert.That(stored.AverageKps, Is.EqualTo(1.5));
                Assert.That(stored.MaxKps, Is.EqualTo(3));
            });
        }

        /// <summary>
        /// A recomputation for the same chart replaces the memoised snapshot: the newer pending write wins over the
        /// value the memo already answered with.
        /// </summary>
        [Test]
        public void MemoisedPositive_is_replaced_by_a_newer_pending_write()
        {
            withEnabledStore((store, beatmap) =>
            {
                store.Store(beatmap, createSlice(1.5, 3));
                Assert.That(store.TryGetMemoised(beatmap, out var first), Is.True);
                Assert.That(first.MaxKps, Is.EqualTo(3));

                store.Store(beatmap, createSlice(2.5, 7));

                Assert.That(store.TryGetMemoised(beatmap, out var second), Is.True);
                Assert.That(second.AverageKps, Is.EqualTo(2.5));
                Assert.That(second.MaxKps, Is.EqualTo(7));
            });
        }

        private static void withEnabledStore(Action<EzAnalysisPersistentStore, BeatmapInfo> test)
        {
            using var storage = new TemporaryNativeStorage($"ez-analysis-memo-test-{Guid.NewGuid()}");

            bool previousEnabled = EzAnalysisPersistentStore.Enabled;
            EzAnalysisPersistentStore.Enabled = true;

            try
            {
                using var store = new EzAnalysisPersistentStore(storage);
                test(store, createBeatmap());
            }
            finally
            {
                EzAnalysisPersistentStore.Enabled = previousEnabled;
            }
        }

        private static EzAnalysisResult createSlice(double averageKps, double maxKps)
            => new EzAnalysisResult(
                new KpsSummary(averageKps, maxKps, new List<double> { averageKps, maxKps }),
                pp: null,
                new EzManiaSummary(new Dictionary<int, int> { [4] = 10 }, null, xxySr: null));

        private static BeatmapInfo createBeatmap() => new BeatmapInfo
        {
            ID = Guid.NewGuid(),
            Hash = Guid.NewGuid().ToString(),
            MD5Hash = Guid.NewGuid().ToString(),
            Ruleset = new ManiaRuleset().RulesetInfo,
            DifficultyName = "test",
        };
    }
}
