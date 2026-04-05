// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHitExplosion : EzNoteBase, IHitExplosion
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

        // public override bool RemoveWhenNotAlive => true;

        private TextureAnimation? primaryAnimation;
        private TextureAnimation? goodAnimation;

        public EzHitExplosion()
        {
            RelativeSizeAxes = Axes.Both;
            Blending = additive_preserve_alpha;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
        }

        protected override void UpdateTexture()
        {
            MainContainer?.Clear();

            primaryAnimation = Factory.CreateAnimation("noteflare", true);
            goodAnimation = Factory.CreateAnimation("noteflaregood", true);

            if (primaryAnimation != null)
                MainContainer?.Add(primaryAnimation);

            if (goodAnimation != null)
            {
                goodAnimation.Alpha = 0;
                MainContainer?.Add(goodAnimation);
            }
        }

        protected override void UpdateDrawable()
        {
            float moveY = NoteSizeBindable.Value.Y / 2;
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

            if (result.Type >= HitResult.Great && goodAnimation?.FrameCount > 0)
            {
                goodAnimation.Alpha = 1;
                goodAnimation.GotoFrame(0);
                // goodAnimation.Restart();
            }
        }
    }
}
