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

        private float halfNoteHeight;
        private float lastBodyContainerHeight = float.NaN;
        private float lastBodyScaleY = float.NaN;
        private float lastTopContainerY = float.NaN;
        private float cachedTailMaskHeight = float.NaN;

        private bool lnGradient;

        public EzHoldNoteMiddle()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject drawableObject, IEzSkinInfo ezSkinInfo, Ez2ConfigManager ezConfig)
        {
            lnGradient = ezConfig.Get<bool>(Ez2Setting.ManiaLNGradientEnable);

            tailMaskHeight = ezSkinInfo.HoldTailMaskHeight;
            tailAlpha = ezSkinInfo.HoldTailAlpha;
            tailMaskHeight.BindValueChanged(onTailMaskHeightChanged, true);
            tailAlpha.BindValueChanged(_ =>
            {
                if (lnGradient)
                    OnColourChanged();
            }, true);

            ezSkinInfo.ManiaLNGradientEnable.BindValueChanged(e =>
            {
                if (lnGradient == e.NewValue)
                    return;

                lnGradient = e.NewValue;
                OnLoadChanged();
            });

            holdNote = (DrawableHoldNote)drawableObject;

            isHitting = holdNote.IsHolding;
            isHitting.BindValueChanged(onIsHittingChanged, true);

            if (lightContainer == null)
                OnLightChanged();
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

            var body = Factory.CreateAnimation(ColorPrefix + "longnote/middle");
            var tail = Factory.CreateAnimation(ColorPrefix + "longnote/tail");

            string newComponentName = ColorPrefix + "note";
            if (body.FrameCount == 0)
                body = Factory.CreateAnimation(newComponentName);

            if (tail.FrameCount == 0)
                tail = Factory.CreateAnimation(newComponentName);

            topContainer?.Expire();
            bodyContainer?.Expire();

            if (lnGradient)
            {
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
            }
            else
            {
                topContainer = null;
            }

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

            MainContainer.Clear();
            MainContainer.Children = lnGradient
                ? [bodyContainer, topContainer!]
                : [bodyContainer];

            // 重新初始化光效层
            OnLightChanged();
            resetLayoutCache();
            // 立即刷新 + 下一帧刷新，确保 HoldNote 布局稳定
            Schedule(OnDrawableChanged);
        }

        protected override void UpdateDrawable()
        {
            halfNoteHeight = NoteHeight * 0.5f;

            if (lnGradient)
                updateTopContainerLayout(getTailMaskHeight());

            if (bodyInnerContainer != null)
            {
                bodyInnerContainer.Height = NoteHeight;
                bodyInnerContainer.Y = -halfNoteHeight;
            }

            updateBodyLayout(lnGradient ? getTailMaskHeight() : 0);

            // TODO: V3 版应该增加一个顶部 Dot 标识，以免常规图无法分辨正确的面尾
        }

        protected override void UpdateColor()
        {
            if (!lnGradient)
            {
                if (bodyContainer != null)
                    bodyContainer.Colour = NoteColor;

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

            if (MainContainer.Children.Count == 0)
                return;

            halfNoteHeight = NoteHeight * 0.5f;

            if (halfNoteHeight <= 0)
                return;

            float moveDown = lnGradient ? getTailMaskHeight() : 0;

            if (lnGradient)
                updateTopContainerLayout(moveDown);

            updateBodyLayout(moveDown);
        }

        private void updateTopContainerLayout(float maskHeight)
        {
            if (topContainer?.Child is not Container topInner)
                return;

            // 负值：隐藏伪面尾，由 body 上探代替；非负值：面尾形状不变，整体下移以缩短中部
            topContainer.Alpha = maskHeight >= 0 ? 1 : 0;

            if (maskHeight >= 0)
            {
                topContainer.Height = halfNoteHeight;

                if (layoutChanged(lastTopContainerY, maskHeight))
                {
                    topContainer.Y = maskHeight;
                    lastTopContainerY = maskHeight;
                }

                topInner.Y = 0;
            }
            else
            {
                float extendedHeight = halfNoteHeight - maskHeight;
                topContainer.Height = extendedHeight;
                topContainer.Y = halfNoteHeight - extendedHeight;
                lastTopContainerY = topContainer.Y;
            }

            topInner.Height = NoteHeight;
        }

        private void onTailMaskHeightChanged(ValueChangedEvent<double> height)
        {
            cachedTailMaskHeight = (float)height.NewValue;
            resetLayoutCache();
            OnDrawableChanged();
        }

        private void updateBodyLayout(float moveDown)
        {
            if (bodyContainer == null)
                return;

            float targetBodyHeight;
            float bodyScaleY;

            if (!lnGradient)
            {
                // 常规模式：独立 tail，body 占满面身区域（不再预留半 note 给伪面尾）
                targetBodyHeight = DrawHeight;
                bodyScaleY = DrawHeight;

                if (layoutChanged(lastBodyContainerHeight, targetBodyHeight))
                {
                    bodyContainer.Height = targetBodyHeight;
                    lastBodyContainerHeight = targetBodyHeight;
                }

                if (bodyScaleContainer != null && layoutChanged(lastBodyScaleY, bodyScaleY))
                {
                    bodyScaleContainer.Scale = new Vector2(1, bodyScaleY);
                    lastBodyScaleY = bodyScaleY;
                }

                return;
            }

            float drawHeightMinusHalf = DrawHeight - halfNoteHeight;
            float middleHeight = Math.Max(drawHeightMinusHalf, halfNoteHeight);
            bodyScaleY = drawHeightMinusHalf;

            // 当 maskHeight 为正值时，缩短中间部分并下移 top；为负值时，延长中间部分实现上移效果
            targetBodyHeight = moveDown >= 0
                ? middleHeight - moveDown + 1
                : middleHeight + MathF.Abs(moveDown) + 2;

            if (layoutChanged(lastBodyContainerHeight, targetBodyHeight))
            {
                bodyContainer.Height = targetBodyHeight;
                lastBodyContainerHeight = targetBodyHeight;
            }

            if (bodyScaleContainer != null && layoutChanged(lastBodyScaleY, bodyScaleY))
            {
                bodyScaleContainer.Scale = new Vector2(1, bodyScaleY);
                lastBodyScaleY = bodyScaleY;
            }
        }

        private void resetLayoutCache()
        {
            lastBodyContainerHeight = float.NaN;
            lastBodyScaleY = float.NaN;
            lastTopContainerY = float.NaN;
        }

        private float getTailMaskHeight() => float.IsNaN(cachedTailMaskHeight) ? 0 : cachedTailMaskHeight;

        private static bool layoutChanged(float oldValue, float newValue) => float.IsNaN(oldValue) || MathF.Abs(oldValue - newValue) > 0.001f;

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
    }
}
