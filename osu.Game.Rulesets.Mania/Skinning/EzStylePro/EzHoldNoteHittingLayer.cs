// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Game.Screens;
using osu.Game.Screens.LAsEzExtensions;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteHittingLayer : EzNoteBase
    {
        protected override bool BoolUpdateColor => false;
        public readonly Bindable<bool> IsHitting = new Bindable<bool>();
        private TextureAnimation? animation;

        public IBindable<double> HitPosition { get; set; } = new Bindable<double>();

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        public EzHoldNoteHittingLayer()
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.Centre;
            RelativeSizeAxes = Axes.Both;
            Blending = BlendingParameters.Additive;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            HitPosition = EZSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            HitPosition.BindValueChanged(_ => UpdateSize(), true);
            IsHitting.BindValueChanged(hitting =>
            {
                ClearTransforms();
                // Logger.Log($"IsHitting changed to: {hitting.NewValue}", LoggingTarget.Runtime, LogLevel.Debug);
                // animation.IsPlaying = hitting.NewValue;

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
            string[] componentsToTry = { "longnoteflare", "noteflaregood", "noteflare" };

            foreach (string component in componentsToTry)
            {
                animation = factory.CreateAnimation(component);

                if (animation.FrameCount > 0)
                {
                    animation.Loop = true;
                    AddInternal(animation);
                    UpdateSize();
                }
                else
                {
                    animation.Dispose();
                    UpdateColor();
                    return;
                }
            }
        }

        protected override void UpdateSize()
        {
            base.UpdateSize();
            float v = NoteSize.Value.Y / 2;
            Position = new Vector2(0, -(float)HitPosition.Value - v);
        }
    }
}
