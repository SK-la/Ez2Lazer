// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbIHoldNoteTailPiece : SbINotePiece
    {
        [Resolved]
        private DrawableHitObject? drawableObject { get; set; }

        private IBindable<double> tailAlpha = null!;

        // private SbIHoldNoteHittingLayer hittingLayer { get; set; }

        public SbIHoldNoteTailPiece()
        {
            RelativeSizeAxes = Axes.X;
            Height = 8;
            Alpha = 0;
        }

        [BackgroundDependencyLoader(true)]
        private void load()
        {
            if (MainContainer != null)
            {
                MainContainer.Rotation = 180;
            }

            if (drawableObject != null)
            {
                drawableObject.HitObjectApplied += hitObjectApplied;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            tailAlpha = Column.EzSkinInfo.HoldTailAlpha;
            tailAlpha.BindValueChanged(alpha =>
            {
                Alpha = (float)alpha.NewValue;
            }, true);
        }

        private void hitObjectApplied(DrawableHitObject drawableHitObject)
        {
            // var holdNoteTail = (DrawableHoldNoteTail)drawableHitObject;

            // hittingLayer.Recycle();
            //
            // hittingLayer.AccentColour.UnbindBindings();
            // hittingLayer.AccentColour.BindTo(holdNoteTail.HoldNote.AccentColour);
            //
            // hittingLayer.IsHitting.UnbindBindings();
            // ((IBindable<bool>)hittingLayer.IsHitting).BindTo(holdNoteTail.HoldNote.IsHitting);
        }

        // protected override void Update()
        // {
        //     base.Update();
        //     Height = DrawWidth / DrawWidth;
        // }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (drawableObject != null)
                drawableObject.HitObjectApplied -= hitObjectApplied;
        }
    }
}
