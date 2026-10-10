// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorStageSettingsSection : FillFlowContainer
    {
        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

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
            var panelItems = new List<string> { string.Empty };
            panelItems.AddRange(EzResourceDiscovery.ListPanelImages(storage));

            string currentPanel = ezSkinConfig.Get<string>(Ez2Setting.StageBackground);

            if (!string.IsNullOrEmpty(currentPanel) && !panelItems.Contains(currentPanel))
                panelItems.Insert(1, currentPanel);

            var columnItems = EzResourceDiscovery.ListColumnSets(storage).ToList();

            if (!columnItems.Contains("EzColumnLight"))
                columnItems.Insert(0, "EzColumnLight");

            string currentColumn = ezSkinConfig.Get<string>(Ez2Setting.ColumnLightName);

            if (!string.IsNullOrEmpty(currentColumn) && !columnItems.Contains(currentColumn))
                columnItems.Insert(0, currentColumn);

            Children = new Drawable[]
            {
                new EzOptionalStringDropdown
                {
                    LabelText = EzColumnStrings.STAGE_BACKGROUND,
                    TooltipText = EzColumnStrings.STAGE_BACKGROUND_TOOLTIP,
                    EmptyLabel = EzColumnStrings.STAGE_BACKGROUND_NONE,
                    Current = ezSkinConfig.GetBindable<string>(Ez2Setting.StageBackground),
                    Items = panelItems,
                },
                new SettingsDropdown<string>
                {
                    LabelText = EzColumnStrings.COLUMN_SET,
                    TooltipText = EzColumnStrings.COLUMN_SET_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<string>(Ez2Setting.ColumnLightName),
                    Items = columnItems,
                },
                new SettingsSlider<double>
                {
                    LabelText = EzColumnStrings.STAGE_DIAGONAL_LANE_ANGLE,
                    TooltipText = EzColumnStrings.STAGE_DIAGONAL_LANE_ANGLE_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ManiaPseudo3DRotation),
                    KeyboardStep = 1f,
                    DisplayAsPercentage = false,
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
                new SettingsCheckbox
                {
                    LabelText = EzColumnStrings.STAGE_PANEL,
                    TooltipText = EzColumnStrings.STAGE_PANEL_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<bool>(Ez2Setting.StagePanelEnabled),
                },
            };
        }
    }
}
