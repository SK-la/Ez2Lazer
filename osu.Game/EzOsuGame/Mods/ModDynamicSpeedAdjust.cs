// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Mods
{
    public abstract partial class ModDynamicSpeedAdjust : ModRateAdjust, ILinkedDynamicSpeedHUD
    {
        [SettingSource(typeof(DynamicSpeedHUDStrings), nameof(DynamicSpeedHUDStrings.LINK_SPEED_HUD_LABEL), nameof(DynamicSpeedHUDStrings.LINK_SPEED_HUD_DESCRIPTION))]
        public BindableBool LinkSpeedHUD { get; } = new BindableBool(true);

        [SettingSource(typeof(DynamicSpeedHUDStrings), nameof(DynamicSpeedHUDStrings.SHOW_SPEED_TEXT_LABEL), nameof(DynamicSpeedHUDStrings.SHOW_SPEED_TEXT_DESCRIPTION))]
        public BindableBool ShowSpeedText { get; } = new BindableBool(true);

        [SettingSource(typeof(DynamicSpeedHUDStrings), nameof(DynamicSpeedHUDStrings.SHOW_SPEED_LINE_LABEL), nameof(DynamicSpeedHUDStrings.SHOW_SPEED_LINE_DESCRIPTION))]
        public BindableBool ShowSpeedLine { get; } = new BindableBool(true);

        public void ApplyToHUD(HUDOverlay overlay) => DynamicSpeedHUDApplicator.Apply(this, overlay);
    }
}
