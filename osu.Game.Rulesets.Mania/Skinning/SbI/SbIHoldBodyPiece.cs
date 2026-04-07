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
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Mania.Skinning.EzStylePro;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbIHoldBodyPiece : EzNoteBase, IHoldNoteBody
    {
        protected override bool UseColorization => true;
        protected readonly IBindable<double> CornerRadiusBindable = new Bindable<double>();

        private IBindable<bool> isHitting = null!;
        private IBindable<double> tailAlpha = null!;
        private IBindable<double> tailMaskHeight = null!;

        private Container? topContainer;
        private Container? bodyContainer;
        private Box box = null!;

        private DrawableHoldNote holdNote = null!;
        private Drawable? lightContainer;
        private SbIHoldNoteHittingLayer? hittingLayer;

        private float tailHeight;

        private float lastBodyContainerHeight = float.NaN;
        private float lastBodyScaleY = float.NaN;
        private float lastTopContainerY = float.NaN;
        private bool advancedMode;

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject drawableObject, IEzSkinInfo ezSkinInfo, Ez2ConfigManager ezConfig)
        {
            advancedMode = ezConfig.Get<bool>(Ez2Setting.ManiaLNGradientEnable);
            CornerRadiusBindable.BindTo(ezSkinInfo.NoteCornerRadius);
            Masking = CornerRadiusBindable.Value > 0;
            RelativeSizeAxes = Axes.Both;

            if (advancedMode)
            {
                tailMaskHeight = ezSkinInfo.HoldTailMaskHeight;
                tailAlpha = ezSkinInfo.HoldTailAlpha;
                tailMaskHeight.BindValueChanged(_ => UpdateDrawable());
                tailAlpha.BindValueChanged(_ => UpdateDrawable());
            }

            holdNote = (DrawableHoldNote)drawableObject;

            isHitting = holdNote.IsHolding;
            isHitting.BindValueChanged(onIsHittingChanged, true);

            UpdateTexture();

            if (lightContainer == null)
                OnLightChanged();

            CornerRadiusBindable.BindValueChanged(_ => UpdateDrawable(), true);
        }

        protected override void UpdateTexture()
        {
            // 清理之前的光效层和容器
            if (lightContainer != null)
            {
                if (lightContainer.Parent != null)
                    Column.TopLevelContainer.Remove(lightContainer, false);
                lightContainer.Expire();
                lightContainer = null;
            }

            if (hittingLayer != null)
            {
                hittingLayer.Expire();
                hittingLayer = null;
            }

            if (!advancedMode)
            {
                if (MainContainer != null)
                {
                    MainContainer.Child = box = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    };
                }

                return;
            }

            topContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = NoteSizeBindable.Value.Y * 0.5f,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Masking = true,
                Child = box = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };

            bodyContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = NoteSizeBindable.Value.Y,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Masking = true,
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

            OnLightChanged();
            resetLayoutCache();
            // 立即刷新 + 下一帧刷新，确保 HoldNote 布局稳定
            Schedule(UpdateDrawable);

            UpdateColor();
        }

        protected override void UpdateDrawable()
        {
            // 非高级模式：不触碰 top/body 布局，仅更新颜色（simpleBody）
            if (!advancedMode)
            {
                UpdateColor();
                return;
            }

            tailHeight = NoteSizeBindable.Value.Y * 0.5f;

            // 当设置为负值时，隐藏 topContainer；非负值时显示
            if (topContainer != null)
            {
                topContainer.Alpha = tailMaskHeight.Value >= 0 ? 1 : 0;
                topContainer.CornerRadius = (float)CornerRadiusBindable.Value;
                topContainer.Height = tailHeight;
                box.Height = tailHeight * 2;
                topContainer.Y = tailMaskHeight.Value > 0
                    ? (float)tailMaskHeight.Value
                    : 0;
            }

            if (bodyContainer != null)
            {
                bodyContainer.Height = tailHeight * 2;
                // 将 body 的底部向下调整半个尾部高度，使其底部对齐到 Head 的中部（而非 Head 的顶部）。
                bodyContainer.Y = -tailHeight * 0.5f;
            }

            CornerRadius = (float)CornerRadiusBindable.Value;
            float noteHeight = NoteSizeBindable.Value.Y;
            Y = noteHeight;
        }

        protected override void UpdateColor()
        {
            base.UpdateColor();

            if (!advancedMode)
            {
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

            if (advancedMode && MainContainer?.Children.Count > 0 && bodyContainer != null && topContainer != null && tailHeight > 0)
            {
                float targetTopY = tailMaskHeight.Value > 0
                    ? (float)tailMaskHeight.Value
                    : 0;

                if (topContainer != null && layoutChanged(lastTopContainerY, targetTopY))
                {
                    topContainer.Y = targetTopY;
                    lastTopContainerY = targetTopY;
                }

                float drawHeightMinusHalf = DrawHeight - tailHeight;
                float middleHeight = Math.Max(drawHeightMinusHalf, tailHeight);

                // 当 maskHeight 为正值时，缩短中间部分并下移 top；为负值时，延长中间部分实现上移效果
                float targetBodyHeight = tailMaskHeight.Value >= 0
                    ? middleHeight - (float)tailMaskHeight.Value + 1
                    : middleHeight + MathF.Abs((float)tailMaskHeight.Value) + 2;

                if (layoutChanged(lastBodyContainerHeight, targetBodyHeight))
                {
                    bodyContainer.Height = targetBodyHeight;
                    lastBodyContainerHeight = targetBodyHeight;
                }

                if (bodyContainer != null && layoutChanged(lastBodyScaleY, drawHeightMinusHalf))
                {
                    bodyContainer.Scale = new Vector2(1, drawHeightMinusHalf);
                    lastBodyScaleY = drawHeightMinusHalf;
                }
            }
        }

        private void resetLayoutCache()
        {
            lastBodyContainerHeight = float.NaN;
            lastBodyScaleY = float.NaN;
            lastTopContainerY = float.NaN;
        }

        private void OnLightChanged()
        {
            if (lightContainer != null)
            {
                Column.TopLevelContainer.Remove(lightContainer, false);
                lightContainer.Expire();
                lightContainer = null;
            }

            hittingLayer = new SbIHoldNoteHittingLayer
            {
                Alpha = 0
            };

            lightContainer = new HitTargetInsetContainer
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                Child = hittingLayer
            };

            ((IBindable<bool>)hittingLayer.IsHitting).BindTo(isHitting);
        }

        private void onIsHittingChanged(ValueChangedEvent<bool> isHitting)
        {
            if (hittingLayer != null) hittingLayer.IsHitting.Value = isHitting.NewValue;

            if (lightContainer == null)
                return;

            if (isHitting.NewValue)
            {
                lightContainer.ClearTransforms();

                if (lightContainer.Parent == null)
                    Column.TopLevelContainer.Add(lightContainer);

                lightContainer.FadeIn(80);
            }
            else
            {
                lightContainer.FadeOut(120)
                              .OnComplete(d => Column.TopLevelContainer.Remove(d, false));
            }
        }

        public void Recycle()
        {
            ClearTransforms();
            hittingLayer?.Recycle();
        }

        private static bool layoutChanged(float oldValue, float newValue) => float.IsNaN(oldValue) || MathF.Abs(oldValue - newValue) > 0.001f;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                hittingLayer?.Expire();
                topContainer?.Expire();
                bodyContainer?.Expire();
                box.Expire();
            }
        }
    }
}
