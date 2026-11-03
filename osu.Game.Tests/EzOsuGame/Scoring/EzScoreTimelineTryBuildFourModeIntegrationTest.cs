// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.IO.Archives;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Replays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.Replays;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Rulesets.Taiko.Replays;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Scores.IO;
using osuTK;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    /// <summary>
    /// P6：四模式 <see cref="EzScoreTimelineBuilder.TryBuild"/> ≡ Session <c>RunTimelineDirect(ForLive)</c>。
    /// 使用显式 shared playable（与 Race 同 Mod 复用路径一致）；Mania Race 生产传 null 另走 provider。
    /// </summary>
    [TestFixture]
    public class EzScoreTimelineTryBuildFourModeIntegrationTest : ImportTest
    {
        [TestCase(typeof(OsuRuleset))]
        [TestCase(typeof(ManiaRuleset))]
        [TestCase(typeof(TaikoRuleset))]
        [TestCase(typeof(CatchRuleset))]
        public void TryBuildMatchesSessionTimelineDirect(Type rulesetType)
        {
            using var host = new CleanRunHeadlessGameHost();

            try
            {
                var osu = LoadOsuIntoHost(host, withBeatmap: false);
                EzScoreTimeline? builderTimeline = null;
                long sessionTotal = 0;
                bool done = false;
                Exception? error = null;

                host.UpdateThread.Scheduler.Add(() =>
                {
                    try
                    {
                        GlobalConfigStore.EnsureInitialized();

                        var scoreManager = osu.Dependencies.Get<ScoreManager>();
                        var beatmapManager = osu.Dependencies.Get<BeatmapManager>();
                        var ruleset = (Ruleset)Activator.CreateInstance(rulesetType)!;
                        var (score, playableBeatmap) = createTinyScore(ruleset);

                        var importedSet = beatmapManager.Import(TestResources.CreateTestBeatmapSetInfo(1, rulesets: new[] { ruleset.RulesetInfo }));
                        Assert.That(importedSet, Is.Not.Null);

                        var beatmapInfo = importedSet!.PerformRead(set => set.Beatmaps.First(b => b.Ruleset.ShortName == ruleset.RulesetInfo.ShortName).Detach());

                        score.ScoreInfo.BeatmapInfo = beatmapInfo;
                        score.ScoreInfo.Ruleset = ruleset.RulesetInfo;
                        score.ScoreInfo.User = new APIUser { Id = 2100 + ruleset.RulesetInfo.OnlineID, Username = $"trybuild_{ruleset.ShortName}" };

                        using var replayStream = new MemoryStream();
                        new LegacyScoreEncoder(score, playableBeatmap).Encode(replayStream);
                        var imported = ImportScoreTest.LoadScoreIntoOsu(osu, score.ScoreInfo, new ByteArrayArchiveReader(replayStream.ToArray(), "replay.osr"));

                        builderTimeline = EzScoreTimelineBuilder.TryBuild(
                            scoreManager,
                            beatmapManager,
                            imported,
                            sharedPlayableBeatmap: playableBeatmap);

                        var databasedScore = scoreManager.GetScore(imported)!;
                        var session = ruleset.CreateEzReplaySession()!;
                        sessionTotal = session.RunTimelineDirect(databasedScore, playableBeatmap, ReplayRunPurpose.ForLive).FinalTotalScore;
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                    finally
                    {
                        done = true;
                    }
                });

                waitForOrAssert(() => done, $"{rulesetType.Name} TryBuild did not complete");

                if (error != null)
                    throw error;

                Assert.That(builderTimeline, Is.Not.Null);
                Assert.That(builderTimeline!.FinalTotalScore, Is.EqualTo(sessionTotal));
                Assert.That(builderTimeline.FinalTotalScore, Is.GreaterThan(0));
            }
            finally
            {
                host.Exit();
            }
        }

        private static (Score score, IBeatmap beatmap) createTinyScore(Ruleset ruleset) => ruleset switch
        {
            OsuRuleset => createOsu(),
            ManiaRuleset => createMania(),
            TaikoRuleset => createTaiko(),
            CatchRuleset => createCatch(),
            _ => throw new ArgumentOutOfRangeException(nameof(ruleset)),
        };

        private static (Score score, IBeatmap beatmap) createOsu()
        {
            var ruleset = new OsuRuleset();
            var test = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new HitCircle { StartTime = 1000, Position = new Vector2(256, 192) },
                    new HitCircle { StartTime = 2000, Position = new Vector2(300, 200) },
                },
            };
            var beatmap = prepareConverted(ruleset, test);
            var c1 = (HitCircle)beatmap.HitObjects[0];
            var c2 = (HitCircle)beatmap.HitObjects[1];
            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new OsuReplayFrame(1000, c1.StackedPosition, OsuAction.LeftButton),
                    new OsuReplayFrame(1100, c1.StackedPosition),
                    new OsuReplayFrame(2000, c2.StackedPosition, OsuAction.LeftButton),
                },
            };
            return (score(ruleset, beatmap, replay), beatmap);
        }

        private static (Score score, IBeatmap beatmap) createMania()
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                    new Note { StartTime = 2000, Column = 0 },
                },
                ControlPointInfo = new ControlPointInfo(),
            };
            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(1100),
                    new ManiaReplayFrame(2000, ManiaAction.Key1),
                    new ManiaReplayFrame(2100),
                },
            };
            return (score(ruleset, beatmap, replay), beatmap);
        }

        private static (Score score, IBeatmap beatmap) createTaiko()
        {
            var ruleset = new TaikoRuleset();
            var test = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new Hit { StartTime = 1000, Type = HitType.Centre },
                    new Hit { StartTime = 2000, Type = HitType.Centre },
                },
            };
            var beatmap = prepareConverted(ruleset, test);
            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new TaikoReplayFrame(1000, TaikoAction.LeftCentre),
                    new TaikoReplayFrame(1100),
                    new TaikoReplayFrame(2000, TaikoAction.LeftCentre),
                    new TaikoReplayFrame(2100),
                },
            };
            return (score(ruleset, beatmap, replay), beatmap);
        }

        private static (Score score, IBeatmap beatmap) createCatch()
        {
            var ruleset = new CatchRuleset();
            var test = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new Fruit { StartTime = 1000, X = 200 },
                    new Fruit { StartTime = 2000, X = 200 },
                },
            };
            var beatmap = prepareConverted(ruleset, test);
            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new CatchReplayFrame(999, 200),
                    new CatchReplayFrame(1000, 200),
                    new CatchReplayFrame(1999, 200),
                    new CatchReplayFrame(2000, 200),
                },
            };
            return (score(ruleset, beatmap, replay), beatmap);
        }

        private static IBeatmap prepareConverted(Ruleset ruleset, TestBeatmap testBeatmap)
        {
            var beatmap = ruleset.CreateBeatmapConverter(testBeatmap).Convert();
            var processor = ruleset.CreateBeatmapProcessor(beatmap);
            processor?.PreProcess();

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty);

            processor?.PostProcess();
            return beatmap;
        }

        private static Score score(Ruleset ruleset, IBeatmap beatmap, Replay replay) => new Score
        {
            ScoreInfo = new ScoreInfo
            {
                Ruleset = ruleset.RulesetInfo,
                BeatmapInfo = beatmap.BeatmapInfo,
                ID = Guid.NewGuid(),
                Hash = Guid.NewGuid().ToString("N"),
            },
            Replay = replay,
        };

        private static void waitForOrAssert(Func<bool> result, string failureMessage, int timeout = 60000)
        {
            Task task = Task.Run(() =>
            {
                while (!result()) Thread.Sleep(200);
            });

            ClassicAssert.True(task.Wait(timeout), failureMessage);
        }
    }
}
