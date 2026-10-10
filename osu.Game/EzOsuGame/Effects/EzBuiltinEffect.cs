// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Effects
{
    /// <summary>
    /// 内置动效。缩放和弹跳各自保存自己的设置。
    /// </summary>
    public interface IEzBuiltinEffect
    {
        void Play(Drawable target, bool wasIncrease, bool wasMiss);
    }

    public sealed class EzScaleEffect : IEzBuiltinEffect
    {
        public BindableNumber<float> StartFactor { get; }

        public BindableNumber<float> EndFactor { get; }

        public BindableNumber<float> StartTime { get; }

        public BindableNumber<float> EndDuration { get; }

        private EzScaleEffect(float startFactor)
        {
            StartFactor = new BindableNumber<float>(startFactor)
            {
                MinValue = 0.1f,
                MaxValue = 5f,
                Precision = 0.05f,
            };
            EndFactor = new BindableNumber<float>(1f)
            {
                MinValue = 0.1f,
                MaxValue = 5f,
                Precision = 0.05f,
            };
            StartTime = new BindableNumber<float>(10)
            {
                MinValue = 1,
                MaxValue = 300,
                Precision = 1f,
            };
            EndDuration = new BindableNumber<float>(300)
            {
                MinValue = 10,
                MaxValue = 500,
                Precision = 10f,
            };
        }

        public static EzScaleEffect ForCounter() => new EzScaleEffect(1.5f);

        public static EzScaleEffect ForTitle() => new EzScaleEffect(2f);

        public void Play(Drawable target, bool wasIncrease, bool wasMiss)
        {
            float newScaleValue = Math.Clamp(target.Scale.X * (wasIncrease ? StartFactor.Value : EndFactor.Value), 0.5f, 3f);

            target
                .ScaleTo(new Vector2(newScaleValue), StartTime.Value, Easing.OutQuint)
                .Then()
                .ScaleTo(Vector2.One, EndDuration.Value, Easing.OutQuint);

            if (wasMiss)
                target.FlashColour(Color4.Red, EndDuration.Value, Easing.OutQuint);
        }
    }

    public sealed class EzBounceEffect : IEzBuiltinEffect
    {
        private const double overshoot_travel = 33.33;
        private const double settle_travel = 50;

        public BindableNumber<float> Peak { get; }

        public BindableNumber<float> ReturnDuration { get; }

        public Bindable<Easing> Ease { get; }

        public BindableNumber<float> Overshoot { get; }

        public BindableNumber<float> StartAlpha { get; }

        public BindableNumber<float> Alpha { get; }

        private EzBounceEffect(float peak, float minPeak, float maxPeak, float overshoot)
        {
            Peak = new BindableNumber<float>(peak)
            {
                MinValue = minPeak,
                MaxValue = maxPeak,
                Precision = 1f,
            };
            ReturnDuration = new BindableNumber<float>(50)
            {
                MinValue = 1,
                MaxValue = 300,
                Precision = 1f,
            };
            Ease = new Bindable<Easing>(Easing.None);
            Overshoot = new BindableNumber<float>(overshoot)
            {
                MinValue = -15,
                MaxValue = 15,
                Precision = 1f,
            };
            StartAlpha = new BindableNumber<float>(0.2f)
            {
                MinValue = 0,
                MaxValue = 1,
                Precision = 0.05f,
            };
            Alpha = new BindableNumber<float>(0.7f)
            {
                MinValue = 0.7f,
                MaxValue = 0.9f,
                Precision = 0.05f,
            };
        }

        public static EzBounceEffect ForDigits() => new EzBounceEffect(25, 10, 28, -3);

        public static EzBounceEffect ForTitle() => new EzBounceEffect(-10, -10, 10, 0);

        public void Play(Drawable target, bool wasIncrease, bool wasMiss)
        {
            target.FinishTransforms();
            target.Scale = Vector2.One;

            if (!wasIncrease)
            {
                target.Position = Vector2.Zero;
                target.Alpha = 1;

                if (wasMiss)
                    target.FlashColour(Color4.Red, ReturnDuration.Value, Easing.OutQuint);

                return;
            }

            Easing easing = Ease.Value;
            double returnDuration = ReturnDuration.Value;

            target.Position = new Vector2(target.X, Peak.Value);
            target.Alpha = StartAlpha.Value;
            target.MoveToY(0, returnDuration, easing);
            target.FadeTo(Alpha.Value, returnDuration, easing);

            double played = returnDuration;

            if (Overshoot.Value != 0)
            {
                using (target.BeginDelayedSequence(returnDuration))
                    target.MoveToY(Overshoot.Value, overshoot_travel, easing);

                played += overshoot_travel;

                using (target.BeginDelayedSequence(played))
                    target.MoveToY(0, settle_travel, easing);

                played += settle_travel;
            }

            if (wasMiss)
                target.FlashColour(Color4.Red, played, Easing.OutQuint);
        }
    }
}
