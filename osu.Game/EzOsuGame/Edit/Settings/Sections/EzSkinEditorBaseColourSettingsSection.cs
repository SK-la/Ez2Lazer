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

            colorBindables[Ez2Setting.ColumnTypeA] = wrapColumnTypeColour(EzColumnType.A);
            colorBindables[Ez2Setting.ColumnTypeB] = wrapColumnTypeColour(EzColumnType.B);
            colorBindables[Ez2Setting.ColumnTypeS] = wrapColumnTypeColour(EzColumnType.S);
            colorBindables[Ez2Setting.ColumnTypeE] = wrapColumnTypeColour(EzColumnType.E);
            colorBindables[Ez2Setting.ColumnTypeP] = wrapColumnTypeColour(EzColumnType.P);

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
                new SettingsEnumDropdown<EzColouringMode>
                {
                    LabelText = EzSkinStrings.COLOURING_MODE,
                    TooltipText = EzSkinStrings.COLOURING_MODE_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<EzColouringMode>(Ez2Setting.ColouringMode),
                },
                new SettingsEnumDropdown<EzColouringRange>
                {
                    LabelText = EzSkinStrings.COLOURING_RANGE,
                    TooltipText = EzSkinStrings.COLOURING_RANGE_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<EzColouringRange>(Ez2Setting.ColouringRange),
                },
                new SettingsSlider<double>
                {
                    LabelText = EzSkinStrings.COLOURING_STRENGTH,
                    TooltipText = EzSkinStrings.COLOURING_STRENGTH_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ColouringStrength),
                    KeyboardStep = 1,
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

        private BindableColour4 wrapColumnTypeColour(EzColumnType type)
        {
            var result = new BindableColour4();
            result.BindTo(ezSkinConfig.GetColumnColorBindableByType(type));
            return result;
        }
    }
}
