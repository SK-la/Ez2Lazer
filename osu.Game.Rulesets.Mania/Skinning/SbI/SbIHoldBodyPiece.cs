// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbIHoldBodyPiece : FastNoteBase, IHoldNoteBody
    {
        private IBindable<double> tailAlpha = null!;
        private IBindable<double> tailMaskHeight = null!;

        private Container? topContainer;
        private Container? bodyContainer;

        private float tailHeight;
        private float cachedTailMaskHeight = float.NaN;
        private float lastBodyContainerHeight = float.NaN;
        private float lastTopContainerY = float.NaN;
        private bool lnGradient;

        public SbIHoldBodyPiece()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader(true)]
        private void load(IEzSkinInfo ezSkinInfo, Ez2ConfigManager ezConfig)
        {
            lnGradient = ezConfig.Get<bool>(Ez2Setting.ManiaLNGradientEnable);

            if (lnGradient)
            {
                tailMaskHeight = ezSkinInfo.HoldTailMaskHeight;
                tailAlpha = ezSkinInfo.HoldTailAlpha;
                tailMaskHeight.BindValueChanged(e =>
                {
                    cachedTailMaskHeight = (float)e.NewValue;
                    UpdateDrawable();
                }, true);
                tailAlpha.BindValueChanged(_ => UpdateColor(), true);
            }
        }

        protected override void UpdateLoad()
        {
            if (!lnGradient)
            {
                MainContainer.Clear();
                MainContainer.RelativeSizeAxes = Axes.X;
                MainContainer.Anchor = Anchor.TopCentre;
                MainContainer.Origin = Anchor.TopCentre;
                MainContainer.Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                };

                topContainer = null;
                bodyContainer = null;
                resetLayoutCache();
                return;
            }

            topContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
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
                    Colour = Color4.White,
                }
            };

            MainContainer.Clear();
            MainContainer.Children = [bodyContainer, topContainer];

            resetLayoutCache();
            Schedule(UpdateDrawable);
        }

        protected override void UpdateDrawable()
        {
            if (DrawWidth <= 1)
            {
                Schedule(UpdateDrawable);
                return;
            }

            float radius = (float)CornerRadiusBindable.Value;

            Masking = true;
            CornerRadius = radius;

            Y = UnitHeight;

            if (!lnGradient)
            {
                MainContainer.Height = DrawHeight + UnitHeight;
                return;
            }

            tailHeight = UnitHeight;
            float topHeight = Math.Max(tailHeight - cachedTailMaskHeight, 0);
            float topY = tailHeight - topHeight;

            if (topContainer != null)
            {
                topContainer.Alpha = cachedTailMaskHeight >= 0 ? 1 : 0;
                topContainer.Height = topHeight;
                topContainer.Y = topY;
            }

            if (bodyContainer != null)
                bodyContainer.Height = Math.Max(DrawHeight - tailHeight, 0);
        }

        protected override void UpdateColor()
        {
            if (!lnGradient)
            {
                MainContainer.Colour = NoteColor;
                return;
            }

            if (topContainer != null)
            {
                topContainer.Colour = ColourInfo.GradientVertical(
                    NoteColor.Opacity((float)tailAlpha.Value),
                    NoteColor);
            }

            if (bodyContainer != null)
                bodyContainer.Colour = NoteColor;
        }

        protected override void Update()
        {
            base.Update();

            if (!lnGradient || MainContainer.Children.Count == 0 || tailHeight <= 0)
                return;

            float maskHeight = float.IsNaN(cachedTailMaskHeight) ? 0 : cachedTailMaskHeight;
            float targetTopHeight = Math.Max(tailHeight - maskHeight, 0);
            float targetTopY = tailHeight - targetTopHeight;

            if (topContainer != null && layoutChanged(lastTopContainerY, targetTopY))
            {
                topContainer.Y = targetTopY;
                lastTopContainerY = targetTopY;
            }

            float drawHeightMinusHalf = DrawHeight - tailHeight;
            float middleHeight = Math.Max(drawHeightMinusHalf, tailHeight);
            float targetBodyHeight = maskHeight >= 0
                ? middleHeight - maskHeight + 1
                : middleHeight + MathF.Abs(maskHeight) + 2;
            targetBodyHeight = Math.Max(targetBodyHeight, 0);

            if (bodyContainer != null && layoutChanged(lastBodyContainerHeight, targetBodyHeight))
            {
                bodyContainer.Height = targetBodyHeight;
                lastBodyContainerHeight = targetBodyHeight;
            }
        }

        private void resetLayoutCache()
        {
            lastBodyContainerHeight = float.NaN;
            lastTopContainerY = float.NaN;
        }

        public void Recycle()
        {
            ClearTransforms();
        }

        private static bool layoutChanged(float oldValue, float newValue) => float.IsNaN(oldValue) || MathF.Abs(oldValue - newValue) > 0.001f;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                topContainer?.Expire();
                bodyContainer?.Expire();
            }
        }
    }
}
