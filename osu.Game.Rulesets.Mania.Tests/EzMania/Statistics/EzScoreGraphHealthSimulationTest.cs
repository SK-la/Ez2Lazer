// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.Statistics;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Statistics
{
    /// <summary>
    /// 图表血量曲线与游戏内血量处理器同源：档位行号、Lazer 公式、Poor 计入。
    /// </summary>
    [TestFixture]
    public class EzScoreGraphHealthSimulationTest
    {
        private const float drain_rate = 8;

        [Test]
        public void TestHealthModeRowMapping()
        {
            // 枚举值不是行号（Mania 私有档编码 31+），必须显式映射到 HEALTH_MODE_MAP。
            Assert.Multiple(() =>
            {
                Assert.That(HealthModeHelper.GetRow(EzEnumHealthMode.StableClassic), Is.EqualTo(1));
                Assert.That(HealthModeHelper.GetRow(EzEnumHealthMode.O2JamEasy), Is.EqualTo(1));
                Assert.That(HealthModeHelper.GetRow(EzEnumHealthMode.O2JamNormal), Is.EqualTo(2));
                Assert.That(HealthModeHelper.GetRow(EzEnumHealthMode.O2JamHard), Is.EqualTo(3));
                Assert.That(HealthModeHelper.GetRow(EzEnumHealthMode.Ez2Ac), Is.EqualTo(4));
                Assert.That(HealthModeHelper.GetRow(EzEnumHealthMode.IIDX_HD), Is.EqualTo(5));
                Assert.That(HealthModeHelper.GetRow(EzEnumHealthMode.LR2_HD), Is.EqualTo(6));
                Assert.That(HealthModeHelper.GetRow(EzEnumHealthMode.Raja_HD), Is.EqualTo(7));
            });
        }

        [Test]
        public void TestLazerMissUsesGameFormula()
        {
            var beatmap = createBeatmap(10);
            var health = simulate(beatmap, EzEnumHealthMode.Lazer, new[] { createEvent(beatmap.HitObjects[0], HitResult.Miss) });

            // Lazer 档 Miss = -(DR + 1) * 0.0075，不是 ManiaJudgement 的固定 -0.1。
            Assert.That(health.Single(), Is.EqualTo(1 - (drain_rate + 1) * 0.0075).Within(1e-6));
        }

        [TestCase(EzEnumHealthMode.StableClassic, 0.05)]
        [TestCase(EzEnumHealthMode.O2JamEasy, 0.05)]
        [TestCase(EzEnumHealthMode.O2JamNormal, 0.04)]
        [TestCase(EzEnumHealthMode.O2JamHard, 0.03)]
        [TestCase(EzEnumHealthMode.Ez2Ac, 0.05)]
        [TestCase(EzEnumHealthMode.IIDX_HD, 0.09)]
        [TestCase(EzEnumHealthMode.LR2_HD, 0.10)]
        [TestCase(EzEnumHealthMode.Raja_HD, 0.10)]
        public void TestNonLazerMissUsesMappedRow(EzEnumHealthMode healthMode, double expectedLoss)
        {
            var beatmap = createBeatmap(10);
            var health = simulate(beatmap, healthMode, new[] { createEvent(beatmap.HitObjects[0], HitResult.Miss) });

            Assert.That(health.Single(), Is.EqualTo(1 - expectedLoss).Within(1e-6));
        }

        [Test]
        public void TestPoorCountsTowardsHealth()
        {
            var beatmap = createBeatmap(10);

            // Poor 不是散点事件，但游戏里扣血（Ez2Ac 行 -0.02），必须计入血量曲线。
            Assert.That(HitResult.Poor.IsBasic(), Is.True);

            var health = simulate(beatmap, EzEnumHealthMode.Ez2Ac, new[] { createEvent(beatmap.HitObjects[0], HitResult.Poor) });
            Assert.That(health.Single(), Is.EqualTo(0.98).Within(1e-6));
        }

        [Test]
        public void TestPassingRunDoesNotBottomOut()
        {
            // 1000 note / 20 miss 的 Lazer 成绩：游戏内接近满血，曲线不能被画到见底。
            var beatmap = createBeatmap(1000);
            var events = beatmap.HitObjects.Select((hitObject, index) => createEvent(hitObject, index % 50 == 0 ? HitResult.Miss : HitResult.Perfect)).ToList();

            var health = simulate(beatmap, EzEnumHealthMode.Lazer, events);

            Assert.That(health.Min(), Is.GreaterThan(0.9));
        }

        private static IReadOnlyList<float> simulate(IBeatmap beatmap, EzEnumHealthMode healthMode, IReadOnlyList<HitEvent> events)
            => EzScoreGraphMania.SimulateHealth(beatmap, healthMode, events, e => e.Result);

        private static HitEvent createEvent(HitObject hitObject, HitResult result) => new HitEvent(0, 1, result, hitObject, null, null);

        private static ManiaBeatmap createBeatmap(int noteCount)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(4))
            {
                Difficulty = new BeatmapDifficulty { DrainRate = drain_rate, OverallDifficulty = 8 }
            };

            for (int i = 0; i < noteCount; i++)
                beatmap.HitObjects.Add(new Note { StartTime = i * 100 });

            return beatmap;
        }
    }
}
