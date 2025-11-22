// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteHittingLayer : EzNoteBase
    {
        protected override bool BoolUpdateColor => false;
        public readonly Bindable<bool> IsHitting = new Bindable<bool>();
        private TextureAnimation? animation;

        public IBindable<double> HitPosition { get; } = new Bindable<double>();

        public EzHoldNoteHittingLayer()
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.Centre;
            RelativeSizeAxes = Axes.None;
            Blending = BlendingParameters.Additive;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (animation == null)
                OnDrawableChanged();

            HitPosition.BindValueChanged(_ => UpdateSize(), true);
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

        protected override void OnDrawableChanged()
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
                        UpdateSize();
                        break;
                    }

                    animation.Dispose();
                }
            }

            if (animation == null || animation.FrameCount == 0)
            {
                UpdateColor();
            }
        }

        protected override void UpdateSize()
        {
            base.UpdateSize();
            float v = -(float)HitPosition.Value - NoteSize.Value.Y / 2;
            Position = new Vector2(0, v);
        }
    }
}
