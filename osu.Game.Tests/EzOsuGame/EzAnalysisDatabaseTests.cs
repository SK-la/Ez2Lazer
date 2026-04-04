// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Tests.Database;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.EzOsuGame
{
    [TestFixture]
    public partial class EzAnalysisCacheTests : RealmTest
    {
        private bool previousPersistentStoreEnabled;

        [SetUp]
        public void SetUp()
        {
            previousPersistentStoreEnabled = EzAnalysisPersistentStore.Enabled;
            EzAnalysisPersistentStore.Enabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            EzAnalysisPersistentStore.Enabled = previousPersistentStoreEnabled;
        }

        [Test]
        public void TestBindableSeedsStoredAnalysisImmediately()
        {
            RunTestWithRealm((_, storage) =>
            {
                var beatmap = createBeatmap();
                var storedAnalysis = createAnalysis(4.2, 8.4, 12.6);

                var persistentStore = new EzAnalysisPersistentStore(storage);
                persistentStore.StoreIfDifferent(beatmap, storedAnalysis);

                var config = createConfig(storage, runtimeEnabled: false, sqliteEnabled: true);
                var cache = new TestEzAnalysisCache(persistentStore, config);

                var bindable = cache.GetBindableAnalysis(beatmap);

                Assert.That(cache.GetDynamicAnalysisCalls, Is.EqualTo(0));
                assertAnalysis(bindable.Value, storedAnalysis);
            });
        }

        [Test]
        public void TestGetAnalysisAsyncUsesRuntimeWhenStoredAnalysisExists()
        {
            RunTestWithRealm((_, storage) =>
            {
                var beatmap = createBeatmap();
                var storedAnalysis = createAnalysis(4.2, 8.4, 12.6);
                var runtimeAnalysis = createAnalysis(1.2, 2.4, 3.6);

                var persistentStore = new EzAnalysisPersistentStore(storage);
                persistentStore.StoreIfDifferent(beatmap, storedAnalysis);

                var config = createConfig(storage, runtimeEnabled: true, sqliteEnabled: true);
                var cache = new TestEzAnalysisCache(persistentStore, config)
                {
                    DynamicResult = runtimeAnalysis,
                };

                var analysis = cache.GetAnalysisAsync(beatmap, beatmap.Ruleset).GetAwaiter().GetResult();

                Assert.That(cache.GetDynamicAnalysisCalls, Is.EqualTo(1));
                Assert.That(analysis.HasValue, Is.True);
                assertAnalysis(analysis!.Value, runtimeAnalysis);
            });
        }

        [Test]
        public void TestGetAnalysisAsyncUsesRuntimeWhenStoredAnalysisIsMissing()
        {
            RunTestWithRealm((_, storage) =>
            {
                var beatmap = createBeatmap();
                var runtimeAnalysis = createAnalysis(1.2, 2.4, 3.6);

                var config = createConfig(storage, runtimeEnabled: true, sqliteEnabled: true);
                var cache = new TestEzAnalysisCache(new EzAnalysisPersistentStore(storage), config)
                {
                    DynamicResult = runtimeAnalysis,
                };

                var analysis = cache.GetAnalysisAsync(beatmap, beatmap.Ruleset).GetAwaiter().GetResult();

                Assert.That(cache.GetDynamicAnalysisCalls, Is.EqualTo(1));
                Assert.That(analysis.HasValue, Is.True);
                assertAnalysis(analysis!.Value, runtimeAnalysis);
            });
        }

        [Test]
        public void TestTryGetXxySrReadsStoredAnalysis()
        {
            RunTestWithRealm((_, storage) =>
            {
                var beatmap = createBeatmap();
                var storedAnalysis = createAnalysis(6.3, 10.1, 14.8);

                var persistentStore = new EzAnalysisPersistentStore(storage);
                persistentStore.StoreIfDifferent(beatmap, storedAnalysis);

                var config = createConfig(storage, runtimeEnabled: false, sqliteEnabled: true);
                var cache = new TestEzAnalysisCache(persistentStore, config);

                Assert.That(cache.TryGetXxySr(beatmap, beatmap.Ruleset, out double xxySr), Is.True);
                Assert.That(xxySr, Is.EqualTo(14.8).Within(0.0001));
            });
        }

        [Test]
        public void TestStoredAnalysisIsReturnedWithoutRuntimeCache()
        {
            RunTestWithRealm((_, storage) =>
            {
                var beatmap = createBeatmap();
                var storedAnalysis = createAnalysis(7.1, 11.3, 15.9);

                var persistentStore = new EzAnalysisPersistentStore(storage);
                persistentStore.StoreIfDifferent(beatmap, storedAnalysis);

                var config = createConfig(storage, runtimeEnabled: false, sqliteEnabled: true);

                var cache = new TestEzAnalysisCache(persistentStore, config)
                {
                    DynamicResult = null,
                };

                var bindable = cache.GetBindableAnalysis(beatmap);

                Assert.That(cache.GetDynamicAnalysisCalls, Is.EqualTo(0));
                assertAnalysis(bindable.Value, storedAnalysis);
            });
        }

        [Test]
        public void TestBindableSeedsStoredAnalysisImmediatelyForNonManiaBeatmap()
        {
            RunTestWithRealm((_, storage) =>
            {
                var beatmap = createBeatmap(new OsuRuleset().RulesetInfo);
                var storedAnalysis = createCommonAnalysis(5.5, 9.5);

                var persistentStore = new EzAnalysisPersistentStore(storage);
                persistentStore.StoreIfDifferent(beatmap, storedAnalysis);

                var config = createConfig(storage, runtimeEnabled: false, sqliteEnabled: true);
                var cache = new TestEzAnalysisCache(persistentStore, config);

                var bindable = cache.GetBindableAnalysis(beatmap);

                Assert.That(cache.GetDynamicAnalysisCalls, Is.EqualTo(0));
                assertAnalysis(bindable.Value, storedAnalysis);
            });
        }

        [Test]
        public void TestGetAnalysisAsyncUsesRuntimeForModdedLookup()
        {
            RunTestWithRealm((_, storage) =>
            {
                var beatmap = createBeatmap();
                var storedAnalysis = createAnalysis(7.1, 11.3, 15.9);
                var runtimeAnalysis = createAnalysis(2.1, 4.2, 6.3);

                var persistentStore = new EzAnalysisPersistentStore(storage);
                persistentStore.StoreIfDifferent(beatmap, storedAnalysis);

                var config = createConfig(storage, runtimeEnabled: true, sqliteEnabled: true);
                var cache = new TestEzAnalysisCache(persistentStore, config)
                {
                    DynamicResult = runtimeAnalysis,
                };

                var analysis = cache.GetAnalysisAsync(beatmap, beatmap.Ruleset, new[] { new TestMod() }).GetAwaiter().GetResult();

                Assert.That(cache.GetDynamicAnalysisCalls, Is.EqualTo(1));
                Assert.That(analysis.HasValue, Is.True);
                assertAnalysis(analysis!.Value, runtimeAnalysis);
            });
        }

        [Test]
        public void TestGetAnalysisAsyncFallsBackToStoredWhenDynamicAnalysisIsUnavailable()
        {
            RunTestWithRealm((_, storage) =>
            {
                var beatmap = createBeatmap();
                var storedAnalysis = createAnalysis(3.2, 6.4, 9.6);

                var persistentStore = new EzAnalysisPersistentStore(storage);
                persistentStore.StoreIfDifferent(beatmap, storedAnalysis);

                var config = createConfig(storage, runtimeEnabled: true, sqliteEnabled: true);
                var cache = new TestEzAnalysisCache(persistentStore, config)
                {
                    DynamicResult = null,
                };

                var analysis = cache.GetAnalysisAsync(beatmap, beatmap.Ruleset).GetAwaiter().GetResult();

                Assert.That(cache.GetDynamicAnalysisCalls, Is.EqualTo(1));
                Assert.That(analysis.HasValue, Is.True);
                assertAnalysis(analysis!.Value, storedAnalysis);
            });
        }

        [Test]
        public void TestMatchingXxySrBranchOverridesBaselineValue()
        {
            RunTestWithRealm((_, storage) =>
            {
                var beatmap = createBeatmap();
                var baselineAnalysis = createAnalysis(3.2, 6.4, 9.6);
                var mods = new Mod[] { new TestMod() };

                var persistentStore = new EzAnalysisPersistentStore(storage);
                persistentStore.StoreIfDifferent(beatmap, baselineAnalysis);

                var database = createDatabase(persistentStore, storage);
                storeBranch(persistentStore, beatmap, mods, 15.7, out string databasePath);
                database.ActivateXxySrBranch(databasePath, beatmap.Ruleset, mods, 1, "branch");

                var values = database.GetStoredXxySrValues(new[] { beatmap }, beatmap.Ruleset, mods);

                Assert.That(values.TryGetValue(beatmap.ID, out double xxySr), Is.True);
                Assert.That(xxySr, Is.EqualTo(15.7).Within(0.0001));
            });
        }

        [Test]
        public void TestMismatchedXxySrBranchFallsBackToBaseline()
        {
            RunTestWithRealm((_, storage) =>
            {
                var beatmap = createBeatmap();
                var baselineAnalysis = createAnalysis(3.2, 6.4, 9.6);
                var activeBranchMods = new Mod[] { new TestMod() };

                var persistentStore = new EzAnalysisPersistentStore(storage);
                persistentStore.StoreIfDifferent(beatmap, baselineAnalysis);

                var database = createDatabase(persistentStore, storage);
                storeBranch(persistentStore, beatmap, activeBranchMods, 15.7, out string databasePath);
                database.ActivateXxySrBranch(databasePath, beatmap.Ruleset, activeBranchMods, 1, "branch");

                var values = database.GetStoredXxySrValues(new[] { beatmap }, beatmap.Ruleset, mods: null);

                Assert.That(values.TryGetValue(beatmap.ID, out double xxySr), Is.True);
                Assert.That(xxySr, Is.EqualTo(9.6).Within(0.0001));
            });
        }

        [Test]
        public void TestXxySrBranchSkipsHashMismatch()
        {
            RunTestWithRealm((_, storage) =>
            {
                var beatmap = createBeatmap();
                var mods = new Mod[] { new TestMod() };

                var persistentStore = new EzAnalysisPersistentStore(storage);
                var database = createDatabase(persistentStore, storage);
                storeBranch(persistentStore, beatmap, mods, 15.7, out string databasePath, storedHash: "mismatch-hash");
                database.ActivateXxySrBranch(databasePath, beatmap.Ruleset, mods, 1, "branch");

                var values = database.GetStoredXxySrValues(new[] { beatmap }, beatmap.Ruleset, mods);

                Assert.That(values.ContainsKey(beatmap.ID), Is.False);
            });
        }

        private static Ez2ConfigManager createConfig(OsuStorage storage, bool runtimeEnabled, bool sqliteEnabled)
        {
            var config = new Ez2ConfigManager(storage);
            config.GetBindable<bool>(Ez2Setting.EzAnalysisRecEnabled).Value = runtimeEnabled;
            config.GetBindable<bool>(Ez2Setting.EzAnalysisSqliteEnabled).Value = sqliteEnabled;
            return config;
        }

        private static EzAnalysisDatabase createDatabase(EzAnalysisPersistentStore persistentStore, OsuStorage storage)
            => new EzAnalysisDatabase(persistentStore, null!, createConfig(storage, runtimeEnabled: false, sqliteEnabled: true));

        private static void storeBranch(EzAnalysisPersistentStore persistentStore, BeatmapInfo beatmap, IReadOnlyList<Mod>? mods, double xxySr, out string databasePath, string? storedHash = null)
        {
            databasePath = persistentStore.CreateXxySrBranchDatabasePath(beatmap.Ruleset.ShortName);
            persistentStore.StoreXxySrBranch(
                databasePath,
                new EzAnalysisPersistentStore.XxySrBranchMetadata(
                    beatmap.Ruleset.OnlineID,
                    beatmap.Ruleset.ShortName,
                    mods == null || mods.Count == 0 ? string.Empty : string.Join(',', mods.Select(m => m.Acronym)),
                    mods == null || mods.Count == 0 ? "NoMod" : string.Join(',', mods.Select(m => m.Acronym)),
                    1,
                    0,
                    "branch"),
                new[]
                {
                    new EzAnalysisPersistentStore.XxySrBranchRow(
                        beatmap.ID,
                        storedHash ?? beatmap.Hash,
                        beatmap.MD5Hash,
                        xxySr)
                });
        }

        private static BeatmapInfo createBeatmap(RulesetInfo? rulesetInfo = null)
            => TestResources.CreateTestBeatmapSetInfo(1, new[] { rulesetInfo ?? new ManiaRuleset().RulesetInfo }).Beatmaps.Single();

        private static EzAnalysisResult createAnalysis(double averageKps, double maxKps, double? xxySr)
            => new EzAnalysisResult(
                EzCommonAnalysisAttributes.Create(averageKps, maxKps, new List<double> { averageKps, maxKps }),
                EzManiaAnalysisAttributes.Create(new Dictionary<int, int> { [4] = 128 }, new Dictionary<int, int> { [4] = 16 }, xxySr));

        private static EzAnalysisResult createCommonAnalysis(double averageKps, double maxKps)
            => new EzAnalysisResult(EzCommonAnalysisAttributes.Create(averageKps, maxKps, new List<double> { averageKps, maxKps }));

        private static void assertAnalysis(EzAnalysisResult actual, EzAnalysisResult expected)
        {
            Assert.That(actual.AverageKps, Is.EqualTo(expected.AverageKps).Within(0.0001));
            Assert.That(actual.MaxKps, Is.EqualTo(expected.MaxKps).Within(0.0001));
            Assert.That(actual.ManiaAttributes?.XxySr, Is.EqualTo(expected.ManiaAttributes?.XxySr).Within(0.0001));
            Assert.That(actual.CommonAttributes!.KpsList, Is.EquivalentTo(expected.CommonAttributes!.KpsList));

            if (expected.ManiaAttributes == null)
            {
                Assert.That(actual.ManiaAttributes, Is.Null);
                return;
            }

            Assert.That(actual.ManiaAttributes, Is.Not.Null);
            Assert.That(actual.ManiaAttributes!.ColumnCounts, Is.EquivalentTo(expected.ManiaAttributes.ColumnCounts));
            Assert.That(actual.ManiaAttributes.HoldNoteCounts, Is.EquivalentTo(expected.ManiaAttributes.HoldNoteCounts));
        }

        private partial class TestEzAnalysisCache : EzAnalysisCache
        {
            public EzAnalysisResult? DynamicResult { get; set; }

            public int GetDynamicAnalysisCalls { get; private set; }

            public TestEzAnalysisCache(EzAnalysisPersistentStore persistentStore, Ez2ConfigManager config)
                : base(new EzAnalysisDatabase(persistentStore, null!, config), config)
            {
            }

            protected override Task<EzAnalysisResult?> GetDynamicAnalysisAsync(BeatmapInfo beatmapInfo, RulesetInfo rulesetInfo, IEnumerable<Mod>? mods, CancellationToken cancellationToken = default, int computationDelay = 0)
            {
                GetDynamicAnalysisCalls++;
                return Task.FromResult(DynamicResult);
            }
        }

        private class TestMod : Mod
        {
            public override string Name => string.Empty;

            public override LocalisableString Description => string.Empty;

            public override string Acronym => "TM";

            public override ModType Type => ModType.DifficultyIncrease;

            public override double ScoreMultiplier => 1;
        }
    }
}
