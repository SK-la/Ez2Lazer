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
using osu.Game.Rulesets.Mania.Skinning.EzStylePro;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbIHoldBodyPiece : EzNoteBase, IHoldNoteBody
    {
        protected override bool UseColorization => true;
        protected readonly IBindable<double> CornerRadiusBindable = new Bindable<double>();

        private IBindable<double> tailAlpha = null!;
        private IBindable<double> tailMaskHeight = null!;

        private Container? topContainer;
        private Container? bodyContainer;
        private Container? bodyInnerContainer;

        private float tailHeight;
        private float cachedTailMaskHeight = float.NaN;
        private float lastBodyContainerHeight = float.NaN;
        private float lastTopContainerY = float.NaN;
        private bool advancedMode;

        public SbIHoldBodyPiece()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader(true)]
        private void load(IEzSkinInfo ezSkinInfo, Ez2ConfigManager ezConfig)
        {
            advancedMode = ezConfig.Get<bool>(Ez2Setting.ManiaLNGradientEnable);
            CornerRadiusBindable.BindTo(ezSkinInfo.NoteCornerRadius);
            RelativeSizeAxes = Axes.Both;

            if (advancedMode)
            {
                tailMaskHeight = ezSkinInfo.HoldTailMaskHeight;
                tailAlpha = ezSkinInfo.HoldTailAlpha;
                tailMaskHeight.BindValueChanged(e =>
                {
                    cachedTailMaskHeight = (float)e.NewValue;
                    UpdateDrawable();
                }, true);
                tailAlpha.BindValueChanged(_ => UpdateDrawable());
            }

            UpdateTexture();

            CornerRadiusBindable.BindValueChanged(_ => UpdateDrawable(), true);
        }

        protected override void UpdateTexture()
        {
            if (!advancedMode)
            {
                if (MainContainer != null)
                {
                    MainContainer.Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    };
                }

                bodyInnerContainer = null;

                return;
            }

            topContainer?.Expire();
            bodyContainer?.Expire();

            topContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Masking = true,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.X,
                    }
                }
            };

            bodyContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Masking = true,
                Child = bodyInnerContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    }
                }
            };

            if (MainContainer != null)
            {
                MainContainer.Clear();
                MainContainer.Children = [bodyContainer, topContainer];
            }

            resetLayoutCache();
            // 立即刷新 + 下一帧刷新，确保 HoldNote 布局稳定
            Schedule(UpdateDrawable);

            UpdateColor();
        }

        protected override void UpdateDrawable()
        {
            if (advancedMode)
            {
                tailHeight = NoteSizeBindable.Value.Y * 0.5f;

                // 当设置为负值时，隐藏 topContainer；非负值时显示
                if (topContainer != null)
                {
                    topContainer.Alpha = tailMaskHeight.Value >= 0 ? 1 : 0;
                    topContainer.CornerRadius = (float)CornerRadiusBindable.Value;
                }

                if (topContainer?.Child is Container topInner)
                {
                    topContainer.Height = tailHeight;
                    topInner.Height = tailHeight * 2;
                    topContainer.Y = tailMaskHeight.Value > 0
                        ? (float)tailMaskHeight.Value
                        : 0;
                }

                if (bodyInnerContainer != null)
                {
                    bodyInnerContainer.Height = tailHeight * 2;
                    bodyInnerContainer.Y = -tailHeight;
                }
            }
            else
            {
                Masking = CornerRadiusBindable.Value > 0;
                CornerRadius = (float)CornerRadiusBindable.Value;
            }

            float noteHeight = NoteSizeBindable.Value.Y;
            Y = noteHeight;
            UpdateColor();
        }

        protected override void UpdateColor()
        {
            if (!advancedMode)
            {
                base.UpdateColor();
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

            if (!advancedMode || MainContainer?.Children.Count == 0)
                return;

            float targetTopY = cachedTailMaskHeight > 0 ? cachedTailMaskHeight : 0;

            if (topContainer != null && layoutChanged(lastTopContainerY, targetTopY))
            {
                topContainer.Y = targetTopY;
                lastTopContainerY = targetTopY;
            }

            float drawHeightMinusHalf = DrawHeight - tailHeight;
            float middleHeight = Math.Max(drawHeightMinusHalf, tailHeight);
            float targetBodyHeight = cachedTailMaskHeight >= 0
                ? middleHeight - cachedTailMaskHeight + 1
                : middleHeight + MathF.Abs(cachedTailMaskHeight) + 2;

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
