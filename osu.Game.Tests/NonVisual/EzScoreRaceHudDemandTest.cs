// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Game.Audio;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Layout;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// 角逐 HUD 消费者判定：只读布局数据 / 只构造组件，不加载不绘制。
    /// </summary>
    [TestFixture]
    public class EzScoreRaceHudDemandTest
    {
        [Test]
        public void TestSkinCodeDefaultProvidesScoreRaceHud()
        {
            var skin = new TestSkin(providesScoreRaceHud: true);

            var container = EzScoreRaceService.GetSkinComponentsContainer(skin, new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents));

            Assert.That(EzScoreRaceService.ContainsScoreRaceHud(container), Is.True);
        }

        [Test]
        public void TestSkinCodeDefaultWithoutScoreRaceHud()
        {
            var skin = new TestSkin(providesScoreRaceHud: false);

            var container = EzScoreRaceService.GetSkinComponentsContainer(skin, new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents));

            Assert.That(EzScoreRaceService.ContainsScoreRaceHud(container), Is.False);
        }

        /// <summary>
        /// 用户保存过布局时，皮肤代码默认必须被忽略（与 <see cref="SkinnableContainer"/> 的加载顺序一致）。
        /// </summary>
        [Test]
        public void TestUserLayoutOverridesCodeDefault()
        {
            var skin = new TestSkin(providesScoreRaceHud: true);

            storeLayout(skin, GlobalSkinnableContainers.MainHUDComponents, null, new SerialisedDrawableInfo { Type = typeof(BoxElement) });

            var container = EzScoreRaceService.GetSkinComponentsContainer(skin, new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents));

            Assert.That(EzScoreRaceService.ContainsScoreRaceHud(container), Is.False);
        }

        [Test]
        public void TestUserLayoutWithScoreRaceHud()
        {
            var skin = new TestSkin(providesScoreRaceHud: false);

            storeLayout(skin, GlobalSkinnableContainers.MainHUDComponents, null, new SerialisedDrawableInfo { Type = typeof(EzHUDScoreRaceLeaderboard) });

            var container = EzScoreRaceService.GetSkinComponentsContainer(skin, new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents));

            Assert.That(EzScoreRaceService.ContainsScoreRaceHud(container), Is.True);
        }

        /// <summary>
        /// 规则集专属布局只在对应规则集下命中，global 查询不应看到它。
        /// </summary>
        [Test]
        public void TestRulesetScopedLayoutIsScoped()
        {
            var skin = new TestSkin(providesScoreRaceHud: false);
            var mania = new RulesetInfo("mania", "osu!mania", string.Empty, 3);

            storeLayout(skin, GlobalSkinnableContainers.MainHUDComponents, mania, new SerialisedDrawableInfo { Type = typeof(EzHUDScoreCompareBars) });

            Assert.That(EzScoreRaceService.ContainsScoreRaceHud(EzScoreRaceService.GetSkinComponentsContainer(skin, new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents))), Is.False);
            Assert.That(EzScoreRaceService.ContainsScoreRaceHud(EzScoreRaceService.GetSkinComponentsContainer(skin, new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, mania))), Is.True);
        }

        [Test]
        public void TestNestedChildrenDetected()
        {
            var nested = new Container
            {
                ChildrenEnumerable = new Drawable[]
                {
                    new Box(),
                    new Container
                    {
                        ChildrenEnumerable = new Drawable[]
                        {
                            new Box(),
                            new EzHUDScoreCompareBars(),
                        }
                    },
                },
            };

            Assert.That(EzScoreRaceService.ContainsScoreRaceHud(nested), Is.True);
            Assert.That(EzScoreRaceService.ContainsScoreRaceHud(new Container { ChildrenEnumerable = new Drawable[] { new Box() } }), Is.False);
            Assert.That(EzScoreRaceService.ContainsScoreRaceHud(null), Is.False);
        }

        /// <summary>
        /// 序列化数据扫描：递归比对 Type，不构造任何实例。
        /// </summary>
        [Test]
        public void TestContainsScoreRaceHudType()
        {
            Assert.That(EzScoreRaceService.ContainsScoreRaceHudType(null), Is.False);
            Assert.That(EzScoreRaceService.ContainsScoreRaceHudType(Array.Empty<SerialisedDrawableInfo>()), Is.False);

            Assert.That(EzScoreRaceService.ContainsScoreRaceHudType(new[]
            {
                new SerialisedDrawableInfo { Type = typeof(BoxElement) },
                new SerialisedDrawableInfo { Type = typeof(EzHUDScoreRaceLeaderboard) },
            }), Is.True);

            var outer = new SerialisedDrawableInfo { Type = typeof(Container) };
            outer.Children.Add(new SerialisedDrawableInfo { Type = typeof(BoxElement) });
            outer.Children.Add(new SerialisedDrawableInfo { Type = typeof(EzHUDScoreCompareBars) });

            Assert.That(EzScoreRaceService.ContainsScoreRaceHudType(new[] { outer }), Is.True);

            Assert.That(EzScoreRaceService.ContainsScoreRaceHudType(new[] { new SerialisedDrawableInfo { Type = typeof(Container) } }), Is.False);
        }

        [Test]
        public void TestIsScoreRaceHudType()
        {
            Assert.That(EzScoreRaceService.IsScoreRaceHudType(typeof(EzHUDScoreRaceLeaderboard)), Is.True);
            Assert.That(EzScoreRaceService.IsScoreRaceHudType(typeof(EzHUDScoreCompareBars)), Is.True);
            Assert.That(EzScoreRaceService.IsScoreRaceHudType(typeof(BoxElement)), Is.False);
            Assert.That(EzScoreRaceService.IsScoreRaceHudType(typeof(Container)), Is.False);
        }

        /// <summary>
        /// 硬不变量：无消费者时角逐工作一律不做（不查 Realm、不建 timeline、不阻塞进局）。
        /// 消费者 = 静态预测或运行时实际注册，任一为真即可工作。
        /// </summary>
        [Test]
        public void TestNoConsumersMeansNoWork()
        {
            // 预测与实际注册都为假：无消费者，不做工作。
            Assert.That(EzScoreRaceService.ShouldPerformScoreRaceWork(serviceActive: true, predicted: false, hasConsumers: false), Is.False);

            // 服务关闭时一律不工作，即使两个来源都为真。
            Assert.That(EzScoreRaceService.ShouldPerformScoreRaceWork(serviceActive: false, predicted: true, hasConsumers: true), Is.False);
            Assert.That(EzScoreRaceService.ShouldPerformScoreRaceWork(serviceActive: false, predicted: false, hasConsumers: true), Is.False);
        }

        [Test]
        public void TestEitherTruthSourceEnablesWork()
        {
            // 预测命中：不必等注册，构建与 Player 装载并行。
            Assert.That(EzScoreRaceService.ShouldPerformScoreRaceWork(serviceActive: true, predicted: true, hasConsumers: false), Is.True);

            // 预测漏判、运行时注册补救（规则集漂移等）。
            Assert.That(EzScoreRaceService.ShouldPerformScoreRaceWork(serviceActive: true, predicted: false, hasConsumers: true), Is.True);

            Assert.That(EzScoreRaceService.ShouldPerformScoreRaceWork(serviceActive: true, predicted: true, hasConsumers: true), Is.True);
        }

        /// <summary>
        /// 需求判定本身不得成为工作入口：只有需求消失才回到静默态，需求出现不做任何事。
        /// </summary>
        [Test]
        public void TestDemandTransitionOnlyActsOnLoss()
        {
            Assert.That(EzScoreRaceService.ShouldEnterQuiescentState(previousDemand: false, currentDemand: true), Is.False);
            Assert.That(EzScoreRaceService.ShouldEnterQuiescentState(previousDemand: true, currentDemand: true), Is.False);
            Assert.That(EzScoreRaceService.ShouldEnterQuiescentState(previousDemand: false, currentDemand: false), Is.False);
            Assert.That(EzScoreRaceService.ShouldEnterQuiescentState(previousDemand: true, currentDemand: false), Is.True);
        }

        /// <summary>
        /// loading 末尾的补建裁决：必须等 Player 装载完成、必须存在运行时注册、每轮只做一次。
        /// 不做「注册为 0 就取消」的反向裁决，避免注册晚到被误杀。
        /// </summary>
        [Test]
        public void TestLateDemandResolution()
        {
            // 注册已表明存在消费者，但 Player 还没装载完（皮肤组件未定）→ 不下结论。
            Assert.That(EzScoreRaceService.ShouldResolveDemandFromConsumers(alreadyResolved: false, hasConsumers: true, playerFullyLoaded: false), Is.False);

            // 无运行时注册 → 不需要补建。
            Assert.That(EzScoreRaceService.ShouldResolveDemandFromConsumers(alreadyResolved: false, hasConsumers: false, playerFullyLoaded: true), Is.False);

            // 每轮 loading 只裁决一次。
            Assert.That(EzScoreRaceService.ShouldResolveDemandFromConsumers(alreadyResolved: true, hasConsumers: true, playerFullyLoaded: true), Is.False);

            // 预测漏判 + 注册为真 + 装载完成 → 补建。
            Assert.That(EzScoreRaceService.ShouldResolveDemandFromConsumers(alreadyResolved: false, hasConsumers: true, playerFullyLoaded: true), Is.True);
        }

        [Test]
        public void TestEzLayoutStoreEmptyAndPopulated()
        {
            string path = Path.Combine(Path.GetTempPath(), "ez-race-demand-test-" + Guid.NewGuid().ToString("N"));
            var storage = new NativeStorage(path);

            try
            {
                var store = new EzLayoutStore(storage);

                Assert.That(EzScoreRaceService.ContainsScoreRaceHud(store.CreateComponentsContainer(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents))), Is.False);

                var layout = new SkinLayoutInfo();
                layout.Update(null, new[] { new SerialisedDrawableInfo { Type = typeof(EzHUDScoreRaceLeaderboard) } });
                store.Save(GlobalSkinnableContainers.MainHUDComponents, layout);

                Assert.That(EzScoreRaceService.ContainsScoreRaceHud(store.CreateComponentsContainer(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents))), Is.True);

                // 规则集专属查询不应命中 global 条目。
                var mania = new RulesetInfo("mania", "osu!mania", string.Empty, 3);
                Assert.That(EzScoreRaceService.ContainsScoreRaceHud(store.CreateComponentsContainer(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, mania))), Is.False);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(path))
                        Directory.Delete(path, true);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// ez 层序列化嵌套容器时，<see cref="SerialisedDrawableInfo.Children"/> 也应被识别。
        /// </summary>
        [Test]
        public void TestEzLayoutStoreNestedChildrenDetected()
        {
            string path = Path.Combine(Path.GetTempPath(), "ez-race-demand-nested-" + Guid.NewGuid().ToString("N"));
            var storage = new NativeStorage(path);

            try
            {
                var store = new EzLayoutStore(storage);

                var outer = new SerialisedDrawableInfo { Type = typeof(Container) };
                outer.Children.Add(new SerialisedDrawableInfo { Type = typeof(EzHUDScoreCompareBars) });

                var layout = new SkinLayoutInfo();
                layout.Update(null, new[] { outer });
                store.Save(GlobalSkinnableContainers.MainHUDComponents, layout);

                Assert.That(EzScoreRaceService.ContainsScoreRaceHud(store.CreateComponentsContainer(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents))), Is.True);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(path))
                        Directory.Delete(path, true);
                }
                catch
                {
                }
            }
        }

        private static void storeLayout(Skin skin, GlobalSkinnableContainers target, RulesetInfo? ruleset, params SerialisedDrawableInfo[] components)
        {
            if (!skin.LayoutInfos.TryGetValue(target, out var layout))
                skin.LayoutInfos[target] = layout = new SkinLayoutInfo();

            layout.Update(ruleset, components);
        }

        /// <summary>
        /// 模拟真实皮肤：用户保存的布局交回基类（<see cref="Skin.LayoutInfos"/>），
        /// 无布局时才由皮肤代码返回默认组件。
        /// </summary>
        private class TestSkin : Skin
        {
            private readonly bool providesScoreRaceHud;

            public TestSkin(bool providesScoreRaceHud)
                : base(new SkinInfo(), null, null, string.Empty)
            {
                this.providesScoreRaceHud = providesScoreRaceHud;
            }

            public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
            {
                if (lookup is UserSkinComponentLookup)
                    return base.GetDrawableComponent(lookup);

                if (providesScoreRaceHud && lookup is GlobalSkinnableContainerLookup { Lookup: GlobalSkinnableContainers.MainHUDComponents })
                {
                    return new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        ChildrenEnumerable = new Drawable[] { new EzHUDScoreRaceLeaderboard() },
                    };
                }

                return base.GetDrawableComponent(lookup);
            }

            public override Texture GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => throw new NotImplementedException();

            public override IBindable<TValue> GetConfig<TLookup, TValue>(TLookup lookup) => throw new NotImplementedException();

            public override ISample GetSample(ISampleInfo sampleInfo) => throw new NotImplementedException();
        }
    }
}
