// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Layout;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osu.Game.Screens;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Ez 皮肤编辑器 HUD 蓝图层。只把 Ez 布局层（<see cref="EzLayoutLayer"/>）的容器作为可编辑目标，
    /// 皮肤层 HUD 只在 layer 开关下临时显隐，既不建蓝图也不写回皮肤。
    /// </summary>
    public partial class EzSkinEditorHudLayer : CompositeDrawable, IEditorChangeHandler
    {
        /// <summary>
        /// 蓝图是否接受输入。由场景是否为 HUD 场景与 HUD layer 开关共同决定；与蓝图是否显示无关。
        /// </summary>
        public readonly BindableBool InteractionEnabled = new BindableBool();

        /// <summary>
        /// HUD layer 显隐总闸：对标官方编辑器的全开/全关，只有「隐藏 HUD」层才整体隐藏。
        /// 只控制显隐，不决定编辑哪一层容器。
        /// </summary>
        public readonly BindableBool LayerVisible = new BindableBool(true);

        public IBindableList<ISerialisableDrawable> SelectedComponents => selectedComponents;

        /// <summary>
        /// Raised when the active editing target changes (layer switch, scene switch, or ruleset change),
        /// so library sections can rebuild their previews against the new container.
        /// </summary>
        public event Action? ActiveTargetChanged;

        /// <summary>
        /// Undo/redo over the active layer's container, reusing the official
        /// <see cref="SkinEditorChangeHandler"/>. Recreated whenever the active target changes because a
        /// handler binds exactly one container. Null while there is no editable target.
        /// </summary>
        public SkinEditorChangeHandler? ChangeHandler => changeHandler;

        private SkinEditorChangeHandler? changeHandler;

        // Nested BeginChange/EndChange from the selection handler must not unbalance the delegate swap.
        private IEditorChangeHandler? activeChangeHandler;
        private int changeDepth;

        public event Action? OnStateChange;

        public void BeginChange()
        {
            activeChangeHandler = changeHandler;

            if (activeChangeHandler == null)
                return;

            changeDepth++;
            activeChangeHandler.BeginChange();
            activeChangeHandler.OnStateChange += raiseStateChange;
        }

        public void EndChange()
        {
            if (activeChangeHandler == null)
                return;

            activeChangeHandler.EndChange();
            activeChangeHandler.OnStateChange -= raiseStateChange;

            if (--changeDepth <= 0)
            {
                changeDepth = 0;
                activeChangeHandler = null;
            }
        }

        public void SaveState() => changeHandler?.SaveState();

        public void RestoreState(int direction) => changeHandler?.RestoreState(direction);

        private void raiseStateChange() => OnStateChange?.Invoke();

        private readonly BindableList<ISerialisableDrawable> selectedComponents = new BindableList<ISerialisableDrawable>();

        public override bool PropagatePositionalInputSubTree => InteractionEnabled.Value;

        public override bool PropagateNonPositionalInputSubTree => InteractionEnabled.Value;

        private Container blueprintHost = null!;
        private EzLayoutLayer? layoutLayer;
        private OsuScreen? liveScreen;
        private EzSkinEditorSceneType scene = EzSkinEditorSceneType.Gameplay;
        private EzSkinEditorLayer layer = EzSkinEditorLayer.EzHud;
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
        public void Apply(EzSkinEditorSceneType scene, OsuScreen? screen, EzLayoutLayer? ezLayout) => Apply(scene, screen, ezLayout, layer);

        /// <summary>
        /// Binds the editor HUD to a scene and selects which layout layer is actively edited.
        /// Visibility stays decoupled: it is driven solely by <see cref="LayerVisible"/>.
        /// </summary>
        public void Apply(EzSkinEditorSceneType scene, OsuScreen? screen, EzLayoutLayer? ezLayout, EzSkinEditorLayer activeLayer)
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
            layer = EzSkinEditorLayerOptions.Normalize(scene, activeLayer);

            updateLayoutLayerVisibility();
            updateSkinLayerVisibility();
            refreshBlueprints();
        }

        public void DetachLiveTargets() => Apply(scene, null, null, layer);

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
            {
                ActiveTargetChanged?.Invoke();
                return;
            }

            foreach (var target in EnumerateTargets())
                blueprintHost.Add(new EzSkinEditorHudBlueprintContainer(target, selectedComponents, PersistLayoutToCurrentSkin));

            rebuildChangeHandler();
            ActiveTargetChanged?.Invoke();
        }

        /// <summary>
        /// Recreates the undo/redo handler against the current active container. The official handler
        /// binds exactly one container, so it must be rebuilt on every target change.
        /// </summary>
        private void rebuildChangeHandler()
        {
            changeHandler?.Dispose();
            changeHandler = null;

            EzLayoutContainer? target = ActiveTarget;

            if (target == null)
                return;

            var handler = new SkinEditorChangeHandler(target);
            handler.CanUndo.BindValueChanged(_ => raiseStateChange());
            handler.CanRedo.BindValueChanged(_ => raiseStateChange());
            changeHandler = handler;
        }

        /// <summary>
        /// Persists every layout container edited by the editor. Used on apply/save.
        /// </summary>
        public void PersistAllLayouts()
        {
            if (layoutLayer == null)
                return;

            foreach (var target in layoutLayer.LayoutContainers)
                layoutLayer.Store.Save(target);
        }

        /// <summary>
        /// Ez 布局层中属于当前场景/当前编辑层的容器。可编辑目标由
        /// <see cref="EzSkinEditorSceneRegistry"/> 与 <see cref="EzSkinEditorLayerOptions"/> 解析，
        /// 本层不再自行维护场景到目标的映射。只匹配精确 lookup（Lookup + Ruleset 均相等），
        /// 保证只编辑所选层，其余层保持可见但不进蓝图。
        /// </summary>
        public IEnumerable<EzLayoutContainer> EnumerateTargets()
        {
            if (layoutLayer == null)
                yield break;

            GlobalSkinnableContainerLookup? wanted = EzSkinEditorLayerOptions.ResolveLookup(scene, layer, ruleset);

            if (wanted == null)
                yield break;

            foreach (var container in layoutLayer.LayoutContainers.Where(c => c.Lookup.Equals(wanted)))
                yield return container;
        }

        public void PersistLayoutToCurrentSkin()
        {
            foreach (var target in EnumerateTargets())
                layoutLayer?.Store.Save(target);
        }

        /// <summary>
        /// The container currently being edited (the active layer's only target), or null when the
        /// layer has no editable container. Used as the dependency source for library previews.
        /// </summary>
        public EzLayoutContainer? ActiveTarget => EnumerateTargets().FirstOrDefault();

        /// <summary>
        /// The ruleset active in the editor; drives which library (global / mode) can be edited.
        /// </summary>
        public RulesetInfo? CurrentRuleset => ruleset;

        /// <summary>
        /// Instantiates and places a component into the active layer's container, persisting the
        /// layout and selecting the new component. Returns false when there is no editable target
        /// or the container rejects the component.
        /// </summary>
        public bool TryPlaceComponent(Type type)
        {
            EzLayoutContainer? target = ActiveTarget;

            if (target == null)
                return false;

            if (Activator.CreateInstance(type) is not ISerialisableDrawable component)
                return false;

            if (!target.TryPlace(component))
                return false;

            selectedComponents.Clear();
            selectedComponents.Add(component);

            layoutLayer?.Store.Save(target);
            return true;
        }

        private void updateLayoutLayerVisibility()
        {
            // 全开/全关对标官方编辑器：只有「隐藏 HUD」层才整体隐藏，切层不影响其它层显隐。
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

            changeHandler?.Dispose();
            changeHandler = null;

            if (layoutLayer != null)
            {
                layoutLayer.LayoutTargetsChanged -= onLayoutTargetsChanged;
                layoutLayer.EditorHidden = false;
            }

            releaseSkinLayer();
        }
    }
}
