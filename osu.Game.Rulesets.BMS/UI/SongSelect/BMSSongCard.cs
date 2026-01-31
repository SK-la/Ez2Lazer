// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Colour;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.BMS.Beatmaps;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// Card displaying a BMS song in the song list.
    /// </summary>
    public partial class BMSSongCard : CompositeDrawable
    {
        public BMSSongCache Song { get; }
        public Action? Action { get; set; }

        private Box background = null!;
        private Box hoverBox = null!;
        private Color4 normalColour;
        private Color4 selectedColour;

        private bool selected;
        public bool Selected
        {
            get => selected;
            set
            {
                if (selected == value) return;
                selected = value;
                background.Colour = selected ? selectedColour : normalColour;
            }
        }

        public BMSSongCard(BMSSongCache song)
        {
            Song = song;

            RelativeSizeAxes = Axes.X;
            Height = 60;
            Masking = true;
            CornerRadius = 5;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            normalColour = colours.Gray4;
            selectedColour = colours.Blue;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = normalColour,
                },
                hoverBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.White.Opacity(0.1f),
                    Alpha = 0,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding(10),
                    Spacing = new Vector2(0, 2),
                    Children = new Drawable[]
                    {
                        new TruncatingSpriteText
                        {
                            Text = string.IsNullOrEmpty(Song.Title) ? "(无标题)" : Song.Title,
                            Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                            RelativeSizeAxes = Axes.X,
                        },
                        new TruncatingSpriteText
                        {
                            Text = string.IsNullOrEmpty(Song.Artist) ? "(未知艺术家)" : Song.Artist,
                            Font = OsuFont.GetFont(size: 12),
                            Colour = colours.Yellow,
                            RelativeSizeAxes = Axes.X,
                        },
                        new OsuSpriteText
                        {
                            Text = $"{Song.Charts.Count} 难度",
                            Font = OsuFont.GetFont(size: 11),
                            Colour = colours.Gray9,
                        },
                    },
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverBox.FadeIn(100);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverBox.FadeOut(100);
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            Action?.Invoke();
            return true;
        }
    }
}
