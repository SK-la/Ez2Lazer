// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    internal partial class EzHoldNoteTail : EzNoteBase
    {
        protected override bool ShowSeparators => false;

        private readonly EzHoldNoteHittingLayer hittingLayer = new EzHoldNoteHittingLayer();

        private TextureAnimation? animation;

        private IBindable<double> tailAlpha = null!;
        private IBindable<double> tailMaskHeight = null!;

        private bool gradient;

        [Resolved]
        private DrawableHitObject? drawableObject { get; set; }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject? drawableObject, IEzSkinInfo ezSkinInfo, Ez2ConfigManager ezConfig)
        {
            gradient = ezConfig.Get<bool>(Ez2Setting.ManiaLNGradientEnable);

            if (gradient)
            {
                Alpha = 0;
                return;
            }

            Alpha = 1f;

            if (drawableObject != null)
            {
                drawableObject.HitObjectApplied += hitObjectApplied;
            }

            tailAlpha = ezSkinInfo.HoldTailAlpha;
            tailMaskHeight = ezSkinInfo.HoldTailMaskHeight;

            tailMaskHeight.BindValueChanged(_ => OnDrawableChanged(), true);
            tailAlpha.BindValueChanged(_ => OnColourChanged(), true);
        }

        protected override void UpdateTexture()
        {
            if (gradient)
                return;

            animation = Factory.CreateAnimation(TailName);

            if (animation.FrameCount == 0)
            {
                animation.Dispose();
                animation = Factory.CreateAnimation(HeadName);

                if (animation.FrameCount == 0)
                {
                    animation.Dispose();
                    animation = Factory.CreateAnimation(NoteName);

                    if (animation.FrameCount == 0)
                    {
                        animation.Dispose();
                        return;
                    }
                }
            }

            MainContainer.Child = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Masking = true,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Child = animation,
                }
            };
        }

        protected override void UpdateDrawable()
        {
            if (gradient)
                return;

            float visibleHeight = NoteHeight - (float)tailMaskHeight.Value;
            Height = visibleHeight;
        }

        protected override void UpdateColor()
        {
            if (gradient)
                return;

            MainContainer.Colour = ColourInfo.GradientVertical(
                NoteColor.Opacity((float)tailAlpha.Value),
                NoteColor);
        }

        private void hitObjectApplied(DrawableHitObject drawableHitObject)
        {
            var holdNoteTail = (DrawableHoldNoteTail)drawableHitObject;

            // 先解绑再绑定，避免重复绑定异常
            ((IBindable<bool>)hittingLayer.IsHitting).UnbindBindings();
            ((IBindable<bool>)hittingLayer.IsHitting).BindTo(holdNoteTail.HoldNote.IsHolding);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (drawableObject != null)
                drawableObject.HitObjectApplied -= hitObjectApplied;

            animation = null;
        }
    }
}
