// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteHittingLayer : EzNoteBase
    {
        private static readonly BlendingParameters additive_preserve_alpha = new BlendingParameters
        {
            Source = BlendingType.SrcAlpha,
            Destination = BlendingType.One,
            SourceAlpha = BlendingType.Zero,
            DestinationAlpha = BlendingType.One,
            RGBEquation = BlendingEquation.Add,
            AlphaEquation = BlendingEquation.Add,
        };

        public readonly Bindable<bool> IsHitting = new Bindable<bool>();
        // public IBindable<double> HitPosition = null!;

        private Drawable? animation;

        public EzHoldNoteHittingLayer()
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.Centre;
            RelativeSizeAxes = Axes.None;
            Blending = additive_preserve_alpha;
        }

        // [BackgroundDependencyLoader]
        // private void load(IEzSkinInfo ezSkinInfo)
        // {
        //     HitPosition = ezSkinInfo.HitPosition;
        //     HitPosition.BindValueChanged(_ => OnDrawableChanged());
        // }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Scheduler.AddOnce(OnDrawableChanged);

            if (animation == null)
                UpdateTexture();

            IsHitting.BindValueChanged(hitting =>
            {
                ClearTransforms();

                if (hitting.NewValue && animation != null)
                {
                    Alpha = 1;

                    if (animation is TextureAnimation textureAnimation)
                        textureAnimation.Restart();
                }
                else
                {
                    Alpha = 0;
                }
            }, true);
        }

        public void Recycle()
        {
            ClearTransforms();
            Alpha = 0;
        }

        protected override void UpdateTexture()
        {
            ClearInternal();
            string[] componentsToTry = { "longnoteflare", "noteflaregood", "noteflare" };

            foreach (string component in componentsToTry)
            {
                animation = Factory.CreateAnimation(component, true);

                if (animation != null)
                {
                    if (animation is TextureAnimation textureAnimation)
                        textureAnimation.Loop = true;

                    AddInternal(animation);
                    break;
                }
            }
        }

        protected override void UpdateDrawable()
        {
            float v =  -NoteHeight / 2;
            Position = new Vector2(0, v);
        }
    }
}
