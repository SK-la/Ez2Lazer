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
    /// 把 <see cref="ManiaModLNTransformer"/> 的输出钉死。
    /// </summary>
    /// <remarks>
    /// 它和 <see cref="ManiaModYuModHelper"/> 共享整条 LN 改写链路（locations 展平、Duration 抽样、
    /// RandomLN / Invert / TrueRandom、AfterTransform 的间隔整理），这些地方全用同一个 <see cref="System.Random"/>，
    /// 少抽一次或换一种遍历顺序都会换掉成品，所以重构前先钉基线。
    /// </remarks>
    [TestFixture]
    public class ManiaModLNTransformerConverterTest
    {
        [Test]
        public void TestFixedSeedOutputMatchesGolden()
        {
            var scenarios = new[]
            {
                // 把长按全部打回单点，并按 Gap 周期性换随机列（唯一走 oldObjects 快照的分支）。
                new Scenario(Level: -3, Divide: 4, Percentage: 100, Gap: 3, SelectColumn: 3, LineSpacing: 0, InvertLineSpacing: false, Seed: 114514),
                // Real RandomLN：时长直接取随机毫秒。
                new Scenario(Level: -2, Divide: 4, Percentage: 100, Gap: 12, SelectColumn: 10, LineSpacing: 0, InvertLineSpacing: false, Seed: 42),
                // RandomLN（按 TimingPoint 分割）。
                new Scenario(Level: -1, Divide: 4, Percentage: 100, Gap: 12, SelectColumn: 10, LineSpacing: 0, InvertLineSpacing: false, Seed: 42),
                // RegularLN。
                new Scenario(Level: 0, Divide: 4, Percentage: 100, Gap: 12, SelectColumn: 10, LineSpacing: 0, InvertLineSpacing: false, Seed: 7),
                // LightLN + 只处理一半概率 + OriginalLN。
                new Scenario(Level: 3, Divide: 8, Percentage: 50, Gap: 12, SelectColumn: 10, LineSpacing: 2, InvertLineSpacing: false, Seed: 7, OriginalLN: true),
                // HeavyLN + InvertLineSpacing。
                new Scenario(Level: 8, Divide: 4, Percentage: 100, Gap: 12, SelectColumn: 10, LineSpacing: 0, InvertLineSpacing: true, Seed: 99),
                // FullLN（走 Invert 分支）。
                new Scenario(Level: 10, Divide: 4, Percentage: 100, Gap: 12, SelectColumn: 10, LineSpacing: 1, InvertLineSpacing: false, Seed: 99),
            };

            var actual = new StringBuilder();

            foreach (var scenario in scenarios)
            {
                ManiaBeatmap beatmap = convert(scenario);

                actual.Append($"lvl{scenario.Level}/d{scenario.Divide}/p{scenario.Percentage}/gap{scenario.Gap}/col{scenario.SelectColumn}")
                      .Append($"/ls{scenario.LineSpacing}/ils{(scenario.InvertLineSpacing ? 1 : 0)}/ol{(scenario.OriginalLN ? 1 : 0)}/seed{scenario.Seed}: ");
                actual.Append(serialise(beatmap));
                actual.Append('\n');
            }

            // 基线失败时把实际产物原样打出来，方便逐条比对而不是从截断的断言消息里拼。
            TestContext.Progress.WriteLine($"LT actual: {actual.ToString().Replace("\n", "\\n")}");

            Assert.That(actual.ToString(), Is.EqualTo(golden));
        }

        private static ManiaBeatmap convert(Scenario scenario)
        {
            ManiaBeatmap beatmap = createChart(7);

            new ManiaModLNTransformer
            {
                Level = { Value = scenario.Level },
                Divide = { Value = scenario.Divide },
                Percentage = { Value = scenario.Percentage },
                Gap = { Value = scenario.Gap },
                SelectColumn = { Value = scenario.SelectColumn },
                LineSpacing = { Value = scenario.LineSpacing },
                InvertLineSpacing = { Value = scenario.InvertLineSpacing },
                OriginalLN = { Value = scenario.OriginalLN },
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
        /// 固定图案：单键、同刻和弦、长短不一的长条、互相重叠的长条与一段 1/16 密集音。
        /// 覆盖 locations 展平顺序（Note 先于 HoldNote、各自按原顺序）、Duration 抽样与间隔整理。
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

                if (i % 5 == 0)
                    objects.Add(new HoldNote { StartTime = startTime, Duration = 400 + i % 3 * 100, Column = (i + 1) % keys });

                if (i % 7 == 0)
                    objects.Add(new HoldNote { StartTime = startTime, Duration = 1000, Column = (i + 3) % keys });
            }

            for (int i = 0; i < 24; i++)
                objects.Add(new Note { StartTime = 3000 + i * 62.5, Column = i % keys });

            return beatmap;
        }

        private readonly record struct Scenario(int Level, int Divide, int Percentage, int Gap, int SelectColumn, int LineSpacing, bool InvertLineSpacing, int Seed,
                                                bool OriginalLN = false);

        /// <remarks>
        /// 由重构前的实现产出（先跑一遍把 TestContext.Progress 的输出原样贴回），重构后逐字符比对必须一致。
        /// 若这里失败，先判断新值是否更正确，再决定改实现还是改这条基线。
        /// </remarks>
        private const string golden =
            "lvl-3/d4/p100/gap3/col3/ls0/ils0/ol0/seed114514: N0@1000 N1@1000 H1@1000+400 H3@1000+1000 N1@1120 N2@1240 N3@1360 N0@1360 N4@1480 N5@1600 H6@1600+600 N6@1720 N6@1720 N0@1840 H3@1840+1000 N1@1960 N2@2080 N5@2080 N3@2200 H4@2200+500 N4@2320 N5@2440 N4@2440 N6@2560 N0@2680 H3@2680+1000 N1@2800 N3@2800 H2@2800+400 N0@3000 N1@3062.5 N2@3125 N3@3187.5 N4@3250 N5@3312.5 N6@3375 N0@3437.5 N1@3500 N2@3562.5 N3@3625 N4@3687.5 N5@3750 N6@3812.5 N0@3875 N1@3937.5 N2@4000 N3@4062.5 N4@4125 N5@4187.5 N6@4250 N0@4312.5 N1@4375 N2@4437.5\n"
            + "lvl-2/d4/p100/gap12/col10/ls0/ils0/ol0/seed42: H0@1000+240.141 H1@1000+-0.91 H1@1000+97.54 H3@1000+281.626 H1@1120+597.148 H2@1240+670.439 H0@1360+250.168 H3@1360+119.772 H4@1480+102.437 H5@1600+372.149 H6@1600+41.929 H6@1720+-0.735 H6@1720+181.592 H0@1840+608.513 H3@1840+233.144 H1@1960+581.516 H2@2080+359.009 H5@2080+77.592 H3@2200+290.516 H4@2200+63.401 H4@2320+108.587 H4@2440+15.882 H5@2440+860.766 H6@2560+76.993 H0@2680+243.235 H3@2680+50.707 H1@2800+10.308 H3@2800+84.013 H2@2800+4.003 H0@3000+220.32 H1@3062.5+358.004 H2@3125+285.035 H3@3187.5+116.069 H4@3250+145.711 H5@3312.5+312.289 H6@3375+46.321 H0@3437.5+113.517 H1@3500+351.748 H2@3562.5+405.461 H3@3625+31.464 H4@3687.5+45.452 H5@3750+432.651 H6@3812.5+328.826 H0@3875+355.577 H1@3937.5+65.026 H2@4000+269.044\n"
            + "lvl-1/d4/p100/gap12/col10/ls0/ils0/ol0/seed42: H0@1000+62.5 N1@1000 H1@1000+62.5 H3@1000+62.5 H1@1120+62.5 H2@1240+687.5 H0@1360+375 H3@1360+375 H4@1480+687.5 H5@1600+125 H6@1600+62.5 N6@1720 H6@1720+312.5 H0@1840+187.5 H3@1840+250 H1@1960+312.5 H2@2080+625 H5@2080+187.5 H3@2200+62.5 H4@2200+62.5 H4@2320+62.5 H4@2440+125 H5@2440+250 H6@2560+750 H0@2680+125 H3@2680+62.5 H1@2800+187.5 H3@2800+312.5 H2@2800+62.5 H0@3000+312.5 H1@3062.5+312.5 H2@3125+312.5 H3@3187.5+62.5 H4@3250+125 H5@3312.5+375 H6@3375+62.5 H0@3437.5+62.5 H1@3500+62.5 H2@3562.5+250 H3@3625+312.5 H4@3687.5+62.5 H5@3750+375 H6@3812.5+187.5 H0@3875+62.5 H1@3937.5+187.5 H2@4000+312.5 N3@4062.5 N4@4125 N5@4187.5 N6@4250 N0@4312.5 N1@4375 N2@4437.5\n"
            + "lvl0/d4/p100/gap12/col10/ls0/ils0/ol0/seed7: H0@1000+62.5 N1@1000 H1@1000+62.5 H3@1000+62.5 H1@1120+62.5 H2@1240+62.5 H0@1360+62.5 H3@1360+62.5 H4@1480+62.5 H5@1600+62.5 H6@1600+62.5 N6@1720 H6@1720+62.5 H0@1840+62.5 H3@1840+62.5 H1@1960+62.5 H2@2080+62.5 H5@2080+62.5 H3@2200+62.5 H4@2200+62.5 H4@2320+62.5 H4@2440+62.5 H5@2440+62.5 H6@2560+62.5 H0@2680+62.5 H3@2680+62.5 H1@2800+62.5 H3@2800+62.5 H2@2800+62.5 H0@3000+62.5 H1@3062.5+62.5 H2@3125+62.5 H3@3187.5+62.5 H4@3250+62.5 H5@3312.5+62.5 H6@3375+62.5 H0@3437.5+62.5 H1@3500+62.5 H2@3562.5+62.5 H3@3625+62.5 H4@3687.5+62.5 H5@3750+62.5 H6@3812.5+62.5 H0@3875+62.5 H1@3937.5+62.5 H2@4000+62.5 N3@4062.5 N4@4125 N5@4187.5 N6@4250 N0@4312.5 N1@4375 N2@4437.5\n"
            + "lvl3/d8/p50/gap12/col10/ls2/ils0/ol1/seed7: N0@1000 N1@1000 N1@1000 N3@1000 N1@1120 H2@1240+218.75 N0@1360 N3@1360 N4@1480 H5@1600+187.5 H6@1600+600 N6@1720 N6@1720 N0@1840 N3@1840 H1@1960+250 N2@2080 N5@2080 N3@2200 N4@2200 H4@2320+31.25 N4@2440 N5@2440 N6@2560 N0@2680 H3@2680+1000 N1@2800 N3@2800 N2@2800 N0@3000 H1@3062.5+62.5 N2@3125 N3@3187.5 N4@3250 N5@3312.5 N6@3375 H0@3437.5+93.75 N1@3500 N2@3562.5 H3@3625+187.5 N4@3687.5 N5@3750 N6@3812.5 N0@3875 N1@3937.5 H2@4000+187.5 N3@4062.5 N4@4125 N5@4187.5 N6@4250 N0@4312.5 N1@4375 N2@4437.5\n"
            + "lvl8/d4/p100/gap12/col10/ls0/ils1/ol0/seed99: H0@1000+250 N1@1000 H1@1000+62.5 H3@1000+312.5 H1@1120+625 H2@1240+625 H0@1360+375 H3@1360+437.5 H4@1480+687.5 H5@1600+437.5 H6@1600+62.5 N6@1720 H6@1720+750 H0@1840+750 H3@1840+250 H1@1960+750 H2@2080+687.5 H5@2080+312.5 H3@2200+437.5 H4@2200+62.5 H4@2320+62.5 H4@2440+750 H5@2440+750 H6@2560+687.5 H0@2680+250 H3@2680+62.5 H1@2800+187.5 H3@2800+312.5 H2@2800+250 H0@3000+375 H1@3062.5+250 H2@3125+375 H3@3187.5+375 H4@3250+375 H5@3312.5+375 H6@3375+375 H0@3437.5+375 H1@3500+375 H2@3562.5+375 H3@3625+375 H4@3687.5+312.5 H5@3750+375 H6@3812.5+312.5 H0@3875+312.5 H1@3937.5+375 H2@4000+375 N3@4062.5 N4@4125 N5@4187.5 N6@4250 N0@4312.5 N1@4375 N2@4437.5\n"
            + "lvl10/d4/p100/gap12/col10/ls1/ils0/ol0/seed99: N0@1000 N1@1000 N1@1000 N3@1000 H1@1120+777.5 N2@1240 H0@1360+417.5 H3@1360+417.5 N4@1480 H5@1600+417.5 H6@1600+62.5 N6@1720 N6@1720 H0@1840+777.5 H3@1840+297.5 N1@1960 H2@2080+657.5 H5@2080+297.5 N3@2200 N4@2200 H4@2320+62.5 N4@2440 N5@2440 H6@2560+752.5 N0@2680 N3@2680 H1@2800+200 H3@2800+325 H2@2800+262.5 N0@3000 H1@3062.5+375 N2@3125 H3@3187.5+375 N4@3250 H5@3312.5+375 N6@3375 H0@3437.5+375 N1@3500 H2@3562.5+375 N3@3625 H4@3687.5+375 N5@3750 H6@3812.5+375 N0@3875 H1@3937.5+375 N2@4000 N3@4062.5 N4@4125 N5@4187.5 N6@4250 N0@4312.5 N1@4375 N2@4437.5\n";
    }
}
