// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Mania.Skinning.EzStylePro;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbIKeyArea : SbINotePiece
    {
        private Container directionContainer = null!;
        private Drawable background = null!;

        private Bindable<Color4> accentColour = null!;

        [Resolved]
        private Column column { get; set; } = null!;

        public SbIKeyArea()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = directionContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                // Height = Stage.HIT_TARGET_POSITION + SbINotePiece.CORNER_RADIUS * 2,
                Children = new Drawable[]
                {
                    new Container
                    {
                        Masking = true,
                        RelativeSizeAxes = Axes.Both,
                        CornerRadius = (float)CORNER_RADIUS.Value,
                        Child = background = new Box
                        {
                            Name = "Key gradient",
                            Alpha = 0,
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                }
            };

            accentColour = column.AccentColour.GetBoundCopy();
            accentColour.BindValueChanged(colour =>
                {
                    background.Colour = colour.NewValue.Darken(0.2f);
                },
                true);

            column.TopLevelContainer.Add(CreateProxy());
        }

        protected KeyCounter CreateCounter(InputTrigger trigger) => new ArgonKeyCounter(trigger);

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            switch (direction.NewValue)
            {
                case ScrollingDirection.Up:
                    directionContainer.Scale = new Vector2(1, -1);
                    directionContainer.Anchor = Anchor.TopCentre;
                    directionContainer.Origin = Anchor.BottomCentre;
                    break;

                case ScrollingDirection.Down:
                    directionContainer.Scale = new Vector2(1, 1);
                    directionContainer.Anchor = Anchor.BottomCentre;
                    directionContainer.Origin = Anchor.BottomCentre;
                    break;
            }
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            if (e.Action != column.Action.Value) return;

            const double lighting_fade_out_duration = 800;
            background.FadeTo(0f, 50, Easing.OutQuint)
                      .Then()
                      .FadeOut(lighting_fade_out_duration, Easing.OutQuint);
        }
    }
}
