// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.Skinning.EzStylePro;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbIHoldNoteTailPiece : SbINotePiece
    {
        [Resolved]
        private DrawableHitObject? drawableObject { get; set; }

        private Bindable<bool> enabledColor = null!;
        private Bindable<double> tailAlpha = null!;

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

            enabledColor = EzSkinConfig.GetBindable<bool>(Ez2Setting.ColorSettingsEnabled);
            tailAlpha = EzSkinConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailAlpha);
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
