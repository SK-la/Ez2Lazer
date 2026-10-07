// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Scoring.Bonus;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    [TestFixture]
    public class EzScoreBonusCalculatorTest
    {
        [Test]
        public void TestWeightsAreMonotonicAndSaturate()
        {
            double previous = -1;

            for (double k = 0; k <= 60; k += 0.5)
            {
                double judge = EzScoreBonusFormula.JudgeWeight(k);
                Assert.That(judge, Is.GreaterThanOrEqualTo(previous));
                previous = judge;
            }

            Assert.That(EzScoreBonusFormula.JudgeWeight(15), Is.EqualTo(0));
            Assert.That(EzScoreBonusFormula.JudgeWeight(40), Is.EqualTo(1));
            Assert.That(EzScoreBonusFormula.JudgeWeight(30), Is.EqualTo(0.648).Within(0.001));
        }

        [Test]
        public void TestReversedWeightsMirror()
        {
            for (double k = 0; k <= 60; k += 0.5)
            {
                Assert.That(EzScoreBonusFormula.JudgeWeight(k, favourHighKps: false), Is.EqualTo(1 - EzScoreBonusFormula.JudgeWeight(k)).Within(1e-12));
                Assert.That(EzScoreBonusFormula.MissWeight(k, favourHighKps: false),
                    Is.EqualTo(EzScoreBonusFormula.MissWeight(EzScoreBonusFormula.KPS_SATURATION - Math.Min(k, EzScoreBonusFormula.KPS_SATURATION))).Within(1e-12));
            }

            Assert.That(EzScoreBonusFormula.MissWeight(0, favourHighKps: false), Is.EqualTo(1));
            Assert.That(EzScoreBonusFormula.MissWeight(50, favourHighKps: false), Is.EqualTo(EzScoreBonusFormula.MISS_WEIGHT_FLOOR));
        }

        [Test]
        public void TestMissWeightCountsEveryKpsAndIncreases()
        {
            double previous = 0;

            for (double k = 0; k <= 60; k += 0.5)
            {
                double miss = EzScoreBonusFormula.MissWeight(k);
                Assert.That(miss, Is.GreaterThan(0));
                Assert.That(miss, Is.GreaterThanOrEqualTo(previous));
                previous = miss;
            }

            Assert.That(EzScoreBonusFormula.MissWeight(0), Is.EqualTo(EzScoreBonusFormula.MISS_WEIGHT_FLOOR));
            Assert.That(EzScoreBonusFormula.MissWeight(40), Is.EqualTo(1));
        }

        [Test]
        public void TestTendencyDirectionsAreMirrored()
        {
            // 判定加成跨谱面比较：低 KPS（8）与高 KPS（48）两张谱。
            var easyChart = createChart(120, chordSize: 1, quarterBeats: 256);
            var denseChart = createChart(180, chordSize: 4, quarterBeats: 64);
            var easy = EzScoreBonusCalculator.Calculate(easyChart, createEvents(easyChart, sigma: 4, missEvery: 0), 1);
            var dense = EzScoreBonusCalculator.Calculate(denseChart, createEvents(denseChart, sigma: 4, missEvery: 0), 1);

            Assert.That(easy.JudgeToMiss.JudgeBonus, Is.GreaterThan(dense.JudgeToMiss.JudgeBonus));
            Assert.That(dense.MissToJudge.JudgeBonus, Is.GreaterThan(easy.MissToJudge.JudgeBonus));

            // Miss 率谱内比较：前 16 小节单键（12 KPS）、后 16 小节四押（48 KPS），Miss 分别只放在稀疏段 / 密集段。
            var mixed = createMixedChart();
            int sparseCount = 16 * 16;

            var missInSparse = EzScoreBonusCalculator.Calculate(mixed, createEvents(mixed, i => i == 5 || i == 100), 1);
            var missInDense = EzScoreBonusCalculator.Calculate(mixed, createEvents(mixed, i => i == sparseCount + 5 || i == sparseCount + 500), 1);

            // JudgeToMiss：密集段 Miss 更重；MissToJudge：稀疏段 Miss 更重。
            Assert.That(missInDense.JudgeToMiss.WeightedMissRate, Is.GreaterThan(missInSparse.JudgeToMiss.WeightedMissRate));
            Assert.That(missInSparse.MissToJudge.WeightedMissRate, Is.GreaterThan(missInDense.MissToJudge.WeightedMissRate));
        }

        [Test]
        public void TestOffsetQualityMatchesResearchTable()
        {
            Assert.That(EzScoreBonusFormula.OffsetQuality(0), Is.EqualTo(1).Within(1e-9));
            Assert.That(EzScoreBonusFormula.OffsetQuality(-8), Is.EqualTo(0.825).Within(0.002));
            Assert.That(EzScoreBonusFormula.OffsetQuality(13), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(EzScoreBonusFormula.OffsetQuality(14), Is.EqualTo(0.151).Within(0.002));
            Assert.That(EzScoreBonusFormula.OffsetQuality(16), Is.EqualTo(0));

            double previous = 2;

            for (double e = 0; e <= 20; e += 0.25)
            {
                double f = EzScoreBonusFormula.OffsetQuality(e);
                Assert.That(f, Is.LessThanOrEqualTo(previous));
                previous = f;
            }
        }

        [Test]
        public void TestPerfectPlayOnDenseChartReachesCap()
        {
            // 200 BPM、每 1/4 拍 4 键（48 KPS）全 0ms。
            var beatmap = createChart(200, chordSize: 4, quarterBeats: 256);
            var result = EzScoreBonusCalculator.Calculate(beatmap, createEvents(beatmap, sigma: 0, missEvery: 0), 1);

            Assert.That(result.MissToJudge.JudgeBonus, Is.EqualTo(EzScoreBonusFormula.JUDGE_BONUS_MAX));
            Assert.That(result.JudgeToMiss.JudgeBonus, Is.EqualTo(0));
            Assert.That(result.MissToJudge.MissPenalty, Is.EqualTo(0));
            Assert.That(result.JudgeToMiss.MissPenalty, Is.EqualTo(0));
        }

        [Test]
        public void TestEasyChartGetsNoJudgeBonusButMissesStillCount()
        {
            // 120 BPM 单键 1/4（8 KPS）：MissToJudge 下判定加成为 0，但 Miss 仍计罚分。
            // 均匀密度时加权 Miss 率等于原始 Miss 率（4 / 256）。
            var beatmap = createChart(120, chordSize: 1, quarterBeats: 256);
            var result = EzScoreBonusCalculator.Calculate(beatmap, createEvents(beatmap, sigma: 0, missEvery: 64), 1);

            const double raw_rate = 4.0 / 256;

            Assert.That(result.MissToJudge.JudgeBonus, Is.EqualTo(0));
            Assert.That(result.JudgeToMiss.WeightedMissRate, Is.EqualTo(raw_rate).Within(1e-9));
            Assert.That(result.MissToJudge.WeightedMissRate, Is.EqualTo(raw_rate).Within(1e-9));
            Assert.That(result.JudgeToMiss.MissPenalty, Is.EqualTo(EzScoreBonusFormula.MissPenalty(raw_rate, judgeCoverage: 1)));
            Assert.That(result.MissToJudge.MissPenalty, Is.EqualTo(EzScoreBonusFormula.MissPenalty(raw_rate, judgeCoverage: 0)));
            Assert.That(result.MissToJudge.MissPenalty, Is.LessThan(0));
        }

        [Test]
        public void TestMissPenaltyCurveAnchors()
        {
            double anchor = EzScoreBonusFormula.AcceptedMissPenalty(1);

            Assert.That(anchor, Is.EqualTo(EzScoreBonusFormula.JUDGE_BONUS_MAX * EzScoreBonusFormula.OffsetQuality(EzScoreBonusFormula.ACCEPTED_ERROR_MS)).Within(1e-9));
            Assert.That(EzScoreBonusFormula.MissPenalty(0, 1), Is.EqualTo(0));
            Assert.That(EzScoreBonusFormula.MissPenalty(EzScoreBonusFormula.MISS_RATE_ACCEPTED, 1), Is.EqualTo(-(int)Math.Round(anchor)));
            Assert.That(EzScoreBonusFormula.MissPenalty(EzScoreBonusFormula.MISS_RATE_DISCOURAGED, 1), Is.EqualTo(-EzScoreBonusFormula.MISS_PENALTY_CAP));
            Assert.That(EzScoreBonusFormula.MissPenalty(0.1, 0), Is.EqualTo(-EzScoreBonusFormula.MISS_PENALTY_CAP));

            int previous = 1;

            for (double r = 0; r <= 0.03; r += 0.0005)
            {
                int penalty = EzScoreBonusFormula.MissPenalty(r, 0.5);
                Assert.That(penalty, Is.LessThanOrEqualTo(previous));
                previous = penalty;
            }
        }

        [Test]
        public void TestAcceptedErrorPlayerBreaksEvenAtAcceptedMissRate()
        {
            // 全谱 11ms、0.5% Miss：判定加成约等于 Miss 罚分（≥40 KPS 谱面、MissToJudge 下判定权重为 1）。
            var beatmap = createChart(200, chordSize: 4, quarterBeats: 512);
            var result = EzScoreBonusCalculator.Calculate(beatmap, createEvents(beatmap, sigma: 0, missEvery: 200, fixedOffset: 11), 1).MissToJudge;

            Assert.That(result.WeightedMissRate, Is.EqualTo(EzScoreBonusFormula.MISS_RATE_ACCEPTED).Within(0.0005));
            Assert.That(Math.Abs(result.Total), Is.LessThan(EzScoreBonusFormula.JUDGE_BONUS_MAX * 0.05));
        }

        [Test]
        public void TestMissPenaltyIsCapped()
        {
            var beatmap = createChart(200, chordSize: 4, quarterBeats: 256);
            var result = EzScoreBonusCalculator.Calculate(beatmap, createEvents(beatmap, sigma: 0, missEvery: 1), 1);

            Assert.That(result.JudgeToMiss.MissPenalty, Is.EqualTo(-EzScoreBonusFormula.MISS_PENALTY_CAP));
            Assert.That(result.MissToJudge.MissPenalty, Is.EqualTo(-EzScoreBonusFormula.MISS_PENALTY_CAP));
            Assert.That(result.MissToJudge.JudgeBonus, Is.EqualTo(0));
        }

        [Test]
        public void TestRateScalesOffsetsAndKps()
        {
            // 100 BPM 双押 1/4 = 13.3 KPS；1.5x 后 20 KPS 才起效。track 时间 15ms 在 1.5x 下为真实 10ms。
            var beatmap = createChart(100, chordSize: 2, quarterBeats: 128);
            var events = createEvents(beatmap, sigma: 0, missEvery: 0, fixedOffset: 15);

            Assert.That(EzScoreBonusCalculator.Calculate(beatmap, events, 1).MissToJudge.JudgeBonus, Is.EqualTo(0));

            var fast = EzScoreBonusCalculator.Calculate(beatmap, createEvents(beatmap, sigma: 0, missEvery: 0, fixedOffset: 15, gameplayRate: 1.5), 1.5);
            double expected = EzScoreBonusFormula.JUDGE_BONUS_MAX * EzScoreBonusFormula.JudgeWeight(20) * EzScoreBonusFormula.OffsetQuality(10);
            Assert.That(fast.MissToJudge.JudgeBonus, Is.EqualTo((int)Math.Round(expected)).Within(1));
        }

        [Test]
        public void TestHoldNoteTailIsIgnored()
        {
            var beatmap = new Beatmap();
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 250 });

            var hold = new HoldNote { StartTime = 1000, Duration = 500, Column = 0 };
            hold.ApplyDefaults(beatmap.ControlPointInfo, new BeatmapDifficulty());
            beatmap.HitObjects.Add(hold);

            var events = new List<HitEvent>
            {
                new HitEvent(0, 1, HitResult.Perfect, hold.Head, null, null),
                new HitEvent(0, 1, HitResult.Miss, hold.Tail, null, null),
            };

            var result = EzScoreBonusCalculator.Calculate(beatmap, events, 1, new FixedKps(40)).MissToJudge;

            Assert.That(result.CountedNotes, Is.EqualTo(1));
            Assert.That(result.JudgeBonus, Is.EqualTo(EzScoreBonusFormula.JUDGE_BONUS_MAX));
            Assert.That(result.MissPenalty, Is.EqualTo(0));
        }

        /// <summary>
        /// 合成谱面仿真：输出 BPM × 键型 × 误差分布 × Miss 率 的附加分矩阵，用于校准判定加成与 Miss 罚分曲线的量级。
        /// 每格为「JudgeToMiss / MissToJudge」两种倾向。
        /// </summary>
        [Test]
        public void TestSyntheticSimulationMatrix()
        {
            var sb = new StringBuilder();
            sb.AppendLine("bpm chord kps | judge sigma=3 / 6 / 9 / 12 (A/B) | miss 0.5% / 1% / 2% (A/B)");

            foreach (double bpm in new[] { 150.0, 180.0, 200.0, 240.0 })
            {
                foreach (int chord in new[] { 1, 2, 3, 4 })
                {
                    var beatmap = createChart(bpm, chord, quarterBeats: 512);
                    double kps = chord * 4 * bpm / 60;

                    sb.Append($"{bpm,3} {chord,5} {kps,5:0.0} |");

                    foreach (double sigma in new[] { 3.0, 6.0, 9.0, 12.0 })
                    {
                        var r = EzScoreBonusCalculator.Calculate(beatmap, createEvents(beatmap, sigma, missEvery: 0), 1);
                        Assert.That(r.JudgeToMiss.JudgeBonus, Is.InRange(0, EzScoreBonusFormula.JUDGE_BONUS_MAX));
                        Assert.That(r.MissToJudge.JudgeBonus, Is.InRange(0, EzScoreBonusFormula.JUDGE_BONUS_MAX));
                        sb.Append($" {r.JudgeToMiss.JudgeBonus,4}/{r.MissToJudge.JudgeBonus,-4}");
                    }

                    sb.Append(" |");

                    foreach (int missEvery in new[] { 200, 100, 50 })
                    {
                        var r = EzScoreBonusCalculator.Calculate(beatmap, createEvents(beatmap, 6, missEvery), 1);
                        Assert.That(r.JudgeToMiss.MissPenalty, Is.InRange(-EzScoreBonusFormula.MISS_PENALTY_CAP, 0));
                        Assert.That(r.MissToJudge.MissPenalty, Is.InRange(-EzScoreBonusFormula.MISS_PENALTY_CAP, 0));
                        sb.Append($" {r.JudgeToMiss.MissPenalty,6}/{r.MissToJudge.MissPenalty,-6}");
                    }

                    sb.AppendLine();
                }
            }

            TestContext.WriteLine(sb.ToString());
        }

        private static Beatmap createChart(double bpm, int chordSize, int quarterBeats)
        {
            var beatmap = new Beatmap();
            double beatLength = 60000 / bpm;
            beatmap.BeatmapInfo.BPM = bpm;
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = beatLength });

            for (int i = 0; i < quarterBeats; i++)
            {
                // 从 0ms 起、按整小节排布，避免 KPS list（每 4 拍一段）首尾残段拉低密度。
                double time = i * beatLength / 4;

                for (int c = 0; c < chordSize; c++)
                    beatmap.HitObjects.Add(new Note { StartTime = time, Column = (c + i) % 4 });
            }

            return beatmap;
        }

        /// <summary>
        /// 180 BPM：前 16 小节单键 1/4（12 KPS），后 16 小节四押 1/4（48 KPS）。
        /// </summary>
        private static Beatmap createMixedChart()
        {
            var beatmap = new Beatmap();
            const double bpm = 180;
            const double beat_length = 60000 / bpm;
            beatmap.BeatmapInfo.BPM = bpm;
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = beat_length });

            for (int i = 0; i < 512; i++)
            {
                double time = i * beat_length / 4;
                int chordSize = i < 256 ? 1 : 4;

                for (int c = 0; c < chordSize; c++)
                    beatmap.HitObjects.Add(new Note { StartTime = time, Column = (c + i) % 4 });
            }

            return beatmap;
        }

        private static List<HitEvent> createEvents(IBeatmap beatmap, double sigma, int missEvery, double? fixedOffset = null, double gameplayRate = 1)
            => createEvents(beatmap, i => missEvery > 0 && i % missEvery == 0, sigma, fixedOffset, gameplayRate);

        private static List<HitEvent> createEvents(IBeatmap beatmap, Func<int, bool> isMiss, double sigma = 0, double? fixedOffset = null, double gameplayRate = 1)
        {
            var random = new Random(1234);
            var events = new List<HitEvent>(beatmap.HitObjects.Count);

            for (int i = 0; i < beatmap.HitObjects.Count; i++)
            {
                HitObject hitObject = beatmap.HitObjects[i];

                if (isMiss(i))
                {
                    events.Add(new HitEvent(-150, 1, HitResult.Miss, hitObject, null, null));
                    continue;
                }

                double offset = fixedOffset ?? sigma * gaussian(random);
                events.Add(new HitEvent(offset, gameplayRate, HitResult.Perfect, hitObject, null, null));
            }

            return events;
        }

        private static double gaussian(Random random)
        {
            double u1 = 1 - random.NextDouble();
            double u2 = random.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        private class FixedKps : IEzKpsSectionLookup
        {
            private readonly double kps;

            public FixedKps(double kps)
            {
                this.kps = kps;
            }

            public double KpsAt(double time) => kps;
        }
    }
}
