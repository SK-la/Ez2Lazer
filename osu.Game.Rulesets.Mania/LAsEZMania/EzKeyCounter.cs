// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.Rulesets.Mania.LAsEZMania
{
    public partial class EzKeyCounter : KeyCounter
    {
        private Circle inputIndicator = null!;
        private OsuSpriteText keyNameText = null!;
        private OsuSpriteText countText = null!;

        private const float line_height = 3;
        private const float name_font_size = 10;
        private const float count_font_size = 14;
        private const float scale_factor = 1.5f;

        private const float indicator_press_offset = 4;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public bool ShowKeyName { get; set; } = false;

        // private readonly string keyDisplayName;

        public EzKeyCounter(InputTrigger trigger)
            : base(trigger)
        {
            // this.keyDisplayName = keyDisplayName;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                inputIndicator = new Circle
                {
                    RelativeSizeAxes = Axes.X,
                    Height = line_height * scale_factor,
                    Alpha = 0.5f
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = line_height * scale_factor + indicator_press_offset },
                    Children = new Drawable[]
                    {
                        keyNameText = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = OsuFont.Torus.With(size: name_font_size * scale_factor, weight: FontWeight.Bold),
                            Colour = colours.Blue0,
                            // Text = ShowKeyName ? keyDisplayName : Trigger.Name
                            Text = Trigger.Name
                        },
                        countText = new OsuSpriteText
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.BottomCentre,
                            Font = OsuFont.Torus.With(size: count_font_size * scale_factor, weight: FontWeight.Bold),
                        },
                    }
                },
            };

            Height = 30 * scale_factor;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            CountPresses.BindValueChanged(e => countText.Text = e.NewValue.ToString(@"#,0"), true);
        }

        protected override void Activate(bool forwardPlayback = true)
        {
            base.Activate(forwardPlayback);

            keyNameText
                .FadeColour(Colour4.White, 10, Easing.OutQuint);

            inputIndicator
                .FadeIn(10, Easing.OutQuint)
                .MoveToY(0)
                .Then()
                .MoveToY(indicator_press_offset, 60, Easing.OutQuint);
        }

        protected override void Deactivate(bool forwardPlayback = true)
        {
            base.Deactivate(forwardPlayback);

            keyNameText
                .FadeColour(colours.Blue0, 200, Easing.OutQuart);

            inputIndicator
                .MoveToY(0, 250, Easing.OutQuart)
                .FadeTo(0.5f, 250, Easing.OutQuart);
        }
    }
}
