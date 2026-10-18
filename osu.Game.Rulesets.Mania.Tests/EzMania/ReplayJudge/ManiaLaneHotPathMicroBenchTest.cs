// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// DRAWABLE-MICRO-BENCH：10 列叠 LN；每列 KPS（10 / 15 / 20）× 每列存活密度扫描。
    /// </summary>
    [TestFixture]
    public class ManiaLaneHotPathMicroBenchTest
    {
        [TestCase(10, 8)]
        [TestCase(15, 8)]
        [TestCase(20, 8)]
        [TestCase(20, 24)]
        [TestCase(20, 40)]
        public void TestTenKeyDenseLnPerColumnKpsAndAliveSweep(int perColumnKps, int alivePerColumn)
        {
            var result = run(perColumnKps, alivePerColumn, EzEnumJudgePrecedence.Combo);
            TestContext.WriteLine(result.ToString());

            Assert.That(result.FrameCount, Is.EqualTo(2000));
            Assert.That(result.PressCount, Is.GreaterThan(0));
            Assert.That(result.ElapsedMilliseconds, Is.LessThan(5_000), result.ToString());
            Assert.That(result.AutoMissQueuePolls, Is.EqualTo(result.FrameCount * result.Keys));
        }

        [Test]
        public void TestPerColumnKpsScaleIsNearLinearOnSelectCalls()
        {
            var results = new Dictionary<int, ManiaLaneHotPathWorkloadResult>();

            foreach (int kps in new[] { 10, 15, 20 })
            {
                results[kps] = run(kps, alivePerColumn: 24, EzEnumJudgePrecedence.Combo);
                TestContext.WriteLine(results[kps].ToString());
            }

            double ratio15 = (double)results[15].PressCount / results[10].PressCount;
            double ratio20 = (double)results[20].PressCount / results[10].PressCount;

            Assert.That(ratio15, Is.EqualTo(1.5).Within(0.15));
            Assert.That(ratio20, Is.EqualTo(2.0).Within(0.2));
            Assert.That(results[20].ElapsedMilliseconds, Is.LessThan(results[10].ElapsedMilliseconds * 5 + 100),
                $"Non-linear blow-up: {results[10]} | {results[20]}");
        }

        [Test]
        public void TestAliveDensityDoesNotBlowUpAllocPerPress()
        {
            var light = run(20, alivePerColumn: 8, EzEnumJudgePrecedence.Combo);
            var heavy = run(20, alivePerColumn: 40, EzEnumJudgePrecedence.Combo);
            TestContext.WriteLine($"light: {light}");
            TestContext.WriteLine($"heavy: {heavy}");

            Assert.That(heavy.BytesPerPress, Is.LessThan(light.BytesPerPress * 3 + 256),
                $"Alloc/press blow-up light={light.BytesPerPress:F0} heavy={heavy.BytesPerPress:F0}");
            Assert.That(heavy.BytesPerPress, Is.LessThan(512),
                $"Combo Select still allocating heavily: {heavy.BytesPerPress:F0} B/press");
            Assert.That(heavy.ElapsedMilliseconds, Is.LessThan(light.ElapsedMilliseconds * 8 + 100),
                $"Time blow-up light={light.ElapsedMilliseconds} heavy={heavy.ElapsedMilliseconds}");
        }

        [TestCase(EzEnumJudgePrecedence.Earliest)]
        [TestCase(EzEnumJudgePrecedence.Combo)]
        [TestCase(EzEnumJudgePrecedence.Duration)]
        public void TestPrecedenceVariantsAt20KpsDense(EzEnumJudgePrecedence precedence)
        {
            var result = run(20, alivePerColumn: 40, precedence);
            TestContext.WriteLine(result.ToString());
            Assert.That(result.ElapsedMilliseconds, Is.LessThan(5_000), result.ToString());
        }

        [Test]
        public void TestBmsPoorSelectFlagsDoNotBlowUpAlloc()
        {
            var baseline = run(20, 40, EzEnumJudgePrecedence.Combo);
            var bms = new ManiaLaneHotPathWorkload
            {
                Keys = 10,
                PerColumnKps = 20,
                ConcurrentAlivePerColumn = 40,
                AliveSpacingMs = 12,
                DurationMs = 2000,
                FrameStepMs = 1,
                Precedence = EzEnumJudgePrecedence.Combo,
                JudgeMode = EzEnumJudgeMode.IIDX_HD,
                AllowBmsFallbackToEarliest = true,
                PoorEnabled = true,
            }.Run();

            TestContext.WriteLine($"baseline: {baseline}");
            TestContext.WriteLine($"bms: {bms}");

            Assert.That(bms.BytesPerPress, Is.LessThan(512), bms.ToString());
            Assert.That(bms.BytesPerPress, Is.LessThan(baseline.BytesPerPress * 4 + 256),
                $"BMS/Poor alloc blow-up baseline={baseline.BytesPerPress:F0} bms={bms.BytesPerPress:F0}");
            Assert.That(bms.ElapsedMilliseconds, Is.LessThan(5_000), bms.ToString());
        }

        [Test]
        public void TestFutureDeadlinesNeverVisitAutoMissCandidate()
        {
            var result = ManiaLaneHotPathWorkload.RunFutureDeadlineGuard();
            TestContext.WriteLine(result.ToString());

            Assert.That(result.AutoMissQueuePolls, Is.GreaterThan(0));
            Assert.That(result.AutoMissDueVisits, Is.EqualTo(0));
            Assert.That(result.AllocatedBytes, Is.LessThan(64_000), result.ToString());
        }

        private static ManiaLaneHotPathWorkloadResult run(int perColumnKps, int alivePerColumn, EzEnumJudgePrecedence precedence)
            => new ManiaLaneHotPathWorkload
            {
                Keys = 10,
                PerColumnKps = perColumnKps,
                ConcurrentAlivePerColumn = alivePerColumn,
                AliveSpacingMs = 12,
                DurationMs = 2000,
                FrameStepMs = 1,
                Precedence = precedence,
            }.Run();
    }
}
