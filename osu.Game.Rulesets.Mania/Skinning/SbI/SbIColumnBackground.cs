// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbIColumnBackground : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
        private Bindable<double> hitPosition = null!;

        private Color4 brightColour;
        private Color4 dimColour;

        private Box backgroundOverlay = null!;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        public SbIColumnBackground()
        {
            RelativeSizeAxes = Axes.Both;

            Masking = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Name = "Background",
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.0f,
                },
                backgroundOverlay = new Box
                {
                    Name = "Background Gradient Overlay",
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.X,
                    Height = 20,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                    Colour = Color4.White,
                },
            };

            hitPosition = ezConfig.GetBindable<double>(Ez2Setting.HitPosition);
            hitPosition.BindValueChanged(_ => updateDrawable(), true);
        }

        private void updateDrawable()
        {
            float bottomHeight = (float)hitPosition.Value;
            backgroundOverlay.Height = bottomHeight;
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action == column.Action.Value)
            {
                var noteColour = column.EzNoteColourBindable.Value;
                brightColour = noteColour.Opacity(0.6f);
                dimColour = noteColour.Opacity(0);

                backgroundOverlay.Colour = ColourInfo.GradientVertical(brightColour, dimColour);

                backgroundOverlay.FadeTo(0.8f, 50, Easing.OutQuint).Then().FadeTo(0.5f, 250, Easing.OutQuint);
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
