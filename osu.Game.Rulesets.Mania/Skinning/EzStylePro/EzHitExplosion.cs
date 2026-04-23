// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    internal partial class EzHitExplosion : EzNoteBase, IHitExplosion
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

        // 不要启用，此功能会直接释放预加载纹理，导致预加载白玩。
        // public override bool RemoveWhenNotAlive => true;

        private TextureAnimation? primaryAnimation;
        private TextureAnimation? goodAnimation;

        public EzHitExplosion()
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
            RelativeSizeAxes = Axes.Both;
            Blending = additive_preserve_alpha;
        }

        protected override void UpdateTexture()
        {
            primaryAnimation = Factory.CreateAnimation("noteflare", true);
            goodAnimation = Factory.CreateAnimation("noteflaregood", true);

            if (primaryAnimation != null)
                MainContainer.Add(primaryAnimation);

            if (goodAnimation != null)
            {
                goodAnimation.Alpha = 0;
                MainContainer.Add(goodAnimation);
            }
        }

        protected override void UpdateDrawable()
        {
            float moveY = NoteHeight / 2;
            // baseYPosition = LegacyManiaSkinConfiguration.DEFAULT_HIT_POSITION - (float)hitPosition.Value - moveY;
            Position = new Vector2(0, -moveY);
        }

        public void Animate(JudgementResult result)
        {
            if (primaryAnimation?.FrameCount > 0)
            {
                primaryAnimation.GotoFrame(0);
                // primaryAnimation.Restart();
            }

            if (goodAnimation?.FrameCount > 0 && result.Type >= HitResult.Great)
            {
                goodAnimation.Alpha = 1;
                goodAnimation.GotoFrame(0);
                // goodAnimation.Restart();
            }
        }
    }
}
