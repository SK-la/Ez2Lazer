// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Shared evidence list row (UX-2 axis plays; UX-4 dan clears).
    /// </summary>
    public partial class EzEvidenceScoreRow : OsuClickableContainer
    {
        private readonly string title;
        private readonly string meta;

        public EzEvidenceScoreRow(string title, string meta, Action? action = null)
        {
            this.title = title;
            this.meta = meta;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Action = action;
            Masking = true;
            CornerRadius = 6;
            Alpha = action != null ? 0.9f : 0.75f;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Background5,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Horizontal = 10, Vertical = 8 },
                    Spacing = new Vector2(0, 2),
                    Children = new Drawable[]
                    {
                        new TruncatingSpriteText
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = title,
                            Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                        },
                        new TruncatingSpriteText
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = meta,
                            Font = OsuFont.GetFont(size: 11),
                            Colour = colours.Content2,
                        },
                    }
                }
            };
        }

        public static string FormatAxisMeta(double axisValue, double accuracy, double rate, DateTimeOffset scoredAt)
        {
            string rateText = Math.Abs(rate - 1) < 0.001
                ? "1.0x"
                : rate.ToString("0.##x", CultureInfo.InvariantCulture);

            return string.Join(" · ", new[]
            {
                axisValue.ToString("0.00", CultureInfo.InvariantCulture),
                accuracy.ToString("0.00%", CultureInfo.InvariantCulture),
                rateText,
                scoredAt.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            });
        }
    }
}
