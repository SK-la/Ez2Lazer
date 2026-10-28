// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.HUD.ClicksPerSecond;
using osu.Game.Screens.Play.HUD.JudgementCounter;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Ez 皮肤编辑器 HUD 预览层。显隐跟顶栏 HUD layer 开关；
    /// <see cref="InteractionEnabled"/> 为 false 时只读且不吃输入。
    /// 须与游玩区同处 <c>ScalingMode.Gameplay</c> 宿主内。
    /// </summary>
    public partial class EzSkinEditorHudLayer : CompositeDrawable
    {
        public readonly BindableBool InteractionEnabled = new BindableBool();

        public IBindableList<ISerialisableDrawable> SelectedComponents => selectedComponents;

        private readonly BindableList<ISerialisableDrawable> selectedComponents = new BindableList<ISerialisableDrawable>();

        public override bool PropagatePositionalInputSubTree => InteractionEnabled.Value;

        public override bool PropagateNonPositionalInputSubTree => InteractionEnabled.Value;

        // 与 HUDOverlay 相同：字段 [Cached] 供皮肤 HUD 组件解析。
        [Cached]
        private readonly JudgementCountController judgementCountController = new JudgementCountController();

        [Cached]
        private readonly ClicksPerSecondController clicksPerSecondController = new ClicksPerSecondController();

        [Cached]
        private readonly InputCountController inputCountController = new InputCountController();

        private DependencyContainer dependencies = null!;
        private Container rulesetHost = null!;
        private Container blueprintHost = null!;
        private SkinnableContainer mainComponents = null!;
        private RulesetInfo? ruleset;
        private SkinManager skins = null!;

        public EzSkinEditorHudLayer()
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            => dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        [BackgroundDependencyLoader]
        private void load(IBindable<RulesetInfo> rulesetBindable, SkinManager skinManager)
        {
            skins = skinManager;
            // 无局内 Player：缓存最小游玩依赖，供默认 HUD 组件解析。
            var rulesetInstance = rulesetBindable.Value.CreateInstance();
            var emptyBeatmap = new Beatmap();
            var scoreProcessor = rulesetInstance.CreateScoreProcessor();
            scoreProcessor.ApplyEzGameplayEnvironment();
            scoreProcessor.ApplyBeatmap(emptyBeatmap);
            var healthProcessor = rulesetInstance.CreateHealthProcessor(0);
            healthProcessor.ApplyEzGameplayEnvironment();
            healthProcessor.ApplyBeatmap(emptyBeatmap);

            dependencies.CacheAs(scoreProcessor);
            dependencies.CacheAs(healthProcessor);
            dependencies.CacheAs(new GameplayState(
                emptyBeatmap,
                rulesetInstance,
                Array.Empty<Mod>(),
                new Score(),
                scoreProcessor,
                healthProcessor,
                new Storyboard(),
                new Bindable<LocalUserPlayingState>()));

            // 控制器须先于 SkinnableContainer 进入树，才能被 [Cached] 字段注入到子树。
            InternalChild = new GameplayClockContainer(new StopwatchClock(true), applyOffsets: false, requireDecoupling: false)
            {
                Children = new Drawable[]
                {
                    scoreProcessor,
                    healthProcessor,
                    judgementCountController,
                    clicksPerSecondController,
                    inputCountController,
                    mainComponents = new SkinnableContainer(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents))
                    {
                        RelativeSizeAxes = Axes.Both,
                        AlwaysPresent = true,
                    },
                    rulesetHost = new Container { RelativeSizeAxes = Axes.Both },
                    blueprintHost = new Container { RelativeSizeAxes = Axes.Both },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            InteractionEnabled.BindValueChanged(_ => refreshBlueprints(), true);
        }

        public void SetRuleset(RulesetInfo? newRuleset)
        {
            if (ruleset?.Equals(newRuleset) == true)
                return;

            ruleset = newRuleset;
            rulesetHost.Clear();

            if (ruleset != null)
            {
                rulesetHost.Child = new SkinnableContainer(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, ruleset))
                {
                    RelativeSizeAxes = Axes.Both,
                    AlwaysPresent = true,
                };
            }

            refreshBlueprints();
        }

        public void SetVisible(bool visible) =>
            this.FadeTo(visible ? 1 : 0, 200, Easing.OutQuint);

        private void refreshBlueprints()
        {
            blueprintHost.Clear();
            selectedComponents.Clear();

            if (!InteractionEnabled.Value)
                return;

            foreach (var target in EnumerateTargets())
                blueprintHost.Add(new EzSkinEditorHudBlueprintContainer(target, selectedComponents, PersistLayoutToCurrentSkin));
        }

        public IEnumerable<SkinnableContainer> EnumerateTargets()
        {
            yield return mainComponents;

            foreach (var c in rulesetHost.Children.OfType<SkinnableContainer>())
                yield return c;
        }

        /// <summary>
        /// 将当前 HUD 目标序列化写回当前皮肤并落盘。
        /// </summary>
        public void PersistLayoutToCurrentSkin()
        {
            var skin = skins.CurrentSkin.Value;

            foreach (var target in EnumerateTargets())
            {
                if (target.ComponentsLoaded)
                    skin.UpdateDrawableTarget(target);
            }

            skins.Save(skin);
        }
    }
}
