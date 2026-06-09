// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorSkinSpecificSettingsSection : FillFlowContainer
    {
        private readonly ISkin editorSkin;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        public EzSkinEditorSkinSpecificSettingsSection(ISkin editorSkin)
        {
            this.editorSkin = editorSkin;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(8);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (!isSupportedSkin(editorSkin))
            {
                Alpha = 0;
                return;
            }

            Children = new Drawable[]
            {
                new SettingsCheckbox
                {
                    LabelText = EzSkinStrings.MANIA_LN_GRADIENT_ENABLE,
                    TooltipText = EzSkinStrings.MANIA_LN_GRADIENT_ENABLE_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<bool>(Ez2Setting.ManiaLNGradientEnable),
                },
                new SettingsSlider<double>
                {
                    LabelText = EzSkinStrings.LN_GRADIENT_TAIL_HEIGHT,
                    TooltipText = EzSkinStrings.LN_GRADIENT_TAIL_HEIGHT_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailMaskGradientHeight),
                    KeyboardStep = 1.0f,
                },
                new SettingsSlider<double>
                {
                    LabelText = EzSkinStrings.LN_TAIL_ALPHA,
                    TooltipText = EzSkinStrings.LN_TAIL_ALPHA_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailAlpha),
                    KeyboardStep = 0.1f,
                },
                new SettingsSlider<double>
                {
                    LabelText = EzSkinStrings.NOTE_TRACK_LINE,
                    TooltipText = EzSkinStrings.NOTE_TRACK_LINE_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.NoteTrackLineHeight),
                },
            };
        }

        private static bool isSupportedSkin(ISkin skin) =>
            skin is EzStyleProSkin or Ez2Skin or SbISkin;
    }
}
