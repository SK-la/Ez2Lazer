// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    public partial class Ez2HoldNoteHittingLayer : CompositeDrawable
    {
        public readonly Bindable<Color4> AccentColour = new Bindable<Color4>();
        public readonly Bindable<bool> IsHitting = new Bindable<bool>();

        private readonly Ez2HoldBodyPiece bodyPiece;

        public Ez2HoldNoteHittingLayer(Ez2HoldBodyPiece bodyPiece)
        {
            this.bodyPiece = bodyPiece;
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.TopCentre;
            Origin = Anchor.TopCentre;
            Blending = BlendingParameters.Mixture;
            Alpha = 0;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // AccentColour.BindValueChanged(colour =>
            // {
            //     Colour = colour.NewValue.Lighten(0.2f).Opacity(0.3f);
            // }, true);

            IsHitting.BindValueChanged(hitting =>
            {
                const float animation_length = 80;

                ClearTransforms();

                if (hitting.NewValue)
                {
                    double synchronisedOffset = animation_length * 2 - Time.Current % (animation_length * 2);

                    using (BeginDelayedSequence(synchronisedOffset))
                    {
                        this.FadeTo(1f, animation_length, Easing.OutSine).Then()
                            .FadeTo(0.6f, animation_length, Easing.InSine)
                            .Loop();
                        // Colour = ColourInfo.GradientVertical(AccentColour.Value.Opacity(1f), Color4.Gray.Opacity(0f));
                    }

                    this.FadeIn(animation_length);
                    bodyPiece.UpdateAppearance(Color4.White.Opacity(0.8f), Color4.Gray.Darken(0.3f).Opacity(0.5f), 0.9f);
                }
                else
                {
                    this.FadeOut(animation_length);
                    bodyPiece.UpdateAppearance(AccentColour.Value.Opacity(1f), AccentColour.Value.Opacity(1f), 1f);
                }
            }, true);
        }

        public void Recycle()
        {
            ClearTransforms();
            Alpha = 0;
        }
    }
}
