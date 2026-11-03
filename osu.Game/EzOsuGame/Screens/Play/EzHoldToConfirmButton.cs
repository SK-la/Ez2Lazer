// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Screens.Play
{
    /// <summary>
    /// Circular hold-to-confirm control for the force-results action. Used both by the pause overlay and the
    /// in-gameplay settings sidebar, so the required hold and visual feedback stay identical in both places.
    /// </summary>
    public partial class EzHoldToConfirmButton : HoldToConfirmContainer
    {
        /// <summary>
        /// Fixed delay applied on top of the user's hold setting: a short tap on this control must never force results.
        /// </summary>
        private const double hold_duration_ms = 2000;

        private CircularProgress circularProgress = null!;
        private SpriteIcon icon = null!;

        public EzHoldToConfirmButton()
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
