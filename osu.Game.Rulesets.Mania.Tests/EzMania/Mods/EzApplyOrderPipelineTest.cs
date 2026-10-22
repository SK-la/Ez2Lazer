// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod;
using osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Mods
{
    /// <summary>
    /// 转换顺序：转换器阶段与后转换阶段都要按 <see cref="IEzApplyOrder"/> 执行，
    /// 且排在后面的 mod 能读到前面（如 PS）改过的列数。
    /// </summary>
    [TestFixture]
    public class EzApplyOrderPipelineTest
    {
        /// <summary>
        /// 转换器阶段原本按列表顺序执行，用户设的顺序不起作用。
        /// </summary>
        [Test]
        public void TestConverterModsRunInApplyOrder()
        {
            var working = createManiaWorkingBeatmap(4);

            var mods = new Mod[]
            {
                new ConverterProbeMod { ApplyOrderIndex = { Value = 10 }, TargetColumns = { Value = 6 } },
                new ConverterProbeMod { ApplyOrderIndex = { Value = 20 }, TargetColumns = { Value = 9 } },
            };

            var beatmap = (ManiaBeatmap)working.GetPlayableBeatmap(new ManiaRuleset().RulesetInfo, mods);

            Assert.That(beatmap.TotalColumns, Is.EqualTo(9), "顺序靠后的转换 mod 应覆盖前者");
        }

        /// <summary>
        /// mania 源的谱面进转换器时列数已经是自己的键数，Pattern Shift 不应在这一阶段抢先改成它自己的目标列数，
        /// 否则任何 ApplyOrder 比它大的 mod 都会读到 PS 的列数。
        /// </summary>
        [Test]
        public void TestPatternShiftDoesNotPreemptConverterForManiaSource()
        {
            var working = createManiaWorkingBeatmap(7);

            var probe = new ColumnProbeMod { ApplyOrderIndex = { Value = 0 } };
            var ps = new ManiaModPatternShift { KeyCount = { Value = 6 }, Seed = { Value = 1 }, ApplyOrderIndex = { Value = 10 } };

            // 探针排在 PS 之前，读到的必须是源谱键数；PS 在转换器阶段不该已经改过列数。
            working.GetPlayableBeatmap(new ManiaRuleset().RulesetInfo, new Mod[] { probe, ps });

            Assert.That(probe.ObservedColumns, Is.EqualTo(7), "PS 之前应仍是源谱键数");
        }

        /// <summary>
        /// 选歌键数走 mod 声明而不真正转换，所以必须按 ApplyOrder 串起所有改键 mod。
        /// </summary>
        [Test]
        public void TestDisplayColumnCountFollowsApplyOrder()
        {
            var ruleset = new ManiaRuleset();
            var beatmapInfo = new BeatmapInfo(ruleset.RulesetInfo, new BeatmapDifficulty { CircleSize = 7 });

            var ps = new ManiaModPatternShift { KeyCount = { Value = 6 } };
            var n2n = new ManiaModKrrN2Nc { TargetKeys = { Value = 8 } };

            // 默认：PS(-20) 先，N2N(-10) 后。
            Assert.That(ruleset.GetKeyCount(beatmapInfo, new Mod[] { ps, n2n }), Is.EqualTo(8));

            // 交换顺序：N2N(10) 先，PS(50) 后。
            n2n.ApplyOrderIndex.Value = 10;
            ps.ApplyOrderIndex.Value = 50;

            Assert.That(ruleset.GetKeyCount(beatmapInfo, new Mod[] { ps, n2n }), Is.EqualTo(6));
        }

        /// <summary>
        /// 整条流水线：PS 先扩到 6，N2N 再按 6 扩到 8，成品必须是 8 列且列号不越界。
        /// </summary>
        [Test]
        public void TestPatternShiftThenN2NProducesN2NTargetColumns()
        {
            var working = createManiaWorkingBeatmap(7);

            var ps = new ManiaModPatternShift { KeyCount = { Value = 6 }, Seed = { Value = 1 } };
            var n2n = new ManiaModKrrN2Nc
            {
                TargetKeys = { Value = 8 },
                MaxKeys = { Value = 8 },
                MinKeys = { Value = 0 },
                Seed = { Value = 1 },
            };

            var beatmap = (ManiaBeatmap)working.GetPlayableBeatmap(new ManiaRuleset().RulesetInfo, new Mod[] { ps, n2n });

            Assert.That(beatmap.TotalColumns, Is.EqualTo(8));
            Assert.That(beatmap.HitObjects.All(h => h.Column < 8), Is.True);
        }

        /// <summary>
        /// NTM 属于改键 mod，读的是排在前面（如 PS）之后的列数；谁排在后面谁决定最终列数。
        /// </summary>
        [Test]
        public void TestNtoMFollowsApplyOrder()
        {
            var ruleset = new ManiaRuleset().RulesetInfo;

            var ps = new ManiaModPatternShift { KeyCount = { Value = 6 }, Seed = { Value = 1 } };
            var ntm = new ManiaModNtoM { Key = { Value = 8 }, ApplyOrderIndex = { Value = 10 } };
            ps.ApplyOrderIndex.Value = 0;

            var ntmLast = (ManiaBeatmap)createManiaWorkingBeatmap(7).GetPlayableBeatmap(ruleset, new Mod[] { ps, ntm });
            Assert.That(ntmLast.TotalColumns, Is.EqualTo(8), "NTM 在后：应把 PS 的 6 列扩成 8");

            ps.ApplyOrderIndex.Value = 50;
            ntm.ApplyOrderIndex.Value = 10;

            var psLast = (ManiaBeatmap)createManiaWorkingBeatmap(7).GetPlayableBeatmap(ruleset, new Mod[] { ps, ntm });
            Assert.That(psLast.TotalColumns, Is.EqualTo(6), "PS 在后：应把 NTM 的 8 列重建为 6");
        }

        /// <summary>
        /// NtoMAnother 与 NTM 同源，同样按 ApplyOrder 处理前面（如 PS）设置的列数。
        /// </summary>
        [Test]
        public void TestNtoMAnotherFollowsApplyOrder()
        {
            var ruleset = new ManiaRuleset().RulesetInfo;

            var ps = new ManiaModPatternShift { KeyCount = { Value = 6 }, Seed = { Value = 1 } };
            var another = new ManiaModNtoMAnother { Key = { Value = 8 }, ApplyOrderIndex = { Value = 10 } };

            var beatmap = (ManiaBeatmap)createManiaWorkingBeatmap(7).GetPlayableBeatmap(ruleset, new Mod[] { ps, another });

            Assert.That(beatmap.TotalColumns, Is.EqualTo(8));
            Assert.That(beatmap.HitObjects.All(h => h.Column < 8), Is.True);
        }

        /// <summary>
        /// 选歌键数也要认出 NTM / NtoMAnother 的目标列数。
        /// </summary>
        [Test]
        public void TestDisplayColumnCountIncludesNtoMMods()
        {
            var ruleset = new ManiaRuleset();
            var beatmapInfo = new BeatmapInfo(ruleset.RulesetInfo, new BeatmapDifficulty { CircleSize = 7 });

            Assert.That(ruleset.GetKeyCount(beatmapInfo, new Mod[] { new ManiaModNtoM { Key = { Value = 8 } } }), Is.EqualTo(8));
            Assert.That(ruleset.GetKeyCount(beatmapInfo, new Mod[] { new ManiaModNtoMAnother { Key = { Value = 9 } } }), Is.EqualTo(9));
        }

        private static TestWorkingBeatmap createManiaWorkingBeatmap(int columns)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(columns))
            {
                BeatmapInfo = new BeatmapInfo(new ManiaRuleset().RulesetInfo, new BeatmapDifficulty { CircleSize = columns }),
            };

            for (int column = 0; column < columns; column++)
                beatmap.HitObjects.Add(new Note { StartTime = 1000 + column * 100, Column = column });

            return new TestWorkingBeatmap(beatmap);
        }

        private class ConverterProbeMod : Mod, IApplicableToBeatmapConverter, IEzApplyOrder
        {
            public override string Name => "converter probe";
            public override string Acronym => "CP";
            public override LocalisableString Description => "test";

            public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt { MinValue = 0, MaxValue = 100 };

            public BindableNumber<int> TargetColumns { get; } = new BindableInt(4);

            public int ApplyOrder => ApplyOrderIndex.Value;

            public void ApplyToBeatmapConverter(IBeatmapConverter converter)
                => ((ManiaBeatmapConverter)converter).TargetColumns = TargetColumns.Value;
        }

        private class ColumnProbeMod : Mod, IApplicableAfterBeatmapConversion, IEzApplyOrder
        {
            public override string Name => "column probe";
            public override string Acronym => "CLP";
            public override LocalisableString Description => "test";

            public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt { MinValue = 0, MaxValue = 100 };

            public int ApplyOrder => ApplyOrderIndex.Value;

            public int? ObservedColumns { get; private set; }

            public void ApplyToBeatmap(IBeatmap beatmap)
                => ObservedColumns = ((ManiaBeatmap)beatmap).ResolveColumnCount();
        }
    }
}
