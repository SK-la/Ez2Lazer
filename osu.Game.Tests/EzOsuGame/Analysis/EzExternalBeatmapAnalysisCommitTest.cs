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
    public class EzExternalBeatmapAnalysisCommitTest
    {
        [Test]
        public void StoreNoModSlice_round_trips_through_persistent_store()
        {
            using var storage = new TemporaryNativeStorage("ez-external-commit-test");

            bool previousEnabled = EzAnalysisPersistentStore.Enabled;
            EzAnalysisPersistentStore.Enabled = true;

            try
            {
                var persistentStore = new EzAnalysisPersistentStore(storage);
                var beatmap = createBeatmap();

                var kpsList = new List<double> { 1, 2, 3 };
                var slice = new EzAnalysisResult(new KpsSummary(1.5, 3, kpsList), pp: null, new EzManiaSummary(new Dictionary<int, int> { [4] = 10 }, null, xxySr: null));

                persistentStore.Store(beatmap, slice);

                Assert.That(persistentStore.TryGet(beatmap, out var stored), Is.True);
                Assert.That(stored.AverageKps, Is.EqualTo(1.5));
                Assert.That(stored.MaxKps, Is.EqualTo(3));
                Assert.That(stored.KpsList, Is.EqualTo(kpsList));
                Assert.That(stored.ManiaSummary?.ColumnCounts[4], Is.EqualTo(10));
            }
            finally
            {
                EzAnalysisPersistentStore.Enabled = previousEnabled;
            }
        }

        [Test]
        public void HasStorableNoModSlice_requires_kps_or_column_counts()
        {
            var empty = new EzAnalysisResult(new KpsSummary(0, 0, Array.Empty<double>()));
            Assert.That(EzSongSelectAnalysisDisplay.HasDisplayableKps(empty), Is.False);

            var withKps = new EzAnalysisResult(new KpsSummary(1, 2, new[] { 1.0 }));
            Assert.That(EzSongSelectAnalysisDisplay.HasDisplayableKps(withKps), Is.True);
        }

        private static BeatmapInfo createBeatmap()
        {
            var ruleset = new ManiaRuleset().RulesetInfo;
            return new BeatmapInfo
            {
                ID = Guid.NewGuid(),
                Hash = Guid.NewGuid().ToString(),
                MD5Hash = Guid.NewGuid().ToString(),
                Ruleset = ruleset,
                DifficultyName = "test",
            };
        }
    }
}
