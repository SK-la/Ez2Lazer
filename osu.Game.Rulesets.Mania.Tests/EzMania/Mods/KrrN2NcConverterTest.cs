// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Mods.KrrConversion;
using osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Mods
{
    /// <summary>
    /// 把 <see cref="KrrN2NcConverter"/> 的输出钉死。
    /// </summary>
    /// <remarks>
    /// 这是一个带随机数的稠密矩阵算法，且此前没有任何测试覆盖；分配与复杂度的重构只有「同种子同输入给出同输出」
    /// 才安全。断言的是整份产物的稳定序列化，任何让落键位置、长条长度或对象顺序漂移的改动都会在这里失败。
    /// </remarks>
    [TestFixture]
    public class KrrN2NcConverterTest
    {
        [Test]
        public void TestFixedSeedOutputMatchesGolden()
        {
            var scenarios = new[]
            {
                // 目标键数 > 原键数：走 convertMtx / convert 的扩展分支。
                new Scenario(8, 6, 2, 4, 114514),
                // 目标键数 < 原键数：走 smartReduceColumns。
                new Scenario(5, 4, 2, 4, 114514),
                new Scenario(4, 10, 0, 6, 42),
                new Scenario(9, 3, 1, 4, 7),
            };

            var actual = new StringBuilder();

            foreach (var scenario in scenarios)
            {
                ManiaBeatmap beatmap = createChart();

                KrrN2NcConverter.Transform(beatmap, new KrrOptions
                {
                    TargetKeys = scenario.TargetKeys,
                    MaxKeys = scenario.MaxKeys,
                    MinKeys = scenario.MinKeys,
                    BeatSpeed = scenario.BeatSpeed,
                    Seed = scenario.Seed,
                });

                actual.Append(scenario.TargetKeys).Append('/').Append(scenario.Seed).Append(": ");
                actual.Append(serialise(beatmap));
                actual.Append('\n');
            }

            Assert.That(actual.ToString(), Is.EqualTo(golden));
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

        private readonly record struct Scenario(int TargetKeys, int MaxKeys, int MinKeys, int BeatSpeed, int Seed);

        /// <summary>
        /// 7K、120 BPM 的固定谱面：单键 / chord / 长条 / 长条重叠 / 空档都有，扩展与裁剪两条分支都能吃到输入。
        /// </summary>
        private static ManiaBeatmap createChart()
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(7));
            beatmap.Difficulty.CircleSize = 7;
            beatmap.BeatmapInfo.Difficulty.CircleSize = 7;
            beatmap.BeatmapInfo.BPM = 120;
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            List<ManiaHitObject> objects = beatmap.HitObjects;

            objects.Add(new Note { StartTime = 1000, Column = 0 });
            objects.Add(new Note { StartTime = 1000, Column = 3 });
            objects.Add(new Note { StartTime = 1250, Column = 1 });
            objects.Add(new Note { StartTime = 1500, Column = 2 });
            objects.Add(new Note { StartTime = 1500, Column = 5 });
            objects.Add(new HoldNote { StartTime = 1750, Duration = 750, Column = 0 });
            objects.Add(new Note { StartTime = 2000, Column = 4 });
            objects.Add(new Note { StartTime = 2100, Column = 6 });
            objects.Add(new HoldNote { StartTime = 2250, Duration = 1250, Column = 2 });
            objects.Add(new Note { StartTime = 2400, Column = 1 });
            objects.Add(new Note { StartTime = 2500, Column = 3 });
            objects.Add(new Note { StartTime = 2500, Column = 6 });
            objects.Add(new Note { StartTime = 2650, Column = 0 });
            objects.Add(new Note { StartTime = 2800, Column = 5 });
            objects.Add(new HoldNote { StartTime = 3000, Duration = 500, Column = 4 });
            objects.Add(new Note { StartTime = 3250, Column = 2 });
            objects.Add(new Note { StartTime = 3500, Column = 1 });
            objects.Add(new Note { StartTime = 3500, Column = 3 });
            objects.Add(new Note { StartTime = 3750, Column = 6 });
            objects.Add(new Note { StartTime = 4000, Column = 0 });
            objects.Add(new Note { StartTime = 4000, Column = 2 });
            objects.Add(new Note { StartTime = 4000, Column = 5 });
            objects.Add(new HoldNote { StartTime = 4250, Duration = 1000, Column = 3 });
            objects.Add(new Note { StartTime = 4500, Column = 4 });
            objects.Add(new Note { StartTime = 4750, Column = 1 });
            objects.Add(new Note { StartTime = 5000, Column = 0 });
            objects.Add(new Note { StartTime = 5100, Column = 6 });
            objects.Add(new Note { StartTime = 5250, Column = 5 });

            return beatmap;
        }

        /// <remarks>
        /// 由当前实现产出并人工核对过（每条都读过一遍，确认没有叠加应用、没有越界列、长条长度合理）。
        /// 重构后若这里失败，先判断新值是否更正确，再决定改实现还是改这条基线。
        /// </remarks>
        private const string golden =
            "8/114514: N0@1000 N3@1000 N1@1250 N2@1500 N6@1500 H0@1750+750 N4@2000 N5@2000 H2@2250+1250 N1@2400 N7@2500 N0@2650 N6@2800 H4@3000+500 H5@3000+500 N1@3500 N3@3500 N7@3750 N2@4000 N5@4000 H3@4250+1000 H6@4250+1000 N4@4500 N1@4750 N0@5000 N5@5250\n"
            + "5/114514: N0@1000 N1@1250 N3@1500 H0@1750+750 N2@2000 N4@2100 N1@2400 N4@2500 N2@2650 N3@2800 H2@3000+500 N3@3250 N1@3500 N1@3750 N0@4000 N4@4000 H4@4250+1000 N3@4500 N1@4750 N0@5000 N4@5100 N3@5250\n"
            + "4/42: N1@1000 N0@1250 N2@1500 H2@1750+750 N1@2000 N3@2100 N0@2400 N1@2500 N3@2500 N0@2650 N2@2800 H3@3000+500 N2@3250 N0@3500 N1@3500 N3@3750 N2@4000 H1@4250+1000 N2@4500 N0@4750 N2@5000 N3@5100 N2@5250\n"
            + "9/7: N4@1000 N6@1250 N3@1500 H0@1750+750 N5@2000 H1@2250+1250 N2@2400 N8@2500 N0@2650 N7@2800 H5@3000+500 N2@3500 N8@3750 N3@4000 H4@4250+1000 N6@4500 N1@4750 N0@5000 N8@5100 N7@5250\n";
    }
}
