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
        public void TestTendencyWeighting()
        {
            var raw = new EzScoreBonusResult(8000, -4000, 1000);

            var lowJudge = EzScoreBonusWeighting.Resolve(raw, 2, EzScoreBonusTendency.JudgeToMiss);
            Assert.That(lowJudge.JudgeBonus, Is.EqualTo(8000));
            Assert.That(lowJudge.MissPenalty, Is.EqualTo(-1000));

            var highJudge = EzScoreBonusWeighting.Resolve(raw, 9, EzScoreBonusTendency.JudgeToMiss);
            Assert.That(highJudge.JudgeBonus, Is.EqualTo(2000));
            Assert.That(highJudge.MissPenalty, Is.EqualTo(-4000));

            var lowMiss = EzScoreBonusWeighting.Resolve(raw, 2, EzScoreBonusTendency.MissToJudge);
            Assert.That(lowMiss.JudgeBonus, Is.EqualTo(2000));
            Assert.That(lowMiss.MissPenalty, Is.EqualTo(-4000));
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
        public void TestPerfectPlayOnBurstChartApproachesCap()
        {
            // 200 BPM、每 1/4 拍 4 键（48 KPS）全 0ms。
            var beatmap = createChart(200, chordSize: 4, quarterBeats: 256);
            var result = EzScoreBonusCalculator.Calculate(beatmap, createEvents(beatmap, sigma: 0, missEvery: 0), 1);

            Assert.That(result.JudgeBonus, Is.EqualTo(EzScoreBonusFormula.JUDGE_BONUS_MAX));
            Assert.That(result.MissPenalty, Is.EqualTo(0));
        }

        [Test]
        public void TestEasyChartGetsNoJudgeBonusButMissesStillCount()
        {
            // 120 BPM 单键 1/4（8 KPS）：低于判定加成起效点；Miss 罚分与 KPS 无关。
            var beatmap = createChart(120, chordSize: 1, quarterBeats: 256);
            var result = EzScoreBonusCalculator.Calculate(beatmap, createEvents(beatmap, sigma: 0, missEvery: 64), 1);

            Assert.That(result.JudgeBonus, Is.EqualTo(0));
            Assert.That(result.MissPenalty, Is.EqualTo(-4 * EzScoreBonusFormula.MISS_PENALTY_UNIT));
        }

        [Test]
        public void TestMissPenaltyIsCapped()
        {
            var beatmap = createChart(200, chordSize: 4, quarterBeats: 256);
            var result = EzScoreBonusCalculator.Calculate(beatmap, createEvents(beatmap, sigma: 0, missEvery: 1), 1);

            Assert.That(result.MissPenalty, Is.EqualTo(-EzScoreBonusFormula.MISS_PENALTY_CAP));
            Assert.That(result.JudgeBonus, Is.EqualTo(0));
        }

        [Test]
        public void TestRateScalesOffsetsAndKps()
        {
            // 100 BPM 双押 1/4 = 13.3 KPS；1.5x 后 20 KPS 才起效。track 时间 15ms 在 1.5x 下为真实 10ms。
            var beatmap = createChart(100, chordSize: 2, quarterBeats: 128);
            var events = createEvents(beatmap, sigma: 0, missEvery: 0, fixedOffset: 15);

            Assert.That(EzScoreBonusCalculator.Calculate(beatmap, events, 1).JudgeBonus, Is.EqualTo(0));

            var fast = EzScoreBonusCalculator.Calculate(beatmap, createEvents(beatmap, sigma: 0, missEvery: 0, fixedOffset: 15, gameplayRate: 1.5), 1.5);
            double expected = EzScoreBonusFormula.JUDGE_BONUS_MAX * EzScoreBonusFormula.JudgeWeight(20) * EzScoreBonusFormula.OffsetQuality(10);
            Assert.That(fast.JudgeBonus, Is.EqualTo((int)Math.Round(expected)).Within(1));
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

            var result = EzScoreBonusCalculator.Calculate(beatmap, events, 1, new FixedKps(40));

            Assert.That(result.CountedNotes, Is.EqualTo(1));
            Assert.That(result.JudgeBonus, Is.EqualTo(EzScoreBonusFormula.JUDGE_BONUS_MAX));
            Assert.That(result.MissPenalty, Is.EqualTo(0));
        }

        /// <summary>
        /// 合成谱面仿真：输出 BPM × 键型 × 误差分布 × Miss 率 的附加分矩阵（未加权），用于校准 B_j / P_unit / P_cap 的量级。
        /// </summary>
        [Test]
        public void TestSyntheticSimulationMatrix()
        {
            var sb = new StringBuilder();
            sb.AppendLine("bpm chord kps | sigma=3 sigma=6 sigma=9 sigma=12 | miss1% miss3%");

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
                        Assert.That(r.JudgeBonus, Is.InRange(0, EzScoreBonusFormula.JUDGE_BONUS_MAX));
                        sb.Append($" {r.JudgeBonus,7}");
                    }

                    sb.Append(" |");

                    foreach (int missEvery in new[] { 100, 33 })
                    {
                        var r = EzScoreBonusCalculator.Calculate(beatmap, createEvents(beatmap, 6, missEvery), 1);
                        Assert.That(r.MissPenalty, Is.InRange(-EzScoreBonusFormula.MISS_PENALTY_CAP, 0));
                        sb.Append($" {r.MissPenalty,6}");
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

        private static List<HitEvent> createEvents(IBeatmap beatmap, double sigma, int missEvery, double? fixedOffset = null, double gameplayRate = 1)
        {
            var random = new Random(1234);
            var events = new List<HitEvent>(beatmap.HitObjects.Count);

            for (int i = 0; i < beatmap.HitObjects.Count; i++)
            {
                HitObject hitObject = beatmap.HitObjects[i];

                if (missEvery > 0 && i % missEvery == 0)
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
