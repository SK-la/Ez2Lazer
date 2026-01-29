// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
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

        private BindableNumber<double> pill = new BindableDouble
        {
            MinValue = 0,
            MaxValue = 1,
        };

        protected bool InitialAnimationPlaying => initialIncrease != null;

        private ScheduledDelegate? initialIncrease;

        /// <summary>
        /// Triggered when a <see cref="Judgement"/> is a successful hit, signaling the health display to perform a flash animation (if designed to do so).
        /// Calls to this method are debounced.
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
                // Map pill count to 0..1. Assume max pill count is 5.
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

            if (!InitialAnimationPlaying || pill.Value != initialPillValue)
            {
                Current.Value = pill.Value;

                if (initialIncrease != null)
                    FinishInitialAnimation(Current.Value);
            }

            // Health changes every frame in draining situations.
            // Manually handle value changes to avoid bindable event flow overhead.
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

            // TODO: this should run in gameplay time, including showing a larger increase when skipping.
            // TODO: it should also start increasing relative to the first hitobject.
            const double increase_delay = 150;

            initialIncrease = Scheduler.AddDelayed(() =>
            {
                double newValue = Math.Min(Current.Value + 0.05f, pill.Value);
                this.TransformBindableTo(Current, newValue, increase_delay);
                Scheduler.AddOnce(Flash);

                if (newValue >= pill.Value)
                    FinishInitialAnimation(pill.Value);
            }, increase_delay, true);
        }

        protected virtual void FinishInitialAnimation(double value)
        {
            if (initialIncrease == null)
                return;

            initialIncrease.Cancel();
            initialIncrease = null;

            // aside from the repeating `initialIncrease` scheduled task,
            // there may also be a `Current` transform in progress from that schedule.
            // ensure it plays out fully, to prevent changes to `Current.Value` being discarded by the ongoing transform.
            // and yes, this funky `targetMember` spec is seemingly the only way to do this
            // (see: https://github.com/ppy/osu-framework/blob/fe2769171c6e26d1b6fdd6eb7ea8353162fe9065/osu.Framework/Graphics/Transforms/TransformBindable.cs#L21)
            FinishTransforms(targetMember: $"{Current.GetHashCode()}.{nameof(Current.Value)}");
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
        }
    }
}
