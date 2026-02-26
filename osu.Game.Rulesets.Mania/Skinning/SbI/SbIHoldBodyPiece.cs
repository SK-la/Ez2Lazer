// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Mania.Skinning.EzStylePro;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbIHoldBodyPiece : EzNoteBase, IHoldNoteBody
    {
        private IBindable<double> tailMaskHeight = null!;

        private Container? topContainer;
        private Container? bodyContainer;

        private float tailHeight;

        public SbIHoldBodyPiece()
        {
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
            Masking = true;
            CornerRadius = 0;
        }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject? drawableObject)
        {
            if (MainContainer != null)
            {
                MainContainer.Children = new[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.White,
                    },
                };
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            tailMaskHeight = Column.EzSkinInfo.HoldTailMaskHeight;
            tailMaskHeight.BindValueChanged(_ => UpdateSize(), true);
        }

        protected override void OnDrawableChanged()
        {
            topContainer?.Expire();
            bodyContainer?.Expire();

            topContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                }
            };
            bodyContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };

            if (MainContainer != null)
            {
                MainContainer.Clear();
                MainContainer.Children = [bodyContainer, topContainer];
            }

            Schedule(UpdateSize);
        }

        protected override void UpdateSize()
        {
            base.UpdateSize();
            tailHeight = (float)tailMaskHeight.Value;

            if (topContainer != null)
            {
                topContainer.Y = tailHeight > 0
                    ? tailHeight
                    : 0;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (MainContainer?.Children.Count > 0 && bodyContainer != null)
            {
                float drawHeightMinusHalf = DrawHeight - tailHeight;
                float middleHeight = Math.Max(drawHeightMinusHalf, tailHeight);

                bodyContainer.Height = tailHeight > 0
                    ? middleHeight - tailHeight + 1
                    : middleHeight + 1;

                bodyContainer.Scale = new Vector2(1, drawHeightMinusHalf);
            }
        }

        public void Recycle()
        {
        }
    }
}
