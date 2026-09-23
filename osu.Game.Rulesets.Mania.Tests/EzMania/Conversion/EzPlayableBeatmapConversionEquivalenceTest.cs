// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Conversion
{
    /// <summary>
    /// 转换结果回归护栏：同一 (谱面, ruleset, mods) 反复取 playable 谱面必须逐对象等价。
    /// 缓存与 mod 链优化都以「同样的输入给出同样的输出」为前提，任何让结果漂移的改动都应在这里失败。
    /// </summary>
    /// <remarks>
    /// 只覆盖源谱面为官方 ruleset 的路径：该路径的转换产物是新对象，等价性是当前实现本就具备的性质。
    /// 源谱面已是 mania 的路径（产物与源共享实例）由独立用例单独约束，见 P1a2 的所有权不变量测试。
    /// </remarks>
    [TestFixture]
    public class EzPlayableBeatmapConversionEquivalenceTest
    {
        [Test]
        public void TestRepeatedConversionWithKeyModIsElementWiseEquivalent()
        {
            var working = createWorkingBeatmap();
            var ruleset = new ManiaRuleset().RulesetInfo;
            var mods = new Mod[] { new ManiaModKey7() };

            assertElementWiseEquivalent(working.GetPlayableBeatmap(ruleset, mods),
                                        working.GetPlayableBeatmap(ruleset, mods));
        }

        [Test]
        public void TestRepeatedConversionWithPostConversionModsIsElementWiseEquivalent()
        {
            var working = createWorkingBeatmap();
            var ruleset = new ManiaRuleset().RulesetInfo;

            // 两个都在 IApplicableAfterBeatmapConversion 阶段就地改写已转换谱面：Mirror 改列、NTM 整体重建对象。
            var mods = new Mod[]
            {
                new ManiaModKey8(),
                new ManiaModNtoM { Key = { Value = 9 } },
                new ManiaModAdjust { RandomMirror = { Value = false }, Mirror = { Value = true } },
            };

            assertElementWiseEquivalent(working.GetPlayableBeatmap(ruleset, mods),
                                        working.GetPlayableBeatmap(ruleset, mods));
        }

        [Test]
        public void TestConversionPreservesHoldDurationAndSamples()
        {
            var working = createWorkingBeatmap();
            var ruleset = new ManiaRuleset().RulesetInfo;
            var mods = new Mod[] { new ManiaModKey7() };

            IBeatmap beatmap = working.GetPlayableBeatmap(ruleset, mods);

            Assert.That(beatmap.HitObjects, Is.Not.Empty, "前置条件：本用例的源谱面必须转换出 note");
            Assert.That(beatmap.HitObjects.OfType<HoldNote>(), Is.Not.Empty, "前置条件：转换结果应含长条（源谱面有 slider/hold）");
        }

        private static void assertElementWiseEquivalent(IBeatmap expected, IBeatmap actual)
        {
            Assert.That(actual.HitObjects.Count, Is.EqualTo(expected.HitObjects.Count), "HitObject 数量不一致");
            Assert.That(actual.Difficulty.CircleSize, Is.EqualTo(expected.Difficulty.CircleSize), "总列数不一致");

            for (int i = 0; i < expected.HitObjects.Count; i++)
            {
                HitObject e = expected.HitObjects[i];
                HitObject a = actual.HitObjects[i];

                Assert.That(a.GetType(), Is.EqualTo(e.GetType()), $"#{i} 对象类型不一致");
                Assert.That(a.StartTime, Is.EqualTo(e.StartTime), $"#{i} StartTime 不一致");
                Assert.That(((IHasColumn)a).Column, Is.EqualTo(((IHasColumn)e).Column), $"#{i} Column 不一致");

                if (e is IHasDuration eDuration)
                {
                    Assert.That(a, Is.InstanceOf<IHasDuration>(), $"#{i} 一侧是长条而另一侧不是");
                    Assert.That(((IHasDuration)a).Duration, Is.EqualTo(eDuration.Duration), $"#{i} Duration 不一致");
                }

                assertSamplesEqual(e.Samples, a.Samples, $"#{i} Samples");

                if (e is IHasRepeats eRepeats && a is IHasRepeats aRepeats)
                {
                    Assert.That(aRepeats.NodeSamples.Count, Is.EqualTo(eRepeats.NodeSamples.Count), $"#{i} NodeSamples 数量不一致");

                    for (int node = 0; node < eRepeats.NodeSamples.Count; node++)
                        assertSamplesEqual(eRepeats.NodeSamples[node], aRepeats.NodeSamples[node], $"#{i} NodeSamples[{node}]");
                }
            }
        }

        private static void assertSamplesEqual(IList<HitSampleInfo> expected, IList<HitSampleInfo> actual, string context)
        {
            Assert.That(actual.Select(s => s.Name), Is.EqualTo(expected.Select(s => s.Name)), $"{context} 名称序列不一致");
        }

        /// <summary>
        /// 用官方 ruleset 作为源谱面，让 <see cref="ManiaBeatmapConverter"/> 走真实转换分支（键数 Mod 才会生效）。
        /// </summary>
        private static IWorkingBeatmap createWorkingBeatmap()
        {
            var sourceRuleset = new RulesetInfo { ShortName = @"osu", Name = @"osu!" };
            return new TestWorkingBeatmap(new TestBeatmap(sourceRuleset));
        }
    }
}
