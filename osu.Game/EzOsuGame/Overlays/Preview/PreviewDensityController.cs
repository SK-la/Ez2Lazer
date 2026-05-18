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

        private double baselineTimeRange;
        private float currentDensity = 1.0f;
        private bool hasBaseline;
        private IPreviewScrollDensityAdjustable? previewDensityAdjustable;

        public void CaptureBaseline(IDrawableScrollingRuleset ruleset)
        {
            previewDensityAdjustable = ruleset as IPreviewScrollDensityAdjustable;

            if (previewDensityAdjustable != null)
            {
                previewDensityAdjustable.PreviewDensityMultiplier = 1;
            }
            else
            {
                baselineTimeRange = ruleset.ScrollingInfo.TimeRange.Value;
            }

            currentDensity = 1.0f;
            hasBaseline = true;
        }

        public void Reset()
        {
            if (previewDensityAdjustable != null)
                previewDensityAdjustable.PreviewDensityMultiplier = 1;

            previewDensityAdjustable = null;
            baselineTimeRange = 0;
            currentDensity = 1.0f;
            hasBaseline = false;
        }

        public bool TryAdjust(IDrawableScrollingRuleset ruleset, int scrollDirection, out float displayDensity)
        {
            displayDensity = currentDensity;

            if (!hasBaseline || scrollDirection == 0)
                return false;

            float newDensity = Math.Clamp(currentDensity + scrollDirection * density_step, min_density, max_density);

            if (Math.Abs(newDensity - currentDensity) <= 0.001f)
                return false;

            currentDensity = newDensity;
            displayDensity = currentDensity;

            if (ruleset is IPreviewScrollDensityAdjustable adjustable)
            {
                // Rulesets that recompute TimeRange every frame (e.g. mania) must apply density here.
                adjustable.PreviewDensityMultiplier = currentDensity;
                previewDensityAdjustable = adjustable;
                return true;
            }

            if (ruleset.ScrollingInfo.TimeRange is not BindableDouble bindableTimeRange)
                return false;

            double target = Math.Clamp(baselineTimeRange / currentDensity, bindableTimeRange.MinValue, bindableTimeRange.MaxValue);
            bindableTimeRange.Value = target;

            return true;
        }
    }
}
