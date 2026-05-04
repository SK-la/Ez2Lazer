// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Statistics;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.IO.Archives;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Replays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Ranking.Statistics.User;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Resources;
using osu.Game.Users;
using osuTK;

namespace osu.Game.Tests.Visual.Ranking
{
    public partial class TestSceneStatisticsPanel : OsuTestScene
    {
        private DummyAPIAccess dummyAPI => (DummyAPIAccess)API;

        private ScoreManager scoreManager = null!;
        private RulesetStore rulesetStore = null!;
        private BeatmapManager beatmapManager = null!;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            dependencies.Cache(rulesetStore = new RealmRulesetStore(Realm));
            dependencies.Cache(beatmapManager = new BeatmapManager(LocalStorage, Realm, null, dependencies.Get<AudioManager>(), Resources, dependencies.Get<GameHost>(), Beatmap.Default));
            dependencies.Cache(scoreManager = new ScoreManager(rulesetStore, () => beatmapManager, LocalStorage, Realm, API));
            Dependencies.Cache(Realm);

            return dependencies;
        }

        [Test]
        public void TestScoreWithPositionStatistics()
        {
            var score = TestResources.CreateTestScoreInfo();
            score.OnlineID = 1234;
            score.HitEvents = CreatePositionDistributedHitEvents();

            loadPanel(score);
        }

        [Test]
        public void TestScoreWithTimeStatistics()
        {
            var score = TestResources.CreateTestScoreInfo();
            score.HitEvents = TestSceneHitEventTimingDistributionGraph.CreateDistributedHitEvents();

            loadPanel(score);
        }

        [Test]
        public void TestScoreWithoutStatistics()
        {
            loadPanel(TestResources.CreateTestScoreInfo());
        }

        [Test]
        public void TestHitEventsAreGeneratedIntoDisplayedScore()
        {
            ScoreInfo score = null!;

            AddStep("import replay-backed score", () => score = createReplayBackedScore());
            AddAssert("score starts without hit events", () => score.HitEvents, () => Is.Empty);

            loadPanel(score);

            AddUntilStep("score hit events generated", () => score.HitEvents.Count > 0);
            AddUntilStep("statistics rows displayed", () => this.ChildrenOfType<StatisticItemContainer>().Any());
        }

        [Test]
        public void TestEzScoreServerReturnsValidHitEvents()
        {
            EzScoreServer.AnalysisHost analysisHost = null!;
            ScoreInfo score = null!;
            Score databasedScore = null!;
            List<HitEvent> generatedHitEvents = null!;

            AddStep("create analysis host", () => Add(analysisHost = new EzScoreServer.AnalysisHost()));
            AddUntilStep("analysis host loaded", () => analysisHost.IsLoaded);

            AddStep("create replay-backed score", () => score = createReplayBackedScore());
            AddStep("resolve databased score", () => databasedScore = scoreManager.GetScore(score));
            AddAssert("databased score exists", () => databasedScore != null);

            AddStep("start analysis", () => generatedHitEvents = analysisHost.GenerateAsync(databasedScore!).GetResultSafely());

            AddAssert("returned hit events", () => generatedHitEvents.Count > 0);
            AddAssert("returned positions are present", () => generatedHitEvents.All(e => e.Position != null));
            AddAssert("returned hit objects are present", () => generatedHitEvents.All(e => e.HitObject != null));
        }

        [Test]
        public void TestOsuBridgeGeneratesExpectedReplayPositions()
        {
            List<HitEvent> generatedHitEvents = null;

            AddStep("generate osu bridge hit events", () => generatedHitEvents = generateOsuBridgeHitEvents());
            AddAssert("osu bridge returned hit events", () => generatedHitEvents is { Count: 2 });
            AddAssert("first hit offset matches replay", () => generatedHitEvents![0].TimeOffset == 0);
            AddAssert("second hit offset matches replay", () => Math.Abs(generatedHitEvents![1].TimeOffset - 12) < 0.01);
            AddAssert("all osu events carry hit positions", () => generatedHitEvents!.All(e => e.Position != null));
            AddAssert("first osu event has no previous object", () => generatedHitEvents![0].LastHitObject == null);
            AddAssert("second osu event links previous object", () => ReferenceEquals(generatedHitEvents![1].LastHitObject, generatedHitEvents[0].HitObject));
        }

        [Test]
        public void TestManiaBridgeRespectsConfiguredHitMode()
        {
            List<HitEvent> lazerHitEvents = null;
            List<HitEvent> bmsHitEvents = null;

            AddStep("generate lazer mania hit events", () => lazerHitEvents = generateManiaBridgeHitEvents(EzEnumHitMode.Lazer, 1300));
            AddStep("generate bms mania hit events", () => bmsHitEvents = generateManiaBridgeHitEvents(EzEnumHitMode.IIDX_HD, 1300));
            AddAssert("lazer mania bridge returned hit events", () => lazerHitEvents is { Count: 1 });
            AddAssert("bms mania bridge returned hit events", () => bmsHitEvents is { Count: 1 });
            AddAssert("lazer late hit is miss", () => lazerHitEvents![0].Result == HitResult.Miss);
            AddAssert("bms late hit is poor", () => bmsHitEvents![0].Result == HitResult.Poor);
            AddAssert("bms preserves raw late offset", () => Math.Abs(bmsHitEvents![0].TimeOffset - 300) < 0.01);
        }

        [Test]
        public void TestScoreInRulesetWhereAllStatsRequireHitEvents()
        {
            loadPanel(TestResources.CreateTestScoreInfo(new TestRulesetAllStatsRequireHitEvents().RulesetInfo));
        }

        [Test]
        public void TestScoreInRulesetWhereNoStatsRequireHitEvents()
        {
            loadPanel(TestResources.CreateTestScoreInfo(new TestRulesetNoStatsRequireHitEvents().RulesetInfo));
        }

        [Test]
        public void TestScoreInMixedRuleset()
        {
            loadPanel(TestResources.CreateTestScoreInfo(new TestRulesetMixed().RulesetInfo));
        }

        [Test]
        public void TestNullScore()
        {
            loadPanel(null);
        }

        [Test]
        public void TestStatisticsShownCorrectlyIfUpdateDeliveredBeforeLoad()
        {
            UserStatisticsWatcher userStatisticsWatcher = null!;
            ScoreInfo score = null!;

            AddStep("create user statistics watcher", () => Add(userStatisticsWatcher = new UserStatisticsWatcher(new LocalUserStatisticsProvider())));
            AddStep("set user statistics update", () =>
            {
                score = TestResources.CreateTestScoreInfo();
                score.OnlineID = 1234;
                ((Bindable<ScoreBasedUserStatisticsUpdate>)userStatisticsWatcher.LatestUpdate).Value = new ScoreBasedUserStatisticsUpdate(score,
                    new UserStatistics
                    {
                        Level = new UserStatistics.LevelInfo
                        {
                            Current = 5,
                            Progress = 20,
                        },
                        GlobalRank = 38000,
                        CountryRank = 12006,
                        PP = 2134,
                        RankedScore = 21123849,
                        Accuracy = 0.985,
                        PlayCount = 13375,
                        PlayTime = 354490,
                        TotalScore = 128749597,
                        TotalHits = 0,
                        MaxCombo = 1233,
                    }, new UserStatistics
                    {
                        Level = new UserStatistics.LevelInfo
                        {
                            Current = 5,
                            Progress = 30,
                        },
                        GlobalRank = 36000,
                        CountryRank = 12000,
                        PP = (decimal)2134.5,
                        RankedScore = 23897015,
                        Accuracy = 0.984,
                        PlayCount = 13376,
                        PlayTime = 35789,
                        TotalScore = 132218497,
                        TotalHits = 0,
                        MaxCombo = 1233,
                    });
            });
            AddStep("load user statistics panel", () => Child = new DependencyProvidingContainer
            {
                CachedDependencies = [(typeof(UserStatisticsWatcher), userStatisticsWatcher)],
                RelativeSizeAxes = Axes.Both,
                Child = new StatisticsPanel
                {
                    RelativeSizeAxes = Axes.Both,
                    State = { Value = Visibility.Visible },
                    Score = { Value = score, },
                    AchievedScore = score,
                }
            });
            AddUntilStep("overall ranking present", () => this.ChildrenOfType<OverallRanking>().Any());
            AddUntilStep("loading spinner not visible",
                () => this.ChildrenOfType<OverallRanking>().Single()
                          .ChildrenOfType<LoadingLayer>().All(l => l.State.Value == Visibility.Hidden));
        }

        [Test]
        public void TestTagging()
        {
            var score = TestResources.CreateTestScoreInfo();

            setUpTaggingRequests(() => score.BeatmapInfo);
            AddStep("load panel", () =>
            {
                Child = new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new StatisticsPanel
                    {
                        RelativeSizeAxes = Axes.Both,
                        State = { Value = Visibility.Visible },
                        Score = { Value = score },
                        AchievedScore = score,
                    }
                };
            });
        }

        private void setUpTaggingRequests(Func<BeatmapInfo> beatmap) =>
            AddStep("set up network requests", () =>
            {
                dummyAPI.HandleRequest = request =>
                {
                    switch (request)
                    {
                        case ListTagsRequest listTagsRequest:
                        {
                            Scheduler.AddDelayed(() => listTagsRequest.TriggerSuccess(new APITagCollection
                            {
                                Tags =
                                [
                                    new APITag { Id = 1, Name = "song representation/simple", Description = "Accessible and straightforward map design.", },
                                    new APITag
                                    {
                                        Id = 2, Name = "style/clean",
                                        Description = "Visually uncluttered and organised patterns, often involving few overlaps and equal visual spacing between objects.",
                                    },
                                    new APITag
                                    {
                                        Id = 3, Name = "aim/aim control", Description = "Patterns with velocity or direction changes which strongly go against a player's natural movement pattern.",
                                    },
                                    new APITag { Id = 4, Name = "tap/bursts", Description = "Patterns requiring continuous movement and alternating, typically 9 notes or less.", },
                                ]
                            }), 500);
                            return true;
                        }

                        case GetBeatmapSetRequest getBeatmapSetRequest:
                        {
                            var beatmapSet = CreateAPIBeatmapSet(beatmap.Invoke());
                            beatmapSet.Beatmaps.Single().TopTags =
                            [
                                new APIBeatmapTag { TagId = 3, VoteCount = 9 },
                            ];
                            Scheduler.AddDelayed(() => getBeatmapSetRequest.TriggerSuccess(beatmapSet), 500);
                            return true;
                        }

                        case AddBeatmapTagRequest:
                        case RemoveBeatmapTagRequest:
                        {
                            Scheduler.AddDelayed(request.TriggerSuccess, 500);
                            return true;
                        }
                    }

                    return false;
                };
            });

        [Test]
        public void TestTaggingWhenRankTooLow()
        {
            var score = TestResources.CreateTestScoreInfo();
            score.Rank = ScoreRank.D;

            setUpTaggingRequests(() => score.BeatmapInfo);
            AddStep("load panel", () =>
            {
                Child = new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new StatisticsPanel
                    {
                        RelativeSizeAxes = Axes.Both,
                        State = { Value = Visibility.Visible },
                        Score = { Value = score },
                        AchievedScore = score,
                    }
                };
            });
        }

        [Test]
        public void TestTaggingConvert()
        {
            var score = TestResources.CreateTestScoreInfo();
            score.Ruleset = new ManiaRuleset().RulesetInfo;

            setUpTaggingRequests(() => score.BeatmapInfo);
            AddStep("load panel", () =>
            {
                Child = new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new StatisticsPanel
                    {
                        RelativeSizeAxes = Axes.Both,
                        State = { Value = Visibility.Visible },
                        Score = { Value = score },
                        AchievedScore = score,
                    }
                };
            });
        }

        [Test]
        public void TestTaggingInteractionWithLocalScores()
        {
            BeatmapInfo beatmapInfo = null!;

            AddStep(@"Import beatmap", () =>
            {
                beatmapManager.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();
                beatmapInfo = beatmapManager.GetAllUsableBeatmapSets().First().Beatmaps.First();
            });

            AddStep("import bad score", () =>
            {
                var score = TestResources.CreateTestScoreInfo();
                score.BeatmapInfo = beatmapInfo;
                score.BeatmapHash = beatmapInfo.Hash;
                score.Ruleset = beatmapInfo.Ruleset;
                score.Rank = ScoreRank.D;
                score.User = API.LocalUser.Value;
                scoreManager.Import(score);
            });

            AddStep("import score by another user", () =>
            {
                var score = TestResources.CreateTestScoreInfo();
                score.BeatmapInfo = beatmapInfo;
                score.BeatmapHash = beatmapInfo.Hash;
                score.Ruleset = beatmapInfo.Ruleset;
                score.Rank = ScoreRank.D;
                score.User = new APIUser { Username = "notme", Id = 5678 };
                scoreManager.Import(score);
            });

            AddStep("import convert score", () =>
            {
                var score = TestResources.CreateTestScoreInfo();
                score.BeatmapInfo = beatmapInfo;
                score.BeatmapHash = beatmapInfo.Hash;
                score.Ruleset = new OsuRuleset().RulesetInfo;
                score.User = API.LocalUser.Value;
                scoreManager.Import(score);
            });

            AddStep("import correct score", () =>
            {
                var score = TestResources.CreateTestScoreInfo();
                score.BeatmapInfo = beatmapInfo;
                score.BeatmapHash = beatmapInfo.Hash;
                score.Ruleset = beatmapInfo.Ruleset;
                score.User = API.LocalUser.Value;
                scoreManager.Import(score);
            });

            setUpTaggingRequests(() => beatmapInfo);
            AddStep("load panel", () =>
            {
                var score = TestResources.CreateTestScoreInfo();
                score.BeatmapInfo = beatmapInfo;

                Child = new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new StatisticsPanel
                    {
                        RelativeSizeAxes = Axes.Both,
                        State = { Value = Visibility.Visible },
                        Score = { Value = score },
                    }
                };
            });
        }

        private void loadPanel(ScoreInfo score) => AddStep("load panel", () =>
        {
            Child = new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new StatisticsPanel
                {
                    RelativeSizeAxes = Axes.Both,
                    State = { Value = Visibility.Visible },
                    Score = { Value = score },
                    AchievedScore = score,
                },
            };
        });

        private ScoreInfo createReplayBackedScore()
        {
            const long replay_score_online_id = 91234567;

            beatmapManager.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();
            scoreManager.Delete(s => s.OnlineID == replay_score_online_id, true);

            var beatmapInfo = beatmapManager.GetAllUsableBeatmapSets()
                                            .SelectMany(s => s.Beatmaps)
                                            .First(b => b.Ruleset.ShortName == new OsuRuleset().RulesetInfo.ShortName);

            var playableBeatmap = beatmapManager.GetWorkingBeatmap(beatmapInfo).GetPlayableBeatmap(beatmapInfo.Ruleset);

            var scoreInfo = TestResources.CreateTestScoreInfo(new OsuRuleset().RulesetInfo);
            scoreInfo.BeatmapInfo = beatmapInfo;
            scoreInfo.BeatmapHash = beatmapInfo.Hash;
            scoreInfo.Ruleset = beatmapInfo.Ruleset;
            scoreInfo.User = API.LocalUser.Value;
            scoreInfo.OnlineID = replay_score_online_id;
            scoreInfo.HitEvents = new List<HitEvent>();

            ScoreInfo importedScore;

            using (var stream = new MemoryStream())
            {
                new LegacyScoreEncoder(new Score
                {
                    ScoreInfo = scoreInfo,
                    Replay = new Replay
                    {
                        Frames = createReplayFrames(playableBeatmap),
                    }
                }, playableBeatmap).Encode(stream, leaveOpen: true);

                importedScore = scoreManager.Import(scoreInfo, new MemoryArchiveReader(stream.ToArray()))!.Value.Detach();
            }

            Assert.That(importedScore, Is.Not.Null);
            Assert.That(importedScore.HitEvents, Is.Empty);

            return importedScore;
        }

        private static List<ReplayFrame> createReplayFrames(IBeatmap playableBeatmap)
        {
            var hitCircles = playableBeatmap.HitObjects.OfType<HitCircle>().Take(3).ToList();

            Assert.That(hitCircles, Has.Count.GreaterThan(0));

            var frames = new List<ReplayFrame>
            {
                new OsuReplayFrame(0, hitCircles[0].Position)
            };

            foreach (var hitCircle in hitCircles)
            {
                frames.Add(new OsuReplayFrame(Math.Max(0, hitCircle.StartTime - 20), hitCircle.Position));
                frames.Add(new OsuReplayFrame(hitCircle.StartTime, hitCircle.Position, OsuAction.LeftButton));
                frames.Add(new OsuReplayFrame(hitCircle.StartTime + 20, hitCircle.Position));
            }

            return frames.OrderBy(f => f.Time).ToList();
        }

        private static List<HitEvent> generateOsuBridgeHitEvents()
        {
            var ruleset = new OsuRuleset();
            var beatmap = new Beatmap<HitObject>
            {
                BeatmapInfo = new BeatmapInfo(ruleset.RulesetInfo, new BeatmapDifficulty
                {
                    OverallDifficulty = 5,
                    CircleSize = 4,
                }, new BeatmapMetadata())
            };

            beatmap.HitObjects.Add(new HitCircle
            {
                StartTime = 1000,
                Position = new Vector2(64, 64),
            });
            beatmap.HitObjects.Add(new HitCircle
            {
                StartTime = 1500,
                Position = new Vector2(196, 196),
            });

            applyDefaults(beatmap);

            var score = new Score
            {
                ScoreInfo =
                {
                    Ruleset = ruleset.RulesetInfo,
                    BeatmapInfo = beatmap.BeatmapInfo,
                },
                Replay = new Replay
                {
                    Frames = new List<ReplayFrame>
                    {
                        new OsuReplayFrame(0, new Vector2(-100, -100)),
                        new OsuReplayFrame(980, new Vector2(64, 64)),
                        new OsuReplayFrame(1000, new Vector2(64, 64), OsuAction.LeftButton),
                        new OsuReplayFrame(1010, new Vector2(64, 64)),
                        new OsuReplayFrame(1488, new Vector2(196, 196)),
                        new OsuReplayFrame(1512, new Vector2(196, 196), OsuAction.RightButton),
                        new OsuReplayFrame(1520, new Vector2(196, 196)),
                    }
                }
            };

            EzScoreReloadBridge.InitializeAllGenerators();
            return EzScoreReloadBridge.TryGenerate(score, beatmap);
        }

        private static List<HitEvent> generateManiaBridgeHitEvents(EzEnumHitMode hitMode, double pressTime)
        {
            var ruleset = new ManiaRuleset();
            EzEnumHitMode originalHitMode = GlobalConfigStore.EzConfig.Get<EzEnumHitMode>(Ez2Setting.ManiaHitMode);

            try
            {
                GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaHitMode, hitMode);

                var beatmap = new Beatmap<HitObject>
                {
                    BeatmapInfo = new BeatmapInfo(ruleset.RulesetInfo, new BeatmapDifficulty
                    {
                        OverallDifficulty = 5,
                    }, new BeatmapMetadata())
                };

                beatmap.HitObjects.Add(new Note
                {
                    StartTime = 1000,
                    Column = 0,
                });

                applyDefaults(beatmap);

                var score = new Score
                {
                    ScoreInfo =
                    {
                        Ruleset = ruleset.RulesetInfo,
                        BeatmapInfo = beatmap.BeatmapInfo,
                    },
                    Replay = new Replay
                    {
                        Frames = new List<ReplayFrame>
                        {
                            new ManiaReplayFrame(0),
                            new ManiaReplayFrame(pressTime, ManiaAction.Key1),
                            new ManiaReplayFrame(pressTime + 20),
                        }
                    }
                };

                EzScoreReloadBridge.InitializeAllGenerators();
                return EzScoreReloadBridge.TryGenerate(score, beatmap);
            }
            finally
            {
                GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaHitMode, originalHitMode);
            }
        }

        private static void applyDefaults(IBeatmap beatmap)
        {
            foreach (var hitObject in beatmap.HitObjects)
                hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
        }

        public static List<HitEvent> CreatePositionDistributedHitEvents()
        {
            var hitEvents = TestSceneHitEventTimingDistributionGraph.CreateDistributedHitEvents();

            // Use constant seed for reproducibility
            var random = new Random(0);

            for (int i = 0; i < hitEvents.Count; i++)
            {
                double angle = random.NextDouble() * 2 * Math.PI;
                double radius = random.NextDouble() * 0.5f * OsuHitObject.OBJECT_RADIUS;

                var position = new Vector2((float)(radius * Math.Cos(angle)), (float)(radius * Math.Sin(angle)));

                hitEvents[i] = hitEvents[i].With(position);
            }

            return hitEvents;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (rulesetStore.IsNotNull())
                rulesetStore?.Dispose();
        }

        private class MemoryArchiveReader : ArchiveReader
        {
            private readonly byte[] data;

            public MemoryArchiveReader(byte[] data)
                : base("generated-replay.osr")
            {
                this.data = data;
            }

            public override Stream GetStream(string name) => new MemoryStream(data, writable: false);

            public override IEnumerable<string> Filenames => ["generated-replay.osr"];

            public override void Dispose()
            {
            }
        }

        private class TestRuleset : Ruleset
        {
            public override IEnumerable<Mod> GetModsFor(ModType type)
            {
                throw new NotImplementedException();
            }

            public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod> mods = null)
            {
                throw new NotImplementedException();
            }

            public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) => new TestBeatmapConverter(beatmap);

            public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
            {
                throw new NotImplementedException();
            }

            public override string Description => string.Empty;

            public override string ShortName => string.Empty;

            protected static Drawable CreatePlaceholderStatistic(string message) => new Container
            {
                RelativeSizeAxes = Axes.X,
                Masking = true,
                CornerRadius = 20,
                Height = 250,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = OsuColour.Gray(0.5f),
                        Alpha = 0.5f
                    },
                    new OsuSpriteText
                    {
                        Origin = Anchor.CentreLeft,
                        Anchor = Anchor.CentreLeft,
                        Text = message,
                        Margin = new MarginPadding { Left = 20 }
                    }
                }
            };

            private class TestBeatmapConverter : IBeatmapConverter
            {
#pragma warning disable CS0067 // The event is never used
                public event Action<HitObject, IEnumerable<HitObject>> ObjectConverted;
#pragma warning restore CS0067

                public IBeatmap Beatmap { get; }

                // ReSharper disable once NotNullOrRequiredMemberIsNotInitialized
                public TestBeatmapConverter(IBeatmap beatmap)
                {
                    Beatmap = beatmap;
                }

                public bool CanConvert() => true;

                public IBeatmap Convert(CancellationToken cancellationToken = default) => Beatmap.Clone();
            }
        }

        private class TestRulesetAllStatsRequireHitEvents : TestRuleset
        {
            public override StatisticItem[] CreateStatisticsForScore(ScoreInfo score, IBeatmap playableBeatmap) => new[]
            {
                new StatisticItem("Statistic Requiring Hit Events 1", () => CreatePlaceholderStatistic("Placeholder statistic. Requires hit events"), true),
                new StatisticItem("Statistic Requiring Hit Events 2", () => CreatePlaceholderStatistic("Placeholder statistic. Requires hit events"), true)
            };
        }

        private class TestRulesetNoStatsRequireHitEvents : TestRuleset
        {
            public override StatisticItem[] CreateStatisticsForScore(ScoreInfo score, IBeatmap playableBeatmap)
            {
                return new[]
                {
                    new StatisticItem("Statistic Not Requiring Hit Events 1", () => CreatePlaceholderStatistic("Placeholder statistic. Does not require hit events")),
                    new StatisticItem("Statistic Not Requiring Hit Events 2", () => CreatePlaceholderStatistic("Placeholder statistic. Does not require hit events"))
                };
            }
        }

        private class TestRulesetMixed : TestRuleset
        {
            public override StatisticItem[] CreateStatisticsForScore(ScoreInfo score, IBeatmap playableBeatmap)
            {
                return new[]
                {
                    new StatisticItem("Statistic Requiring Hit Events", () => CreatePlaceholderStatistic("Placeholder statistic. Requires hit events"), true),
                    new StatisticItem("Statistic Not Requiring Hit Events", () => CreatePlaceholderStatistic("Placeholder statistic. Does not require hit events"))
                };
            }
        }
    }
}
