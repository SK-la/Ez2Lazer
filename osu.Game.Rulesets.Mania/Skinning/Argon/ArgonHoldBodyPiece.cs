// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Argon
{
    /// <summary>
    /// Represents length-wise portion of a hold note.
    /// </summary>
    public partial class ArgonHoldBodyPiece : CompositeDrawable, IHoldNoteBody
    {
        protected readonly Bindable<Color4> AccentColour = new Bindable<Color4>();
        private readonly Bindable<Color4> paintColour = new Bindable<Color4>(Color4.White);

        [Resolved(CanBeNull = true)]
        private ManiaPlayfieldNoteDisplayTint? noteDisplayTint { get; set; }

        private Drawable background = null!;
        private ArgonHoldNoteHittingLayer hittingLayer = null!;

        public ArgonHoldBodyPiece()
        {
            RelativeSizeAxes = Axes.Both;

            // Without this, the width of the body will be slightly larger than the head/tail.
            Masking = true;
            CornerRadius = ArgonNotePiece.CORNER_RADIUS;
        }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject? drawableObject)
        {
            InternalChildren = new[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both },
                hittingLayer = new ArgonHoldNoteHittingLayer()
            };

            if (drawableObject != null)
            {
                var holdNote = (DrawableHoldNote)drawableObject;

                AccentColour.BindTo(holdNote.AccentColour);
                hittingLayer.AccentColour.BindTo(paintColour);
                ((IBindable<bool>)hittingLayer.IsHitting).BindTo(holdNote.IsHolding);
            }

            AccentColour.BindValueChanged(_ => refreshPaint(), true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (noteDisplayTint != null)
                noteDisplayTint.Changed += refreshPaint;

            refreshPaint();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && noteDisplayTint != null)
                noteDisplayTint.Changed -= refreshPaint;

            base.Dispose(isDisposing);
        }

        private void refreshPaint()
        {
            paintColour.Value = noteDisplayTint?.DisplayTintActive == true ? Color4.White : AccentColour.Value;
            background.Colour = paintColour.Value.Darken(0.6f);
        }

        public void Recycle()
        {
            hittingLayer.Recycle();
        }
    }
}
