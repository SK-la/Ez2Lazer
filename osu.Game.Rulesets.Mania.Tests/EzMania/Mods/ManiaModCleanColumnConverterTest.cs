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
    /// 把 <see cref="ManiaModCleanColumn"/> 的产物钉死。
    /// </summary>
    /// <remarks>
    /// 自定义重排会重写列数、列映射、清空/贯穿长按/随机列（<c>?</c> 的取数顺序与 UseRowRandom 直接决定结果），
    /// 删除列则改变 HitObjects 集合本身。重构（诊断日志省掉、按行随机改单趟扫描、删除改一次过滤）必须逐元素等价。
    /// </remarks>
    [TestFixture]
    public class ManiaModCleanColumnConverterTest
    {
        [Test]
        public void TestFixedSeedOutputMatchesGolden()
        {
            var scenarios = new[]
            {
                // 自定义删除列：只剩被保留的列。
                new Scenario("", "1357", false, 42),
                // 自定义重排：映射 + 清空 + 贯穿长按 + 按列随机的 ?。
                new Scenario("21364", "", false, 7),
                // 同上但 ? 走按行随机（rng 在行内抽取，顺序敏感）。
                new Scenario("21364", "", true, 7),
                // 重排 + 删除同时开：先重排再删。
                new Scenario("2|31-?", "1", true, 99),
                // ? 走按列随机（random.Next(0, sourceColumns) 映射到某一列）。
                new Scenario("2136?", "", false, 7),
                // 更多 ? 与贯穿长按混排，走按行随机。
                new Scenario("21-?|", "", true, 2024),
            };

            var actual = new StringBuilder();

            foreach (var scenario in scenarios)
            {
                ManiaBeatmap beatmap = convert(scenario);

                actual.Append($"reorder[{scenario.Reorder}]/delete[{scenario.Delete}]/row{(scenario.RowRandom ? '1' : '0')}/seed{scenario.Seed}: ");
                actual.Append(serialise(beatmap));
                actual.Append('\n');
            }

            // 基线失败时把实际产物原样打出来，方便逐条比对而不是从截断的断言消息里拼。
            TestContext.Progress.WriteLine($"CC actual: {actual.ToString().Replace("\n", "\\n")}");

            Assert.That(actual.ToString(), Is.EqualTo(golden));
        }

        private static ManiaBeatmap convert(Scenario scenario)
        {
            ManiaBeatmap beatmap = createChart(7);

            var mod = new ManiaModCleanColumn
            {
                UseRowRandom = { Value = scenario.RowRandom },
                Seed = { Value = scenario.Seed },
            };

            if (!string.IsNullOrEmpty(scenario.Reorder))
            {
                mod.EnableCustomReorder.Value = true;
                mod.CustomReorderColumn.Value = scenario.Reorder;
            }

            if (!string.IsNullOrEmpty(scenario.Delete))
            {
                mod.EnableCustomDelete.Value = true;
                mod.CustomDeleteColumn.Value = scenario.Delete;
            }

            mod.ApplyToBeatmap(beatmap);

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
        /// 固定图案：单键、同刻和弦、长条与一段 1/16 密集音，覆盖列映射、清空、贯穿长按与按行随机。
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

            for (int i = 0; i < 24; i++)
                objects.Add(new Note { StartTime = 3000 + i * 62.5, Column = i % keys });

            return beatmap;
        }

        private readonly record struct Scenario(string Reorder, string Delete, bool RowRandom, int Seed);

        /// <remarks>
        /// 由重构前的实现产出（先跑一遍把 TestContext.Progress 的输出原样贴回），重构后逐字符比对必须一致。
        /// 若这里失败，先判断新值是否更正确，再决定改实现还是改这条基线。
        /// </remarks>
        private const string golden =
            "reorder[]/delete[1357]/row0/seed42: N0@1000 N1@1000 N6@1000 H1@1000+400 N1@1120 N2@1240 N3@1360 N0@1360 N4@1480 N3@1480 N5@1600 H6@1600+600 N6@1720 N6@1720 N0@1840 N1@1960 N0@1960 N2@2080 N5@2080 N3@2200 H4@2200+500 N4@2320 N5@2440 N4@2440 N4@2440 N6@2560 N0@2680 N1@2800 N3@2800 H2@2800+400 N0@3000 N1@3062.5 N2@3125 N3@3187.5 N4@3250 N5@3312.5 N6@3375 N0@3437.5 N1@3500 N2@3562.5 N3@3625 N4@3687.5 N5@3750 N6@3812.5 N0@3875 N1@3937.5 N2@4000 N3@4062.5 N4@4125 N5@4187.5 N6@4250 N0@4312.5 N1@4375 N2@4437.5\n"
            + "reorder[21364]/delete[]/row0/seed7: N1@1000 N0@1000 H0@1000+400 N0@1120 N2@1240 N1@1360 N4@1360 N4@1480 N3@1600 N1@1840 N1@1960 N0@1960 N2@2080 N3@2080 N4@2200 N3@2440 N1@2680 N0@2800 H2@2800+400 N4@2800 N1@3000 N0@3062.5 N2@3125 N4@3187.5 N3@3312.5 N1@3437.5 N0@3500 N2@3562.5 N4@3625 N3@3750 N1@3875 N0@3937.5 N2@4000 N4@4062.5 N3@4187.5 N1@4312.5 N0@4375 N2@4437.5\n"
            + "reorder[21364]/delete[]/row1/seed7: N1@1000 N0@1000 H0@1000+400 N0@1120 N2@1240 N1@1360 N4@1360 N4@1480 N3@1600 N1@1840 N1@1960 N0@1960 N2@2080 N3@2080 N4@2200 N3@2440 N1@2680 N0@2800 H2@2800+400 N4@2800 N1@3000 N0@3062.5 N2@3125 N4@3187.5 N3@3312.5 N1@3437.5 N0@3500 N2@3562.5 N4@3625 N3@3750 N1@3875 N0@3937.5 N2@4000 N4@4062.5 N3@4187.5 N1@4312.5 N0@4375 N2@4437.5\n"
            + "reorder[2|31-?]/delete[1]/row1/seed99: N3@1000 H5@1000+400 H1@1000+3437.5 N2@1240 N3@1360 N5@1600 N5@1720 N3@1840 N5@1840 N3@1960 N5@1960 N2@2080 N5@2200 N5@2440 N3@2680 N5@2680 H2@2800+400 N3@3000 N5@3000 N5@3062.5 N2@3125 N5@3125 N5@3312.5 N3@3437.5 N5@3500 N2@3562.5 N5@3562.5 N5@3625 N5@3687.5 N3@3875 N2@4000 N5@4000 N5@4062.5 N5@4125 N5@4187.5 N5@4250 N3@4312.5 N5@4375 N2@4437.5 N5@4437.5\n"
            + "reorder[2136?]/delete[]/row0/seed7: N1@1000 N0@1000 H0@1000+400 N0@1120 N2@1240 N4@1240 N1@1360 N3@1600 N1@1840 N1@1960 N0@1960 N2@2080 N4@2080 N3@2080 N3@2440 N1@2680 N0@2800 H2@2800+400 H4@2800+400 N1@3000 N0@3062.5 N2@3125 N4@3125 N3@3312.5 N1@3437.5 N0@3500 N2@3562.5 N4@3562.5 N3@3750 N1@3875 N0@3937.5 N2@4000 N4@4000 N3@4187.5 N1@4312.5 N0@4375 N2@4437.5 N4@4437.5\n"
            + "reorder[21-?|]/delete[]/row1/seed2024: N1@1000 N0@1000 H0@1000+400 N3@1000 H4@1000+3437.5 N0@1120 N1@1360 N3@1360 N3@1480 N3@1600 N1@1840 N1@1960 N0@1960 N3@1960 N3@2080 H3@2200+500 N3@2440 N3@2560 N1@2680 N0@2800 N3@2800 N1@3000 N0@3062.5 N3@3125 N3@3187.5 N3@3312.5 N3@3375 N1@3437.5 N3@3437.5 N0@3500 N3@3500 N3@3562.5 N3@3625 N3@3812.5 N1@3875 N3@3875 N0@3937.5 N3@4000 N3@4125 N3@4187.5 N1@4312.5 N0@4375 N3@4437.5\n";
    }
}
