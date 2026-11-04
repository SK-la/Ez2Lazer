// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Extensions;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorBaseColourSettingsSection : EzSkinEditorSettingsGroup
    {
        private readonly Dictionary<Ez2Setting, BindableColour4> colorBindables = new Dictionary<Ez2Setting, BindableColour4>();

        private FillFlowContainer baseColorsContainer = null!;
        private Bindable<bool> columnColorEnabled = null!;
        private Bindable<bool> divisorColorEnabled = null!;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        public EzSkinEditorBaseColourSettingsSection()
            : base(EzEditorStrings.COLUMN_COLOURING)
        {
            Spacing = new Vector2(8);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            columnColorEnabled = ezSkinConfig.GetBindable<bool>(Ez2Setting.ColorSettingsEnabled);
            divisorColorEnabled = ezSkinConfig.GetBindable<bool>(Ez2Setting.GameplayDivisorNoteColouring);

            colorBindables[Ez2Setting.ColumnTypeA] = createColorBindable(Ez2Setting.ColumnTypeA);
            colorBindables[Ez2Setting.ColumnTypeB] = createColorBindable(Ez2Setting.ColumnTypeB);
            colorBindables[Ez2Setting.ColumnTypeS] = createColorBindable(Ez2Setting.ColumnTypeS);
            colorBindables[Ez2Setting.ColumnTypeE] = createColorBindable(Ez2Setting.ColumnTypeE);
            colorBindables[Ez2Setting.ColumnTypeP] = createColorBindable(Ez2Setting.ColumnTypeP);

            Children = new Drawable[]
            {
                new SettingsCheckbox
                {
                    LabelText = EzSettingsStrings.GAMEPLAY_DIVISOR_NOTE_COLOURING,
                    TooltipText = EzSettingsStrings.GAMEPLAY_DIVISOR_NOTE_COLOURING_TOOLTIP,
                    Current = divisorColorEnabled,
                },
                new SettingsCheckbox
                {
                    LabelText = EzColumnStrings.COLUMN_COLOURING_ENABLE_BUTTON,
                    TooltipText = EzColumnStrings.COLUMN_COLOURING_ENABLE_BUTTON_TOOLTIP,
                    Current = columnColorEnabled,
                },
                baseColorsContainer = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(5),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = EzEditorStrings.BASE_COLOURS_HEADER,
                            Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 14),
                        }.WithUnderline(),
                        SettingsColourExtensions.CreateStyledSettingsColour(EzConstants.COLUMN_TYPE_A, colorBindables[Ez2Setting.ColumnTypeA]),
                        SettingsColourExtensions.CreateStyledSettingsColour(EzConstants.COLUMN_TYPE_B, colorBindables[Ez2Setting.ColumnTypeB]),
                        SettingsColourExtensions.CreateStyledSettingsColour(EzConstants.COLUMN_TYPE_S, colorBindables[Ez2Setting.ColumnTypeS]),
                        SettingsColourExtensions.CreateStyledSettingsColour(EzConstants.COLUMN_TYPE_E, colorBindables[Ez2Setting.ColumnTypeE]),
                        SettingsColourExtensions.CreateStyledSettingsColour(EzConstants.COLUMN_TYPE_P, colorBindables[Ez2Setting.ColumnTypeP]),
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            columnColorEnabled.BindValueChanged(e =>
            {
                if (e.NewValue)
                    baseColorsContainer.Show();
                else
                    baseColorsContainer.Hide();
            }, true);
        }

        private BindableColour4 createColorBindable(Ez2Setting setting)
        {
            var result = new BindableColour4();
            ezSkinConfig.BindWith(setting, result);
            return result;
        }
    }
}
