// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// Shared base for mods which replace button-based movement with direct touch or mouse positioning.
    /// </summary>
    /// <remarks>
    /// This type owns the shared settings: the mouse movement multiplier, and the emergency stop which repurposes
    /// the dash button from accelerating movement to locking it. Ruleset movement itself stays in the ruleset, and
    /// is reached through <see cref="UI.TouchMoveInputMapper{TAction}"/>.
    /// </remarks>
    public abstract class ModTouchBase : Mod
    {
        /// <summary>
        /// Whether the dash button should lock movement instead of being ignored.
        /// </summary>
        [SettingSource(typeof(TouchMoveStrings), nameof(TouchMoveStrings.INVERT_DASH_LABEL), nameof(TouchMoveStrings.INVERT_DASH_DESCRIPTION))]
        public BindableBool InvertDash { get; } = new BindableBool(true);

        /// <summary>
        /// Multiplier converting mouse travel distance into on-screen travel distance.
        /// Defaults to the midpoint of its range.
        /// </summary>
        [SettingSource(typeof(TouchMoveStrings), nameof(TouchMoveStrings.MOVE_MAPPING_LABEL), nameof(TouchMoveStrings.MOVE_MAPPING_DESCRIPTION),
            SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> MoveMapping { get; } = new BindableDouble(3)
        {
            MinValue = 1,
            MaxValue = 5,
            Precision = 0.1,
        };

        public override string Name => "Touch Move";

        public override string Acronym => "TM";

        public override LocalisableString Description => TouchMoveStrings.TOUCH_MOVE_DESCRIPTION;

        public override IconUsage? Icon => FontAwesome.Solid.HandPointer;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => false;

        public override bool ValidForMultiplayerAsFreeMod => false;

        public override bool ValidForFreestyleAsRequiredMod => false;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return (TouchMoveStrings.INVERT_DASH_LABEL, InvertDash.Value ? "On" : "Off");
                yield return (TouchMoveStrings.MOVE_MAPPING_LABEL, $"{MoveMapping.Value:0.#}x");
            }
        }
    }

    public static class TouchMoveStrings
    {
        public static readonly LocalisableString TOUCH_MOVE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "使用触屏拖动或鼠标移动直接控制位置，速度无级变化",
            "Control your position directly by dragging on a touchscreen or moving the mouse, with continuous speed.");

        public static readonly LocalisableString INVERT_DASH_LABEL = new EzLocalizationManager.EzLocalisableString("反转急停", "Invert Dash");

        public static readonly LocalisableString INVERT_DASH_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "开启后，原本的加速键变成急停键，按住时锁定移动",
            "When enabled, the dash button becomes an emergency stop and locks movement while held.");

        public static readonly LocalisableString MOVE_MAPPING_LABEL = new EzLocalizationManager.EzLocalisableString("移动映射", "Move Mapping");

        public static readonly LocalisableString MOVE_MAPPING_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "鼠标移动距离到画面移动距离的换算倍率",
            "Multiplier converting mouse travel distance into on-screen travel distance.");
    }
}
