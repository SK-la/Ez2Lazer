// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Game.EzOsuGame.Configuration;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    internal partial class EzHoldNoteHittingLayer : EzNoteBase
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
        public IBindable<double> HitPosition = null!;

        private TextureAnimation? animation;

        public EzHoldNoteHittingLayer()
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.Centre;
            RelativeSizeAxes = Axes.None;
            Blending = additive_preserve_alpha;
        }

        [BackgroundDependencyLoader]
        private void load(IEzSkinInfo ezSkinInfo)
        {
            HitPosition = ezSkinInfo.HitPosition;
            HitPosition.BindValueChanged(_ => OnDrawableChanged());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Scheduler.AddOnce(OnDrawableChanged);

            if (animation == null)
                UpdateTexture();

            IsHitting.BindValueChanged(hitting =>
            {
                ClearTransforms();

                if (hitting.NewValue && animation.IsNotNull() && animation.FrameCount > 0)
                {
                    Alpha = 1;
                    animation.Restart();
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
                    if (animation.FrameCount > 0)
                    {
                        animation.Loop = true;
                        AddInternal(animation);
                        break;
                    }

                    animation.Dispose();
                }
            }
        }

        protected override void UpdateDrawable()
        {
            float v = -(float)HitPosition.Value - NoteHeight / 2;
            Position = new Vector2(0, v);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                HitPosition.UnbindBindings();
            }

            base.Dispose(isDisposing);

            animation = null;
        }
    }
}
