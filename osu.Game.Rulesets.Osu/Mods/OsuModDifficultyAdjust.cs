// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Mods
{
    public partial class OsuModDifficultyAdjust : ModDifficultyAdjust
    {
        [SettingSource("Circle Size", "Override a beatmap's set CS.", FIRST_SETTING_ORDER - 1, SettingControlType = typeof(DifficultyAdjustSettingsControl))]
        public DifficultyBindable CircleSize { get; } = new DifficultyBindable
        {
            Precision = 0.1f,
            MinValue = 0,
            MaxValue = 10,
            ExtendedMaxValue = 11,
            ReadCurrentFromDifficulty = diff => diff.CircleSize,
        };

        [SettingSource("Approach Rate", "Override a beatmap's set AR.", LAST_SETTING_ORDER + 1, SettingControlType = typeof(ApproachRateSettingsControl))]
        public DifficultyBindable ApproachRate { get; } = new DifficultyBindable
        {
            Precision = 0.1f,
            MinValue = 0,
            MaxValue = 10,
            ExtendedMinValue = -10,
            ExtendedMaxValue = 11,
            ReadCurrentFromDifficulty = diff => diff.ApproachRate,
        };

        public override string ExtendedIconInformation
        {
            get
            {
                if (!IsExactlyOneSettingChanged(CircleSize, ApproachRate, OverallDifficulty, DrainRate))
                    return string.Empty;

                if (!CircleSize.IsDefault) return format("CS", CircleSize);
                if (!ApproachRate.IsDefault) return format("AR", ApproachRate);
                if (!OverallDifficulty.IsDefault) return format("OD", OverallDifficulty);
                if (!DrainRate.IsDefault) return format("HP", DrainRate);

                return string.Empty;

                string format(string acronym, DifficultyBindable bindable) => $"{acronym}{bindable.Value!.Value.ToStandardFormattedString(1)}";
            }
        }

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (!CircleSize.IsDefault)
                    yield return ("Circle size", $"{CircleSize.Value:N1}");

                foreach (var setting in base.SettingDescription)
                    yield return setting;

                if (!ApproachRate.IsDefault)
                    yield return ("Approach rate", $"{ApproachRate.Value:N1}");
            }
        }

        protected override void ApplySettings(BeatmapDifficulty difficulty)
        {
            base.ApplySettings(difficulty);

            if (CircleSize.Value != null) difficulty.CircleSize = CircleSize.Value.Value;
            if (ApproachRate.Value != null) difficulty.ApproachRate = ApproachRate.Value.Value;
        }

        private partial class ApproachRateSettingsControl : DifficultyAdjustSettingsControl
        {
            protected override RoundedSliderBar<float> CreateSlider(BindableNumber<float> current) => new ApproachRateSlider();

            /// <summary>
            /// A slider bar with more detailed approach rate info for its given value
            /// </summary>
            public partial class ApproachRateSlider : RoundedSliderBar<float>
            {
                public override LocalisableString TooltipText =>
                    (Current as BindableNumber<float>)?.MinValue < 0
                        ? $"{base.TooltipText} ({getPreemptTime(Current.Value):0} ms)"
                        : base.TooltipText;

                private double getPreemptTime(float approachRate)
                {
                    var hitCircle = new HitCircle();
                    hitCircle.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { ApproachRate = approachRate });
                    return hitCircle.TimePreempt;
                }
            }
        }
    }
}
