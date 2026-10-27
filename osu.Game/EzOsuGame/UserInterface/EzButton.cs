// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.UI;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// General-purpose button matching Screens.Footer.ScreenFooterButton chrome
    /// (fill, glow, shadow, flash) without the footer bottom accent bar or icon stack.
    /// </summary>
    public partial class EzButton : OsuClickableContainer
    {
        public const float CORNER_RADIUS = EzUIStyleV1.CORNER_RADIUS_FOOTER;

        /// <summary>Suggested absolute height when the parent does not provide one.</summary>
        public const float DEFAULT_HEIGHT = 50;

        /// <summary>
        /// Sets an absolute height and stops following the parent on the Y axis.
        /// Default layout is <see cref="Axes.Y"/> relative to the parent.
        /// </summary>
        public new float Height
        {
            get => base.Height;
            set
            {
                RelativeSizeAxes &= ~Axes.Y;
                base.Height = value;
            }
        }

        public LocalisableString Text
        {
            get => text.Text;
            set => text.Text = value;
        }

        public float TextSize
        {
            get => text.Font.Size;
            set => text.Font = OsuFont.TorusAlternate.With(size: value);
        }

        private Colour4 buttonAccentColour;

        public Colour4 AccentColour
        {
            get => buttonAccentColour;
            set
            {
                buttonAccentColour = value;
                Scheduler.AddOnce(UpdateDisplay);
            }
        }

        /// <summary>
        /// When true, the button fills with <see cref="AccentColour"/> (e.g. toggle on).
        /// </summary>
        protected bool Accented
        {
            get => accented;
            set
            {
                if (accented == value)
                    return;

                accented = value;
                Scheduler.AddOnce(UpdateDisplay);
            }
        }

        [Resolved]
        protected OverlayColourProvider ColourProvider { get; private set; } = null!;

        private bool accented;

        private readonly Container content;
        private readonly Container textContainer;
        private readonly OsuSpriteText text;
        private readonly Box backgroundBox;
        private readonly Box glowBox;
        private readonly Box flashLayer;

        public EzButton()
        {
            RelativeSizeAxes = Axes.Y;

            Children = new Drawable[]
            {
                content = new Container
                {
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Radius = 4,
                        Colour = Colour4.Black.Opacity(0.25f),
                        Offset = new Vector2(0, 2),
                    },
                    Masking = true,
                    CornerRadius = CORNER_RADIUS,
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        backgroundBox = new Box
                        {
                            RelativeSizeAxes = Axes.Both
                        },
                        glowBox = new Box
                        {
                            RelativeSizeAxes = Axes.Both
                        },
                        flashLayer = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Colour4.White.Opacity(0.9f),
                            Blending = BlendingParameters.Additive,
                            Alpha = 0,
                        },
                    },
                },
                textContainer = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Child = text = new OsuSpriteText
                    {
                        Font = OsuFont.TorusAlternate.With(size: 17),
                        Margin = new MarginPadding { Horizontal = 15 },
                    }
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (buttonAccentColour == default)
                buttonAccentColour = ColourProvider.Highlight1;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Enabled.BindValueChanged(_ => UpdateDisplay(), true);
            FinishTransforms(true);
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => content.ReceivePositionalInputAt(screenSpacePos);

        protected override bool OnClick(ClickEvent e)
        {
            if (Enabled.Value)
                Flash();

            return base.OnClick(e);
        }

        protected virtual void Flash() => flashLayer.FadeOutFromOne(800, Easing.OutQuint);

        protected override bool OnHover(HoverEvent e)
        {
            UpdateDisplay();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e) => UpdateDisplay();

        protected void UpdateDisplay()
        {
            // Accented fill is a darkened accent (not Highlight1 solid) so the on-state stays muted.
            Color4 backgroundColour = Accented ? buttonAccentColour.Darken(0.5f) : ColourProvider.Background3;
            Color4 textColour = ColourProvider.Content1;

            if (!Enabled.Value)
                backgroundColour = backgroundColour.Darken(1f);
            else if (IsHovered)
                backgroundColour = backgroundColour.Lighten(0.15f);

            backgroundBox.FadeColour(backgroundColour, 150, Easing.OutQuint);

            if (!Enabled.Value)
                textColour = textColour.Opacity(0.6f);

            textContainer.FadeColour(textColour, 150, Easing.OutQuint);

            float glowBottom = Accented ? 0.12f : 0.2f;
            glowBox.FadeColour(ColourInfo.GradientVertical(buttonAccentColour.Opacity(0f), buttonAccentColour.Opacity(glowBottom)), 150, Easing.OutQuint);
        }
    }
}
