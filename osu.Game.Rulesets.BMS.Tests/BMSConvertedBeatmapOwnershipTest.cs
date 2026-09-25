// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Rulesets.BMS.Tests
{
    /// <summary>
    /// BMS → mania 的 playable 派生必须每次给出独立副本，且不把产物写到 BMS 源谱面上。
    /// </summary>
    /// <remarks>
    /// 原先 playable 是在构造期那份共享 mania 谱面上就地改写的：post-conversion mod 的效果会随调用次数叠加，
    /// 并且污染展示谱面与所有其他消费者（仿真、分析、预览）。
    /// </remarks>
    [TestFixture]
    public class BMSConvertedBeatmapOwnershipTest
    {
        [Test]
        public void TestPlayableDerivationDoesNotStackModEffects()
        {
            BMSBeatmap source = createSourceBeatmap();
            var ruleset = new BMSRuleset().RulesetInfo;
            var mods = new Mod[] { new TestMirrorMod() };

            IBeatmap first = ManiaConvertedWorkingBeatmap.CreatePlayableFromSource(source, ruleset, mods, CancellationToken.None);
            IBeatmap second = ManiaConvertedWorkingBeatmap.CreatePlayableFromSource(source, ruleset, mods, CancellationToken.None);

            Assert.That(columnsOf(second), Is.EqualTo(columnsOf(first)), "同一次转换被重复派生，mod 效果不得叠加");
            Assert.That(columnsOf(first), Is.EqualTo(new[] { 2, 1, 0 }), "前置条件：mirror 必须真的作用在产物上");
            Assert.That(columnsOf(source), Is.EqualTo(new[] { 0, 1, 2 }), "源谱面被转换管线改写");
        }

        [Test]
        public void TestProductDoesNotShareHitObjectInstancesOrLists()
        {
            BMSBeatmap source = createSourceBeatmap();

            ManiaBeatmap product = ManiaConvertedWorkingBeatmap.ConvertToManiaBeatmap(source);

            Assert.That(product.ControlPointInfo, Is.Not.SameAs(source.ControlPointInfo), "产物与源共享 control points");
            Assert.That(product.HitObjects.Count, Is.EqualTo(source.HitObjects.Count), "前置条件：转换应保留对象数量");

            for (int i = 0; i < source.HitObjects.Count; i++)
            {
                Assert.That(product.HitObjects[i], Is.Not.SameAs(source.HitObjects[i]), $"#{i} 产物与源共享同一实例");
                Assert.That(product.HitObjects[i].SamplesBindable, Is.Not.SameAs(source.HitObjects[i].SamplesBindable), $"#{i} 产物与源共享 SamplesBindable");
            }
        }

        [Test]
        public void TestWritingToProductDoesNotReachSource()
        {
            BMSBeatmap source = createSourceBeatmap();

            int sourceColumn = source.HitObjects[0].Samples.Count;
            var sourceSamples = source.HitObjects[0].Samples.ToList();

            ManiaBeatmap product = ManiaConvertedWorkingBeatmap.ConvertToManiaBeatmap(source);
            product.HitObjects[0].Samples.Clear();
            ((ManiaHitObject)product.HitObjects[0]).Column = 99;

            Assert.That(source.HitObjects[0].Samples, Is.EqualTo(sourceSamples), "产物清空 Samples 穿透到了源");
            Assert.That(((BMSHitObject)source.HitObjects[0]).Column, Is.Not.EqualTo(99), "产物改写 Column 穿透到了源");
            Assert.That(source.HitObjects[0].Samples.Count, Is.EqualTo(sourceColumn), "源谱面的 samples 被改写");
        }

        [Test]
        public void TestKeysoundListIsDetachedOnClone()
        {
            var note = new BmsManiaNote
            {
                StartTime = 1000,
                Column = 0,
                KeysoundSamples = { new HitSampleInfo("hitnormal") },
            };

            BmsManiaNote clone = note.Clone();

            Assert.That(clone.KeysoundSamples, Is.Not.SameAs(note.KeysoundSamples), "克隆体与源共享 keysound 列表");

            clone.KeysoundSamples.Clear();

            Assert.That(note.KeysoundSamples, Is.Not.Empty, "清空克隆体的 keysound 列表穿透到了源");
        }

        private static int[] columnsOf(IBeatmap beatmap) => beatmap.HitObjects.Cast<IHasColumn>().Select(h => h.Column).ToArray();

        private static BMSBeatmap createSourceBeatmap()
        {
            var controlPoints = new ControlPointInfo();
            controlPoints.Add(0, new TimingControlPoint { BeatLength = 500 });

            return new BMSBeatmap
            {
                ControlPointInfo = controlPoints,
                HitObjects =
                {
                    new BMSNote { StartTime = 1000, Column = 0, Samples = { new HitSampleInfo("hitnormal") } },
                    new BMSNote { StartTime = 2000, Column = 1, Samples = { new HitSampleInfo("hitnormal") } },
                    new BMSHoldNote { StartTime = 3000, Duration = 500, Column = 2, Samples = { new HitSampleInfo("hitnormal") } },
                },
            };
        }

        /// <summary>
        /// 就地镜像列：连做两次会回到原状，所以「两次派生结果一致」只在不叠加时才成立。
        /// </summary>
        private class TestMirrorMod : Mod, IApplicableAfterBeatmapConversion
        {
            public override string Name => "Test Mirror";
            public override string Acronym => "TM";
            public override LocalisableString Description => "Test-only column mirror.";

            public void ApplyToBeatmap(IBeatmap beatmap)
            {
                int columns = (int)beatmap.Difficulty.CircleSize;

                foreach (var hitObject in beatmap.HitObjects)
                {
                    switch (hitObject)
                    {
                        case ManiaHitObject mania:
                            mania.Column = columns - 1 - mania.Column;
                            break;

                        case BMSHitObject bms:
                            bms.Column = columns - 1 - bms.Column;
                            break;
                    }
                }
            }
        }
    }
}
