// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteTail : EzNoteBase
    {
        private readonly EzHoldNoteHittingLayer hittingLayer = new EzHoldNoteHittingLayer();

        private TextureAnimation? animation;

        private IBindable<double> tailAlpha = null!;
        private IBindable<double> tailMaskHeight = null!;

        [Resolved]
        private DrawableHitObject? drawableObject { get; set; }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject? drawableObject, IEzSkinInfo ezSkinInfo)
        {
            RelativeSizeAxes = Axes.Both;
            Alpha = 0f;

            if (drawableObject != null)
            {
                // accentColour.BindTo(drawableObject.AccentColour);
                // accentColour.BindValueChanged(onAccentChanged, true);

                drawableObject.HitObjectApplied += hitObjectApplied;
            }

            tailAlpha = ezSkinInfo.HoldTailAlpha;
            tailMaskHeight = ezSkinInfo.HoldTailMaskHeight;

            // 当设置为负值时显示 tail，非负值时隐藏
            tailMaskHeight.BindValueChanged(maskHeight =>
            {
                Alpha = maskHeight.NewValue < 0 ? (float)tailAlpha.Value : 0f;
            }, true);
        }

        // protected override void LoadComplete()
        // {
        //     base.LoadComplete();
        //
        //     // 当设置为负值时显示 tail，非负值时隐藏
        //     tailMaskHeight.BindValueChanged(maskHeight =>
        //     {
        //         Alpha = maskHeight.NewValue < 0 ? (float)tailAlpha.Value : 0f;
        //     }, true);
        // }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (drawableObject != null)
                drawableObject.HitObjectApplied -= hitObjectApplied;
        }

        protected virtual string ComponentSuffix => "longnote/tail";
        protected virtual string ComponentName => $"{ColorPrefix}{ComponentSuffix}";

        protected override void UpdateTexture()
        {
            MainContainer?.Clear();
            animation = Factory.CreateAnimation(ComponentName);

            if (animation.FrameCount == 0)
            {
                animation.Dispose();
                animation = Factory.CreateAnimation($"{ColorPrefix}note");
            }

            if (MainContainer != null)
            {
                MainContainer.Child = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Masking = true,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Child = animation,
                    }
                };
            }

            UpdateColor();
        }

        private void hitObjectApplied(DrawableHitObject drawableHitObject)
        {
            var holdNoteTail = (DrawableHoldNoteTail)drawableHitObject;

            // hittingLayer.AccentColour.UnbindBindings();
            // hittingLayer.AccentColour.BindTo(holdNoteTail.HoldNote.AccentColour);

            // 先解绑再绑定，避免重复绑定异常
            ((IBindable<bool>)hittingLayer.IsHitting).UnbindBindings();
            ((IBindable<bool>)hittingLayer.IsHitting).BindTo(holdNoteTail.HoldNote.IsHolding);
        }
    }
}
