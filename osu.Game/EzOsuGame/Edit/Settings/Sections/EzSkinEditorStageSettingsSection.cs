// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Layout;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Screens;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorStageSettingsSection : FillFlowContainer
    {
        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private ManiaPlayfieldLayoutConfig playfieldLayout { get; set; } = null!;

        public EzSkinEditorStageSettingsSection()
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
                new SettingsCheckbox
                {
                    LabelText = EzColumnStrings.STAGE_PANEL,
                    TooltipText = EzColumnStrings.STAGE_PANEL_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<bool>(Ez2Setting.StagePanelEnabled),
                },
                new EzSettingsColour
                {
                    LabelText = EzColumnStrings.STAGE_BACKGROUND_COLOUR,
                    TooltipText = EzColumnStrings.STAGE_BACKGROUND_COLOUR_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<Colour4>(Ez2Setting.StageBackgroundColour),
                },
                new SettingsSlider<double>
                {
                    LabelText = EzColumnStrings.COLUMN_BACKGROUND_DIM,
                    TooltipText = EzColumnStrings.COLUMN_BACKGROUND_DIM_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnDim),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                },
                new SettingsSlider<double>
                {
                    LabelText = EzColumnStrings.COLUMN_BACKGROUND_BLUR,
                    TooltipText = EzColumnStrings.COLUMN_BACKGROUND_BLUR_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnBlur),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                },
                new SettingsSlider<float>
                {
                    LabelText = EzColumnStrings.STAGE_POSITION_X,
                    TooltipText = EzColumnStrings.STAGE_POSITION_X_TOOLTIP,
                    Current = playfieldLayout.StageX,
                    KeyboardStep = 1f,
                    DisplayAsPercentage = true,
                },
                new SettingsSlider<float>
                {
                    LabelText = EzColumnStrings.STAGE_POSITION_Y,
                    TooltipText = EzColumnStrings.STAGE_POSITION_Y_TOOLTIP,
                    Current = playfieldLayout.StageY,
                    KeyboardStep = 1f,
                    DisplayAsPercentage = true,
                },
                new SettingsSlider<double>
                {
                    LabelText = EzColumnStrings.STAGE_DIAGONAL_LANE_ANGLE,
                    TooltipText = EzColumnStrings.STAGE_DIAGONAL_LANE_ANGLE_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ManiaPseudo3DRotation),
                    KeyboardStep = 1f,
                    DisplayAsPercentage = false,
                },
            };
        }
    }
}
