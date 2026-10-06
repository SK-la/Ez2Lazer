// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.EzOsuGame.Beatmaps
{
    /// <summary>
    /// 钉住 <see cref="EzPlayableBeatmapCache"/> 的两条契约：键不合并不同输入，以及共享作用域只转换一次。
    /// </summary>
    /// <remarks>
    /// 这里的 working beatmap 记录转换次数，所以「命中真的省下一次转换」和「只是返回了不同的对象」是分开验证的。
    /// </remarks>
    [TestFixture]
    public class EzPlayableBeatmapCacheTest
    {
        [SetUp]
        [TearDown]
        public void ResetCache() => EzPlayableBeatmapCache.Reset();

        [Test]
        public void TestRepeatedLookupReturnsSameInstanceAndConvertsOnce()
        {
            var working = createWorkingBeatmap();
            var mods = new Mod[] { new TestSeededMod() };

            IBeatmap first = EzPlayableBeatmapCache.GetShared(working, working.BeatmapInfo.Ruleset, mods);
            IBeatmap second = EzPlayableBeatmapCache.GetShared(working, working.BeatmapInfo.Ruleset, mods);

            Assert.That(second, Is.SameAs(first), "同一组输入必须复用同一份转换产物");
            Assert.That(working.Conversions, Is.EqualTo(1), "第二次查询不应再跑一次转换");
        }

        [Test]
        public void TestModOrderChangeConvertsAgain()
        {
            var working = createWorkingBeatmap();
            var ruleset = working.BeatmapInfo.Ruleset;

            var a = new TestSeededMod { Seed = { Value = 2 } };
            var b = new TestSeededMod { Seed = { Value = 3 } };

            EzPlayableBeatmapCache.GetShared(working, ruleset, new Mod[] { a, b });
            EzPlayableBeatmapCache.GetShared(working, ruleset, new Mod[] { b, a });

            Assert.That(working.Conversions, Is.EqualTo(2),
                "顺序不同的两组 mod 是不同的转换输入（ApplyOrder 相同时按列表序生效），不能共用一个键");
        }

        [Test]
        public void TestModSettingChangeConvertsAgain()
        {
            var working = createWorkingBeatmap();
            var ruleset = working.BeatmapInfo.Ruleset;

            var mod = new TestSeededMod { Seed = { Value = 1234 } };
            EzPlayableBeatmapCache.GetShared(working, ruleset, new Mod[] { mod });

            mod.Seed.Value = 4321;
            EzPlayableBeatmapCache.GetShared(working, ruleset, new Mod[] { mod });

            Assert.That(working.Conversions, Is.EqualTo(2), "设置变了就是另一种转换输入");
        }

        [Test]
        public void TestDifferentRulesetConvertsAgain()
        {
            var working = createWorkingBeatmap();
            var ruleset = working.BeatmapInfo.Ruleset;

            EzPlayableBeatmapCache.GetShared(working, ruleset, new Mod[] { new TestSeededMod() });
            EzPlayableBeatmapCache.GetShared(working, new OsuRuleset().RulesetInfo, new Mod[] { new TestSeededMod() });

            Assert.That(working.Conversions, Is.EqualTo(2));
        }

        [Test]
        public void TestContentHashChangeConvertsAgain()
        {
            var working = createWorkingBeatmap();
            var ruleset = working.BeatmapInfo.Ruleset;

            EzPlayableBeatmapCache.GetShared(working, ruleset);

            // 同一个 working beatmap 实例、内容变了（失效后仍被别的调用方持有，文件被重新导入）：
            // 只按实例缓存会拿旧谱面回答，所以内容哈希必须进键。
            working.BeatmapInfo.Hash = "changed-content";

            EzPlayableBeatmapCache.GetShared(working, ruleset);

            Assert.That(working.Conversions, Is.EqualTo(2));
        }

        /// <summary>
        /// null 种子在键里就等于它真正掷出的那个数：快照会把它解析掉再带进键，所以「掷出后还没写回 bindable」
        /// 的那一小段时间里第二次查询仍然命中，而不是每次都重掷、重新转换。
        /// </summary>
        [Test]
        public void TestNullSeedResolvesOnceAndThenHits()
        {
            var working = createWorkingBeatmap();
            var ruleset = working.BeatmapInfo.Ruleset;
            var mod = new TestSeededMod();

            Assert.That(mod.Seed.Value, Is.Null, "前置条件：种子未掷出");

            EzPlayableBeatmapCache.GetShared(working, ruleset, new Mod[] { mod });
            EzPlayableBeatmapCache.GetShared(working, ruleset, new Mod[] { mod });

            Assert.That(working.Conversions, Is.EqualTo(1), "同一次掷出必须给出同一个键");
        }

        [Test]
        public void TestBoundScopeIsSeparateFromSharedAndAppliesBinderOnce()
        {
            var working = createWorkingBeatmap();
            var ruleset = working.BeatmapInfo.Ruleset;
            var mods = new Mod[] { new TestSeededMod() };

            int bindCalls = 0;

            IBeatmap shared = EzPlayableBeatmapCache.GetShared(working, ruleset, mods);
            IBeatmap bound = EzPlayableBeatmapCache.GetBound(working, ruleset, mods, 1, _ => bindCalls++);
            IBeatmap boundAgain = EzPlayableBeatmapCache.GetBound(working, ruleset, mods, 1, _ => bindCalls++);

            Assert.That(bound, Is.Not.SameAs(shared), "共享作用域不能被就地绑定的调用方拿走");
            Assert.That(boundAgain, Is.SameAs(bound));
            Assert.That(bindCalls, Is.EqualTo(1), "绑定只在产生该副本的那一次转换上执行");
            Assert.That(working.Conversions, Is.EqualTo(2));
        }

        [Test]
        public void TestDifferentBindingScopesDoNotShare()
        {
            var working = createWorkingBeatmap();
            var ruleset = working.BeatmapInfo.Ruleset;
            var mods = new Mod[] { new TestSeededMod() };

            IBeatmap first = EzPlayableBeatmapCache.GetBound(working, ruleset, mods, 1, static _ => { });
            IBeatmap second = EzPlayableBeatmapCache.GetBound(working, ruleset, mods, 2, static _ => { });

            Assert.That(second, Is.Not.SameAs(first), "两个 hitmode 不能落在同一份就地绑定的实例上");
            Assert.That(working.Conversions, Is.EqualTo(2));
        }

        [Test]
        public void TestSharedScopeIsRejectedAsBindingScope()
        {
            var working = createWorkingBeatmap();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EzPlayableBeatmapCache.GetBound(working, working.BeatmapInfo.Ruleset, null, EzPlayableBeatmapCache.SHARED_SCOPE, static _ => { }));
        }

        [Test]
        public void TestCancelledConversionIsNotCached()
        {
            var working = createWorkingBeatmap();
            working.CancelNext = true;
            var ruleset = working.BeatmapInfo.Ruleset;

            // 无 token 时走上游带 10s 有界等待的重载，它把取消翻译成私有的 BeatmapLoadTimeoutException（TimeoutException 子类）。
            Exception? failure = Assert.Catch<Exception>(() => EzPlayableBeatmapCache.GetShared(working, ruleset));

            Assert.That(failure, Is.InstanceOf<OperationCanceledException>().Or.InstanceOf<TimeoutException>(),
                "前置条件：这次转换必须以失败收场，否则用例测不到「失败不入缓存」");

            Assert.That(working.Conversions, Is.EqualTo(1));
            Assert.DoesNotThrow(() => EzPlayableBeatmapCache.GetShared(working, ruleset));
            Assert.That(working.Conversions, Is.EqualTo(2), "失败（含取消）不得进入缓存");
        }

        [Test]
        public void TestSharedInstanceIsReportedAsShared()
        {
            var working = createWorkingBeatmap();
            var ruleset = working.BeatmapInfo.Ruleset;
            var mods = new Mod[] { new TestSeededMod() };

            Assert.That(EzPlayableBeatmapCache.IsSharedInstance(EzPlayableBeatmapCache.GetShared(working, ruleset, mods)), Is.True);
            Assert.That(EzPlayableBeatmapCache.IsSharedInstance(EzPlayableBeatmapCache.GetBound(working, ruleset, mods, 1, static _ => { })), Is.False);
        }

        [Test]
        public void TestResetDropsCachedEntries()
        {
            var working = createWorkingBeatmap();
            var ruleset = working.BeatmapInfo.Ruleset;

            EzPlayableBeatmapCache.GetShared(working, ruleset);
            EzPlayableBeatmapCache.Reset();
            EzPlayableBeatmapCache.GetShared(working, ruleset);

            Assert.That(working.Conversions, Is.EqualTo(2));
        }

        private static CountingWorkingBeatmap createWorkingBeatmap()
        {
            RulesetInfo ruleset = new OsuRuleset().RulesetInfo;
            return new CountingWorkingBeatmap(new TestBeatmap(ruleset));
        }

        private class CountingWorkingBeatmap : TestWorkingBeatmap
        {
            public int Conversions;

            /// <summary>Makes the next conversion fail, to pin that nothing is cached on the way out.</summary>
            public bool CancelNext;

            public CountingWorkingBeatmap(IBeatmap beatmap)
                : base(beatmap)
            {
            }

            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token)
            {
                Conversions++;

                if (CancelNext)
                {
                    CancelNext = false;
                    throw new OperationCanceledException();
                }

                return base.GetPlayableBeatmap(ruleset, mods, token);
            }
        }

        /// <summary>
        /// 只用于取键的 mod：没有转换行为，但带 seed 设置项，正好覆盖指纹的「类型 + 设置」两条维度。
        /// </summary>
        private class TestSeededMod : Mod, IHasSeed
        {
            public override string Name => "Test Seeded";

            public override string Acronym => "TS";

            public override LocalisableString Description => "Test-only seeded mod.";

            // 必须是 SettingSource：Mod.Equals 只比较这些设置，而键的相等性靠它。
            [SettingSource("Seed", "Use a custom seed instead of a random one")]
            public Bindable<int?> Seed { get; } = new Bindable<int?>();
        }
    }
}
