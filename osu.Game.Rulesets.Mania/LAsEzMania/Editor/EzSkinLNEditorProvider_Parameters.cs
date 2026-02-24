// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.LAsEzExtensions.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.LAsEzMania.Editor
{
    public partial class EzSkinLNEditorProvider
    {
        private Drawable createParametersPartImpl(ISkin skin)
            => new ParametersPanel();

        private partial class ParametersPanel : FillFlowContainer
        {
            [Resolved]
            private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

            public ParametersPanel()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Vertical;
                Spacing = new Vector2(8);
                Padding = new MarginPadding(10);
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = "Mania Skin Parameters",
                        Colour = Colour4.White,
                        Font = OsuFont.Default.With(size: 18),
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                    },
                    new SettingsSlider<double>
                    {
                        LabelText = EzSkinStrings.LN_TAIL_MASK_GRADIENT_HEIGHT,
                        TooltipText = EzSkinStrings.LN_TAIL_MASK_GRADIENT_HEIGHT_TOOLTIP,
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
        }
    }
}
