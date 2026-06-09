// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorSizeSettingsSection : FillFlowContainer
    {
        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        public EzSkinEditorSizeSettingsSection()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(8);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                new SettingsEnumDropdown<ColumnWidthStyle>
                {
                    LabelText = EzSkinStrings.COLUMN_WIDTH_STYLE,
                    TooltipText = EzSkinStrings.COLUMN_WIDTH_STYLE_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<ColumnWidthStyle>(Ez2Setting.ColumnWidthStyle),
                },
                new SettingsSlider<double>
                {
                    LabelText = EzSkinStrings.COLUMN_WIDTH,
                    TooltipText = EzSkinStrings.COLUMN_WIDTH_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnWidth),
                    KeyboardStep = 1.0f,
                },
                new SettingsSlider<double>
                {
                    LabelText = EzSkinStrings.SPECIAL_FACTOR,
                    TooltipText = EzSkinStrings.SPECIAL_FACTOR_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.SpecialFactor),
                    KeyboardStep = 0.1f,
                },
                new SettingsCheckbox
                {
                    LabelText = EzSkinStrings.GLOBAL_HIT_POSITION,
                    TooltipText = EzSkinStrings.GLOBAL_HIT_POSITION_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<bool>(Ez2Setting.HitPositionGlobalEnable),
                },
                new SettingsSlider<double>
                {
                    LabelText = EzSkinStrings.HIT_POSITION,
                    TooltipText = EzSkinStrings.HIT_POSITION_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitPosition),
                    KeyboardStep = 1f,
                },
                new SettingsSlider<double>
                {
                    LabelText = EzSkinStrings.HIT_TARGET_FLOAT_FIXED,
                    TooltipText = EzSkinStrings.HIT_TARGET_FLOAT_FIXED_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitTargetFloatFixed),
                    KeyboardStep = 0.1f,
                },
                new SettingsSlider<double>
                {
                    LabelText = EzSkinStrings.HIT_TARGET_ALPHA,
                    TooltipText = EzSkinStrings.HIT_TARGET_ALPHA_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitTargetAlpha),
                    KeyboardStep = 0.01f,
                },
                new SettingsSlider<double>
                {
                    LabelText = EzSkinStrings.NOTE_HEIGHT_SCALE,
                    TooltipText = EzSkinStrings.NOTE_HEIGHT_SCALE_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.NoteHeightScaleToWidth),
                    KeyboardStep = 0.1f,
                },
                new SettingsSlider<double>
                {
                    LabelText = EzSkinStrings.NOTE_CORNER_RADIUS,
                    TooltipText = EzSkinStrings.NOTE_CORNER_RADIUS_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.NoteCornerRadius),
                    KeyboardStep = 1.0f,
                },
            };
        }
    }
}
