// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using osu.Game.Beatmaps.Formats;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.IO.Archives;
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
        [TestCase(4)]
        [TestCase(14)]
        public void EzFramesRoundTripPreservesSubMillisecondFrameTimes(int beatmapVersion)
        {
            var ruleset = new OsuRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo) { BeatmapVersion = beatmapVersion };
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
            double storedFirstTime = 1000.5 - (beatmapVersion < 5 ? LegacyBeatmapDecoder.EARLY_VERSION_TIMING_OFFSET : 0);
            Assert.That(legacyFrames[0].Time, Is.EqualTo(storedFirstTime));

            var replay = new Replay();
            EzHighPrecisionReplayFrames.PopulateReplay(replay, legacyFrames, ruleset, beatmap);

            Assert.That(replay.Frames, Has.Count.EqualTo(2));
            Assert.That(replay.Frames[0].Time, Is.EqualTo(1000.5).Within(1e-6));
            Assert.That(replay.Frames[1].Time, Is.EqualTo(1100.25).Within(1e-6));
            Assert.That(legacyFrames[0].Time, Is.EqualTo(storedFirstTime));
        }

        [TestCase(4, -1, 0)]
        [TestCase(4, 1, 0)]
        [TestCase(24, 0, 20)]
        [TestCase(24, 1, 19)]
        public void EzFramesRejectInvalidPayload(int payloadLength, int frameCount, int frameBytes)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write("EZFR"u8);
            writer.Write(EzHighPrecisionReplayFrames.VERSION);
            writer.Write(payloadLength);
            writer.Write(frameCount);
            writer.Write(new byte[frameBytes]);
            stream.Position = 0;

            Assert.That(EzHighPrecisionReplayFrames.TryRead(stream, out var frames), Is.False);
            Assert.That(frames, Is.Empty);
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

        [Test]
        public void EzOsrContainsRoundedLegacyReplayAndHighPrecisionFrames()
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
                        new OsuReplayFrame(1000.2, OsuPlayfield.BASE_SIZE / 2, OsuAction.LeftButton),
                        new OsuReplayFrame(1000.4, OsuPlayfield.BASE_SIZE / 2),
                        new OsuReplayFrame(1100.25, OsuPlayfield.BASE_SIZE / 2, OsuAction.LeftButton),
                    }
                }
            };
            score.ScoreInfo.JudgeMode = (int)EzEnumJudgeMode.O2Jam;
            score.ScoreInfo.HealthMode = (int)EzEnumHealthMode.O2JamNormal;
            score.ScoreInfo.GameplayEnvironmentSnapshot = new GameplayEnvironment
            {
                JudgeMode = EzEnumJudgeMode.O2Jam,
                HealthMode = EzEnumHealthMode.O2JamNormal,
                JudgePrecedence = EzEnumJudgePrecedence.Earliest,
                OffsetPlusMania = 12.5,
                OffsetPlusNonMania = -4.25,
                BmsPoorHitResultEnable = true,
                ApplyInputOffsetViaReplayFrameShift = true,
            };

            using var sourceHighPrecisionReplay = new MemoryStream();
            EzHighPrecisionReplayFrames.Write(sourceHighPrecisionReplay, score, beatmap);
            byte[] sourceFrames = sourceHighPrecisionReplay.ToArray();

            using var archiveStream = new MemoryStream();
            EzReplayExportEncoder.EncodeEzOsr(score, beatmap, archiveStream, sourceHighPrecisionReplay);
            archiveStream.Position = 0;

            using var archive = new ZipArchiveReader(archiveStream, "test.ezosr");
            Assert.That(archive.Filenames, Is.EquivalentTo(new[] { EzReplayExportEncoder.LEGACY_REPLAY_FILENAME, EzHighPrecisionReplayFrames.FILENAME }));

            using (var highPrecisionStream = archive.GetStream(EzHighPrecisionReplayFrames.FILENAME))
            {
                using var copiedFrames = new MemoryStream();
                highPrecisionStream.CopyTo(copiedFrames);
                Assert.That(copiedFrames.ToArray(), Is.EqualTo(sourceFrames));
                copiedFrames.Position = 0;

                Assert.That(EzHighPrecisionReplayFrames.TryRead(copiedFrames, out var frames), Is.True);
                Assert.That(frames, Has.Count.EqualTo(3));
                Assert.That(frames[0].Time, Is.EqualTo(1000.2).Within(1e-6));
                Assert.That(frames[1].Time, Is.EqualTo(1000.4).Within(1e-6));
                Assert.That(frames[2].Time, Is.EqualTo(1100.25).Within(1e-6));
            }

            using (var legacyStream = archive.GetStream(EzReplayExportEncoder.LEGACY_REPLAY_FILENAME))
            {
                var decoded = new LegacyScoreDecoderTest.TestLegacyScoreDecoder().Parse(legacyStream);
                Assert.That(decoded.Replay.Frames, Has.Count.EqualTo(3));
                Assert.That(decoded.Replay.Frames[0].Time, Is.EqualTo(1000));
                Assert.That(decoded.Replay.Frames[1].Time, Is.EqualTo(1000));
                Assert.That(decoded.Replay.Frames[2].Time, Is.EqualTo(1100));
                Assert.That(decoded.ScoreInfo.JudgeMode, Is.EqualTo((int)EzEnumJudgeMode.O2Jam));
                Assert.That(decoded.ScoreInfo.HealthMode, Is.EqualTo((int)EzEnumHealthMode.O2JamNormal));
                Assert.That(decoded.ScoreInfo.GameplayEnvironmentSnapshot, Is.Not.Null);
                Assert.That(decoded.ScoreInfo.GameplayEnvironmentSnapshot!.JudgeMode, Is.EqualTo(EzEnumJudgeMode.O2Jam));
                Assert.That(decoded.ScoreInfo.GameplayEnvironmentSnapshot.HealthMode, Is.EqualTo(EzEnumHealthMode.O2JamNormal));
                Assert.That(decoded.ScoreInfo.GameplayEnvironmentSnapshot.JudgePrecedence, Is.EqualTo(EzEnumJudgePrecedence.Earliest));
                Assert.That(decoded.ScoreInfo.GameplayEnvironmentSnapshot.OffsetPlusMania, Is.EqualTo(12.5));
                Assert.That(decoded.ScoreInfo.GameplayEnvironmentSnapshot.OffsetPlusNonMania, Is.EqualTo(-4.25));
                Assert.That(decoded.ScoreInfo.GameplayEnvironmentSnapshot.BmsPoorHitResultEnable, Is.True);
                Assert.That(decoded.ScoreInfo.GameplayEnvironmentSnapshot.ApplyInputOffsetViaReplayFrameShift, Is.True);
            }
        }
    }
}
