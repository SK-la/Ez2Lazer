// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Mods
{
    /// <summary>
    /// 把 <see cref="ManiaModNtoMAnother"/> 的输出钉死。
    /// </summary>
    /// <remarks>
    /// 这个 mod 的每条分支（按 Gap 切区域、Clean 清理、空白列、Adjust4Jack/Speed）都会影响产物，
    /// 而它内部大量依赖「已按开始时间排好序」的隐式前提做去重/清理。重构（去掉重复排序、
    /// GroupBy 改单趟扫描、清理改压缩写回）时必须逐元素等价。
    /// </remarks>
    [TestFixture]
    public class ManiaModNtoMAnotherConverterTest
    {
        [Test]
        public void TestFixedSeedOutputMatchesGolden()
        {
            var scenarios = new[]
            {
                // 7->8：单个区域（Gap 0 时 gap 取 double.MaxValue），走 Clean。
                new Scenario(7, 8, 0, Clean: true, BlankColumn: 0, Jack: false, Speed: false, Seed: 114514),
                // 同上但不清理。
                new Scenario(7, 8, 0, Clean: false, BlankColumn: 0, Jack: false, Speed: false, Seed: 114514),
                // 4->10：Gap 20 把谱面切成多个区域，走 Clean + 空白列。
                new Scenario(4, 10, 20, Clean: true, BlankColumn: 2, Jack: false, Speed: false, Seed: 42),
                // 多区域 + Adjust4Jack：cleanDivide 翻倍，跨区域清理的判定窗口随之改变。
                // 注意：这里刻意避开「目标键数 - 源键数 > 源键数」的组合（如 4->10 且空白列为 0）：
                // ProcessArea 抽取不重复源列号的 while 循环在那种参数下永远取不满，会直接死循环。
                new Scenario(5, 10, 20, Clean: true, BlankColumn: 0, Jack: true, Speed: false, Seed: 42),
                // 多区域 + Adjust4Speed：cleanDivide 减半。
                new Scenario(5, 10, 20, Clean: true, BlankColumn: 0, Jack: false, Speed: true, Seed: 42),
            };

            var actual = new StringBuilder();

            foreach (var scenario in scenarios)
            {
                ManiaBeatmap beatmap = convert(scenario);

                actual.Append(scenario.SourceKeys).Append("->").Append(scenario.TargetKeys)
                      .Append("/gap").Append(scenario.Gap).Append("/clean").Append(scenario.Clean ? '1' : '0')
                      .Append("/blank").Append(scenario.BlankColumn).Append("/seed").Append(scenario.Seed).Append(": ");
                actual.Append(serialise(beatmap));
                actual.Append('\n');
            }

            // 基线失败时把实际产物原样打出来，方便逐条比对而不是从截断的断言消息里拼。
            TestContext.Progress.WriteLine($"NtMA actual: {actual.ToString().Replace("\n", "\\n")}");

            Assert.That(actual.ToString(), Is.EqualTo(golden));
        }

        private static ManiaBeatmap convert(Scenario scenario)
        {
            ManiaBeatmap beatmap = createChart(scenario.SourceKeys);

            new ManiaModNtoMAnother
            {
                Key = { Value = scenario.TargetKeys },
                BlankColumn = { Value = scenario.BlankColumn },
                Gap = { Value = scenario.Gap },
                Clean = { Value = scenario.Clean },
                Adjust4Jack = { Value = scenario.Jack },
                Adjust4Speed = { Value = scenario.Speed },
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
        /// 固定图案：单键、同刻和弦、长条、互相重叠的长条、以及一段 1/16 密集音，
        /// 使得 Gap 切区域、Clean 的同刻/间隔清理、空白列插入都能被覆盖。
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

            // 1/16 密集段：cleanDivide 为 4 时会被逐拍判定间隔。
            for (int i = 0; i < 32; i++)
                objects.Add(new Note { StartTime = 3000 + i * 62.5, Column = i % keys });

            return beatmap;
        }

        private readonly record struct Scenario(int SourceKeys, int TargetKeys, int Gap, bool Clean, int BlankColumn, bool Jack, bool Speed, int Seed);

        /// <remarks>
        /// 由重构前的实现产出（先跑一遍把 TestContext.Progress 的输出原样贴回），重构后逐字符比对必须一致。
        /// 若这里失败，先判断新值是否更正确，再决定改实现还是改这条基线。
        /// </remarks>
        private const string golden =
            "7->8/gap0/clean1/blank0/seed114514: N0@1000 N1@1000 N7@1000 N1@1120 N3@1240 N0@1360 N4@1360 N4@1480 N5@1480 H7@1600+600 N2@1600 N6@1600 N0@1840 N0@1960 N1@1960 N3@2080 N2@2080 N6@2080 N4@2200 H5@2200+500 N2@2440 N6@2440 N7@2560 N0@2680 N1@2800 H3@2800+400 N4@2800 N0@3000 N1@3062.5 N4@3187.5 N5@3250 N2@3312.5 N6@3312.5 N7@3375 N0@3437.5 N1@3500 N3@3562.5 N4@3625 N5@3687.5 N2@3750 N6@3750 N7@3812.5 N0@3875 N1@3937.5 N3@4000 N4@4062.5 N5@4125 N2@4187.5 N6@4187.5 N7@4250 N0@4312.5 N1@4375 N3@4437.5 N4@4500 N5@4562.5 N2@4625 N6@4625 N7@4687.5 N0@4750 N1@4812.5 N3@4875 N4@4937.5\n"
            + "7->8/gap0/clean0/blank0/seed114514: N0@1000 N1@1000 H1@1000+400 N7@1000 N1@1120 N3@1240 N0@1360 N4@1360 N4@1480 N5@1480 N2@1600 N6@1600 H7@1600+600 N7@1720 N7@1720 N0@1840 N0@1960 N1@1960 N3@2080 N2@2080 N6@2080 N4@2200 H5@2200+500 N5@2320 N5@2440 N5@2440 N2@2440 N6@2440 N7@2560 N0@2680 N1@2800 H3@2800+400 N4@2800 N0@3000 N1@3062.5 N3@3125 N4@3187.5 N5@3250 N2@3312.5 N6@3312.5 N7@3375 N0@3437.5 N1@3500 N3@3562.5 N4@3625 N5@3687.5 N2@3750 N6@3750 N7@3812.5 N0@3875 N1@3937.5 N3@4000 N4@4062.5 N5@4125 N2@4187.5 N6@4187.5 N7@4250 N0@4312.5 N1@4375 N3@4437.5 N4@4500 N5@4562.5 N2@4625 N6@4625 N7@4687.5 N0@4750 N1@4812.5 N3@4875 N4@4937.5\n"
            + "4->10/gap20/clean1/blank2/seed42: N1@1000 N4@1000 N2@1000 N6@1000 N3@1000 N9@1000 N4@1120 N5@1120 N1@1240 N6@1240 N3@1360 N9@1360 N2@1480 N3@1480 N9@1480 N0@1480 H4@1600+600 N3@1600 N5@1600 H8@1600+600 N3@1720 N5@1720 N1@1840 N9@1840 N1@1960 N2@1960 N9@1960 N0@1960 N2@2080 N6@2080 N5@2080 N7@2080 H6@2200+500 N3@2200 N0@2200 H7@2200+500 N1@2440 N4@2440 N2@2440 N5@2440 N2@2560 N3@2680 N8@2680 N1@2800 H4@2800+400 N9@2800 H0@2800+400 N6@3062.5 N2@3125 N8@3125 N1@3187.5 N9@3187.5 N3@3250 N0@3250 N4@3312.5 N6@3312.5 N2@3375 N8@3375 N1@3437.5 N9@3437.5 N4@3500 N6@3562.5 N3@3562.5 N0@3625 N7@3625 N2@3687.5 N8@3687.5 N1@3750 N4@3750 N6@3812.5 N3@3812.5 N0@3875 N7@3875 N1@3937.5 N8@3937.5 N3@4000 N0@4000 N4@4062.5 N2@4062.5 N6@4125 N5@4125 N1@4187.5 N8@4187.5 N3@4250 N0@4250 N4@4312.5 N2@4312.5 N6@4375 N5@4375 N3@4437.5 N9@4437.5 N1@4500 N0@4500 N4@4562.5 N2@4562.5 N6@4625 N5@4625 N3@4687.5 N9@4687.5 N1@4750 N0@4750 N3@4812.5 N4@4875 N6@4875 N1@4937.5 N8@4937.5\n"
            + "5->10/gap20/clean1/blank0/seed42: N1@1000 N2@1000 N5@1000 N4@1000 N7@1000 N9@1000 N2@1120 N8@1120 N4@1240 N3@1240 N4@1360 N7@1360 N3@1360 N6@1360 N7@1480 N9@1480 N6@1480 N0@1480 N1@1600 H2@1600+600 H4@1600+600 N0@1600 N7@1720 N3@1720 N5@1840 N6@1840 N5@1960 N7@1960 N3@1960 N6@1960 N9@2080 N6@2080 N1@2200 N5@2200 H7@2200+500 N2@2320 N1@2440 N2@2440 N5@2440 N8@2440 N3@2440 N5@2560 N4@2560 N9@2680 N8@2680 N1@2800 N2@2800 N6@2800 N0@2800 N6@3000 N0@3000 N5@3062.5 N3@3062.5 N9@3125 N6@3125 N7@3187.5 N0@3187.5 N4@3250 N8@3250 N1@3312.5 N2@3312.5 N5@3375 N3@3375 N9@3437.5 N6@3437.5 N1@3500 N7@3500 N2@3562.5 N8@3562.5 N3@3625 N6@3625 N4@3687.5 N0@3687.5 N5@3750 N9@3750 N1@3812.5 N7@3812.5 N2@3875 N8@3875 N5@3937.5 N0@3937.5 N2@4000 N7@4000 N4@4062.5 N8@4062.5 N3@4125 N6@4125 N1@4187.5 N9@4187.5 N5@4250 N0@4250 N2@4312.5 N7@4312.5 N1@4375 N3@4375 N9@4437.5 N6@4437.5 N5@4500 N7@4500 N8@4562.5 N0@4562.5 N2@4625 N4@4625 N1@4687.5 N3@4687.5 N9@4750 N6@4750 N9@4812.5 N8@4812.5 N2@4875 N0@4875 N1@4937.5 N3@4937.5\n"
            + "5->10/gap20/clean1/blank0/seed42: N1@1000 N2@1000 N5@1000 N4@1000 N7@1000 N9@1000 N8@1120 N4@1240 N3@1240 N4@1360 N7@1360 N3@1360 N6@1360 N7@1480 N9@1480 N6@1480 N0@1480 N1@1600 H2@1600+600 H4@1600+600 N7@1720 N3@1720 N5@1840 N6@1840 N5@1960 N7@1960 N3@1960 N6@1960 N9@2080 N1@2200 N5@2200 H7@2200+500 N2@2320 N1@2440 N2@2440 N5@2440 N8@2440 N3@2440 N4@2560 N9@2680 N8@2680 N1@2800 N2@2800 N6@2800 N0@2800 N6@3000 N0@3000 N5@3062.5 N3@3062.5 N9@3125 N6@3125 N7@3187.5 N0@3187.5 N4@3250 N8@3250 N1@3312.5 N2@3312.5 N5@3375 N3@3375 N9@3437.5 N6@3437.5 N1@3500 N7@3500 N2@3562.5 N8@3562.5 N3@3625 N6@3625 N4@3687.5 N0@3687.5 N5@3750 N9@3750 N1@3812.5 N7@3812.5 N2@3875 N8@3875 N5@3937.5 N0@3937.5 N2@4000 N7@4000 N4@4062.5 N8@4062.5 N3@4125 N6@4125 N1@4187.5 N9@4187.5 N5@4250 N0@4250 N2@4312.5 N7@4312.5 N1@4375 N3@4375 N9@4437.5 N6@4437.5 N5@4500 N7@4500 N8@4562.5 N0@4562.5 N2@4625 N4@4625 N1@4687.5 N3@4687.5 N9@4750 N6@4750 N8@4812.5 N2@4875 N0@4875 N1@4937.5 N3@4937.5\n";
    }
}
