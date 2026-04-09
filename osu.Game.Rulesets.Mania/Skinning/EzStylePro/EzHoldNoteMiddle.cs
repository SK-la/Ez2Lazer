// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteMiddle : EzNoteBase, IHoldNoteBody
    {
        protected override bool UseColorization => true;
        protected override bool ShowSeparators => true;

        private IBindable<bool> isHitting = null!;
        private IBindable<double> tailAlpha = null!;
        private IBindable<double> tailMaskHeight = null!;

        private Container? topContainer;
        private Container? bodyContainer;
        private Container? bodyScaleContainer;
        private Container? bodyInnerContainer;

        private DrawableHoldNote holdNote = null!;
        private Drawable? lightContainer;
        private EzHoldNoteHittingLayer? hittingLayer;

        private float tailHeight;
        private float lastBodyContainerHeight = float.NaN;
        private float lastBodyScaleY = float.NaN;
        private float lastTopContainerY = float.NaN;
        private float cachedTailMaskHeight = float.NaN;

        // private bool lnGradient;

        public EzHoldNoteMiddle()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject drawableObject, IEzSkinInfo ezSkinInfo, Ez2ConfigManager ezConfig)
        {
            // lnGradient = ezConfig.Get<bool>(Ez2Setting.ManiaLNGradientEnable);

            // 以后改完Tail再恢复判断
            // if (lnGradient)
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

            holdNote = (DrawableHoldNote)drawableObject;

            isHitting = holdNote.IsHolding;
            isHitting.BindValueChanged(onIsHittingChanged, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            UpdateDrawable();
            // 确保光效层被正确初始化
            if (lightContainer == null)
                OnLightChanged();
        }

        private void OnLightChanged()
        {
            if (lightContainer != null)
            {
                Column.TopLevelContainer.Remove(lightContainer, false);
                lightContainer.Expire();
                lightContainer = null;
            }

            hittingLayer = new EzHoldNoteHittingLayer
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

            var body = Factory.CreateAnimation($"{ColorPrefix}longnote/middle");
            var tail = Factory.CreateAnimation($"{ColorPrefix}longnote/tail");

            string newComponentName = $"{ColorPrefix}note";
            if (body.FrameCount == 0)
                body = Factory.CreateAnimation(newComponentName);

            if (tail.FrameCount == 0)
                tail = Factory.CreateAnimation(newComponentName);

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
                    Child = tail
                }
            };
            bodyContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Masking = true,
                Child = bodyScaleContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 1,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Child = bodyInnerContainer = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Child = body
                    }
                }
            };

            if (MainContainer != null)
            {
                MainContainer.Clear();
                MainContainer.Children = [bodyContainer, topContainer];
            }

            // 重新初始化光效层
            OnLightChanged();
            resetLayoutCache();
            // 立即刷新 + 下一帧刷新，确保 HoldNote 布局稳定
            Schedule(UpdateDrawable);

            UpdateColor();
        }

        protected override void UpdateDrawable()
        {
            tailHeight = NoteSizeBindable.Value.Y * 0.5f;

            // 当设置为负值时，隐藏 topContainer；非负值时显示
            if (topContainer != null)
                topContainer.Alpha = cachedTailMaskHeight >= 0 ? 1 : 0;

            if (topContainer?.Child is Container topInner)
            {
                topContainer.Height = tailHeight;
                topInner.Height = tailHeight * 2;
                topContainer.Y = cachedTailMaskHeight;
            }

            if (bodyInnerContainer != null)
            {
                bodyInnerContainer.Height = tailHeight * 2;
                bodyInnerContainer.Y = -tailHeight;
            }

            // TODO: V3 版应该增加一个顶部 Dot 标识，以免常规图无法分辨正确的面尾
        }

        protected override void UpdateColor()
        {
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

            if (MainContainer?.Children.Count > 0 && bodyContainer != null && tailHeight > 0)
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

                if (bodyScaleContainer != null && layoutChanged(lastBodyScaleY, drawHeightMinusHalf))
                {
                    bodyScaleContainer.Scale = new Vector2(1, drawHeightMinusHalf);
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

        private static bool layoutChanged(float oldValue, float newValue) => float.IsNaN(oldValue) || MathF.Abs(oldValue - newValue) > 0.001f;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                hittingLayer?.Expire();
                topContainer?.Expire();
                bodyContainer?.Expire();
            }
        }
    }
}
