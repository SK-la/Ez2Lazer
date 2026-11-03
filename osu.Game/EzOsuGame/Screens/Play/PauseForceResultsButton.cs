// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.EzOsuGame.Screens.Play
{
    public partial class PauseForceResultsButton : Container
    {
        public Action? Action { get; set; }

        private EzHoldToConfirmButton holdButton = null!;
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
                        holdButton = new EzHoldToConfirmButton
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
    }
}
