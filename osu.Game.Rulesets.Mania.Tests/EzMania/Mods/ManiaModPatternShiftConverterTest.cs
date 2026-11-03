// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Mods
{
    /// <summary>
    /// 把 <see cref="ManiaModPatternShift"/> 的输出钉死。
    /// </summary>
    /// <remarks>
    /// 这个 mod 全流程依赖同一个 <see cref="System.Random"/> 的取数顺序（对齐、Delay 抽索引、列分配、
    /// Regenerate 的增删），任何「少抽一次随机数」或「换一种顺序遍历」的等价改写都会改变成品，
    /// 所以重构前必须先有这份逐元素基线。
    /// </remarks>
    [TestFixture]
    public class ManiaModPatternShiftConverterTest
    {
        [Test]
        public void TestFixedSeedOutputMatchesGolden()
        {
            var scenarios = new[]
            {
                // 基线：7->4、默认密度与和弦上限、无对齐/无 Delay/无重生成。
                new Scenario(7, 4, 7, 5, 0, 0, false, 5, 114514),
                // 高密度 + 低和弦上限 + 1/4 对齐。
                new Scenario(7, 8, 10, 3, 4, 0, false, 5, 42),
                // 低密度 + 强和弦压缩 + Delay 抽索引（rng 在 applyDelay 里被消费）。
                new Scenario(7, 6, 5, 2, 0, 5, false, 5, 7),
                // 重生成（走 modifyNotesByDifficulty 的加/删分支）。
                new Scenario(7, 8, 5, 4, 0, 0, true, 8, 99),
                // 重生成 + Delay 同时开启。
                new Scenario(7, 9, 5, 4, 0, 3, true, 3, 99),
            };

            var actual = new StringBuilder();

            foreach (var scenario in scenarios)
            {
                ManiaBeatmap beatmap = convert(scenario);

                actual.Append($"{scenario.SourceKeys}->{scenario.TargetColumns}/d{scenario.Density}/c{scenario.MaxChord}/a{scenario.Align}/dl{scenario.Delay}")
                      .Append(scenario.Regenerate ? $"/regen{scenario.RegenerateDifficulty}" : "/regen0")
                      .Append("/seed").Append(scenario.Seed).Append(": ");
                actual.Append(serialise(beatmap));
                actual.Append('\n');
            }

            // 基线失败时把实际产物原样打出来，方便逐条比对而不是从截断的断言消息里拼。
            TestContext.Progress.WriteLine($"PS actual: {actual.ToString().Replace("\n", "\\n")}");

            Assert.That(actual.ToString(), Is.EqualTo(golden));
        }

        private static ManiaBeatmap convert(Scenario scenario)
        {
            ManiaBeatmap beatmap = createChart(scenario.SourceKeys);

            new ManiaModPatternShift
            {
                KeyCount = { Value = scenario.TargetColumns },
                Density = { Value = scenario.Density },
                MaxChord = { Value = scenario.MaxChord },
                AlignDivisor = { Value = scenario.Align },
                DelayLevel = { Value = scenario.Delay },
                Regenerate = { Value = scenario.Regenerate },
                RegenerateDifficulty = { Value = scenario.RegenerateDifficulty },
                Seed = { Value = scenario.Seed },
            }.ApplyToBeatmap(beatmap);

            return beatmap;
        }

        private static string serialise(ManiaBeatmap beatmap)
        {
            var builder = new StringBuilder();

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (builder.Length > 0)
                    builder.Append(' ');

                builder.Append(hitObject is HoldNote ? 'H' : 'N');
                builder.Append(hitObject.Column.ToString(CultureInfo.InvariantCulture));
                builder.Append('@');
                builder.Append(hitObject.StartTime.ToString("0.###", CultureInfo.InvariantCulture));

                if (hitObject is HoldNote hold)
                {
                    builder.Append('+');
                    builder.Append(hold.Duration.ToString("0.###", CultureInfo.InvariantCulture));
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// 固定图案：单键、同刻和弦、长条、互相重叠的长条、以及一段 1/16 密集音。
        /// 覆盖对齐吸附、和弦压缩、Delay 偏移与列分配时的同列同刻去重。
        /// </summary>
        private static ManiaBeatmap createChart(int keys)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(keys));
            beatmap.Difficulty.CircleSize = keys;
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 250 });

            List<ManiaHitObject> objects = beatmap.HitObjects;

            for (int i = 0; i < 16; i++)
            {
                double startTime = 1000 + i * 120;

                objects.Add(new Note { StartTime = startTime, Column = i % keys });

                if (i % 3 == 0)
                    objects.Add(new Note { StartTime = startTime, Column = (i * 2 + 1) % keys });

                if (i % 4 == 0 && keys > 1)
                    objects.Add(new Note { StartTime = startTime, Column = (i + keys - 1) % keys });

                if (i % 5 == 0)
                    objects.Add(new HoldNote { StartTime = startTime, Duration = 400 + i % 3 * 100, Column = (i + 1) % keys });
            }

            // 1/16 密集段：让同列 0.5ms 容差去重与和弦压缩都真正被命中。
            for (int i = 0; i < 32; i++)
                objects.Add(new Note { StartTime = 3000 + i * 62.5, Column = i % keys });

            return beatmap;
        }

        private readonly record struct Scenario(int SourceKeys, int TargetColumns, int Density, int MaxChord, int Align, int Delay, bool Regenerate, int RegenerateDifficulty, int Seed);

        /// <remarks>
        /// 由重构前的实现产出（先跑一遍把 TestContext.Progress 的输出原样贴回），重构后逐字符比对必须一致。
        /// 若这里失败，先判断新值是否更正确，再决定改实现还是改这条基线。
        /// </remarks>
        private const string golden =
            "7->4/d7/c5/a0/dl0/regen0/seed114514: N0@1000 N1@1000 H2@1000+400 N3@1000 N3@1120 N0@1240 N1@1360 N3@1360 N0@1480 N3@1480 N1@1600 H2@1600+600 N0@1720 N3@1720 N1@1840 N0@1960 N3@1960 N1@2080 N3@2080 N0@2200 H3@2200+500 N1@2320 N0@2440 N1@2440 N2@2440 N2@2560 N0@2680 N0@2800 N1@2800 H2@2800+400 N3@3000 N0@3062.5 N1@3125 N3@3187.5 N0@3250 N1@3312.5 N3@3375 N2@3437.5 N0@3500 N1@3562.5 N3@3625 N2@3687.5 N0@3750 N1@3812.5 N3@3875 N2@3937.5 N0@4000 N1@4062.5 N3@4125 N2@4187.5 N0@4250 N1@4312.5 N3@4375 N2@4437.5 N0@4500 N1@4562.5 N3@4625 N2@4687.5 N0@4750 N1@4812.5 N3@4875 N2@4937.5\n"
            + "7->8/d10/c3/a4/dl0/regen0/seed42: H0@1000+375 N4@1000 N6@1000 N2@1125 N5@1250 N1@1375 N7@1375 N3@1500 N4@1500 H2@1625+562.5 N6@1625 N1@1750 N5@1750 N7@1812.5 N0@1937.5 N4@1937.5 N3@2062.5 N6@2062.5 N1@2187.5 H5@2187.5+500 N7@2312.5 N0@2437.5 N3@2437.5 N4@2437.5 N6@2562.5 N2@2687.5 N0@2812.5 N1@2812.5 H7@2812.5+375 N4@3000 N3@3062.5 N6@3125 N2@3187.5 N5@3250 N0@3312.5 N1@3375 N4@3437.5 N3@3500 N6@3562.5 N2@3625 N7@3687.5 N5@3750 N0@3812.5 N1@3875 N4@3937.5 N3@4000 N6@4062.5 N2@4125 N7@4187.5 N5@4250 N0@4312.5 N1@4375 N4@4437.5 N3@4500 N6@4562.5 N2@4625 N7@4687.5 N5@4750 N0@4812.5 N1@4875 N4@4937.5\n"
            + "7->6/d5/c2/a0/dl5/regen0/seed7: N2@976.563 H4@976.563+400 N5@1120 N1@1240 N3@1360 N0@1383.438 N2@1456.563 N5@1480 N1@1576.563 H4@1600+600 N3@1720 N0@1743.438 N5@1840 N1@1960 N2@1983.438 N3@2056.563 N0@2080 N5@2176.563 H2@2200+500 N1@2320 N3@2416.563 N0@2440 N5@2560 N4@2680 H1@2776.563+400 N3@2823.438 N0@3000 N5@3062.5 N4@3125 N2@3187.5 N3@3250 N0@3312.5 N5@3375 N4@3437.5 N2@3500 N1@3562.5 N3@3625 N0@3687.5 N5@3750 N4@3812.5 N2@3875 N1@3937.5 N3@4000 N0@4062.5 N5@4125 N4@4187.5 N2@4250 N1@4312.5 N3@4375 N0@4437.5 N5@4500 N4@4562.5 N2@4625 N1@4687.5 N3@4750 N0@4812.5 N5@4875 N4@4937.5\n"
            + "7->8/d5/c4/a0/dl0/regen8/seed99: N0@1000 N3@1000 N4@1000 H5@1000+400 N7@1120 N1@1240 N6@1337.452 N2@1360 N4@1360 N0@1480 N3@1480 H1@1600+600 N7@1600 N6@1714.692 N2@1720 N4@1720 N5@1840 N0@1960 N3@1960 N6@2080 N7@2080 N2@2200 H4@2200+500 N5@2320 N0@2369.805 N3@2440 N6@2440 N7@2440 N1@2560 N2@2680 N5@2788.84 N0@2800 N7@2809.137 N3@3000 N6@3062.5 N1@3125 N4@3187.5 N2@3250 N5@3312.5 N0@3375 N7@3437.5 N3@3500 N6@3562.5 N1@3625 N4@3687.5 N2@3745.691 N5@3750 N0@3812.5 N7@3875 N3@3937.5 N6@4000 N1@4062.5 N4@4125 N2@4187.5 N5@4250 N0@4312.5 N7@4375 N3@4437.5 N6@4500 N1@4525.569 N4@4562.5 N2@4567.104 N5@4625 N0@4687.5 N7@4750 N3@4812.5 N6@4875 N1@4909.906 N4@4937.5\n"
            + "7->9/d5/c4/a0/dl3/regen3/seed99: H1@1000+400 N5@1000 N6@1000 N3@1120 N7@1480 H0@1600+600 N2@1720 N8@1720 N4@1960 H5@2200+500 N6@2200 N1@2440 N3@2440 N2@2800 H7@2800+400 N8@3000 N4@3062.5 N0@3437.5 N6@3625 N3@3750 N1@3937.5 N5@4125 N2@4250 N8@4375 N4@4437.5 N7@4625 N0@4687.5 N6@4750 N3@4812.5 N1@4875 N5@4937.5\n";
    }
}
