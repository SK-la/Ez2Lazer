// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Ez 皮肤编辑器 HUD 蓝图层；选用外部选中列表，不依赖官方 SkinEditor。
    /// </summary>
    public partial class EzSkinEditorHudBlueprintContainer : BlueprintContainer<ISerialisableDrawable>
    {
        private readonly SkinnableContainer targetContainer;
        private readonly SelectionSource selection;
        private readonly Action? onLayoutMutated;
        private readonly List<BindableList<ISerialisableDrawable>> targetComponents = new List<BindableList<ISerialisableDrawable>>();

        public EzSkinEditorHudBlueprintContainer(
            SkinnableContainer targetContainer,
            BindableList<ISerialisableDrawable> selection,
            Action? onLayoutMutated = null)
        {
            this.targetContainer = targetContainer;
            this.selection = new SelectionSource(selection);
            this.onLayoutMutated = onLayoutMutated;
        }

        /// <summary>
        /// 共享选中列表的持有者。该列表不能直接存成 <see cref="IUnbindable"/> 字段：框架的
        /// Drawable.Dispose 会用反射对所有这类字段调用 <c>UnbindAll()</c>，
        /// 那会把列表的 <c>CollectionChanged</c> 清空。蓝图容器在每次
        /// <c>EzSkinEditorHudLayer.refreshBlueprints</c> 时都会重建，于是侧栏对同一列表的订阅
        /// 会被反复清除，表现为「蓝图能选中、侧栏永远读不到选中项」。
        /// </summary>
        private sealed class SelectionSource
        {
            public readonly BindableList<ISerialisableDrawable> Value;

            public SelectionSource(BindableList<ISerialisableDrawable> value)
            {
                Value = value;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            SelectedItems.BindTo(selection.Value);

            var bindableList = new BindableList<ISerialisableDrawable> { BindTarget = targetContainer.Components };
            bindableList.BindCollectionChanged(componentsChanged, true);
            targetComponents.Add(bindableList);
        }

        private void componentsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Schedule(() =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    Debug.Assert(e.NewItems != null);

                    foreach (var item in e.NewItems.Cast<ISerialisableDrawable>())
                        AddBlueprintFor(item);
                    break;

                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Reset:
                    Debug.Assert(e.OldItems != null);

                    foreach (var item in e.OldItems.Cast<ISerialisableDrawable>())
                        RemoveBlueprintFor(item);
                    break;

                case NotifyCollectionChangedAction.Replace:
                    Debug.Assert(e.NewItems != null);
                    Debug.Assert(e.OldItems != null);

                    foreach (var item in e.OldItems.Cast<ISerialisableDrawable>())
                        RemoveBlueprintFor(item);

                    foreach (var item in e.NewItems.Cast<ISerialisableDrawable>())
                        AddBlueprintFor(item);
                    break;
            }
        });

        protected override void AddBlueprintFor(ISerialisableDrawable item)
        {
            if (!item.IsEditable)
                return;

            base.AddBlueprintFor(item);
        }

        protected override void SelectAll()
        {
            SelectedItems.AddRange(targetComponents.SelectMany(list => list).Except(SelectedItems).ToArray());
        }

        protected override SelectionHandler<ISerialisableDrawable> CreateSelectionHandler() => new EzSkinEditorHudSelectionHandler
        {
            LayerTarget = targetContainer,
            DeleteTarget = component =>
            {
                targetContainer.Remove(component, true);
                onLayoutMutated?.Invoke();
            },
        };

        protected override SelectionBlueprint<ISerialisableDrawable> CreateBlueprintFor(ISerialisableDrawable component)
            => new SkinBlueprint(component);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            foreach (var list in targetComponents)
                list.UnbindAll();
        }
    }

    /// <summary>
    /// Ez 编辑层的选中处理器。复用官方 <see cref="SkinSelectionHandler"/> 的手柄（旋转/缩放/翻转）、
    /// 锚点与原点菜单和变换逻辑，只把选中源、层级调整与删除接到 Ez 自己的容器上。
    /// </summary>
    public partial class EzSkinEditorHudSelectionHandler : SkinSelectionHandler
    {
        /// <summary>
        /// The container being edited, used for layering operations. The official editor reaches this
        /// through its own target screen, which is not available here.
        /// </summary>
        public ISerialisableDrawableContainer? LayerTarget { get; set; }

        /// <summary>
        /// Removes a single item from the edited container. Owner wires this to the container so the
        /// handler does not need to know which layer is active; the owner also persists the layout.
        /// </summary>
        public Action<ISerialisableDrawable>? DeleteTarget { get; set; }

        /// <summary>
        /// The blueprint binds its shared selection onto <see cref="SelectionHandler{T}.SelectedItems"/>
        /// before this handler loads, so the transform handlers can follow it directly.
        /// </summary>
        protected override BindableList<ISerialisableDrawable>? TransformSelectionSource => SelectedItems;

        protected override void DeleteItems(IEnumerable<ISerialisableDrawable> items)
        {
            var targets = items.ToArray();

            ChangeHandler?.BeginChange();

            foreach (var item in targets)
                DeleteTarget?.Invoke(item);

            ChangeHandler?.EndChange();
        }

        protected override void BringSelectionToFront()
        {
            if (LayerTarget == null)
                return;

            ChangeHandler?.BeginChange();

            // Iterating by target order keeps the relative order of the selection intact.
            foreach (var d in LayerTarget.Components.ToArray())
            {
                if (!SelectedItems.Contains(d))
                    continue;

                LayerTarget.Remove(d, false);

                // Removing resets the selection, so add it back.
                SelectedItems.Add(d);
                LayerTarget.Add(d);
            }

            ChangeHandler?.EndChange();
        }

        protected override void SendSelectionToBack()
        {
            if (LayerTarget == null)
                return;

            ChangeHandler?.BeginChange();

            foreach (var d in LayerTarget.Components.ToArray())
            {
                if (SelectedItems.Contains(d))
                    continue;

                LayerTarget.Remove(d, false);
                LayerTarget.Add(d);
            }

            ChangeHandler?.EndChange();
        }
    }
}
