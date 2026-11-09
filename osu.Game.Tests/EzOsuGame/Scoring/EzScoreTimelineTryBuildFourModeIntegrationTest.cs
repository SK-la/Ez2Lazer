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
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.IO.Archives;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Replays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Replays;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Replays;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Scores.IO;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    /// <summary>
    /// P6: for every mode, <see cref="EzScoreTimelineBuilder.TryBuild"/> and a session driven exactly the way the
    /// builder drives one produce the same timeline for the same score.
    /// </summary>
    /// <remarks>
    /// The fixture has to be a chart the score itself is filed under. Two modes (mania, osu) let the builder attach a
    /// beatmap provider, which re-derives the chart from the score's beatmap and ignores whatever playable it is
    /// handed; a fixture that paired a synthetic chart with an unrelated databased beatmap would therefore compare two
    /// different charts and read as a builder defect. So the replay is generated from the databased chart's own
    /// playable, and the reference session is set up the way production sets it up for that mode.
    /// </remarks>
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
                // The standard test archive carries one difficulty per ruleset, imported before the host finishes
                // loading so the score can be filed under it.
                var osu = LoadOsuIntoHost(host, withBeatmap: true);
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
                        var beatmapInfo = findBeatmap(beatmapManager, ruleset.RulesetInfo);
                        var workingBeatmap = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                        var playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, Array.Empty<Mod>());

                        Assert.That(playableBeatmap.HitObjects.Count, Is.GreaterThan(0), "the fixture chart has no objects");

                        var score = createScore(ruleset, beatmapInfo, playableBeatmap);
                        score.ScoreInfo.User = new APIUser { Id = 2100 + ruleset.RulesetInfo.OnlineID, Username = $"trybuild_{ruleset.ShortName}" };

                        using var replayStream = new MemoryStream();
                        new LegacyScoreEncoder(score, playableBeatmap).Encode(replayStream);
                        var imported = ImportScoreTest.LoadScoreIntoOsu(osu, score.ScoreInfo, new ByteArrayArchiveReader(replayStream.ToArray(), "replay.osr"));

                        // Modes with a beatmap provider take the chart from the provider, so production hands them no
                        // playable at all; the others reuse one session-ready conversion.
                        bool usesProvider = usesBeatmapProvider(ruleset);
                        var sharedPlayable = usesProvider
                            ? null
                            : EzScoreRacePlayableResolver.GetSessionReady(workingBeatmap, ruleset.RulesetInfo, Array.Empty<Mod>());

                        builderTimeline = EzScoreTimelineBuilder.TryBuild(scoreManager, beatmapManager, imported, sharedPlayableBeatmap: sharedPlayable);

                        var databasedScore = scoreManager.GetScore(imported)!;
                        var session = ruleset.CreateEzReplaySession()!;

                        if (usesProvider)
                            session.AttachBeatmaps(beatmapManager);

                        sessionTotal = session.RunTimelineDirect(databasedScore, sharedPlayable ?? playableBeatmap, ReplayRunPurpose.ForLive).FinalTotalScore;
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

        private static bool usesBeatmapProvider(Ruleset ruleset)
            => EzScoreRaceRulesetSupport.GetGhostTimelineMode(ruleset.RulesetInfo)
               is EzScoreRaceGhostTimelineMode.ManiaSession or EzScoreRaceGhostTimelineMode.OsuSession;

        private static BeatmapInfo findBeatmap(BeatmapManager beatmapManager, RulesetInfo rulesetInfo)
        {
            foreach (var set in beatmapManager.GetAllUsableBeatmapSets())
            {
                var detached = set.Detach();
                var match = detached.Beatmaps.FirstOrDefault(b => b.Ruleset.ShortName == rulesetInfo.ShortName);

                if (match != null)
                    return match;
            }

            throw new AssertionException($"no imported beatmap for {rulesetInfo.ShortName}");
        }

        private static Score createScore(Ruleset ruleset, BeatmapInfo beatmapInfo, IBeatmap beatmap) => new Score
        {
            ScoreInfo = new ScoreInfo
            {
                Ruleset = ruleset.RulesetInfo,
                BeatmapInfo = beatmapInfo,
                ID = Guid.NewGuid(),
                Hash = Guid.NewGuid().ToString("N"),
            },
            Replay = new Replay { Frames = buildFrames(ruleset, beatmap) },
        };

        /// <summary>
        /// A deterministic replay against the chart it is paired with.
        /// </summary>
        /// <remarks>
        /// It does not have to be a good play: both sides are handed the same frames and the same chart, so any
        /// difference in the resulting timeline is the thing under test rather than the quality of the input.
        /// </remarks>
        private static List<ReplayFrame> buildFrames(Ruleset ruleset, IBeatmap beatmap)
        {
            switch (ruleset)
            {
                case OsuRuleset:
                {
                    var frames = new List<ReplayFrame>();

                    foreach (var obj in beatmap.HitObjects.OfType<OsuHitObject>())
                    {
                        frames.Add(new OsuReplayFrame(obj.StartTime, obj.StackedPosition, OsuAction.LeftButton));
                        frames.Add(new OsuReplayFrame(obj.StartTime + 10, obj.StackedPosition));
                    }

                    return frames;
                }

                case TaikoRuleset:
                {
                    var frames = new List<ReplayFrame>();

                    foreach (var obj in beatmap.HitObjects)
                    {
                        frames.Add(new TaikoReplayFrame(obj.StartTime, TaikoAction.LeftCentre));
                        frames.Add(new TaikoReplayFrame(obj.StartTime + 10));
                    }

                    return frames;
                }

                case CatchRuleset:
                {
                    var frames = new List<ReplayFrame>();

                    foreach (var obj in beatmap.HitObjects)
                    {
                        float x = obj is IHasXPosition hasX ? hasX.X : 256;
                        frames.Add(new CatchReplayFrame(obj.StartTime, x));
                    }

                    return frames;
                }

                case ManiaRuleset:
                {
                    var frames = new List<ReplayFrame>();

                    foreach (var obj in beatmap.HitObjects.OfType<ManiaHitObject>())
                    {
                        var action = (ManiaAction)((int)ManiaAction.Key1 + obj.Column);

                        frames.Add(new ManiaReplayFrame(obj.StartTime, action));
                        frames.Add(new ManiaReplayFrame(obj.StartTime + 10));
                    }

                    return frames;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(ruleset));
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
