// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Shared Local Profile narrow score card: artist, title, player, mods, date, pp, star, xxySR.
    /// </summary>
    public partial class EzLocalProfileScoreNarrowCard : OsuClickableContainer
    {
        public Guid ScoreId => row.ScoreId;

        private readonly EzLocalProfileDrillScoreRow row;
        private readonly IReadOnlyList<Mod> mods;
        private readonly LocalisableString? caption;
        private readonly string? highlightValue;
        private readonly bool interactive;

        private EzLocalProfileHoverBox background = null!;

        public EzLocalProfileScoreNarrowCard(EzLocalProfileDrillScoreRow row,
                                             IReadOnlyList<Mod> mods,
                                             Action? action = null,
                                             LocalisableString? caption = null,
                                             string? highlightValue = null)
        {
            this.row = row;
            this.mods = mods;
            this.caption = caption;
            this.highlightValue = highlightValue;
            interactive = action != null;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Action = action;
            Masking = true;
            CornerRadius = 6;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours, OsuColour osuColours)
        {
            var fillFlowContainer = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding { Horizontal = 10, Vertical = 8 },
                Spacing = new Vector2(0, 3),
            };

            background = new EzLocalProfileHoverBox();
            background.Configure(colours);
            background.SetInteractive(interactive);

            // Must use Children (Content), never InternalChildren — OsuClickableContainer owns internals.
            Children = new Drawable[]
            {
                background,
                fillFlowContainer,
            };

            if (caption is LocalisableString captionText)
            {
                fillFlowContainer.Add(new OsuSpriteText
                {
                    Text = captionText,
                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
                    Colour = colours.Content2,
                });
            }

            if (!string.IsNullOrEmpty(highlightValue))
            {
                fillFlowContainer.Add(new OsuSpriteText
                {
                    Text = highlightValue,
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                    Colour = EzLocalProfileColours.Numeric(colours),
                });
            }

            fillFlowContainer.Add(new TruncatingSpriteText
            {
                RelativeSizeAxes = Axes.X,
                Text = string.IsNullOrEmpty(row.Artist) ? "—" : row.Artist,
                Font = OsuFont.GetFont(size: 10),
                Colour = EzLocalProfileColours.Meta(colours),
                Alpha = 0.9f,
            });

            fillFlowContainer.Add(new TruncatingSpriteText
            {
                RelativeSizeAxes = Axes.X,
                Text = string.IsNullOrEmpty(row.DifficultyName)
                    ? row.Title
                    : $"{row.Title} [{row.DifficultyName}]",
                Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                Colour = EzLocalProfileColours.Plays(colours),
            });

            fillFlowContainer.Add(new TruncatingSpriteText
            {
                RelativeSizeAxes = Axes.X,
                Text = string.IsNullOrEmpty(row.Username) ? "—" : row.Username,
                Font = OsuFont.GetFont(size: 11),
                Colour = colours.Content1,
            });

            var modsFlow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(-10, 0),
            };

            foreach (var mod in mods.AsOrdered())
            {
                modsFlow.Add(new ModIcon(mod)
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Scale = new Vector2(0.25f),
                    Height = ModIcon.MOD_ICON_SIZE.Y * 3 / 4f,
                });
            }

            modsFlow.Alpha = modsFlow.Count > 0 ? 1 : 0;

            var starFlow = new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(5, 0),
                Children = new Drawable[]
                {
                    new StarRatingDisplay(new StarDifficulty(row.StarRating, 0), StarRatingDisplaySize.Small)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Scale = new Vector2(0.7f),
                    },
                }
            };

            bool showXxy = row.RulesetId == EzLocalProfileConstants.MANIA_RULESET_ID && row.XxyStarRating >= 0;

            if (showXxy)
            {
                starFlow.Add(new EzDisplaySR(row.ReadManiaSummary(), StarRatingDisplaySize.Small)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Scale = new Vector2(0.7f),
                });
            }

            var metaRow = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 14,
                Children = new Drawable[]
                {
                    starFlow
                }
            };

            metaRow.Add(new FillFlowContainer
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(5, 0),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Text = row.FormatPpText(),
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                        Colour = EzLocalProfileColours.Pp(osuColours),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Text = row.Date.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        Font = OsuFont.GetFont(size: 10),
                        Colour = EzLocalProfileColours.Meta(colours),
                        Alpha = 0.85f,
                    },
                    modsFlow,
                }
            });

            fillFlowContainer.Add(metaRow);
        }

        public void SetSelected(bool value)
        {
            background.SetSelected(value);
            background.Refresh(IsHovered);
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
    }
}
