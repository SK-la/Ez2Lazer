// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics.UserInterface;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.EzOsuGame.SkinEditor
{
    internal partial class SkinHudSnapDistanceMenuItem : MenuItem
    {
        private readonly Bindable<float> snapDistance;
        private readonly Dictionary<float, TernaryStateRadioMenuItem> menuItemLookup = new Dictionary<float, TernaryStateRadioMenuItem>();

        public SkinHudSnapDistanceMenuItem(Bindable<float> snapDistance)
            : base(EzEditorStrings.MENU_HUD_SNAP_DISTANCE)
        {
            this.snapDistance = snapDistance;

            var items = new TernaryStateRadioMenuItem[SkinHudSnapSettings.DISTANCE_PRESETS.Length];

            for (int i = 0; i < SkinHudSnapSettings.DISTANCE_PRESETS.Length; i++)
            {
                float preset = SkinHudSnapSettings.DISTANCE_PRESETS[i];
                items[i] = createMenuItem(preset);
            }

            Items = items;

            snapDistance.BindValueChanged(distance =>
            {
                foreach (var kvp in menuItemLookup)
                    kvp.Value.State.Value = kvp.Key == distance.NewValue ? TernaryState.True : TernaryState.False;
            }, true);
        }

        private TernaryStateRadioMenuItem createMenuItem(float distance)
        {
            var item = new TernaryStateRadioMenuItem($"{distance} dx", MenuItemType.Standard, _ => snapDistance.Value = distance);
            menuItemLookup[distance] = item;
            return item;
        }
    }
}
