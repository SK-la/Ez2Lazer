// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbIColumnBackground : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private Color4 brightColour;
        private Color4 dimColour;

        private Box backgroundOverlay = null!;
        // private Box background = null!;
        // private Box? separator;

        [Resolved]
        private Column column { get; set; } = null!;

        // private Bindable<Color4> accentColour = null!;
        private readonly Bindable<float> overlayHeight = new Bindable<float>(0f);

        public SbIColumnBackground()
        {
            RelativeSizeAxes = Axes.Both;

            Masking = true;
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo, ISkinSource skin, StageDefinition stageDefinition)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Name = "Background",
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 1,
                },
                backgroundOverlay = new Box
                {
                    Name = "Background Gradient Overlay",
                    RelativeSizeAxes = Axes.Both,
                    Height = 0.1f,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                    Colour = Color4.White,
                },
            };

            overlayHeight.BindValueChanged(height => backgroundOverlay.Height = height.NewValue, true);
            // accentColour.BindValueChanged(colour =>
            // {
            //     var newColour = colour.NewValue.Darken(3);
            //
            //     if (newColour.A != 0)
            //     {
            //         newColour = newColour.Opacity(1f);
            //     }
            //
            //     background.Colour = newColour;
            //     // background.Colour = colour.NewValue.Darken(3);
            //     // brightColour = colour.NewValue.Opacity(0.6f);
            //     // dimColour = colour.NewValue.Opacity(0);
            // }, true);

            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            if (direction.NewValue == ScrollingDirection.Up)
            {
                backgroundOverlay.Anchor = backgroundOverlay.Origin = Anchor.TopLeft;
            }
            else
            {
                backgroundOverlay.Anchor = backgroundOverlay.Origin = Anchor.BottomLeft;
            }
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action == column.Action.Value)
            {
                var noteColour = column.AccentColour.Value;
                brightColour = noteColour.Opacity(1f);
                dimColour = noteColour.Opacity(0);

                backgroundOverlay.Colour = direction.Value == ScrollingDirection.Up
                    ? ColourInfo.GradientVertical(brightColour, dimColour)
                    : ColourInfo.GradientVertical(dimColour, brightColour);

                overlayHeight.Value = 0.1f;

                backgroundOverlay.FadeTo(1, 50, Easing.OutQuint).Then().FadeTo(0.5f, 250, Easing.OutQuint);
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            if (e.Action == column.Action.Value)
                backgroundOverlay.FadeTo(0, 250, Easing.OutQuint);
        }
    }
}
