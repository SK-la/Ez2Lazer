// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    public partial class Ez2JudgementPiece : EzJudgementText, IAnimatableJudgement
    {
        internal const float JUDGEMENT_Y_POSITION = 140;

        private RingExplosion? ringExplosion;

        // [Resolved]
        // public double TimeOffset { get; set; }

        public Ez2JudgementPiece(HitResult result)
            : base(result)
        {
            AutoSizeAxes = Axes.Both;
            Origin = Anchor.Centre;
            Y = JUDGEMENT_Y_POSITION;
        }

        public Ez2JudgementPiece()
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (Result.IsHit())
            {
                AddInternal(ringExplosion = new RingExplosion(Result)
                {
                    Colour = Color4.White,
                });
            }
            // updateOffsetText(Result);
        }

        protected override SpriteText CreateJudgementText() =>
            new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Colour = Color4.White,
                Blending = BlendingParameters.Additive,
                Spacing = new Vector2(2, 0),
                Font = OsuFont.Default.With(size: 22, weight: FontWeight.Regular),
                AllowMultiline = true,
            };

        /// <summary>
        /// Plays the default animation for this judgement piece.
        /// </summary>
        /// <remarks>
        /// The base implementation only handles fade (for all result types) and misses.
        /// Individual rulesets are recommended to implement their appropriate hit animations.
        /// </remarks>
        public virtual void PlayAnimation()
        {
            const float flash_speed = 60f; // 定义颜色闪烁速度变量

            switch (Result)
            {
                case HitResult.Miss:
                    JudgementText
                        .ScaleTo(Vector2.One)
                        .ScaleTo(new Vector2(1.3f), 1800, Easing.OutQuint);
                    this.MoveToY(JUDGEMENT_Y_POSITION);

                    applyFadeEffect(this, new[] { Color4.Red, Color4.IndianRed }, flash_speed);
                    applyScaleAndFadeOutEffect(this, new Vector2(1.5f), 300, new Vector2(1.5f, 0.1f), 300, 300);
                    break;

                case HitResult.Meh:
                    JudgementText
                        .ScaleTo(Vector2.One)
                        .ScaleTo(new Vector2(1.3f), 1800, Easing.OutQuint);
                    this.MoveToY(JUDGEMENT_Y_POSITION);

                    applyFadeEffect(this, new[] { Color4.Purple, Color4.MediumPurple }, flash_speed);
                    applyScaleAndFadeOutEffect(this, new Vector2(1.5f), 300, new Vector2(1.5f, 0.1f), 300, 300);
                    break;

                case HitResult.Ok:
                    JudgementText
                        .ScaleTo(Vector2.One)
                        .ScaleTo(new Vector2(1.3f), 1800, Easing.OutQuint);
                    this.MoveToY(JUDGEMENT_Y_POSITION);

                    applyFadeEffect(this, new[] { Color4.ForestGreen, Color4.SeaGreen }, flash_speed);
                    applyScaleAndFadeOutEffect(this, new Vector2(1.3f), 200, new Vector2(1.3f, 0.1f), 400, 400);
                    break;

                case HitResult.Good:
                    this.MoveToY(JUDGEMENT_Y_POSITION);
                    JudgementText
                        .ScaleTo(Vector2.One)
                        .ScaleTo(new Vector2(1.3f), 1800, Easing.OutQuint);

                    applyFadeEffect(this, new[] { Color4.Green, Color4.LightGreen }, flash_speed);
                    applyScaleAndFadeOutEffect(this, new Vector2(1.3f), 200, new Vector2(1.3f, 0.1f), 400, 400);
                    break;

                case HitResult.Great:
                    JudgementText
                        .ScaleTo(Vector2.One)
                        .ScaleTo(new Vector2(1.5f), 1800, Easing.OutQuint);

                    applyFadeEffect(this, new[] { Color4.AliceBlue, Color4.LightSkyBlue }, flash_speed);
                    applyScaleAndFadeOutEffect(this, new Vector2(1.5f), 100, new Vector2(1.5f, 0.1f), 500, 500);
                    break;

                case HitResult.Perfect:
                    JudgementText
                        .ScaleTo(Vector2.One)
                        .ScaleTo(new Vector2(1.5f), 1800, Easing.OutQuint);

                    applyFadeEffect(this, new[] { Color4.LightBlue, Color4.LightGreen }, flash_speed);
                    applyScaleAndFadeOutEffect(this, new Vector2(1.5f), 100, new Vector2(1.5f, 0.1f), 500, 500);
                    break;
            }

            ringExplosion?.PlayAnimation(flash_speed);
        }

        private void applyFadeEffect(Drawable drawable, Color4[] colors, double flashSpeed)
        {
            var sequence = drawable.FadeColour(colors[0], flashSpeed, Easing.OutQuint);

            for (int i = 1; i < colors.Length; i++)
            {
                sequence = sequence.Then().FadeColour(colors[i], flashSpeed, Easing.OutQuint);
            }

            sequence.Loop();
        }

        private void applyScaleAndFadeOutEffect(Drawable drawable, Vector2 scaleUp, double scaleUpDuration, Vector2 scaleDown, double scaleDownDuration, double fadeOutDuration)
        {
            drawable.ScaleTo(scaleUp, scaleUpDuration, Easing.OutQuint).Then()
                    .ScaleTo(scaleDown, scaleDownDuration, Easing.InQuint)
                    .FadeOut(fadeOutDuration, Easing.InQuint);
        }

        public class GlowEffect : IEffect<BufferedContainer>
        {
            public float Strength = 1f;
            public Vector2 BlurSigma = Vector2.One;
            public ColourInfo Colour = Color4.White;
            public bool PadExtent;

            public BufferedContainer ApplyTo(Drawable drawable)
            {
                return new BufferedContainer
                {
                    RelativeSizeAxes = Axes.Both, Child = drawable.WithEffect(new BlurEffect
                    {
                        Strength = Strength, Sigma = BlurSigma, Colour = Colour, PadExtent = PadExtent, DrawOriginal = true,
                    })
                };
            }
        }

        public BufferedContainer ApplyGlowEffect(Drawable drawable, Color4 glowColor)
        {
            var glowEffect = new GlowEffect { Colour = glowColor };
            return glowEffect.ApplyTo(drawable);
        }

        public Drawable? GetAboveHitObjectsProxiedContent() => null;

        private partial class RingExplosion : CompositeDrawable
        {
            private readonly float travel = 52;

            public RingExplosion(HitResult result)
            {
                const float thickness = 4;

                const float small_size = 6;
                const float large_size = 10;

                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;

                Blending = BlendingParameters.Additive;

                int countSmall = 0;
                int countLarge = 0;

                switch (result)
                {
                    case HitResult.Meh:
                        countSmall = 1;
                        travel *= 0.3f;
                        break;

                    case HitResult.Ok:
                    case HitResult.Good:
                        countSmall = 2;
                        travel *= 0.6f;
                        break;

                    case HitResult.Great:
                    case HitResult.Perfect:
                        countSmall = 2;
                        countLarge = 3;
                        break;
                }

                for (int i = 0; i < countSmall; i++)
                    AddInternal(new RingPiece(thickness) { Size = new Vector2(small_size) });

                for (int i = 0; i < countLarge; i++)
                    AddInternal(new RingPiece(thickness) { Size = new Vector2(large_size) });
            }

            public void PlayAnimation(float flashSpeed)
            {
                foreach (var c in InternalChildren)
                {
                    const float start_position_ratio = 0.3f;

                    float direction = RNG.NextSingle(0, 360);
                    float distance = RNG.NextSingle(travel / 2, travel);

                    c.MoveTo(new Vector2(
                        MathF.Cos(direction) * distance * start_position_ratio,
                        MathF.Sin(direction) * distance * start_position_ratio
                    ));

                    c.MoveTo(new Vector2(
                        MathF.Cos(direction) * distance,
                        MathF.Sin(direction) * distance
                    ), 600, Easing.OutQuint);
                }

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

// private ScoreProcessor processor => null!;
//
// protected override void LoadComplete()
// {
//     base.LoadComplete();
//     processor.NewJudgement += processorNewJudgement;
// }
//
// protected override void Dispose(bool isDisposing)
// {
//     base.Dispose(isDisposing);
//
//     if (true)
//         processor.NewJudgement -= processorNewJudgement;
// }
//
// private void processorNewJudgement(JudgementResult j) => Schedule(() => OnNewJudgement(j));
//
// private void updateOffsetText(HitResult result)
// {
//     if (result != HitResult.Perfect)
//     {
//         SpriteText offsetText = new OsuSpriteText
//         {
//             Anchor = TimeOffset < 0 ? Anchor.BottomLeft : Anchor.BottomRight,
//             Origin = Anchor.TopCentre,
//             Blending = BlendingParameters.Additive,
//             Font = OsuFont.Default.With(size: 10, weight: FontWeight.Regular),
//             Colour = Color4.White,
//             Text = TimeOffset.ToString("F1"),
//         };
//         AddInternal(offsetText);
//     }
// }
//
// protected void OnNewJudgement(JudgementResult judgement)
// {
//     if (!judgement.IsHit || judgement.HitObject.HitWindows?.WindowFor(HitResult.Miss) == 0)
//         return;
//
//     if (!judgement.Type.IsScorable() || judgement.Type.IsBonus())
//         return;
//
//     TimeOffset = judgement.TimeOffset;
// }
