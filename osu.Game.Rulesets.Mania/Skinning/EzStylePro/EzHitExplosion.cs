// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.LAsEzExtensions;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHitExplosion : EzNoteBase, IHitExplosion
    {
        protected override bool BoolUpdateColor => false;

        // public override bool RemoveWhenNotAlive => true;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        private TextureAnimation? primaryAnimation;
        private TextureAnimation? goodAnimation;
        private bool animationsCreated;

        public EzHitExplosion()
        {
            RelativeSizeAxes = Axes.Both;
            Blending = BlendingParameters.Additive;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
            OnDrawableChanged();
        }

        protected override void OnDrawableChanged()
        {
            ClearInternal();
            primaryAnimation = null;
            goodAnimation = null;

            primaryAnimation = factory.CreateAnimation("noteflare");
            goodAnimation = factory.CreateAnimation("noteflaregood");

            if (primaryAnimation?.FrameCount > 0)
                AddInternal(primaryAnimation);

            if (goodAnimation?.FrameCount > 0)
            {
                goodAnimation.Alpha = 0;
                AddInternal(goodAnimation);
            }

            animationsCreated = true;
        }

        protected override void UpdateSize()
        {
            base.UpdateSize();
            float moveY = NoteSize.Value.Y / 2;
            // baseYPosition = LegacyManiaSkinConfiguration.DEFAULT_HIT_POSITION - (float)hitPosition.Value - moveY;
            Position = new Vector2(0, -moveY);
        }

        public void Animate(JudgementResult result)
        {
            if (!animationsCreated) OnDrawableChanged();

            if (primaryAnimation?.FrameCount > 0)
            {
                primaryAnimation.Alpha = 1;
                primaryAnimation.GotoFrame(0);
                primaryAnimation.Restart();
            }

            if (result.Type >= HitResult.Great && goodAnimation?.FrameCount > 0)
            {
                goodAnimation.Alpha = 1;
                goodAnimation.GotoFrame(0);
                goodAnimation.Restart();
            }
        }
    }
}
