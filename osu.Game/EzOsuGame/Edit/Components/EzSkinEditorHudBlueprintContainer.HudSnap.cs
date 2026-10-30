// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.Extensions;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.SkinEditor;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Skinning;
using osuTK;
using osuTK.Input;

namespace osu.Game.EzOsuGame.Edit.Components
{
    //TODO: 以后应改做出通用吸附插件。
    public partial class EzSkinEditorHudBlueprintContainer
    {
        private Bindable<bool> hudSnapEnabled = null!;
        private Bindable<float> hudSnapDistance = null!;
        private Bindable<bool> hudSnapPixelFlush = null!;

        private SkinHudSnapDragState snapDragState = new SkinHudSnapDragState();

        [BackgroundDependencyLoader]
        private void loadHudSnap(Ez2ConfigManager config)
        {
            hudSnapEnabled = config.GetBindable<bool>(Ez2Setting.SkinEditorHudSnapEnabled);
            hudSnapDistance = config.GetBindable<float>(Ez2Setting.SkinEditorHudSnapDistance);
            hudSnapPixelFlush = config.GetBindable<bool>(Ez2Setting.SkinEditorHudSnapPixelFlush);

            hudSnapDistance.BindValueChanged(v =>
            {
                float preset = SkinHudSnapSettings.SnapToPreset(v.NewValue);
                if (Math.Abs(preset - v.NewValue) > float.Epsilon)
                    hudSnapDistance.Value = preset;
            }, true);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            snapDragState.Reset();
            SkinHudSnapRuntime.DeferClosestAnchorDuringDrag = shouldApplyHudSnap();
            return base.OnDragStart(e);
        }

        protected override void DragOperationCompleted()
        {
            SkinHudSnapRuntime.DeferClosestAnchorDuringDrag = false;
            snapDragState.Reset();

            if (shouldApplyHudSnap())
            {
                foreach (var blueprint in SelectionHandler.SelectedBlueprints)
                {
                    var item = blueprint.Item;

                    if (!item.UsesFixedAnchor)
                        SkinSelectionHandler.ApplyClosestAnchorOrigin((Drawable)item);
                }
            }

            base.DragOperationCompleted();
            onLayoutMutated?.Invoke();
        }

        protected override bool TryMoveBlueprints(DragEvent e, IList<(SelectionBlueprint<ISerialisableDrawable> blueprint, Vector2[] originalSnapPositions)> blueprints)
        {
            Vector2 distanceTravelled = e.ScreenSpaceMousePosition - e.ScreenSpaceMouseDownPosition;

            var referenceBlueprint = blueprints.First().blueprint;
            Vector2 movePosition = blueprints.First().originalSnapPositions.First() + distanceTravelled;
            Vector2 screenDelta = movePosition - referenceBlueprint.ScreenSpaceSelectionPoint;

            if (!shouldApplyHudSnap())
            {
                // 最小拖移：无 HUD 吸附时仅按指针位移移动选中项。
                return SelectionHandler.HandleMovement(new MoveSelectionEvent<ISerialisableDrawable>(referenceBlueprint, screenDelta));
            }

            var referenceDrawable = (Drawable)referenceBlueprint.Item;
            Vector2 parentDelta = referenceDrawable.ScreenSpaceDeltaToParentSpace(screenDelta);

            parentDelta = SkinHudSnapHelper.ApplySnap(
                targetContainer,
                SelectedItems.Cast<Drawable>().ToList(),
                parentDelta,
                hudSnapDistance.Value,
                hudSnapPixelFlush.Value,
                ref snapDragState);

            Vector2 adjustedScreenDelta = parentDeltaToScreenDelta(referenceDrawable, parentDelta);

            return SelectionHandler.HandleMovement(new MoveSelectionEvent<ISerialisableDrawable>(referenceBlueprint, adjustedScreenDelta));
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (!shouldApplyHudSnap())
                return base.OnKeyDown(e);

            Vector2? screenDelta = e.Key switch
            {
                Key.Left => new Vector2(-1, 0),
                Key.Right => new Vector2(1, 0),
                Key.Up => new Vector2(0, -1),
                Key.Down => new Vector2(0, 1),
                _ => null,
            };

            if (screenDelta == null)
                return base.OnKeyDown(e);

            var firstBlueprint = SelectionHandler.SelectedBlueprints.FirstOrDefault();

            if (firstBlueprint == null)
                return base.OnKeyDown(e);

            var drawable = (Drawable)firstBlueprint.Item;
            Vector2 parentDelta = drawable.ScreenSpaceDeltaToParentSpace(screenDelta.Value);
            parentDelta = SkinHudSnapHelper.ApplySnap(
                targetContainer,
                SelectedItems.Cast<Drawable>().ToList(),
                parentDelta,
                hudSnapDistance.Value,
                hudSnapPixelFlush.Value,
                ref snapDragState);

            SelectionHandler.HandleMovement(new MoveSelectionEvent<ISerialisableDrawable>(
                firstBlueprint,
                parentDeltaToScreenDelta(drawable, parentDelta)));

            return true;
        }

        private bool shouldApplyHudSnap() =>
            hudSnapEnabled.Value && SkinHudSnapSettings.CanSnap(targetContainer.Lookup);

        private static Vector2 parentDeltaToScreenDelta(Drawable drawable, Vector2 parentDelta)
        {
            var parent = drawable.Parent!;
            return parent.ToScreenSpace(parentDelta) - parent.ToScreenSpace(Vector2.Zero);
        }
    }
}
