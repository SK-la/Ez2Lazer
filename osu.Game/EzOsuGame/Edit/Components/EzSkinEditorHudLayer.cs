// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Layout;
using osu.Game.Rulesets;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Ez 皮肤编辑器 HUD 蓝图层。只把 Ez 布局层（<see cref="EzLayoutLayer"/>）的容器作为可编辑目标，
    /// 皮肤层 HUD 只在 layer 开关下临时显隐，既不建蓝图也不写回皮肤。
    /// </summary>
    public partial class EzSkinEditorHudLayer : CompositeDrawable
    {
        /// <summary>
        /// 蓝图是否接受输入。由场景是否为 HUD 场景与 HUD layer 开关共同决定；与蓝图是否显示无关。
        /// </summary>
        public readonly BindableBool InteractionEnabled = new BindableBool();

        /// <summary>
        /// HUD layer 开关本身：临时控制 Ez 布局层与皮肤层 HUD 的显示隐藏。
        /// </summary>
        public readonly BindableBool LayerVisible = new BindableBool(true);

        public IBindableList<ISerialisableDrawable> SelectedComponents => selectedComponents;

        private readonly BindableList<ISerialisableDrawable> selectedComponents = new BindableList<ISerialisableDrawable>();

        public override bool PropagatePositionalInputSubTree => InteractionEnabled.Value;

        public override bool PropagateNonPositionalInputSubTree => InteractionEnabled.Value;

        private Container blueprintHost = null!;
        private EzLayoutLayer? layoutLayer;
        private OsuScreen? liveScreen;
        private EzSkinEditorSceneType scene = EzSkinEditorSceneType.Gameplay;
        private RulesetInfo? ruleset;

        private HUDOverlay? boundHudOverlay;
        private LeasedBindable<bool>? leasedShowHud;

        private SkinnableContainer? boundSongSelectSkinLayer;
        private float rememberedSongSelectSkinAlpha = 1;
        private bool? appliedSongSelectSkinHidden;

        public EzSkinEditorHudLayer()
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = blueprintHost = new Container { RelativeSizeAxes = Axes.Both };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            LayerVisible.BindValueChanged(_ =>
            {
                updateLayoutLayerVisibility();
                updateSkinLayerVisibility();
                refreshBlueprints();
            }, true);
        }

        /// <summary>
        /// Binds the editor HUD to a scene. Only <paramref name="ezLayout"/> contributes editable targets;
        /// <paramref name="screen"/> is used to reach the skin layer HUD for temporary visibility only.
        /// </summary>
        public void Apply(EzSkinEditorSceneType scene, OsuScreen? screen, EzLayoutLayer? ezLayout)
        {
            if (!ReferenceEquals(layoutLayer, ezLayout))
            {
                if (layoutLayer != null)
                {
                    layoutLayer.LayoutTargetsChanged -= onLayoutTargetsChanged;
                    layoutLayer.EditorHidden = false;
                }

                layoutLayer = ezLayout;

                if (layoutLayer != null)
                    layoutLayer.LayoutTargetsChanged += onLayoutTargetsChanged;
            }

            this.scene = scene;
            liveScreen = screen;

            updateLayoutLayerVisibility();
            updateSkinLayerVisibility();
            refreshBlueprints();
        }

        public void DetachLiveTargets() => Apply(scene, null, null);

        private void onLayoutTargetsChanged()
        {
            if (IsLoaded)
                Schedule(refreshBlueprints);
        }

        public void SetRuleset(RulesetInfo? newRuleset)
        {
            if (ruleset?.Equals(newRuleset) == true)
                return;

            ruleset = newRuleset;
            refreshBlueprints();
        }

        public void SetVisible(bool visible)
        {
            LayerVisible.Value = visible;
            this.FadeTo(visible ? 1 : 0, 200, Easing.OutQuint);
        }

        private void refreshBlueprints()
        {
            if (!IsLoaded)
                return;

            blueprintHost.Clear();
            selectedComponents.Clear();

            // 显示由 HUD layer 开关决定，交互由场景 + 开关共同决定；两者互不代管。
            if (!LayerVisible.Value)
                return;

            foreach (var target in EnumerateTargets())
                blueprintHost.Add(new EzSkinEditorHudBlueprintContainer(target, selectedComponents, PersistLayoutToCurrentSkin));
        }

        /// <summary>
        /// Ez 布局层中属于当前场景的容器。可编辑目标由 <see cref="EzSkinEditorSceneRegistry"/> 解析，
        /// 本层不再自行维护场景到目标的映射。
        /// </summary>
        public IEnumerable<EzLayoutContainer> EnumerateTargets()
        {
            if (layoutLayer == null)
                yield break;

            GlobalSkinnableContainers? wanted = EzSkinEditorSceneRegistry.ResolveLayoutTarget(scene);

            if (wanted == null)
                yield break;

            foreach (var container in layoutLayer.LayoutContainers.Where(c => c.Lookup.Lookup == wanted))
                yield return container;
        }

        public void PersistLayoutToCurrentSkin()
        {
            foreach (var target in EnumerateTargets())
                layoutLayer?.Store.Save(target);
        }

        private void updateLayoutLayerVisibility()
        {
            layoutLayer?.EditorHidden = !LayerVisible.Value;
        }

        /// <summary>
        /// 皮肤层 HUD 只临时跟随 layer 开关显隐；不写回皮肤，也不进入蓝图。
        /// </summary>
        private void updateSkinLayerVisibility()
        {
            updateGameplaySkinLayer();
            updateSongSelectSkinLayer();
        }

        private void updateGameplaySkinLayer()
        {
            // 只认当前屏：退场中的 Player 仍能被 ChildrenOfType 找到，但屏幕退场会走
            // UnbindAllBindablesSubTree，连同我们从 ShowHud 租的 lease 一并归还；再写就会抛「已归还」。
            HUDOverlay? overlay = liveScreen is Player player && player.IsCurrentScreen()
                ? player.ChildrenOfType<HUDOverlay>().FirstOrDefault()
                : null;

            if (!ReferenceEquals(boundHudOverlay, overlay))
            {
                leasedShowHud?.Return();
                leasedShowHud = null;
                boundHudOverlay = overlay;
            }

            if (overlay == null)
                return;

            // ShowHud 同时是显隐与输入的总闸；lease 期间配置与「按住显示 HUD」都不会把它改回来。
            if (leasedShowHud != null)
            {
                leasedShowHud.Value = LayerVisible.Value;
                return;
            }

            // 已被其他机制接管（例如 cinema / 无 HUD 规则集）时不要插手。
            if (overlay.ShowHud.Disabled)
                return;

            leasedShowHud = overlay.ShowHud.BeginLease(true);
            leasedShowHud.Value = LayerVisible.Value;
        }

        private void updateSongSelectSkinLayer()
        {
            SkinnableContainer? container = liveScreen is SongSelect songSelect
                ? songSelect.ChildrenOfType<SkinnableContainer>().FirstOrDefault(c => c.Lookup.Lookup == GlobalSkinnableContainers.SongSelect)
                : null;

            if (!ReferenceEquals(boundSongSelectSkinLayer, container))
            {
                if (boundSongSelectSkinLayer != null)
                {
                    boundSongSelectSkinLayer.Alpha = rememberedSongSelectSkinAlpha;
                    appliedSongSelectSkinHidden = null;
                }

                boundSongSelectSkinLayer = container;
                rememberedSongSelectSkinAlpha = container?.Alpha ?? 1;
            }

            if (container == null)
                return;

            bool hidden = !LayerVisible.Value;

            if (appliedSongSelectSkinHidden == hidden)
                return;

            if (hidden)
            {
                rememberedSongSelectSkinAlpha = container.Alpha;
                container.Alpha = 0;
            }
            else if (appliedSongSelectSkinHidden == true)
                container.Alpha = rememberedSongSelectSkinAlpha;

            appliedSongSelectSkinHidden = hidden;
        }

        private void releaseSkinLayer()
        {
            leasedShowHud?.Return();
            leasedShowHud = null;
            boundHudOverlay = null;

            if (boundSongSelectSkinLayer != null)
            {
                if (appliedSongSelectSkinHidden == true)
                    boundSongSelectSkinLayer.Alpha = rememberedSongSelectSkinAlpha;

                boundSongSelectSkinLayer = null;
            }

            appliedSongSelectSkinHidden = null;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (layoutLayer != null)
            {
                layoutLayer.LayoutTargetsChanged -= onLayoutTargetsChanged;
                layoutLayer.EditorHidden = false;
            }

            releaseSkinLayer();
        }
    }
}
