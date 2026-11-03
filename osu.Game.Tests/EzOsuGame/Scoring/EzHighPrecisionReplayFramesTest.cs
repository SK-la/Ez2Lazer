// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Beatmaps.Formats;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    [TestFixture]
    public class EzHighPrecisionReplayFramesTest
    {
        [Test]
        public void EzFramesRoundTripPreservesSubMillisecondFrameTimes()
        {
            var ruleset = new OsuRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo);
            var score = new Score
            {
                ScoreInfo = TestResources.CreateTestScoreInfo(ruleset.RulesetInfo),
                Replay = new Replay
                {
                    Frames =
                    {
                        new OsuReplayFrame(1000.5, OsuPlayfield.BASE_SIZE / 2, OsuAction.LeftButton),
                        new OsuReplayFrame(1100.25, OsuPlayfield.BASE_SIZE / 2),
                    }
                }
            };

            using var ms = new MemoryStream();
            EzHighPrecisionReplayFrames.Write(ms, score, beatmap);
            ms.Position = 0;

            Assert.That(EzHighPrecisionReplayFrames.TryRead(ms, out var legacyFrames), Is.True);

            var replay = new Replay();
            EzHighPrecisionReplayFrames.PopulateReplay(replay, legacyFrames, ruleset, beatmap);

            Assert.That(replay.Frames, Has.Count.EqualTo(2));
            Assert.That(replay.Frames[0].Time, Is.EqualTo(1000.5).Within(1e-6));
            Assert.That(replay.Frames[1].Time, Is.EqualTo(1100.25).Within(1e-6));
        }

        [Test]
        public void DefaultOsrEncodeStillQuantisesFrameTimesToIntegers()
        {
            var ruleset = new OsuRuleset().RulesetInfo;
            var beatmap = new TestBeatmap(ruleset);
            var score = new Score
            {
                ScoreInfo = TestResources.CreateTestScoreInfo(ruleset),
                Replay = new Replay
                {
                    Frames =
                    {
                        new OsuReplayFrame(1000.6, OsuPlayfield.BASE_SIZE / 2, OsuAction.LeftButton),
                        new OsuReplayFrame(1100.4, OsuPlayfield.BASE_SIZE / 2),
                    }
                }
            };

            using var ms = new MemoryStream();
            new LegacyScoreEncoder(score, beatmap).Encode(ms, leaveOpen: true);
            ms.Position = 0;

            var decoded = new LegacyScoreDecoderTest.TestLegacyScoreDecoder().Parse(ms);

            Assert.That(decoded.Replay.Frames, Has.Count.EqualTo(2));
            Assert.That(decoded.Replay.Frames[0].Time, Is.EqualTo(1001));
            Assert.That(decoded.Replay.Frames[1].Time, Is.EqualTo(1100));
        }
    }
}
