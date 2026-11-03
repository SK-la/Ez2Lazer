// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Replays;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaReplayFrameEdgeParserTest
    {
        [SetUp]
        public void SetUp() => ReplayJudgeTestConfig.ApplyToGlobalConfig(LazerTapReplayFixtures.CreateTwoNoteColumnTap().environment);

        [TearDown]
        public void TearDown() => ReplayJudgeTestConfig.ResetGlobalConfig();

        [Test]
        public void TestStreamDrainAllMatchesBatchParse()
        {
            var (score, _, _) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
            Assert.That(score.Replay, Is.Not.Null);

            var batch = ManiaReplayFrameEdgeParser.ParseAll(score.Replay!);
            var streamed = ManiaReplayFrameEdgeParser.CreateCursor(score.Replay!).DrainAll();

            Assert.That(streamed.SortedEvents.Count, Is.EqualTo(batch.SortedEvents.Count));

            for (int i = 0; i < batch.SortedEvents.Count; i++)
            {
                Assert.That(streamed.SortedEvents[i].Time, Is.EqualTo(batch.SortedEvents[i].Time));
                Assert.That(streamed.SortedEvents[i].Column, Is.EqualTo(batch.SortedEvents[i].Column));
                Assert.That(streamed.SortedEvents[i].IsPress, Is.EqualTo(batch.SortedEvents[i].IsPress));
            }
        }

        [Test]
        public void TestIsManiaReplayRejectsEmpty()
        {
            Assert.That(ManiaReplayFrameEdgeParser.IsManiaReplay(null), Is.False);
            Assert.That(ManiaReplayFrameEdgeParser.IsManiaReplay(new Replay()), Is.False);
        }
    }

    [TestFixture]
    public class ManiaRunHitEventsLatencyTest
    {
        [SetUp]
        public void SetUp() => ReplayJudgeTestConfig.ApplyToGlobalConfig(LazerTapReplayFixtures.CreateTwoNoteColumnTap().environment);

        [TearDown]
        public void TearDown() => ReplayJudgeTestConfig.ResetGlobalConfig();

        [Test]
        public async Task TestRunHitEventsAsyncWarmLatencyUnderBudget()
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
            var service = new ManiaReplaySessionService();

            // 预热缓存 / JIT
            _ = await service.RunHitEventsAsync(score, beatmap, ReplayRunPurpose.ForStored).ConfigureAwait(true);

            var sw = Stopwatch.StartNew();
            var events = await service.RunHitEventsAsync(score, beatmap, ReplayRunPurpose.ForStored).ConfigureAwait(true);
            sw.Stop();

            Assert.That(events, Is.Not.Null);
            Assert.That(events.Count, Is.GreaterThan(0));
            // 合成两键谱：缓存命中后应远低于 10ms；放宽到 50ms 避免 CI 抖动误杀。
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(50),
                $"RunHitEventsAsync warm latency {sw.ElapsedMilliseconds}ms (events={events.Count})");
        }
    }
}
