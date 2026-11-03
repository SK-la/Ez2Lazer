// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
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
        private readonly EvidenceMeta meta;
        private readonly bool interactive;

        private EzLocalProfileHoverBox background = null!;

        public EzEvidenceScoreRow(string title, string meta, Action? action = null)
            : this(title, EvidenceMeta.FromCombined(meta), action)
        {
        }

        public EzEvidenceScoreRow(string title, EvidenceMeta meta, Action? action = null)
        {
            this.title = title;
            this.meta = meta;
            interactive = action != null;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Action = action;
            Masking = true;
            CornerRadius = 6;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            background = new EzLocalProfileHoverBox();
            background.Configure(colours);
            background.SetInteractive(interactive);

            Children = new Drawable[]
            {
                background,
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
                            Colour = EzLocalProfileColours.Plays(colours),
                        },
                        new OsuSpriteText
                        {
                            Text = meta.Value,
                            Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                            Colour = EzLocalProfileColours.Numeric(colours),
                        },
                        new TruncatingSpriteText
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = string.IsNullOrEmpty(meta.Date)
                                ? meta.Details
                                : string.IsNullOrEmpty(meta.Details)
                                    ? meta.Date
                                    : $"{meta.Details} · {meta.Date}",
                            Font = OsuFont.GetFont(size: 10),
                            Colour = EzLocalProfileColours.Meta(colours),

                            Alpha = string.IsNullOrEmpty(meta.Details) && string.IsNullOrEmpty(meta.Date) ? 0 : 0.9f,
                        },
                    }
                }
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.Refresh(true);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.Refresh(false);
            base.OnHoverLost(e);
        }

        public readonly record struct EvidenceMeta(string Value, string Details, string Date)
        {
            public static EvidenceMeta FromCombined(string combined)
            {
                string[] parts = combined.Split(" · ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    return new EvidenceMeta(combined, string.Empty, string.Empty);
                if (parts.Length == 1)
                    return new EvidenceMeta(parts[0], string.Empty, string.Empty);
                if (parts.Length == 2)
                    return new EvidenceMeta(parts[0], parts[1], string.Empty);

                return new EvidenceMeta(parts[0], string.Join(" · ", parts[1..^1]), parts[^1]);
            }
        }

        public static EvidenceMeta FormatAxisMeta(double axisValue, double accuracy, double rate, DateTimeOffset scoredAt)
        {
            string rateText = Math.Abs(rate - 1) < 0.001
                ? "1.0x"
                : rate.ToString("0.##x", CultureInfo.InvariantCulture);

            return new EvidenceMeta(
                axisValue.ToString("0.00", CultureInfo.InvariantCulture),
                $"{accuracy.ToString("0.00%", CultureInfo.InvariantCulture)} · {rateText}",
                scoredAt.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        public static EvidenceMeta FormatDanMeta(double creditedDan, double accuracy, double rate, DateTimeOffset scoredAt)
        {
            string rateText = Math.Abs(rate - 1) < 0.001
                ? "1.0x"
                : rate.ToString("0.##x", CultureInfo.InvariantCulture);

            return new EvidenceMeta(
                creditedDan.ToString("0.00", CultureInfo.InvariantCulture),
                $"{accuracy.ToString("0.00%", CultureInfo.InvariantCulture)} · {rateText}",
                scoredAt.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
    }
}
