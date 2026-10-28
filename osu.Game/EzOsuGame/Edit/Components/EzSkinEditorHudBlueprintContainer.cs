// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.Extensions;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Ez 皮肤编辑器 HUD 蓝图层；选用外部选中列表，不依赖官方 SkinEditor。
    /// </summary>
    public partial class EzSkinEditorHudBlueprintContainer : BlueprintContainer<ISerialisableDrawable>
    {
        private readonly ISerialisableDrawableContainer targetContainer;
        private readonly BindableList<ISerialisableDrawable> selection;
        private readonly Action? onLayoutMutated;
        private readonly List<BindableList<ISerialisableDrawable>> targetComponents = new List<BindableList<ISerialisableDrawable>>();

        public EzSkinEditorHudBlueprintContainer(
            ISerialisableDrawableContainer targetContainer,
            BindableList<ISerialisableDrawable> selection,
            Action? onLayoutMutated = null)
        {
            this.targetContainer = targetContainer;
            this.selection = selection;
            this.onLayoutMutated = onLayoutMutated;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            SelectedItems.BindTo(selection);

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

        protected override SelectionHandler<ISerialisableDrawable> CreateSelectionHandler() => new EzSkinEditorHudSelectionHandler();

        protected override SelectionBlueprint<ISerialisableDrawable> CreateBlueprintFor(ISerialisableDrawable component)
            => new SkinBlueprint(component);

        // 最小拖移：无 HUD 吸附，仅按指针位移移动选中项。
        protected override bool TryMoveBlueprints(DragEvent e, IList<(SelectionBlueprint<ISerialisableDrawable> blueprint, Vector2[] originalSnapPositions)> blueprints)
        {
            Vector2 distanceTravelled = e.ScreenSpaceMousePosition - e.ScreenSpaceMouseDownPosition;
            var referenceBlueprint = blueprints.First().blueprint;
            Vector2 movePosition = blueprints.First().originalSnapPositions.First() + distanceTravelled;
            Vector2 screenDelta = movePosition - referenceBlueprint.ScreenSpaceSelectionPoint;
            return SelectionHandler.HandleMovement(new MoveSelectionEvent<ISerialisableDrawable>(referenceBlueprint, screenDelta));
        }

        protected override void DragOperationCompleted()
        {
            base.DragOperationCompleted();
            onLayoutMutated?.Invoke();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            foreach (var list in targetComponents)
                list.UnbindAll();
        }
    }

    /// <summary>
    /// 仅移动与选中；删除 / 层级 / 完整菜单留待后续接入。
    /// </summary>
    public partial class EzSkinEditorHudSelectionHandler : SelectionHandler<ISerialisableDrawable>
    {
        public override bool HandleMovement(MoveSelectionEvent<ISerialisableDrawable> moveEvent)
        {
            foreach (var c in SelectedBlueprints)
            {
                var item = c.Item;
                var drawable = (Drawable)item;

                if (!item.UsesFixedAnchor)
                    SkinSelectionHandler.ApplyClosestAnchorOrigin(drawable);

                drawable.Position += drawable.ScreenSpaceDeltaToParentSpace(moveEvent.ScreenSpaceDelta);
            }

            return true;
        }

        protected override void OnSelectionChanged()
        {
            base.OnSelectionChanged();

            SelectionBox.CanFlipX = true;
            SelectionBox.CanFlipY = true;
            SelectionBox.CanReverse = false;
        }

        protected override void DeleteItems(IEnumerable<ISerialisableDrawable> items)
        {
            // 删除接入前先空实现。
        }
    }
}
