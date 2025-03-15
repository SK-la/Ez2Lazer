// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbIJudgementPiece : TextJudgementPiece, IAnimatableJudgement
    {
        private const float judgement_y_position = 140;

        private RingExplosion? ringExplosion;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private IBindable<ScrollingDirection> direction = null!;

        public SbIJudgementPiece(HitResult result)
            : base(result)
        {
            AutoSizeAxes = Axes.Both;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            direction = scrollingInfo.Direction.GetBoundCopy();
            direction.BindValueChanged(_ => onDirectionChanged(), true);

            if (Result.IsHit())
            {
                AddInternal(ringExplosion = new RingExplosion(Result)
                {
                    Colour = colours.ForHitResult(Result),
                });
            }
        }

        private void onDirectionChanged() => Y = direction.Value == ScrollingDirection.Up ? -judgement_y_position : judgement_y_position;

        protected override SpriteText CreateJudgementText() =>
            new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Blending = BlendingParameters.Additive,
                Spacing = new Vector2(10, 0),
                Font = OsuFont.Default.With(size: 28, weight: FontWeight.Regular),
            };

        public virtual void PlayAnimation()
        {
            switch (Result)
            {
                case HitResult.Miss:
                    JudgementText
                        .ScaleTo(Vector2.One)
                        .ScaleTo(new Vector2(1.3f), 1800, Easing.OutQuint);
                    this.MoveToY(judgement_y_position);

                    applyScaleAndFadeOutEffect(this, new Vector2(1.5f), 300, new Vector2(1.5f, 0.1f), 300, 300);
                    break;

                case HitResult.Meh:
                    JudgementText
                        .ScaleTo(Vector2.One)
                        .ScaleTo(new Vector2(1.3f), 1800, Easing.OutQuint);
                    this.MoveToY(judgement_y_position);

                    applyScaleAndFadeOutEffect(this, new Vector2(1.5f), 300, new Vector2(1.5f, 0.1f), 300, 300);
                    break;

                case HitResult.Ok:
                    JudgementText
                        .ScaleTo(Vector2.One)
                        .ScaleTo(new Vector2(1.3f), 1800, Easing.OutQuint);
                    this.MoveToY(judgement_y_position);

                    applyScaleAndFadeOutEffect(this, new Vector2(1.3f), 200, new Vector2(1.3f, 0.1f), 400, 400);
                    break;

                case HitResult.Good:
                    this.MoveToY(judgement_y_position);
                    JudgementText
                        .ScaleTo(Vector2.One)
                        .ScaleTo(new Vector2(1.3f), 1800, Easing.OutQuint);

                    applyScaleAndFadeOutEffect(this, new Vector2(1.3f), 200, new Vector2(1.3f, 0.1f), 400, 400);
                    break;

                case HitResult.Great:
                    JudgementText
                        .ScaleTo(Vector2.One)
                        .ScaleTo(new Vector2(1.5f), 1800, Easing.OutQuint);

                    applyScaleAndFadeOutEffect(this, new Vector2(1.5f), 100, new Vector2(1.5f, 0.1f), 500, 500);
                    break;

                case HitResult.Perfect:
                    JudgementText
                        .ScaleTo(Vector2.One)
                        .ScaleTo(new Vector2(1.5f), 1800, Easing.OutQuint);

                    applyScaleAndFadeOutEffect(this, new Vector2(1.5f), 100, new Vector2(1.5f, 0.1f), 500, 500);
                    break;
            }

            this.FadeOutFromOne(800);

            ringExplosion?.PlayAnimation();
        }

        public Drawable? GetAboveHitObjectsProxiedContent() => null;

        private void applyScaleAndFadeOutEffect(Drawable drawable, Vector2 scaleUp, double scaleUpDuration, Vector2 scaleDown, double scaleDownDuration, double fadeOutDuration)
        {
            drawable.ScaleTo(scaleUp, scaleUpDuration, Easing.OutQuint).Then()
                    .ScaleTo(scaleDown, scaleDownDuration, Easing.InQuint)
                    .FadeOut(fadeOutDuration, Easing.InQuint);
        }

        private partial class RingExplosion : CompositeDrawable
        {
            public RingExplosion(HitResult result)
            {
                const float thickness = 4;

                const float small_size = 9;
                const float large_size = 14;

                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;

                Blending = BlendingParameters.Additive;

                int countSmall = 0;
                int countLarge = 0;

                switch (result)
                {
                    case HitResult.Meh:
                        countSmall = 3;
                        break;

                    case HitResult.Ok:
                    case HitResult.Good:
                        countSmall = 4;
                        break;

                    case HitResult.Great:
                    case HitResult.Perfect:
                        countSmall = 4;
                        countLarge = 4;
                        break;
                }

                for (int i = 0; i < countSmall; i++)
                    AddInternal(new RingPiece(thickness) { Size = new Vector2(small_size) });

                for (int i = 0; i < countLarge; i++)
                    AddInternal(new RingPiece(thickness) { Size = new Vector2(large_size) });
            }

            public void PlayAnimation()
            {
                this.FadeOutFromOne(1000, Easing.OutQuint);
            }

            public partial class RingPiece : CircularContainer
            {
                public RingPiece(float thickness = 9)
                {
                    Anchor = Anchor.Centre;
                    Origin = Anchor.Centre;

                    Masking = true;
                    BorderThickness = thickness;
                    BorderColour = Color4.White;

                    Child = new Box
                    {
                        AlwaysPresent = true,
                        Alpha = 0,
                        RelativeSizeAxes = Axes.Both
                    };
                }
            }
        }
    }
}
