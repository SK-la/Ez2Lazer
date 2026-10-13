// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Overlays.Settings
{
    /// <summary>
    /// A subsection head entry mirroring <see cref="SidebarIconButton"/> for use in a section's
    /// in-content navigation column.
    /// </summary>
    public partial class SubsectionNavButton : SidebarButton
    {
        private const float selection_indicator_height_active = 18;
        private const float selection_indicator_height_inactive = 4;

        private CircularContainer selectionIndicator;
        private Container textIconContent;

        public required LocalisableString Header { get; init; }

        private bool selected;

        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;

                if (IsLoaded)
                    UpdateState();
            }
        }

        public SubsectionNavButton()
        {
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Height = 46;
            Padding = new MarginPadding { Horizontal = 5, Vertical = 2.5f };

            AddRange(new Drawable[]
            {
                textIconContent = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = OsuColour.Gray(0.6f),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = Header,
                            Font = OsuFont.Default.With(size: OsuFont.DEFAULT_FONT_SIZE),
                            Position = new Vector2(20, 0),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                    }
                },
                selectionIndicator = new CircularContainer
                {
                    Colour = ColourProvider.Highlight1,
                    Alpha = 0,
                    Width = 4,
                    Height = selection_indicator_height_inactive,
                    Masking = true,
                    CornerRadius = 1.5f,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Margin = new MarginPadding
                    {
                        Left = 9,
                    },
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.White
                    }
                },
            });
        }

        protected override void UpdateState()
        {
            base.UpdateState();

            if (Selected)
            {
                textIconContent.FadeColour(ColourProvider.Content1, FADE_DURATION, Easing.OutQuint);

                selectionIndicator.FadeIn(FADE_DURATION, Easing.OutQuint);
                selectionIndicator.ResizeHeightTo(selection_indicator_height_active, FADE_DURATION, Easing.OutElasticHalf);
            }
            else
            {
                textIconContent.FadeColour(IsHovered ? ColourProvider.Light1 : ColourProvider.Light3, FADE_DURATION, Easing.OutQuint);

                selectionIndicator.FadeOut(FADE_DURATION, Easing.OutQuint);
                selectionIndicator.ResizeHeightTo(selection_indicator_height_inactive, FADE_DURATION, Easing.OutQuint);
            }
        }
    }
}
