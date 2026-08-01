// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play;
using osu.Game.Tests.Beatmaps.IO;
using osu.Game.Tests.Visual;
using SQLitePCL;

namespace osu.Game.Tests.Database
{
    [HeadlessTest]
    public partial class BackgroundDataStoreProcessorTests : OsuTestScene, ILocalUserPlayInfo
    {
        public IBindable<LocalUserPlayingState> PlayingState => isPlaying;

        private readonly Bindable<LocalUserPlayingState> isPlaying = new Bindable<LocalUserPlayingState>();

        private BeatmapSetInfo importedSet = null!;

        [BackgroundDependencyLoader]
        private void load(OsuGameBase osu)
        {
            importedSet = BeatmapImportHelper.LoadQuickOszIntoOsu(osu).GetResultSafely();
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("Set not playing", () => isPlaying.Value = LocalUserPlayingState.NotPlaying);
            AddStep("Prepare processor test environment", prepareProcessorTestEnvironment);
        }

        private void prepareProcessorTestEnvironment()
        {
            ensureLocalMetadataCachePresent();

            Realm.Write(r =>
            {
                foreach (var ruleset in r.All<RulesetInfo>())
                {
                    if (!ruleset.Available)
                        continue;

                    try
                    {
                        int difficultyVersion = ruleset.CreateInstance().CreateDifficultyCalculator(Beatmap.Value).Version;
                        ruleset.LastAppliedDifficultyVersion = difficultyVersion;
                    }
                    catch (RulesetLoadException)
                    {
                        continue;
                    }

                    if (EzXxyStarRatingSupport.TryGetXxyStarRatingVersion(ruleset, out int xxyVersion))
                        ruleset.LastAppliedXxySrVersion = xxyVersion;
                }

                foreach (var beatmap in r.All<BeatmapInfo>())
                {
                    if (beatmap.XxyStarRating < 0 && EzXxyStarRatingSupport.SupportsRuleset(beatmap.Ruleset))
                        beatmap.XxyStarRating = beatmap.StarRating >= 0 ? beatmap.StarRating : 0;

                    if (beatmap.PerformancePoints < 0 && EzXxyStarRatingSupport.IsRulesetAvailable(beatmap.Ruleset))
                        beatmap.PerformancePoints = 0;

                    beatmap.HasVideo ??= false;
                    beatmap.HasStoryboard ??= false;
                }
            });
        }

        private void ensureLocalMetadataCachePresent()
        {
            const string cache_database_name = "online.db";

            if (LocalStorage.Exists(cache_database_name))
                return;

            Batteries_V2.Init();

            using (var connection = new SqliteConnection($"Data Source={LocalStorage.GetFullPath(cache_database_name)}"))
            {
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"CREATE TABLE schema_version (number INTEGER NOT NULL);
INSERT INTO schema_version (number) VALUES (3);";
                command.ExecuteNonQuery();
            }
        }

        [Test]
        public void TestDifficultyProcessing()
        {
            AddAssert("Difficulty is initially set", () =>
            {
                return Realm.Run(r =>
                {
                    var beatmapSetInfo = r.Find<BeatmapSetInfo>(importedSet.ID)!;
                    return beatmapSetInfo.Beatmaps.All(b => b.StarRating > 0);
                });
            });

            AddStep("Reset difficulty", () =>
            {
                Realm.Write(r =>
                {
                    var beatmapSetInfo = r.Find<BeatmapSetInfo>(importedSet.ID)!;
                    foreach (var b in beatmapSetInfo.Beatmaps)
                        b.StarRating = -1;
                });
            });

            TestBackgroundDataStoreProcessor processor = null!;
            AddStep("Run background processor", () => Add(processor = new TestBackgroundDataStoreProcessor()));
            AddUntilStep("Wait for completion", () => processor.Completed);

            AddAssert("Difficulties repopulated", () =>
            {
                return Realm.Run(r =>
                {
                    var beatmapSetInfo = r.Find<BeatmapSetInfo>(importedSet.ID)!;
                    return beatmapSetInfo.Beatmaps.All(b => b.StarRating > 0);
                });
            });
        }

        [Test]
        public void TestDifficultyProcessingWhilePlaying()
        {
            AddAssert("Difficulty is initially set", () =>
            {
                return Realm.Run(r =>
                {
                    var beatmapSetInfo = r.Find<BeatmapSetInfo>(importedSet.ID)!;
                    return beatmapSetInfo.Beatmaps.All(b => b.StarRating > 0);
                });
            });

            AddStep("Set playing", () => isPlaying.Value = LocalUserPlayingState.Playing);

            AddStep("Reset difficulty", () =>
            {
                Realm.Write(r =>
                {
                    var beatmapSetInfo = r.Find<BeatmapSetInfo>(importedSet.ID)!;
                    foreach (var b in beatmapSetInfo.Beatmaps)
                        b.StarRating = -1;
                });
            });

            TestBackgroundDataStoreProcessor processor = null!;
            AddStep("Run background processor", () => Add(processor = new TestBackgroundDataStoreProcessor()));

            AddWaitStep("wait some", 500);
            AddAssert("Difficulty still not populated", () =>
            {
                return Realm.Run(r =>
                {
                    var beatmapSetInfo = r.Find<BeatmapSetInfo>(importedSet.ID)!;
                    return beatmapSetInfo.Beatmaps.All(b => b.StarRating == -1);
                });
            });

            AddStep("Set not playing", () => isPlaying.Value = LocalUserPlayingState.NotPlaying);
            AddUntilStep("Wait for completion", () => processor.Completed);

            AddAssert("Difficulties repopulated", () =>
            {
                return Realm.Run(r =>
                {
                    var beatmapSetInfo = r.Find<BeatmapSetInfo>(importedSet.ID)!;
                    return beatmapSetInfo.Beatmaps.All(b => b.StarRating > 0);
                });
            });
        }

        [TestCase(30000001)]
        [TestCase(30000002)]
        [TestCase(30000003)]
        [TestCase(30000004)]
        [TestCase(30000005)]
        public void TestScoreUpgradeSuccess(int scoreVersion)
        {
            ScoreInfo scoreInfo = null!;

            AddStep("Add score which requires upgrade (and has beatmap)", () =>
            {
                Realm.Write(r =>
                {
                    r.Add(scoreInfo = new ScoreInfo(ruleset: r.All<RulesetInfo>().First(), beatmap: r.All<BeatmapInfo>().First())
                    {
                        TotalScoreVersion = scoreVersion,
                        LegacyTotalScore = 123456,
                        IsLegacyScore = true,
                    });
                });
            });

            TestBackgroundDataStoreProcessor processor = null!;
            AddStep("Run background processor", () => Add(processor = new TestBackgroundDataStoreProcessor()));
            AddUntilStep("Wait for completion", () => processor.Completed);

            AddAssert("Score version upgraded", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.TotalScoreVersion), () => Is.EqualTo(LegacyScoreEncoder.LATEST_VERSION));
            AddAssert("Score not marked as failed", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.BackgroundReprocessingFailed), () => Is.False);
        }

        /// <summary>
        /// [Ez] 带 Ez 游玩模式嵌入（ManiaHitMode / ManiaHealthMode）的成绩不得被官方分数升级改写：
        /// 仅盖 TotalScoreVersion，Rank / TotalScore / Accuracy 保持原值。
        /// </summary>
        [TestCase(30000001)]
        [TestCase(30000016)]
        public void TestEzGameplayModeScoreExemptFromOfficialUpgrades(int scoreVersion)
        {
            ScoreInfo scoreInfo = null!;

            AddStep("Add Ez gameplay-mode score with old version", () =>
            {
                Realm.Write(r =>
                {
                    r.Add(scoreInfo = new ScoreInfo(ruleset: r.All<RulesetInfo>().First(rs => rs.ShortName == "mania"), beatmap: r.All<BeatmapInfo>().First())
                    {
                        TotalScoreVersion = scoreVersion,
                        LegacyTotalScore = 123456,
                        IsLegacyScore = true,
                        ManiaHitMode = 1, // EZ2AC
                        ManiaHealthMode = 0,
                        TotalScore = 987654,
                        TotalScoreWithoutMods = 987654,
                        Accuracy = 0.97,
                        Rank = ScoreRank.S,
                    });
                });
            });

            TestBackgroundDataStoreProcessor processor = null!;
            AddStep("Run background processor", () => Add(processor = new TestBackgroundDataStoreProcessor()));
            AddUntilStep("Wait for completion", () => processor.Completed);

            AddAssert("Score version stamped", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.TotalScoreVersion), () => Is.EqualTo(LegacyScoreEncoder.LATEST_VERSION));
            AddAssert("Total score untouched", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.TotalScore), () => Is.EqualTo(987654));
            AddAssert("Accuracy untouched", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.Accuracy), () => Is.EqualTo(0.97));
            AddAssert("Rank untouched", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.Rank), () => Is.EqualTo(ScoreRank.S));
            AddAssert("Score not marked as failed", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.BackgroundReprocessingFailed), () => Is.False);
        }

        /// <summary>
        /// [Ez] 局内双 Lazer 成绩落库为 ManiaHitMode=0/HealthMode=0（官方语义，未归一为 -1），
        /// 必须跟随 ppy 上游正常升级，不得被 Ez 豁免批次跳过。
        /// </summary>
        [Test]
        public void TestDoubleLazerModeScoreStillSubjectToOfficialUpgrades()
        {
            ScoreInfo scoreInfo = null!;

            AddStep("Add double-lazer legacy score with old version", () =>
            {
                Realm.Write(r =>
                {
                    r.Add(scoreInfo = new ScoreInfo(ruleset: r.All<RulesetInfo>().First(rs => rs.ShortName == "mania"), beatmap: r.All<BeatmapInfo>().First())
                    {
                        // ≥30000017：跳过 mod 倍率升级批次，确保由 legacy 转换批次处理（可观察 TotalScore 变化）。
                        TotalScoreVersion = 30000018,
                        LegacyTotalScore = 123456,
                        IsLegacyScore = true,
                        ManiaHitMode = 0, // Lazer
                        ManiaHealthMode = 0,
                        TotalScore = 987654,
                        TotalScoreWithoutMods = 987654,
                    });
                });
            });

            TestBackgroundDataStoreProcessor processor = null!;
            AddStep("Run background processor", () => Add(processor = new TestBackgroundDataStoreProcessor()));
            AddUntilStep("Wait for completion", () => processor.Completed);

            AddAssert("Score version upgraded", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.TotalScoreVersion), () => Is.EqualTo(LegacyScoreEncoder.LATEST_VERSION));
            AddAssert("Score not marked as failed", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.BackgroundReprocessingFailed), () => Is.False);
            AddAssert("Score was officially converted (not exempted)", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.TotalScore), () => Is.Not.EqualTo(987654));
        }

        [TestCase(30000002)]
        [TestCase(30000013)]
        public void TestScoreUpgradeFailed(int scoreVersion)
        {
            ScoreInfo scoreInfo = null!;

            AddStep("Add score which requires upgrade (but has no beatmap)", () =>
            {
                Realm.Write(r =>
                {
                    r.Add(scoreInfo = new ScoreInfo(ruleset: r.All<RulesetInfo>().First(), beatmap: new BeatmapInfo
                    {
                        BeatmapSet = new BeatmapSetInfo(),
                        Ruleset = r.All<RulesetInfo>().First(),
                    })
                    {
                        TotalScoreVersion = scoreVersion,
                        IsLegacyScore = true,
                    });
                });
            });

            TestBackgroundDataStoreProcessor processor = null!;
            AddStep("Run background processor", () => Add(processor = new TestBackgroundDataStoreProcessor()));
            AddUntilStep("Wait for completion", () => processor.Completed);

            AddAssert("Score marked as failed", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.BackgroundReprocessingFailed), () => Is.True);
            AddAssert("Score version not upgraded", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.TotalScoreVersion), () => Is.EqualTo(scoreVersion));
        }

        [Test]
        [FlakyTest]
        public void TestCustomRulesetScoreNotSubjectToUpgrades([Values] bool available)
        {
            RulesetInfo rulesetInfo = null!;
            ScoreInfo scoreInfo = null!;
            TestBackgroundDataStoreProcessor processor = null!;

            AddStep("Add unavailable ruleset", () => Realm.Write(r => r.Add(rulesetInfo = new RulesetInfo
            {
                ShortName = Guid.NewGuid().ToString(),
                Available = available
            })));

            AddStep("Add score for unavailable ruleset", () => Realm.Write(r => r.Add(scoreInfo = new ScoreInfo(
                ruleset: rulesetInfo,
                beatmap: r.All<BeatmapInfo>().First())
            {
                TotalScoreVersion = 30000001
            })));

            AddStep("Run background processor", () => Add(processor = new TestBackgroundDataStoreProcessor()));
            AddUntilStep("Wait for completion", () => processor.Completed);

            AddAssert("Score not marked as failed", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.BackgroundReprocessingFailed), () => Is.False);
            AddAssert("Score version not upgraded", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.TotalScoreVersion), () => Is.EqualTo(30000001));
        }

        [Test]
        public void TestModMultiplierUpgrade()
        {
            ScoreInfo scoreInfo = null!;

            AddStep("Add score which requires upgrade (and has beatmap)", () =>
            {
                Realm.Write(r =>
                {
                    r.Add(scoreInfo = new ScoreInfo(ruleset: r.All<RulesetInfo>().First(), beatmap: r.All<BeatmapInfo>().First())
                    {
                        TotalScoreVersion = 30000016,
                        TotalScore = 1_040_000,
                        TotalScoreWithoutMods = 1_000_000,
                        Mods = [new OsuModDoubleTime { SpeedChange = { Value = 1.25 } }]
                    });
                });
            });

            TestBackgroundDataStoreProcessor processor = null!;
            AddStep("Run background processor", () => Add(processor = new TestBackgroundDataStoreProcessor()));
            AddUntilStep("Wait for completion", () => processor.Completed);

            AddAssert("Score version upgraded", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.TotalScoreVersion), () => Is.EqualTo(LegacyScoreEncoder.LATEST_VERSION));
            AddAssert("Total score corrected", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.TotalScore), () => Is.EqualTo(1_082_000));
            AddAssert("Score not marked as failed", () => Realm.Run(r => r.Find<ScoreInfo>(scoreInfo.ID)!.BackgroundReprocessingFailed), () => Is.False);
        }

        public partial class TestBackgroundDataStoreProcessor : BackgroundDataStoreProcessor
        {
            protected override int TimeToSleepDuringGameplay => 10;

            protected override bool SkipProcessing => false;

            protected override TimeSpan StartupBackfillDelay => TimeSpan.Zero;

            public bool Completed => ProcessingTask.IsCompleted;
        }
    }
}
