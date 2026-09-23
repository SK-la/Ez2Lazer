// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Mods
{
    /// <summary>
    /// 把 <see cref="ManiaModNtoM"/> 的输出钉死。
    /// </summary>
    /// <remarks>
    /// 这个 mod 会一边往列表里追加 note、一边对该列表做全表重叠扫描，重构（按列索引替代扫描）时必须逐元素等价。
    /// 断言整份产物的稳定序列化，另加两条与随机数无关的不变量：列号必须在目标范围内、同一列同一时刻不得重复。
    /// </remarks>
    [TestFixture]
    public class ManiaModNtoMConverterTest
    {
        private const int target_keys = 8;

        [Test]
        public void TestFixedSeedOutputMatchesGolden()
        {
            var scenarios = new[]
            {
                // 7K -> 8K：单轮。
                new Scenario(7, 8, 114514),
                // 4K -> 10K：多轮（每轮重建 locations 与索引）。
                new Scenario(4, 10, 114514),
                // 3K -> 9K：源键数 < 4，走「随机指定列」的分支。
                new Scenario(3, 9, 42),
            };

            var actual = new StringBuilder();

            foreach (var scenario in scenarios)
            {
                ManiaBeatmap beatmap = convert(scenario);

                actual.Append(scenario.SourceKeys).Append("->").Append(scenario.TargetKeys).Append('/').Append(scenario.Seed).Append(": ");
                actual.Append(serialise(beatmap));
                actual.Append('\n');
            }

            // 基线失败时把实际产物原样打出来，方便逐条比对而不是从截断的断言消息里拼。
            TestContext.Progress.WriteLine($"NTM actual: {actual.ToString().Replace("\n", "\\n")}");

            Assert.That(actual.ToString(), Is.EqualTo(golden));
        }

        [Test]
        public void TestConversionKeepsColumnsInRangeAndStartsFromSource()
        {
            var sourceStartTimes = createChart(7).HitObjects.Select(h => h.StartTime).ToHashSet();

            ManiaBeatmap beatmap = convert(new Scenario(7, target_keys, 114514));

            Assert.That(beatmap.HitObjects, Is.Not.Empty, "前置条件：本用例的源谱面必须转换出 note");

            Assert.That(beatmap.HitObjects, Has.All.Matches<ManiaHitObject>(h => h.Column >= 0 && h.Column < target_keys),
                        "输出列号必须落在目标列数内");

            Assert.That(beatmap.HitObjects, Has.All.Matches<ManiaHitObject>(h => sourceStartTimes.Contains(h.StartTime)),
                        "输出时间必须来自源谱面（本 mod 只改列）");

            var occupied = new HashSet<(int column, double startTime)>();

            foreach (var hitObject in beatmap.HitObjects)
                Assert.That(occupied.Add(((int)hitObject.Column, hitObject.StartTime)), "同一列同一时刻不应出现两个对象");
        }

        private static ManiaBeatmap convert(Scenario scenario)
        {
            ManiaBeatmap beatmap = createChart(scenario.SourceKeys);

            new ManiaModNtoM
            {
                Key = { Value = scenario.TargetKeys },
                Probability = { Value = 70 },
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

                builder.Append(hitObject is HoldNote hold ? 'H' : 'N');
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

        /// <summary>
        /// 固定图案谱面：单键渐密 + 同刻和弦 + 长条与长条重叠，任何键数下都覆盖到。
        /// </summary>
        private static ManiaBeatmap createChart(int keys)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(keys));
            beatmap.Difficulty.CircleSize = keys;

            List<ManiaHitObject> objects = beatmap.HitObjects;

            for (int i = 0; i < 24; i++)
            {
                double startTime = 1000 + i * 100;

                objects.Add(new Note { StartTime = startTime, Column = i % keys });

                if (i % 3 == 0)
                    objects.Add(new Note { StartTime = startTime, Column = (i * 2 + 1) % keys });

                if (i % 4 == 0 && keys > 1)
                    objects.Add(new Note { StartTime = startTime, Column = (i + keys - 1) % keys });

                if (i % 5 == 0)
                    objects.Add(new HoldNote { StartTime = startTime, Duration = 300 + i % 3 * 100, Column = (i + 1) % keys });
            }

            return beatmap;
        }

        private readonly record struct Scenario(int SourceKeys, int TargetKeys, int Seed);

        /// <remarks>
        /// 由替换前的实现产出（先 stash 掉 ManiaModNtoM 的改动跑一遍留存），替换后逐字符比对必须一致。
        /// 重构后若这里失败，先判断新值是否更正确，再决定改实现还是改这条基线。
        /// </remarks>
        private const string golden =
            "7->8/114514: N0@1000 N1@1000 H3@1000+300 N7@1000 N1@1100 N2@1200 N0@1300 N5@1300 N3@1400 N4@1400 N5@1500 H6@1500+500 N4@1600 N5@1600 N0@1700 N0@1800 N1@1800 N2@1900 N5@1900 N3@2000 H4@2000+400 N6@2100 N6@2200 N7@2200 N5@2200 N7@2300 N0@2400 N1@2500 H2@2500+300 N3@2500 N1@2600 N6@2600 N3@2700 N4@2800 N6@2800 N6@2900 H0@3000+500 N6@3000 N7@3000 N1@3100 N3@3100 N1@3200 N2@3300\n"
            + "4->10/114514: N5@1000 N6@1000 N8@1000 H9@1000+300 N6@1100 N7@1200 N3@1300 N8@1300 N0@1400 N8@1400 N5@1500 H7@1500+500 N4@1600 N9@1600 N9@1700 N0@1800 N9@1800 N1@1900 N9@1900 N8@2000 H9@2000+400 N1@2100 N0@2200 N1@2200 N2@2200 N2@2300 N4@2400 N0@2500 H1@2500+300 N9@2500 N0@2600 N9@2600 N2@2700 N2@2800 N5@2800 N8@2900 N0@3000 H1@3000+500 N5@3000 N3@3100 N4@3100 N3@3200 N4@3300\n"
            + "3->9/42: N5@1000 N6@1000 H7@1000+300 N8@1000 N6@1100 N5@1200 N1@1300 N8@1300 N0@1400 N8@1400 H5@1500+500 N7@1500 N6@1600 N8@1600 N6@1700 N0@1800 N6@1800 N0@1900 N7@1900 H0@2000+400 N6@2000 N8@2100 N1@2200 N2@2200 N7@2200 N7@2300 N4@2400 N0@2500 H4@2500+300 N7@2500 N0@2600 N7@2600 N1@2700 N1@2800 N2@2800 N8@2900 H1@3000+500 N3@3000 N6@3000 N2@3100 N4@3100 N6@3200 N6@3300\n";
    }
}
