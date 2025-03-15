// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbIHoldBodyPiece : CompositeDrawable, IHoldNoteBody
    {
        // private readonly Bindable<Color4> AccentColour = new Bindable<Color4>();

        private Drawable background = null!;

        public SbIHoldBodyPiece()
        {
            RelativeSizeAxes = Axes.Both;

            // Without this, the width of the body will be slightly larger than the head/tail.
            Masking = true;
            CornerRadius = 0;
        }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject? drawableObject)
        {
            InternalChildren = new[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.White,
                },
            };

            // if (drawableObject != null)
            // {
            //     var holdNote = (DrawableHoldNote)drawableObject;
            //
            //     // AccentColour.BindTo(holdNote.AccentColour);
            //     // hittingLayer.AccentColour.BindTo(holdNote.AccentColour);
            //     // ((IBindable<bool>)hittingLayer.IsHitting).BindTo(holdNote.IsHitting);
            // }

            // AccentColour.BindValueChanged(colour =>
            // {
            //     background.Colour = colour.NewValue.Darken(0.6f);
            // }, true);
        }

        public void Recycle()
        {
            // hittingLayer.Recycle();
        }
    }
}
