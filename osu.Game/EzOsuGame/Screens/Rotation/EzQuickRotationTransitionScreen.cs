// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Input.Bindings;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public partial class EzQuickRotationTransitionScreen : ScreenWithBeatmapBackground, IKeyBindingHandler<GlobalAction>
    {
        private OsuSpriteText countdownText = null!;
        private bool advanced;

        [Resolved]
        private OsuGame? game { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Colour4.Black.Opacity(0.6f),
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 12),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = EzQuickRotationStrings.TRANSITION_TITLE,
                                    Font = OsuFont.GetFont(size: 28, weight: FontWeight.Bold),
                                },
                                countdownText = new OsuSpriteText
                                {
                                    Font = OsuFont.GetFont(size: 48, weight: FontWeight.Bold),
                                },
                                new OsuSpriteText
                                {
                                    Text = EzQuickRotationStrings.TRANSITION_SKIP_HINT,
                                    Font = OsuFont.GetFont(size: 18),
                                    Alpha = 0.8f,
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateCountdown(3);
            Scheduler.AddDelayed(() => updateCountdown(2), 1000);
            Scheduler.AddDelayed(() => updateCountdown(1), 2000);
            Scheduler.AddDelayed(advance, 3000);
        }

        private void updateCountdown(int value) => countdownText.Text = value.ToString();

        private void advance()
        {
            if (advanced)
                return;

            advanced = true;
            this.Push(new EzQuickRotationPickScreen());
        }

        protected override bool OnClick(ClickEvent e)
        {
            advance();
            return true;
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.SkipCutscene:
                case GlobalAction.Select:
                    advance();
                    return true;

                case GlobalAction.QuickExit:
                    EzQuickRotationCoordinator.EndSession(game);
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }
    }
}
