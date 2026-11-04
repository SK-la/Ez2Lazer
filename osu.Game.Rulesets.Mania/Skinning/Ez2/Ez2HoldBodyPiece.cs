// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    public partial class Ez2HoldBodyPiece : FastNoteBase, IHoldNoteBody
    {
        private static readonly ColourInfo idle_gradient = ColourInfo.GradientVertical(Colour4.White.Opacity(0.35f), Colour4.White.Opacity(1f));

        private Drawable background = null!;
        private Container tailContainer = null!;

        private Ez2HoldNoteHittingLayer hittingLayer = null!;

        public Ez2HoldBodyPiece()
        {
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
            Masking = true;
            Colour = idle_gradient;
        }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject? drawableObject)
        {
            MainContainer.Children = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Children = new[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.BottomCentre,
                            Height = 1f,
                            Alpha = 1,
                        },
                        tailContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            CornerRadius = 0,
                            Height = CornerRadius,
                            Masking = true,
                            // Colour = ColourInfo.GradientVertical(Color4.White.Opacity(1f), Color4.White.Opacity(0f)),
                            Child = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                // Colour = ColourInfo.GradientVertical(Color4.White.Opacity(1f), Color4.White.Opacity(1f)),
                            }
                        }
                    }
                },
            };

            hittingLayer = new Ez2HoldNoteHittingLayer(this);
            AddInternal(hittingLayer);

            if (drawableObject is DrawableHoldNote holdNote)
                ((IBindable<bool>)hittingLayer.IsHitting).BindTo(holdNote.IsHolding);
        }

        public void BeginHoldHighlight()
        {
            UpdateAppearance(Color4.White.Opacity(0.8f), Color4.Gray.Darken(0.3f).Opacity(0.5f), 0.9f);
        }

        public void EndHoldHighlight()
        {
            this.FadeColour(idle_gradient, 200, Easing.OutQuint);
            this.FadeTo(1f, 200, Easing.OutQuint);
        }

        protected override void Update()
        {
            base.Update();
            background.Height = 1f - DrawWidth / 2;
            tailContainer.CornerRadius = DrawWidth / 2;
            tailContainer.Height = DrawWidth / 2;
        }

        public void UpdateAppearance(Color4 startColour, Color4 endColour, float alpha)
        {
            this.FadeColour(ColourInfo.GradientVertical(startColour, endColour), 200, Easing.OutQuint);
            this.FadeTo(alpha, 200, Easing.OutQuint);
        }

        public void Recycle()
        {
            hittingLayer.Recycle();
            ClearTransforms(targetMember: nameof(Colour));
            ClearTransforms(targetMember: nameof(Alpha));
            Colour = idle_gradient;
            Alpha = 1;
        }
    }
}
