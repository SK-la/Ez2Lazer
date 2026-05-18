// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    /// <summary>
    /// Adjusts preview note density by scaling scroll time range relative to a captured baseline.
    /// </summary>
    public class PreviewDensityController
    {
        private const float min_density = 0.1f;
        private const float max_density = 5.0f;
        private const float density_step = 0.05f;

        private readonly BindableDouble density;

        private double baselineTimeRange;
        private bool hasBaseline;
        private IPreviewScrollDensityAdjustable? previewDensityAdjustable;

        public PreviewDensityController(BindableDouble density)
        {
            this.density = density;
        }

        public float CurrentDensity => (float)density.Value;

        public void CaptureBaseline(IDrawableScrollingRuleset ruleset)
        {
            previewDensityAdjustable = ruleset as IPreviewScrollDensityAdjustable;

            if (previewDensityAdjustable != null)
                applyDensity(previewDensityAdjustable, CurrentDensity);
            else
                baselineTimeRange = ruleset.ScrollingInfo.TimeRange.Value;

            hasBaseline = true;
        }

        public void DisposeSession()
        {
            previewDensityAdjustable = null;
            baselineTimeRange = 0;
            hasBaseline = false;
        }

        public bool TryAdjust(IDrawableScrollingRuleset ruleset, int scrollDirection, out float displayDensity)
        {
            displayDensity = CurrentDensity;

            if (!hasBaseline || scrollDirection == 0)
                return false;

            float newDensity = Math.Clamp(CurrentDensity + scrollDirection * density_step, min_density, max_density);

            if (Math.Abs(newDensity - CurrentDensity) <= 0.001f)
                return false;

            density.Value = newDensity;
            displayDensity = newDensity;

            if (ruleset is IPreviewScrollDensityAdjustable adjustable)
            {
                applyDensity(adjustable, newDensity);
                previewDensityAdjustable = adjustable;
                return true;
            }

            if (ruleset.ScrollingInfo.TimeRange is not BindableDouble bindableTimeRange)
                return false;

            double target = Math.Clamp(baselineTimeRange / newDensity, bindableTimeRange.MinValue, bindableTimeRange.MaxValue);
            bindableTimeRange.Value = target;

            return true;
        }

        private static void applyDensity(IPreviewScrollDensityAdjustable adjustable, float value)
            => adjustable.PreviewDensityMultiplier = value;
    }
}
