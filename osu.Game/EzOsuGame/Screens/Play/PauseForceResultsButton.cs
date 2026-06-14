// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Screens.Play
{
    public partial class PauseForceResultsButton : Container
    {
        private const double hold_duration_ms = 2000;

        public Action? Action { get; set; }

        private HoldButton holdButton = null!;
        private OsuSpriteText labelText = null!;

        public PauseForceResultsButton()
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.BottomRight;
            Origin = Anchor.BottomRight;
            Margin = new MarginPadding { Right = 20, Bottom = 20 };
            Alpha = 0;
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => Alpha > 0;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(12, 0),
                    Children = new Drawable[]
                    {
                        holdButton = new HoldButton
                        {
                            Action = () => Action?.Invoke(),
                        },
                        labelText = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = OsuFont.GetFont(weight: FontWeight.Bold),
                            Text = EzHUDStrings.PAUSE_FORCE_RESULTS_LABEL,
                            Colour = colours.Orange1,
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            holdButton.Progress.BindValueChanged(p =>
            {
                labelText.Text = p.NewValue > 0
                    ? EzHUDStrings.PAUSE_FORCE_RESULTS_HOLDING
                    : EzHUDStrings.PAUSE_FORCE_RESULTS_LABEL;
            }, true);
        }

        public void SetVisible(bool visible)
        {
            this.FadeTo(visible ? 1 : 0, 200, Easing.OutQuint);
        }

        private partial class HoldButton : HoldToConfirmContainer
        {
            private CircularProgress circularProgress = null!;
            private SpriteIcon icon = null!;

            public HoldButton()
                : base(isDangerousAction: false)
            {
                Size = new Vector2(50);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                HoldActivationDelay.UnbindAll();
                ((Bindable<double>)HoldActivationDelay).Value = hold_duration_ms;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                Child = new CircularContainer
                {
                    Masking = true,
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colours.Gray1,
                            Alpha = 0.6f,
                        },
                        circularProgress = new CircularProgress
                        {
                            RelativeSizeAxes = Axes.Both,
                            InnerRadius = 1,
                        },
                        icon = new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(14),
                            Icon = FontAwesome.Solid.FlagCheckered,
                            Shadow = false,
                        },
                    },
                };

                Progress.BindValueChanged(p =>
                {
                    circularProgress.Progress = p.NewValue;
                    icon.Scale = new Vector2(1 + (float)p.NewValue * 0.15f);
                    Colour = Interpolation.ValueAt(p.NewValue, Color4.White, Color4.Red, 0, 1, Easing.OutQuint);
                }, true);
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                BeginConfirm();
                return true;
            }

            protected override void OnMouseUp(MouseUpEvent e)
            {
                if (!e.HasAnyButtonPressed)
                    AbortConfirm();

                base.OnMouseUp(e);
            }
        }
    }
}
