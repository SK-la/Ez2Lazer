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

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    /// <summary>
    /// head：固定半颗 note 下半圆角；top：固定整颗 note 裁切上半 + 外层区域高度；body 按 top 区域半高与 head 计算。
    /// </summary>
    public partial class SbIHoldBodyPiece : FastNoteBase, IHoldNoteBody
    {
        private const float top_height_const = 50f;
        private const float top_height_ratio_min = 0.05f;
        private const float top_height_ratio_max = 0.15f;

        private IBindable<double> tailAlpha = null!;
        private IBindable<double> tailMaskHeight = null!;

        private Container? topClipContainer;
        private Container topNoteClipContainer = null!;
        private Container topInnerContainer = null!;
        private Container bodyContainer = null!;
        private Container headClipContainer = null!;
        private Container headInnerContainer = null!;

        private float noteHeight;
        private float halfNoteHeight;
        private float lastBodyContainerHeight = float.NaN;
        private float lastTopContainerY = float.NaN;
        private float lastTopRegionHeight = float.NaN;
        private float cachedTailMaskHeight = float.NaN;
        private bool lnGradient;

        public SbIHoldBodyPiece()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader(true)]
        private void load(IEzSkinInfo ezSkinInfo, Ez2ConfigManager ezConfig)
        {
            lnGradient = ezConfig.Get<bool>(Ez2Setting.ManiaLNGradientEnable);
            tailMaskHeight = ezSkinInfo.HoldTailMaskHeight;
            tailAlpha = ezSkinInfo.HoldTailAlpha;

            tailMaskHeight.BindValueChanged(onTailMaskHeightChanged, true);
            tailAlpha.BindValueChanged(onTailAlphaChanged, true);

            ezSkinInfo.ManiaLNGradientEnable.BindValueChanged(e =>
            {
                if (lnGradient == e.NewValue)
                    return;

                lnGradient = e.NewValue;
                UpdateLoad();
            });
        }

        protected override void UpdateLoad()
        {
            MainContainer.Clear();
            MainContainer.RelativeSizeAxes = Axes.Both;
            MainContainer.Anchor = Anchor.BottomCentre;
            MainContainer.Origin = Anchor.BottomCentre;

            topClipContainer = null;

            if (lnGradient)
            {
                topClipContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Masking = false,
                    Child = topNoteClipContainer = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Masking = true,
                        Child = topInnerContainer = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Masking = true,
                            Child = new Box { RelativeSizeAxes = Axes.Both },
                        }
                    }
                };
            }

            headClipContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Masking = true,
                Child = headInnerContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Masking = true,
                    Child = new Box { RelativeSizeAxes = Axes.Both },
                }
            };

            bodyContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Masking = true,
                Child = new Box { RelativeSizeAxes = Axes.Both },
            };

            MainContainer.Children = lnGradient
                ? [topClipContainer!, bodyContainer, headClipContainer]
                : [bodyContainer, headClipContainer];

            resetLayoutCache();
            Schedule(() =>
            {
                UpdateDrawable();
                UpdateColor();
            });
        }

        protected override void UpdateDrawable()
        {
            if (headInnerContainer == null)
                return;

            if (DrawWidth <= 1)
            {
                Schedule(UpdateDrawable);
                return;
            }

            noteHeight = UnitHeight;
            halfNoteHeight = noteHeight * 0.5f;

            updateHeadLayout();

            float moveDown = lnGradient ? getTailMaskHeight() : 0;

            refreshTopAndBodyLayout(moveDown);
        }

        protected override void UpdateColor()
        {
            if (headInnerContainer == null)
                return;

            headInnerContainer.Colour = NoteColor;
            bodyContainer.Colour = NoteColor;

            if (!lnGradient || topClipContainer == null)
                return;

            topInnerContainer.Colour = ColourInfo.GradientVertical(
                NoteColor.Opacity((float)tailAlpha.Value),
                NoteColor);
        }

        protected override void Update()
        {
            base.Update();

            if (headInnerContainer == null || DrawWidth <= 1)
                return;

            noteHeight = UnitHeight;
            halfNoteHeight = noteHeight * 0.5f;

            if (halfNoteHeight <= 0)
                return;

            updateHeadLayout();

            float moveDown = lnGradient ? getTailMaskHeight() : 0;

            refreshTopAndBodyLayout(moveDown);
        }

        private void onTailAlphaChanged(ValueChangedEvent<double> _)
        {
            if (!lnGradient)
                return;

            lastTopRegionHeight = float.NaN;
            resetLayoutCache();
            UpdateColor();
            Schedule(refreshTopLayout);
        }

        private void refreshTopLayout()
        {
            if (headInnerContainer == null)
                return;

            if (DrawWidth <= 1)
            {
                Schedule(refreshTopLayout);
                return;
            }

            noteHeight = UnitHeight;
            halfNoteHeight = noteHeight * 0.5f;
            updateHeadLayout();
            refreshTopAndBodyLayout(lnGradient ? getTailMaskHeight() : 0);
        }

        private void refreshTopAndBodyLayout(float moveDown)
        {
            if (lnGradient && topClipContainer != null)
            {
                float topRegionHeight = getTopRegionHeight();

                if (layoutChanged(lastTopRegionHeight, topRegionHeight))
                    lastTopRegionHeight = topRegionHeight;

                updateTopNoteLayout(topRegionHeight);
                updateTopContainerLayout(moveDown, topRegionHeight);
            }

            updateBodyLayout(moveDown);
        }

        /// <summary>
        /// 与 <see cref="SbIHoldNoteHeadPiece"/> 一致：半高裁切，内层整颗 note 底对齐。
        /// </summary>
        private void updateHeadLayout()
        {
            headClipContainer.Height = halfNoteHeight;
            headInnerContainer.Height = noteHeight;
            headInnerContainer.Y = 0;
            headInnerContainer.CornerRadius = (float)CornerRadiusBindable.Value;
        }

        /// <summary>
        /// 使用 top 区域固定高度（非 note 高度）：内层为区域高，裁切半高露出顶部圆角。
        /// </summary>
        private void updateTopNoteLayout(float topRegionHeight)
        {
            float topHalfHeight = topRegionHeight * 0.5f;
            topNoteClipContainer.Height = topHalfHeight;
            topInnerContainer.Height = topRegionHeight;
            topInnerContainer.Y = 0;
            topInnerContainer.CornerRadius = (float)CornerRadiusBindable.Value;
        }

        /// <summary>
        /// top 外层区域高度：max(总高 × [5%,15%](tailAlpha), 50)。body 按此高度的半高参与计算。
        /// </summary>
        private float getTopRegionHeight()
        {
            float t = (float)Math.Clamp(tailAlpha.Value, 0, 1);
            float ratio = top_height_ratio_min + (top_height_ratio_max - top_height_ratio_min) * t;
            float percentHeight = DrawHeight * ratio;
            return Math.Max(percentHeight, top_height_const);
        }

        private float getTopLayoutReserveHeight() => getTopRegionHeight() * 0.5f;

        /// <summary>
        /// 与 <see cref="EzStylePro.EzHoldNoteMiddle.updateTopContainerLayout"/> 一致，作用于外层区域。
        /// </summary>
        private void updateTopContainerLayout(float maskHeight, float topRegionHeight)
        {
            if (topClipContainer == null)
                return;

            topClipContainer.Alpha = maskHeight >= 0 ? 1 : 0;

            if (maskHeight >= 0)
            {
                topClipContainer.Height = topRegionHeight;

                if (layoutChanged(lastTopContainerY, maskHeight))
                {
                    topClipContainer.Y = maskHeight;
                    lastTopContainerY = maskHeight;
                }

                topNoteClipContainer.Y = 0;
            }
            else
            {
                float extendedHeight = topRegionHeight - maskHeight;
                topClipContainer.Height = extendedHeight;
                topClipContainer.Y = topRegionHeight - extendedHeight;
                lastTopContainerY = topClipContainer.Y;
                topNoteClipContainer.Y = 0;
            }
        }

        private void updateBodyLayout(float moveDown)
        {
            float targetBodyHeight;

            if (!lnGradient)
            {
                targetBodyHeight = DrawHeight - halfNoteHeight;
            }
            else
            {
                float topHalfReserve = getTopLayoutReserveHeight();
                float middleHeight = Math.Max(DrawHeight - topHalfReserve - halfNoteHeight, halfNoteHeight);

                targetBodyHeight = middleHeight - moveDown;
            }

            if (layoutChanged(lastBodyContainerHeight, targetBodyHeight))
            {
                bodyContainer.Height = targetBodyHeight;
                lastBodyContainerHeight = targetBodyHeight;
            }

            if (layoutChanged(bodyContainer.Y, halfNoteHeight))
                bodyContainer.Y = -halfNoteHeight;
        }

        private void onTailMaskHeightChanged(ValueChangedEvent<double> height)
        {
            cachedTailMaskHeight = (float)height.NewValue;
            resetLayoutCache();
            UpdateDrawable();
        }

        private void resetLayoutCache()
        {
            lastBodyContainerHeight = float.NaN;
            lastTopContainerY = float.NaN;
            lastTopRegionHeight = float.NaN;
        }

        private float getTailMaskHeight() => float.IsNaN(cachedTailMaskHeight) ? 0 : cachedTailMaskHeight;

        private static bool layoutChanged(float oldValue, float newValue) =>
            float.IsNaN(oldValue) || MathF.Abs(oldValue - newValue) > 0.001f;

        public void Recycle() => ClearTransforms();
    }
}
