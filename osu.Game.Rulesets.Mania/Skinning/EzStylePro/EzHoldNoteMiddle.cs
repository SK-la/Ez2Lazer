// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Screens;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteMiddle : EzNoteBase, IHoldNoteBody
    {
        private readonly IBindable<bool> isHitting = new Bindable<bool>();

        private Container? topContainer;
        private Container? bodyContainer;
        private Container? bodyScaleContainer;
        private Container? bodyInnerContainer;

        private IBindable<double> hitPosition = new Bindable<double>();
        private EzHoldNoteHittingLayer? hittingLayer;

        [Resolved]
        private Column column { get; set; } = null!;

        public EzHoldNoteMiddle()
        {
            RelativeSizeAxes = Axes.Both;
            // FillMode = FillMode.Stretch;

            // Anchor = Anchor.BottomCentre;
            // Origin = Anchor.BottomCentre;
            // Masking = true;
        }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject drawableObject)
        {
            var holdNote = (DrawableHoldNote)drawableObject;
            isHitting.BindTo(holdNote.IsHolding);

            hitPosition = EZSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
            isHitting.BindValueChanged(onIsHittingChanged, true);
            OnSkinChanged();
        }

        // protected override void Update()
        // {
        //     base.Update();
        // }

        private void OnSkinChanged()
        {
            if (hittingLayer != null)
            {
                column.TopLevelContainer.Remove(hittingLayer, false);
                hittingLayer.Expire();
                hittingLayer = null;
            }

            hittingLayer = new EzHoldNoteHittingLayer
            {
                Alpha = 0,
                IsHitting = { BindTarget = isHitting }
            };

            hittingLayer.HitPosition.BindTo(hitPosition);
        }

        private void onIsHittingChanged(ValueChangedEvent<bool> isHitting)
        {
            if (hittingLayer != null) hittingLayer.IsHitting.Value = isHitting.NewValue;

            if (hittingLayer == null)
                return;

            if (isHitting.NewValue)
            {
                hittingLayer.ClearTransforms();

                if (hittingLayer.Parent == null)
                    column.TopLevelContainer.Add(hittingLayer);

                hittingLayer.FadeIn(80);
            }
            else
            {
                hittingLayer.FadeOut(120)
                            .OnComplete(d => column.TopLevelContainer.Remove(d, false));
            }
        }

        public void Recycle()
        {
            ClearTransforms();
            hittingLayer?.Recycle();
        }

        protected override void OnDrawableChanged()
        {
            string newComponentName = $"{ColorPrefix}note";
            var body = Factory.CreateAnimation($"{ColorPrefix}longnote/middle");
            var tail = Factory.CreateAnimation($"{ColorPrefix}longnote/tail");

            if (body.FrameCount == 0)
                body = Factory.CreateAnimation(newComponentName);

            if (tail.FrameCount == 0)
                tail = Factory.CreateAnimation(newComponentName);

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
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
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

            Schedule(UpdateSize);
        }

        protected override void UpdateSize()
        {
            base.UpdateSize();
            if (MainContainer?.Children.Count == 0)
                return;

            float v = NoteSize.Value.Y;

            if (topContainer?.Child is Container topInner)
            {
                topContainer.Height = v / 2;
                topInner.Height = v;
            }

            float middleHeight = Math.Max(DrawHeight - v / 2, v / 2);

            if (bodyContainer != null)
            {
                bodyContainer.Y = v / 2;
                bodyContainer.Height = middleHeight + 2;
            }

            if (bodyScaleContainer != null)
                bodyScaleContainer.Scale = new Vector2(1, DrawHeight - v / 2);

            if (bodyInnerContainer != null)
            {
                bodyInnerContainer.Height = v;
                bodyInnerContainer.Y = -v / 2;
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
