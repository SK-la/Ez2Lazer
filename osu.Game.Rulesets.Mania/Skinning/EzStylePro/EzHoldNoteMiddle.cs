// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Screens;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteMiddle : EzNoteBase, IHoldNoteBody
    {
        private readonly IBindable<bool> isHitting = new Bindable<bool>();
        private DrawableHoldNote holdNote = null!;

        private Container? topContainer;
        private Container? bodyContainer;
        private Container? bodyScaleContainer;
        private Container? bodyInnerContainer;

        private Bindable<double> tailAlpha = null!;
        private Bindable<double> tailMaskHeight = new Bindable<double>();
        private IBindable<double> hitPosition = new Bindable<double>();
        private EzHoldNoteHittingLayer? hittingLayer;
        private Drawable? lightContainer;

        private float tailHeight;

        public EzHoldNoteMiddle()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject drawableObject)
        {
            holdNote = (DrawableHoldNote)drawableObject;
            isHitting.BindTo(holdNote.IsHolding);

            hitPosition = EzSkinConfig.GetBindable<double>(Ez2Setting.HitPosition);
            tailMaskHeight = EzSkinConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailMaskGradientHeight);
            tailAlpha = EzSkinConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailAlpha);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            isHitting.BindValueChanged(onIsHittingChanged, true);

            tailMaskHeight.BindValueChanged(_ => UpdateSize(), true);
            tailAlpha.BindValueChanged(_ => UpdateSize(), true);
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
                Alpha = 0,
                IsHitting = { BindTarget = isHitting }
            };

            lightContainer = new HitTargetInsetContainer
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                Child = hittingLayer
            };

            hittingLayer.HitPosition.BindTo(hitPosition);
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

        protected override void OnDrawableChanged()
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

            Schedule(UpdateSize);
        }

        protected override void UpdateSize()
        {
            base.UpdateSize();
            tailHeight = NoteSize.Value.Y * 0.5f;

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

            // TODO: V3版应该增加一个顶部Dot标识，以免常规图无法分辨正确的面尾
        }

        protected override void Update()
        {
            base.Update();

            if (MainContainer?.Children.Count > 0 && bodyContainer != null && tailHeight > 0)
            {
                float drawHeightMinusHalf = DrawHeight - tailHeight;
                float middleHeight = Math.Max(drawHeightMinusHalf, tailHeight);

                bodyContainer.Height = tailMaskHeight.Value > 0
                    ? middleHeight - (float)tailMaskHeight.Value + 1
                    : middleHeight + 2;

                if (bodyScaleContainer != null)
                    bodyScaleContainer.Scale = new Vector2(1, drawHeightMinusHalf);
            }
        }

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
