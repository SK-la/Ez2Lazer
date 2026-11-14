// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Layout;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osu.Game.Screens;
using osu.Game.Screens.Edit;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Ez 皮肤编辑器 HUD 蓝图层。Ez 层编辑 <see cref="EzLayoutLayer"/> 容器；Skin 层编辑当前屏上
    /// 的皮肤 <see cref="SkinnableContainer"/>，写回当前皮肤 JSON。
    /// </summary>
    public partial class EzSkinEditorHudLayer : CompositeDrawable, IEditorChangeHandler
    {
        private const double hud_fade_duration = 200;

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
        public SkinEditorChangeHandler? ChangeHandler { get; private set; }

        // Nested BeginChange/EndChange from the selection handler must not unbalance the delegate swap.
        private IEditorChangeHandler? activeChangeHandler;
        private int changeDepth;

        public event Action? OnStateChange;

        public void BeginChange()
        {
            activeChangeHandler = ChangeHandler;

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

        public void SaveState() => ChangeHandler?.SaveState();

        public void RestoreState(int direction) => ChangeHandler?.RestoreState(direction);

        private void raiseStateChange() => OnStateChange?.Invoke();

        private readonly BindableList<ISerialisableDrawable> selectedComponents = new BindableList<ISerialisableDrawable>();

        /// <summary>
        /// Ez's own clipboard. The official one is cached inside the official editor overlay's tree,
        /// which this layer cannot resolve.
        /// </summary>
        public readonly EditorClipboard Clipboard = new EditorClipboard();

        public override bool PropagatePositionalInputSubTree => InteractionEnabled.Value;

        public override bool PropagateNonPositionalInputSubTree => InteractionEnabled.Value;

        private Container blueprintHost = null!;
        private EzLayoutLayer? layoutLayer;
        private OsuScreen? liveScreen;
        private EzEditorSceneType scene = EzEditorSceneType.GameplayHud;
        private EzHUDLayer layer = EzHUDLayer.EzHud;
        private Guid dirtySkinId;

        /// <summary>
        /// Pending skin HUD layout writes not yet flushed to disk.
        /// </summary>
        public bool HasPendingSkinLayoutSave { get; private set; }

        /// <summary>
        /// 布局写入磁盘后触发，供编辑器弹出保存通知。
        /// </summary>
        public event Action? LayoutWritten;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

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
            LayerVisible.BindValueChanged(_ => applyVisibility(), true);
        }

        /// <summary>
        /// Binds the editor HUD to a scene and the live screen that owns skin containers.
        /// </summary>
        public void Apply(EzEditorSceneType scene, OsuScreen? screen, EzLayoutLayer? ezLayout) => Apply(scene, screen, ezLayout, layer);

        /// <summary>
        /// Binds the editor HUD to a scene and selects which layout layer is actively edited.
        /// Visibility stays decoupled: it is driven solely by <see cref="LayerVisible"/>.
        /// </summary>
        public void Apply(EzEditorSceneType scene, OsuScreen? screen, EzLayoutLayer? ezLayout, EzHUDLayer activeLayer)
        {
            if (!ReferenceEquals(liveScreen, screen))
                resetSkinLayerTargetAlphas(liveScreen);

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

            applyVisibility();
        }

        private void applyVisibility()
        {
            updateLayoutLayerVisibility();
            refreshBlueprints();
            applyHudLayerVisibility();
        }

        public void DetachLiveTargets() => Apply(scene, null, null, layer);

        private void onLayoutTargetsChanged()
        {
            if (!IsLoaded)
                return;

            // 容器往往在第一次套用显隐之后才出现。下拉值没有再变，必须在这里按当前开关重刷。
            Scheduler.AddOnce(refreshAfterLayoutTargetsChanged);
        }

        private void refreshAfterLayoutTargetsChanged()
        {
            refreshBlueprints();
            applyHudLayerVisibility();
        }

        public void SetRuleset(RulesetInfo? newRuleset)
        {
            if (CurrentRuleset?.Equals(newRuleset) == true)
                return;

            CurrentRuleset = newRuleset;
            refreshBlueprints();
        }

        public void SetVisible(bool visible) => LayerVisible.Value = visible;

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
            ChangeHandler?.Dispose();
            ChangeHandler = null;

            SkinnableContainer? target = ActiveTarget;

            if (target == null)
                return;

            var handler = new SkinEditorChangeHandler(target);
            handler.CanUndo.BindValueChanged(v =>
            {
                raiseStateChange();

                if (v.NewValue)
                    persistActiveTarget();
            });
            handler.CanRedo.BindValueChanged(_ => raiseStateChange());
            ChangeHandler = handler;
        }

        /// <summary>
        /// Persists every Ez layout container and flushes pending skin layout writes. Used on apply/save.
        /// </summary>
        /// <returns>是否把皮肤 HUD 布局写入了磁盘。</returns>
        public bool PersistAllLayouts()
        {
            if (layoutLayer != null)
            {
                foreach (var target in layoutLayer.LayoutContainers)
                    layoutLayer.Store.Save(target);
            }

            return FlushSkinLayoutSave();
        }

        /// <summary>
        /// Writes skin HUD layout JSON when skin layers were edited this session.
        /// </summary>
        /// <returns>是否写入了磁盘。</returns>
        public bool FlushSkinLayoutSave()
        {
            if (!HasPendingSkinLayoutSave)
                return false;

            return FlushPendingForSkin(skins.CurrentSkin.Value);
        }

        /// <summary>
        /// Writes pending layout JSON for <paramref name="skin"/> when it matches the dirty session skin.
        /// </summary>
        public bool FlushPendingForSkin(Skin skin)
        {
            if (!HasPendingSkinLayoutSave)
                return false;

            if (dirtySkinId != Guid.Empty && skin.SkinInfo.Value.ID != dirtySkinId)
                return false;

            skins.Save(skin);
            HasPendingSkinLayoutSave = false;
            dirtySkinId = Guid.Empty;
            LayoutWritten?.Invoke();
            return true;
        }

        public void DiscardPendingSkinLayoutSave()
        {
            HasPendingSkinLayoutSave = false;
            dirtySkinId = Guid.Empty;
        }

        /// <summary>
        /// Containers belonging to the current scene and active edit layer.
        /// </summary>
        public IEnumerable<SkinnableContainer> EnumerateTargets()
        {
            GlobalSkinnableContainerLookup? wanted = EzSkinEditorLayerOptions.ResolveLookup(scene, layer, CurrentRuleset);

            if (wanted == null)
                yield break;

            if (EzSkinEditorLayerOptions.UsesEzLayoutStore(layer))
            {
                if (layoutLayer == null)
                    yield break;

                foreach (var container in layoutLayer.LayoutContainers.Where(c => c.Lookup.Equals(wanted)))
                    yield return container;
            }
            else
            {
                if (liveScreen == null || !liveScreen.IsLoaded)
                    yield break;

                foreach (var container in liveScreen.ChildrenOfType<SkinnableContainer>()
                                                    .Where(c => c is not EzLayoutContainer && c.Lookup.Equals(wanted)))
                    yield return container;
            }
        }

        public void PersistLayoutToCurrentSkin()
        {
            foreach (var target in EnumerateTargets())
                persistTarget(target);
        }

        /// <summary>
        /// Whether there is anything in the Ez clipboard to paste.
        /// </summary>
        public bool CanPaste => !string.IsNullOrEmpty(Clipboard.Content.Value);

        /// <summary>
        /// Copies the current selection into the clipboard.
        /// </summary>
        public void CopySelection()
        {
            var serialised = selectedComponents.OfType<Drawable>().Select(s => s.CreateSerialisedInfo()).ToArray();
            Clipboard.Content.Value = JsonConvert.SerializeObject(serialised);
        }

        public void CutSelection()
        {
            if (selectedComponents.Count == 0)
                return;

            CopySelection();

            ChangeHandler?.BeginChange();

            foreach (var item in selectedComponents.ToArray())
                ActiveTarget?.Remove(item, true);

            ChangeHandler?.EndChange();

            selectedComponents.Clear();
            persistActiveTarget();
        }

        /// <summary>
        /// Places the clipboard contents into the active target, replacing the current selection.
        /// </summary>
        public void PasteClipboard()
        {
            if (!CanPaste)
                return;

            var drawableInfo = JsonConvert.DeserializeObject<SerialisedDrawableInfo[]>(Clipboard.Content.Value);

            if (drawableInfo == null)
                return;

            ChangeHandler?.BeginChange();

            var instances = drawableInfo.Select(d => d.CreateInstance()).OfType<ISerialisableDrawable>().ToArray();
            selectedComponents.Clear();

            foreach (var instance in instances)
            {
                // Pasted components carry their serialised transform, so placement must not reset it.
                if (ActiveTarget != null && EzSkinEditorContainerPlacement.TryPlace(ActiveTarget, instance, false))
                    selectedComponents.Add(instance);
            }

            ChangeHandler?.EndChange();
            persistActiveTarget();
        }

        /// <summary>
        /// Copies the current selection then pastes it back, leaving the copies selected.
        /// </summary>
        public void CloneSelection()
        {
            if (selectedComponents.Count == 0)
                return;

            CopySelection();
            PasteClipboard();
        }

        private void persistActiveTarget()
        {
            var target = ActiveTarget;

            if (target != null)
                persistTarget(target);
        }

        private void persistTarget(SkinnableContainer target)
        {
            if (target is EzLayoutContainer ezLayout)
            {
                layoutLayer?.Store.Save(ezLayout);
                LayoutWritten?.Invoke();
            }
            else
            {
                persistSkinTarget(target);
            }
        }

        private void persistSkinTarget(SkinnableContainer target)
        {
            skins.EnsureMutableSkin();
            skins.CurrentSkin.Value.UpdateDrawableTarget(target);
            HasPendingSkinLayoutSave = true;
            dirtySkinId = skins.CurrentSkinInfo.Value.ID;
        }

        /// <summary>
        /// The container currently being edited (the active layer's only target), or null when the
        /// layer has no editable container. Used as the dependency source for library previews.
        /// </summary>
        public SkinnableContainer? ActiveTarget => EnumerateTargets().FirstOrDefault();

        /// <summary>
        /// The ruleset active in the editor; drives which library (global / mode) can be edited.
        /// </summary>
        public RulesetInfo? CurrentRuleset { get; private set; }

        /// <summary>
        /// Instantiates and places a component into the active layer's container, persisting the
        /// layout and selecting the new component. Returns false when there is no editable target
        /// or the container rejects the component.
        /// </summary>
        public bool TryPlaceComponent(Type type)
        {
            if (Activator.CreateInstance(type) is not ISerialisableDrawable component)
                return false;

            return TryPlaceComponent(component);
        }

        /// <summary>
        /// Places an existing component into the active layer's container, persisting the layout and
        /// selecting it. Returns false when there is no editable target or the container rejects it.
        /// </summary>
        /// <param name="component">The component to place.</param>
        /// <param name="applyDefaults">
        /// Whether to apply the default anchor / origin / position. Imported, pasted and cloned
        /// components carry their own transform, so placement must not overwrite it.
        /// </param>
        public bool TryPlaceComponent(ISerialisableDrawable component, bool applyDefaults = true)
        {
            SkinnableContainer? target = ActiveTarget;

            if (target == null)
                return false;

            if (!EzSkinEditorContainerPlacement.TryPlace(target, component, applyDefaults))
                return false;

            selectedComponents.Clear();
            selectedComponents.Add(component);

            persistTarget(target);
            return true;
        }

        private void updateLayoutLayerVisibility()
        {
            // 全开/全关对标官方编辑器：只有「隐藏 HUD」层才整体隐藏，切层不影响其它层显隐。
            layoutLayer?.EditorHidden = !LayerVisible.Value;
        }

        private void applyHudLayerVisibility()
        {
            float alpha = LayerVisible.Value ? 1 : 0;
            fadeSkinLayerTargets(liveScreen, alpha);

            if (Math.Abs(blueprintHost.Alpha - alpha) > 0.001f)
                blueprintHost.FadeTo(alpha, hud_fade_duration, Easing.OutQuint);
        }

        private static void fadeSkinLayerTargets(OsuScreen? screen, float alpha)
        {
            if (screen == null || !screen.IsLoaded)
                return;

            foreach (var container in screen.ChildrenOfType<SkinnableContainer>().Where(c => c is not EzLayoutContainer))
            {
                if (Math.Abs(container.Alpha - alpha) > 0.001f)
                    container.FadeTo(alpha, hud_fade_duration, Easing.OutQuint);
            }
        }

        private static void resetSkinLayerTargetAlphas(OsuScreen? screen)
        {
            if (screen == null || !screen.IsLoaded)
                return;

            foreach (var container in screen.ChildrenOfType<SkinnableContainer>().Where(c => c is not EzLayoutContainer))
                container.Alpha = 1;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            ChangeHandler?.Dispose();
            ChangeHandler = null;

            if (layoutLayer != null)
            {
                layoutLayer.LayoutTargetsChanged -= onLayoutTargetsChanged;
                layoutLayer.EditorHidden = false;
            }
        }
    }
}
