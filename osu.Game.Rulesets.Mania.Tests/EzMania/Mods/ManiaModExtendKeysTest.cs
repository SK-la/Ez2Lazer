// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Mods
{
    /// <summary>
    /// 把 <see cref="ManiaModExtendKeys"/> 的输出钉死。
    /// </summary>
    /// <remarks>
    /// 这个 mod 的判定完全由「行 × 列」矩阵加随机数决定，重构时必须逐元素等价。
    /// 除了整份产物的稳定序列化，另加几条与随机数无关的不变量：原列只整体平移、
    /// 扩展列只出现在整体最外侧、扩展长条不得超过镜像源、同侧无叠键时扩展列也不许叠键。
    /// </remarks>
    [TestFixture]
    public class ManiaModExtendKeysTest
    {
        /// <summary>
        /// 扩展列两次按下的间隔上限。源谱同轨更紧时可以更紧，超过它就按它算。
        /// </summary>
        private const double max_jack_interval = 200;

        [Test]
        public void TestFixedSeedOutputMatchesGolden()
        {
            var scenarios = new[]
            {
                // 7K -> 10K：增量 3，左 2 右 1。
                new Scenario(7, 10, MiddleColumnSide.Both, 114514),
                // 5K -> 8K：奇数源谱，中间列算双侧。
                new Scenario(5, 8, MiddleColumnSide.Both, 42),
                // 4K -> 8K：增量 4，两侧等量。
                new Scenario(4, 8, MiddleColumnSide.Left, 114514),
            };

            var actual = new StringBuilder();

            foreach (var scenario in scenarios)
            {
                ManiaBeatmap beatmap = convert(scenario);

                actual.Append(scenario.SourceKeys).Append("->").Append(scenario.TargetKeys).Append('/').Append(scenario.Seed).Append(": ");
                actual.Append(serialise(beatmap.HitObjects));
                actual.Append('\n');
            }

            // 基线失败时把实际产物原样打出来，方便逐条比对而不是从截断的断言消息里拼。
            TestContext.Progress.WriteLine($"EXT actual: {actual.ToString().Replace("\n", "\\n")}");

            Assert.That(actual.ToString(), Is.EqualTo(golden));
        }

        /// <summary>
        /// 原列整体右移，扩展列只落在整体最外侧，中间不能凭空多出对象。
        /// </summary>
        [Test]
        public void TestOriginalColumnsAreOnlyShifted()
        {
            ManiaBeatmap source = createChart(4);
            ManiaBeatmap beatmap = convert(new Scenario(4, 6, MiddleColumnSide.Both, 114514));

            const int left_columns = 1;

            string shifted = serialise(source.HitObjects.Select(h => (ManiaHitObject)shift(h, left_columns)));

            Assert.That(serialise(beatmap.HitObjects.Where(h => h.Column > 0 && h.Column < 5)), Is.EqualTo(shifted),
                        "原列区间内的对象必须与源谱逐元素对应，只是整体右移");
        }

        [Test]
        public void TestOddDeltaAddsExtraColumnOnLeft()
        {
            ManiaBeatmap source = createChart(7);
            ManiaBeatmap beatmap = convert(new Scenario(7, 10, MiddleColumnSide.Both, 114514));

            // 增量 3 -> 左 2 右 1，原列落在 2..8。
            string shifted = serialise(source.HitObjects.Select(h => (ManiaHitObject)shift(h, 2)));

            Assert.That(beatmap.TotalColumns, Is.EqualTo(10));
            Assert.That(serialise(beatmap.HitObjects.Where(h => h.Column >= 2 && h.Column <= 8)), Is.EqualTo(shifted),
                        "增量是奇数时多出来的那一列要给左侧");
        }

        /// <summary>
        /// 源谱同侧相邻两行没有任何同轨按键时，扩展列不许自己叠出连打。
        /// </summary>
        [Test]
        public void TestNoJackWhenSourceSideHasNone()
        {
            // 6K、左侧 3 列，每行只按一列且相邻行不重复，源谱这一侧完全没有叠键。
            var source = createEmptyBeatmap(6);
            var objects = source.HitObjects;

            for (int i = 0; i < 24; i++)
                objects.Add(new Note { StartTime = 1000 + i * 125, Column = i % 3 });

            ManiaBeatmap beatmap = convert(new Scenario(6, 8, MiddleColumnSide.Both, 114514));

            Assert.That(beatmap.HitObjects.Where(h => h.Column is 0 or 7).ToList(), Is.Not.Empty,
                        "前置条件：左侧密度非零，扩展列必须有对象");

            foreach (int column in new[] { 0, 7 })
            {
                var times = beatmap.HitObjects.Where(h => h.Column == column).Select(h => h.StartTime).OrderBy(t => t).ToList();

                for (int i = 1; i < times.Count; i++)
                    Assert.That(times[i] - times[i - 1], Is.GreaterThan(max_jack_interval),
                                $"扩展列 {column} 在 {times[i - 1]} 与 {times[i]} 之间违反了同侧叠规则");
            }
        }

        /// <summary>
        /// 扩展列的叠间隔跟着源谱同一轨道走：源谱自己就是 100ms 一跳时可以跟到 100ms，
        /// 源谱宽松（400ms）时扩展列最多放宽到 200ms。
        /// </summary>
        [Test]
        public void TestJackIntervalFollowsSourceTracks()
        {
            var beatmap = createEmptyBeatmap(4);
            var objects = beatmap.HitObjects;

            // 紧凑段：两条轨道都 100ms 一跳，源谱自己的叠间隔就是 100ms。
            for (int i = 0; i <= 4; i++)
            {
                objects.Add(new Note { StartTime = 1000 + i * 100, Column = 0 });
                objects.Add(new Note { StartTime = 1000 + i * 100, Column = 1 });
            }

            // 宽松段：只有列 0，间隔 400ms。
            for (int i = 0; i <= 3; i++)
                objects.Add(new Note { StartTime = 2000 + i * 400, Column = 0 });

            new ManiaModExtendKeys { TargetKeys = { Value = 5 }, Seed = { Value = 114514 } }.ApplyToBeatmap(beatmap);

            var times = beatmap.HitObjects.Where(h => h.Column == 0).Select(h => h.StartTime).OrderBy(t => t).ToList();

            Assert.That(times.Where(t => t <= 1400), Is.EquivalentTo(new[] { 1000.0, 1100, 1200, 1300, 1400 }),
                        "源谱同轨就是 100ms 时，扩展列应该能跟着 100ms");

            for (int i = 1; i < times.Count; i++)
            {
                if (times[i] < 2000)
                    continue;

                Assert.That(times[i] - times[i - 1], Is.GreaterThanOrEqualTo(max_jack_interval),
                            $"源谱这一轨间隔 400ms，扩展列不该在 {times[i - 1]} 与 {times[i]} 之间逼到 200ms 以内");
            }
        }

        /// <summary>
        /// 叠间隔上限是设置项：调成 0 时不再限制，扩展列可以贴着上一行出键。
        /// </summary>
        [Test]
        public void TestMaxJackIntervalIsConfigurable()
        {
            var beatmap = createEmptyBeatmap(4);
            var objects = beatmap.HitObjects;

            for (int i = 0; i < 40; i++)
                objects.Add(new Note { StartTime = 1000 + i * 100, Column = i % 2 });

            new ManiaModExtendKeys
            {
                TargetKeys = { Value = 5 },
                MaxJackInterval = { Value = 0 },
                Seed = { Value = 114514 },
            }.ApplyToBeatmap(beatmap);

            var times = beatmap.HitObjects.Where(h => h.Column == 0).Select(h => h.StartTime).OrderBy(t => t).ToList();

            Assert.That(times.Count, Is.GreaterThan(1), "前置条件：本用例必须产出扩展音符");
            Assert.That(times.Zip(times.Skip(1), (a, b) => b - a), Has.Some.EqualTo(100),
                        "上限为 0 时不应再按 200ms 卡叠");
        }

        /// <summary>
        /// 被叠限制挤掉的键要把概率补给同轨下一个可落点，扩展列不能因此稀一大截。
        /// </summary>
        [Test]
        public void TestSuppressedNotesFeedTheNextPosition()
        {
            var beatmap = createEmptyBeatmap(4);
            var objects = beatmap.HitObjects;

            // 每行 100ms、两列交替：每列自己的间隔是 200ms，扩展列被叠限制卡掉一半的位置。
            for (int i = 0; i < 200; i++)
                objects.Add(new Note { StartTime = 1000 + i * 100, Column = i % 2 });

            new ManiaModExtendKeys { TargetKeys = { Value = 5 }, Seed = { Value = 114514 } }.ApplyToBeatmap(beatmap);

            int extNotes = beatmap.HitObjects.Count(h => h.Column == 0);

            // 理论密度是 200 行 × 0.5 = 100；只靠叠限制会掉到 73，补偿后回到 89 左右。
            Assert.That(extNotes, Is.GreaterThanOrEqualTo(80), $"补偿后不应丢掉两成以上的键（实际 {extNotes}）");
        }

        /// <summary>
        /// 152 BPM 的 1/4 拍是 98.68ms，谱面里会写成 99ms；阈值判定必须容忍这点误差，
        /// 否则整段相邻行都会被当成「间隔够远」，叠规则形同虚设。
        /// </summary>
        [Test]
        public void TestJackRuleSurvivesRoundedQuarterBeats()
        {
            const double beat_length = 60000.0 / 152;

            var beatmap = new ManiaBeatmap(new StageDefinition(4))
            {
                Difficulty = { CircleSize = 4 },
            };

            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = beat_length });

            // 每行 99ms（1/4 拍），左右交替，同侧相邻两行没有同轨按键。
            for (int i = 0; i < 48; i++)
                beatmap.HitObjects.Add(new Note { StartTime = 1000 + i * 99, Column = i % 2 });

            new ManiaModExtendKeys { TargetKeys = { Value = 6 }, Seed = { Value = 114514 } }.ApplyToBeatmap(beatmap);

            var times = beatmap.HitObjects.Where(h => h.Column == 0).Select(h => h.StartTime).OrderBy(t => t).ToList();

            Assert.That(times, Is.Not.Empty, "前置条件：本用例必须产出扩展音符");

            for (int i = 1; i < times.Count; i++)
                // 行距就是 99ms，所以"不在相邻行"等价于间隔大于 99ms。
                Assert.That(times[i] - times[i - 1], Is.GreaterThan(99),
                            $"扩展列在 {times[i - 1]} 与 {times[i]} 之间违反了同侧叠规则");
        }

        /// <summary>
        /// 源谱的缝很匀（反键图）时，扩展列固定按中位数收，长出来的是一串等距的缝。
        /// </summary>
        [Test]
        public void TestUniformGapChartUsesMedianGap()
        {
            // createChart 的列 0 每 250ms 一条 125ms 长条，缝恒为 125。
            const double median_gap = 125;

            var beatmap = createChart(4);

            new ManiaModExtendKeys { TargetKeys = { Value = 5 }, Seed = { Value = 114514 } }.ApplyToBeatmap(beatmap);

            var holds = beatmap.HitObjects.Where(h => h.Column == 0).OfType<HoldNote>().ToList();
            Assert.That(holds, Is.Not.Empty, "前置条件：本用例必须产出扩展长条");

            foreach (var hold in holds)
            {
                var next = beatmap.HitObjects.Where(h => h.Column == 0 && h.StartTime > hold.StartTime).MinBy(h => h.StartTime);

                if (next == null)
                    continue;

                Assert.That(next.StartTime - hold.EndTime, Is.EqualTo(median_gap).Within(0.001),
                            $"{hold.StartTime} 起的长条应留出中位数长度的缝");
            }
        }

        /// <summary>
        /// 源谱的缝不均匀时，扩展长条的缝不能比源谱的最小缝还窄。
        /// </summary>
        [Test]
        public void TestExtendedHoldKeepsMinimumGap()
        {
            const double min_gap = 125;
            var holds = new List<HoldNote>();

            for (int seed = 1; seed <= 20; seed++)
            {
                ManiaBeatmap beatmap = createUnevenGapChart();

                new ManiaModExtendKeys { TargetKeys = { Value = 5 }, Seed = { Value = seed } }.ApplyToBeatmap(beatmap);

                var extended = beatmap.HitObjects.Where(h => h.Column == 0).ToList();

                foreach (var hold in extended.OfType<HoldNote>())
                {
                    holds.Add(hold);

                    Assert.That(hold.Duration, Is.GreaterThanOrEqualTo(min_gap - 0.001), "扩展长条的时长不得短于最小缝");

                    var next = extended.Where(h => h.StartTime > hold.StartTime).MinBy(h => h.StartTime);

                    if (next == null)
                        continue;

                    Assert.That(next.StartTime - hold.EndTime, Is.GreaterThanOrEqualTo(min_gap - 0.001),
                                $"{hold.StartTime} 起的长条到下一个按键的缝不得窄于最小缝");
                }
            }

            Assert.That(holds, Is.Not.Empty, "前置条件：这组种子必须产出扩展长条");
        }

        /// <summary>
        /// 源谱的缝只有 16ms 时，最小缝要抬到 100ms 的 2/3，不能让扩展列跟着糊成一片。
        /// </summary>
        [Test]
        public void TestMinimumGapUsesFloor()
        {
            const double min_gap = 200.0 / 3;
            var holds = new List<HoldNote>();

            for (int seed = 1; seed <= 20; seed++)
            {
                ManiaBeatmap beatmap = createTightGapChart();

                new ManiaModExtendKeys { TargetKeys = { Value = 5 }, Seed = { Value = seed } }.ApplyToBeatmap(beatmap);

                var extended = beatmap.HitObjects.Where(h => h.Column == 0).ToList();

                foreach (var hold in extended.OfType<HoldNote>())
                {
                    holds.Add(hold);

                    Assert.That(hold.Duration, Is.GreaterThanOrEqualTo(min_gap - 0.001), "扩展长条的时长不得短于缝的下限");

                    var next = extended.Where(h => h.StartTime > hold.StartTime).MinBy(h => h.StartTime);

                    if (next == null)
                        continue;

                    Assert.That(next.StartTime - hold.EndTime, Is.GreaterThanOrEqualTo(min_gap - 0.001),
                                $"{hold.StartTime} 起的长条到下一个按键的缝不得窄于缝的下限");
                }
            }

            Assert.That(holds, Is.Not.Empty, "前置条件：这组种子必须产出扩展长条");
        }

        /// <summary>
        /// 4K：列 0 两条长条，尾到下一个单点只有 16ms（远小于下限）；列 1 每 250ms 一个单点提供密度。
        /// </summary>
        private static ManiaBeatmap createTightGapChart()
        {
            ManiaBeatmap beatmap = createEmptyBeatmap(4);
            var objects = beatmap.HitObjects;

            objects.Add(new HoldNote { StartTime = 1000, Column = 0, Duration = 500 });
            objects.Add(new Note { StartTime = 1516, Column = 0 });
            objects.Add(new HoldNote { StartTime = 1800, Column = 0, Duration = 500 });
            objects.Add(new Note { StartTime = 2316, Column = 0 });

            for (int i = 0; i < 7; i++)
                objects.Add(new Note { StartTime = 1000 + i * 250, Column = 1 });

            return beatmap;
        }

        /// <summary>
        /// 4K：列 0 四条长条，各自的缝是 125 / 125 / 250 / 750（20% 分位 125，80% 分位 250，判为不匀）；
        /// 列 1 每 125ms 一个单点，让两侧密度非零。
        /// </summary>
        private static ManiaBeatmap createUnevenGapChart()
        {
            ManiaBeatmap beatmap = createEmptyBeatmap(4);
            var objects = beatmap.HitObjects;

            objects.Add(new HoldNote { StartTime = 1000, Column = 0, Duration = 125 });
            objects.Add(new HoldNote { StartTime = 1250, Column = 0, Duration = 375 });
            objects.Add(new HoldNote { StartTime = 1750, Column = 0, Duration = 500 });
            objects.Add(new HoldNote { StartTime = 2500, Column = 0, Duration = 250 });

            for (int i = 0; i <= 16; i++)
                objects.Add(new Note { StartTime = 1000 + i * 125, Column = 1 });

            return beatmap;
        }

        [Test]
        public void TestEmptyChartOnlyChangesColumnCount()
        {
            ManiaBeatmap beatmap = createEmptyBeatmap(7);

            new ManiaModExtendKeys { TargetKeys = { Value = 10 }, Seed = { Value = 1 } }.ApplyToBeatmap(beatmap);

            Assert.That(beatmap.TotalColumns, Is.EqualTo(10));
            Assert.That(beatmap.HitObjects, Is.Empty);
        }

        [Test]
        public void TestNarrowChartsDoNotThrow()
        {
            foreach (int keys in new[] { 1, 2 })
            {
                foreach (MiddleColumnSide side in Enum.GetValues<MiddleColumnSide>())
                {
                    ManiaBeatmap beatmap = createEmptyBeatmap(keys);

                    for (int i = 0; i < 8; i++)
                        beatmap.HitObjects.Add(new Note { StartTime = 1000 + i * 250, Column = i % keys });

                    new ManiaModExtendKeys
                    {
                        TargetKeys = { Value = 4 },
                        MiddleSide = { Value = side },
                        Seed = { Value = 7 },
                    }.ApplyToBeatmap(beatmap);

                    Assert.That(beatmap.TotalColumns, Is.EqualTo(4), $"{keys}K / {side}");
                    Assert.That(beatmap.HitObjects, Has.All.Matches<ManiaHitObject>(h => h != null && h.Column >= 0 && h.Column < 4), $"{keys}K / {side}");
                }
            }
        }

        private static ManiaBeatmap convert(Scenario scenario)
        {
            ManiaBeatmap beatmap = createChart(scenario.SourceKeys);

            new ManiaModExtendKeys
            {
                TargetKeys = { Value = scenario.TargetKeys },
                MiddleSide = { Value = scenario.MiddleSide },
                Seed = { Value = scenario.Seed },
            }.ApplyToBeatmap(beatmap);

            return beatmap;
        }

        private static ManiaHitObject shift(ManiaHitObject hitObject, int columns)
        {
            var clone = (ManiaHitObject)hitObject.Clone();
            clone.Column += columns;
            return clone;
        }

        private static string serialise(IEnumerable<ManiaHitObject> hitObjects)
        {
            var builder = new StringBuilder();

            foreach (var hitObject in hitObjects.OrderBy(h => h.StartTime).ThenBy(h => h.Column))
            {
                if (builder.Length > 0)
                    builder.Append(' ');

                builder.Append(hitObject is HoldNote ? 'H' : 'N');
                builder.Append(hitObject.Column.ToString(CultureInfo.InvariantCulture));
                builder.Append('@');
                builder.Append(hitObject.StartTime.ToString("0.###", CultureInfo.InvariantCulture));

                if (hitObject is HoldNote timed)
                {
                    builder.Append('+');
                    builder.Append(timed.Duration.ToString("0.###", CultureInfo.InvariantCulture));
                }
            }

            return builder.ToString();
        }

        private static ManiaBeatmap createEmptyBeatmap(int keys)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(keys))
            {
                Difficulty = { CircleSize = keys },
            };

            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            return beatmap;
        }

        /// <summary>
        /// 固定图案谱面：列 0 每两行一条 125ms 长条（缝恒为 125ms），其余键在 1..keys-1 轮转，
        /// 外加每三行一次的同刻和弦，覆盖单键、和弦、长条三种输入。
        /// </summary>
        private static ManiaBeatmap createChart(int keys)
        {
            ManiaBeatmap beatmap = createEmptyBeatmap(keys);
            var objects = beatmap.HitObjects;

            for (int i = 0; i < 24; i++)
            {
                double startTime = 1000 + i * 125;

                objects.Add(new Note { StartTime = startTime, Column = 1 + i % (keys - 1) });

                if (i % 3 == 0 && keys > 2)
                    objects.Add(new Note { StartTime = startTime, Column = 1 + (i + keys / 2) % (keys - 1) });

                if (i % 2 == 0)
                    objects.Add(new HoldNote { StartTime = startTime, Duration = 125, Column = 0 });
            }

            return beatmap;
        }

        private readonly record struct Scenario(int SourceKeys, int TargetKeys, MiddleColumnSide MiddleSide, int Seed);

        private const string golden =
            "7->10/114514: H2@1000+125 N3@1000 N6@1000 N4@1125 H2@1250+125 N5@1250 N9@1250 N3@1375 N6@1375 H2@1500+125 N7@1500 N8@1625 N9@1625 N0@1750 N1@1750 H2@1750+125 N3@1750 N6@1750 N4@1875 N0@2000 H1@2000+375 H2@2000+125 N5@2000 N9@2000 N3@2125 N6@2125 N0@2250 H2@2250+125 N7@2250 N8@2375 N9@2375 N0@2500 H1@2500+250 H2@2500+125 N3@2500 N6@2500 N4@2625 N0@2750 H2@2750+125 N5@2750 N9@2750 N1@2875 N3@2875 N6@2875 N0@3000 H2@3000+125 N7@3000 N9@3000 N8@3125 N0@3250 H2@3250+125 N3@3250 N6@3250 N9@3250 N4@3375 N0@3500 N1@3500 H2@3500+125 N5@3500 N3@3625 N6@3625 H2@3750+125 N7@3750 N8@3875\n"
            + "5->8/42: N1@1000 H2@1000+125 N3@1000 N5@1000 N0@1125 N4@1125 N7@1125 N1@1250 H2@1250+125 N5@1250 N0@1375 N4@1375 N6@1375 N7@1375 N1@1500 H2@1500+125 N3@1500 N4@1625 N7@1625 N0@1750 N1@1750 H2@1750+125 N3@1750 N5@1750 N6@1875 N0@2000 H2@2000+125 N3@2000 N1@2125 N4@2125 N6@2125 N7@2125 H2@2250+125 N5@2250 N6@2375 N0@2500 H1@2500+375 H2@2500+125 N3@2500 N5@2500 N7@2500 N4@2625 N0@2750 H2@2750+125 N5@2750 N7@2750 N4@2875 N6@2875 N0@3000 N1@3000 H2@3000+125 N3@3000 N4@3125 N0@3250 N1@3250 H2@3250+125 N3@3250 N5@3250 N7@3250 N6@3375 H0@3500+125 N1@3500 H2@3500+125 N3@3500 N4@3625 N6@3625 N7@3625 H1@3750+125 H2@3750+125 N5@3750 N6@3875 N7@3875\n"
            + "4->8/114514: N0@1000 N1@1000 H2@1000+125 N3@1000 N5@1000 N6@1000 N7@1000 N4@1125 N0@1250 H2@1250+125 N5@1250 N6@1250 N7@1250 N1@1375 N3@1375 N5@1375 N6@1375 N7@1375 N0@1500 H2@1500+125 N4@1500 N5@1625 N6@1625 H0@1750+250 N1@1750 H2@1750+125 N3@1750 N5@1750 N6@1750 N7@1750 N4@1875 H2@2000+125 N5@2000 N6@2000 N0@2125 N3@2125 N5@2125 N1@2250 H2@2250+125 N4@2250 N7@2250 N5@2375 N6@2375 N0@2500 N1@2500 H2@2500+125 N3@2500 N5@2500 N4@2625 N6@2625 N0@2750 N1@2750 H2@2750+125 N5@2750 N7@2750 N3@2875 N5@2875 N6@2875 N0@3000 N1@3000 H2@3000+125 N4@3000 N7@3000 N5@3125 N0@3250 N1@3250 H2@3250+125 N3@3250 N5@3250 N6@3250 N7@3250 N4@3375 N0@3500 H1@3500+125 H2@3500+125 N5@3500 N6@3500 N7@3500 N3@3625 N5@3625 N6@3625 H0@3750+125 H2@3750+125 N4@3750 N7@3750 N5@3875 N6@3875\n";
    }
}
