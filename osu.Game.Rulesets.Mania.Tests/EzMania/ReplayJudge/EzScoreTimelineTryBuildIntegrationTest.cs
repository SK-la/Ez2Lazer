// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.IO.Archives;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.Scoring;
using osu.Game.Rulesets.Mania.EzMania.Statistics;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Scores.IO;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class EzScoreTimelineTryBuildIntegrationTest : ImportTest
    {
        [Test]
        public void TestTryBuildManiaUsesSessionTimelineThroughScoreManager()
        {
            using var host = new CleanRunHeadlessGameHost();

            try
            {
                var osu = LoadOsuIntoHost(host, withBeatmap: false);
                EzScoreTimeline? timeline = null;
                long sessionTotal = 0;
                bool done = false;
                Exception? error = null;

                host.UpdateThread.Scheduler.Add(() =>
                {
                    try
                    {
                        var scoreManager = osu.Dependencies.Get<ScoreManager>();
                        var beatmapManager = osu.Dependencies.Get<BeatmapManager>();

                        var (score, testBeatmap, _) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
                        var ruleset = new ManiaRuleset();
                        var playableBeatmap = ruleset.CreateBeatmapConverter(testBeatmap).Convert();

                        var importedSet = beatmapManager.Import(TestResources.CreateTestBeatmapSetInfo(1, rulesets: new[] { ruleset.RulesetInfo }));
                        Assert.That(importedSet, Is.Not.Null);

                        var beatmapInfo = importedSet!.PerformRead(set => set.Beatmaps.First(b => b.Ruleset.ShortName == ruleset.RulesetInfo.ShortName).Detach());

                        score.ScoreInfo.BeatmapInfo = beatmapInfo;
                        score.ScoreInfo.Ruleset = ruleset.RulesetInfo;
                        score.ScoreInfo.User = new APIUser { Id = 1001, Username = "timeline_test" };

                        using (var replayStream = new MemoryStream())
                        {
                            new LegacyScoreEncoder(score, playableBeatmap).Encode(replayStream);
                            var imported = ImportScoreTest.LoadScoreIntoOsu(osu, score.ScoreInfo, new ByteArrayArchiveReader(replayStream.ToArray(), "replay.osr"));

                            _ = ManiaScoreHitEventGenerator.Instance;

                            timeline = EzScoreTimelineBuilder.TryBuild(scoreManager, beatmapManager, imported, sharedPlayableBeatmap: playableBeatmap);
                            var databasedScore = scoreManager.GetScore(imported);
                            sessionTotal = ManiaReplaySession.RunTimeline(databasedScore!, playableBeatmap, ManiaRuleset.ResolveEnvironment(null, GlobalConfigStore.EzConfig, ManiaReplayRunPurpose.ForRaceTimeline))
                                .FinalTotalScore;
                        }
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

                waitForOrAssert(() => done, "timeline build did not complete");

                if (error != null)
                    throw error;

                Assert.That(timeline, Is.Not.Null);
                Assert.That(timeline!.FinalTotalScore, Is.EqualTo(sessionTotal));
                Assert.That(timeline.FinalTotalScore, Is.GreaterThan(0));
            }
            finally
            {
                host.Exit();
            }
        }

        [Test]
        public void TestTryBuildReturnsNullWhenReplayUnavailable()
        {
            using var host = new CleanRunHeadlessGameHost();

            try
            {
                var osu = LoadOsuIntoHost(host, withBeatmap: true);
                EzScoreTimeline? timeline = null;
                bool done = false;
                Exception? error = null;

                host.UpdateThread.Scheduler.Add(() =>
                {
                    try
                    {
                        var scoreManager = osu.Dependencies.Get<ScoreManager>();
                        var beatmapManager = osu.Dependencies.Get<BeatmapManager>();
                        var beatmapInfo = beatmapManager.GetAllUsableBeatmapSets().First().Beatmaps.First();

                        var imported = ImportScoreTest.LoadScoreIntoOsu(osu, new ScoreInfo
                        {
                            Ruleset = beatmapInfo.Ruleset,
                            BeatmapInfo = beatmapInfo,
                            User = new APIUser { Id = 1002, Username = "no_replay" },
                        }, new MemoryStreamArchiveReader(new MemoryStream(), "replay.osr"));

                        timeline = EzScoreTimelineBuilder.TryBuild(scoreManager, beatmapManager, imported);
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

                waitForOrAssert(() => done, "TryBuild null check did not complete");

                if (error != null)
                    throw error;

                Assert.That(timeline, Is.Null);
            }
            finally
            {
                host.Exit();
            }
        }

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
