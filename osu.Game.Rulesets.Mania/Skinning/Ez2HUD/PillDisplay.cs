// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Utils;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2HUD
{
    public abstract partial class PillDisplay : CompositeDrawable
    {
        protected virtual bool PlayInitialIncreaseAnimation => true;

        public Bindable<double> Current { get; } = new BindableDouble
        {
            MinValue = 0,
            MaxValue = 1,
        };

        private readonly BindableNumber<double> pill = new BindableDouble
        {
            MinValue = 0,
            MaxValue = 1,
        };

        protected bool InitialAnimationPlaying { get; private set; }

        /// <summary>
        /// 当一个 <see cref="Judgement"/> 被判定为成功命中时触发，通知生命显示执行闪烁动画（如果有实现）。
        /// 对该方法的调用已做防抖处理。
        /// </summary>
        protected virtual void Flash()
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            O2HitModeExtension.PILL_COUNT.BindValueChanged(pill =>
            {
                if (IsDisposed) return;
                // 将药丸计数映射到 0..1。最大药丸数为 5。
                this.pill.Value = Math.Clamp(pill.NewValue / 5.0, 0, 1);
            }, true);

            initialPillValue = pill.Value;

            if (PlayInitialIncreaseAnimation)
                startInitialAnimation();
            else
                Current.Value = pill.Value;
        }

        private double lastValue;
        private double initialPillValue;

        protected override void Update()
        {
            base.Update();

            // 如果正在进行单次初始变换，当达到目标值时结束该动画。
            if (InitialAnimationPlaying && Precision.AlmostEquals(Current.Value, pill.Value, 0.001f))
                FinishInitialAnimation(Current.Value);

            if (!InitialAnimationPlaying || pill.Value != initialPillValue)
            {
                Current.Value = pill.Value;

                if (InitialAnimationPlaying)
                    FinishInitialAnimation(Current.Value);
            }

            // 在持续变化（如下降）时，生命值每帧都会变更。
            // 手动处理值变化以避免通过 Bindable 的事件流产生开销。
            if (!Precision.AlmostEquals(lastValue, Current.Value, 0.001f))
            {
                PillChanged(Current.Value > lastValue);
                lastValue = Current.Value;
            }
        }

        protected virtual void PillChanged(bool increase)
        {
        }

        private void startInitialAnimation()
        {
            if (Current.Value >= pill.Value)
                return;

            const double duration = 100; // ms

            InitialAnimationPlaying = true;
            this.TransformBindableTo(Current, pill.Value, duration);
            Scheduler.AddOnce(Flash);
        }

        protected virtual void FinishInitialAnimation(double value)
        {
            if (!InitialAnimationPlaying)
                return;

            InitialAnimationPlaying = false;

            // 除了可能存在的重复计划任务之外，
            // 还有可能存在由该任务产生的 `Current` 变换正在进行中。
            // 确保该变换完整播放，以防正在进行的变换丢弃对 `Current.Value` 的更改。
            // 是的，这个看似特殊的 `targetMember` 规范似乎是实现此目的的唯一办法
            // (参见: https://github.com/ppy/osu-framework/blob/fe2769171c6e26d1b6fdd6eb7ea8353162fe9065/osu.Framework/Graphics/Transforms/TransformBindable.cs#L21)
            FinishTransforms(targetMember: $"{Current.GetHashCode()}.{nameof(Current.Value)}");
        }
    }
}
