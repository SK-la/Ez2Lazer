// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Mania.Skinning.EzStylePro;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    internal partial class SbINotePiece : EzNoteBase
    {
        public const float NOTE_HEIGHT = 45;
        public const float NOTE_ACCENT_RATIO = 1f;
        public const float CORNER_RADIUS = 0;

        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
        private readonly IBindable<Color4> accentColour = new Bindable<Color4>();

        private Box colouredBox = null!;

        public SbINotePiece()
        {
            RelativeSizeAxes = Axes.X;

            CornerRadius = CORNER_RADIUS;
            // Masking = true;
        }

        protected override void Update()
        {
            base.Update();
            Height = 8;

            // CreateIcon().Size = new Vector2(DrawWidth / 43 * 0.7f);
        }

        [BackgroundDependencyLoader(true)]
        private void load(IScrollingInfo scrollingInfo, DrawableHitObject? drawableObject)
        {
            if (MainContainer != null)
            {
                MainContainer.Children = new[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            // BorderColour = Color4.White.Opacity(1f),
                            // BorderColour = ColourInfo.GradientVertical(Color4.White.Opacity(0), Colour4.Black),
                        }
                    },
                    new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        // Masking = true,
                        // CornerRadius = CORNER_RADIUS,
                        Children = new Drawable[]
                        {
                            colouredBox = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                            }
                        }
                    },
                };
            }

            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);

            if (drawableObject != null)
            {
                accentColour.BindTo(drawableObject.AccentColour);
                accentColour.BindValueChanged(onAccentChanged, true);
            }
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            colouredBox.Anchor = colouredBox.Origin = direction.NewValue == ScrollingDirection.Up
                ? Anchor.TopCentre
                : Anchor.BottomCentre;

            Scale = new Vector2(1, direction.NewValue == ScrollingDirection.Up ? -1 : 1);
        }

        private void onAccentChanged(ValueChangedEvent<Color4> accent)
        {
            colouredBox.Colour = ColourInfo.GradientVertical(
                accent.NewValue.Lighten(0.1f),
                accent.NewValue
            );
        }
    }
}
