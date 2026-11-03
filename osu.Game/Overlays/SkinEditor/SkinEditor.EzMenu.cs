// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.UserInterface;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.SkinEditor;
using osu.Game.Graphics.UserInterface;
using osu.Game.Skinning;

namespace osu.Game.Overlays.SkinEditor
{
    public partial class SkinEditor
    {
        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        private Bindable<bool> hudSnapEnabled = null!;
        private Bindable<float> hudSnapDistance = null!;
        private Bindable<bool> hudSnapPixelFlush = null!;
        private SkinHudSnapDistanceMenuItem? hudSnapDistanceMenuItem;
        private ToggleMenuItem? hudSnapPixelFlushMenuItem;

        private MenuItem createEzSettingsMenu()
        {
            hudSnapEnabled = ezConfig.GetBindable<bool>(Ez2Setting.SkinEditorHudSnapEnabled);
            hudSnapDistance = ezConfig.GetBindable<float>(Ez2Setting.SkinEditorHudSnapDistance);
            hudSnapPixelFlush = ezConfig.GetBindable<bool>(Ez2Setting.SkinEditorHudSnapPixelFlush);
            hudSnapDistanceMenuItem = new SkinHudSnapDistanceMenuItem(hudSnapDistance);
            hudSnapPixelFlushMenuItem = new ToggleMenuItem(EzEditorStrings.MENU_HUD_SNAP_PIXEL_FLUSH)
            {
                State = { BindTarget = hudSnapPixelFlush },
            };

            hudSnapEnabled.BindValueChanged(enabled =>
            {
                bool on = enabled.NewValue;

                if (hudSnapDistanceMenuItem != null)
                    hudSnapDistanceMenuItem.Action.Disabled = !on;

                if (hudSnapPixelFlushMenuItem != null)
                    hudSnapPixelFlushMenuItem.Action.Disabled = !on;
            }, true);

            return new MenuItem(EzEditorStrings.MENU_EZ_SETTINGS)
            {
                Items = new MenuItem[]
                {
                    new ToggleMenuItem(EzEditorStrings.MENU_HUD_SNAP)
                    {
                        State = { BindTarget = hudSnapEnabled },
                    },
                    hudSnapDistanceMenuItem,
                    hudSnapPixelFlushMenuItem,
                },
            };
        }

        internal GlobalSkinnableContainerLookup? CurrentTarget => SelectedTarget.Value;
    }
}
