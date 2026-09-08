// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// Rounded skill-name chip with accent border (mania-hub PatternPicker style).
    /// </summary>
    public partial class EzDisplaySkillName : CompositeDrawable
    {
        private readonly Container chrome;
        private readonly Box background;
        private readonly OsuSpriteText label;

        public EzDisplaySkillName()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = chrome = new Container
            {
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 4,
                BorderThickness = 1.5f,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.Black.Opacity(0.35f),
                    },
                    label = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Margin = new MarginPadding { Horizontal = 6, Vertical = 2 },
                        Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                    },
                }
            };
        }

        public void SetFromAxis(EzMinaSkillAxis axis, LocalisableString? overrideText = null)
            => apply(axis.Chip(), overrideText);

        public void SetFromAxisId(string? axisId, LocalisableString? overrideText = null)
        {
            if (EzMinaSkillAxisExtensions.TryParse(axisId, out var axis))
                SetFromAxis(axis, overrideText);
            else
                apply(new EzSkillChip(overrideText ?? axisId ?? string.Empty, EzSkillChip.Fallback.AccentHex), null);
        }

        private void apply(EzSkillChip chip, LocalisableString? overrideText)
        {
            var accent = Colour4.FromHex(chip.AccentHex);
            label.Text = overrideText ?? chip.Name;
            label.Colour = accent;
            chrome.BorderColour = accent.Opacity(0.45f);
            background.Colour = accent.Opacity(0.12f);
        }
    }
}
