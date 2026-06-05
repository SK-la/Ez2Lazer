// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.HUD;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Mods
{
    public static class DynamicSpeedHUDApplicator
    {
        public static void Apply(ILinkedDynamicSpeedHUD mod, HUDOverlay overlay)
        {
            if (!mod.LinkSpeedHUD.Value)
                return;

            overlay.Add(new EzHUDDynamicSpeedDisplay(
                mod.SpeedChange,
                mod.ShowSpeedText,
                mod.ShowSpeedLine));
        }
    }
}
