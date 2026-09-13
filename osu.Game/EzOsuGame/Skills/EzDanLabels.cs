// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.EzOsuGame.Skills.Dan;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Ports mania-hub labels.ts srToRawDan / dominant axis (RcMina radar).
    /// Label printing: <see cref="EzDanLadders"/>; xxy table path: <see cref="EzSunnyDanIntervals"/>.
    /// </summary>
    public static class EzDanLabels
    {
        private static readonly double[] stream_means =
        {
            3.1, 3.5, 3.9, 4.3, 4.7, 5.05, 5.35, 5.6, 5.78, 5.92,
            6.12, 6.5, 6.92, 7.42, 8.08, 8.8, 9.65, 10.42, 11.2, 12.0,
        };

        private static readonly double[] jack_means =
        {
            3.15, 3.55, 3.95, 4.35, 4.75, 5.15, 5.45, 5.7, 5.92, 6.1,
            6.35, 6.75, 7.15, 7.65, 8.25, 8.85, 9.55, 10.25, 11.0, 11.8,
        };

        private static readonly double[] jumpstream_means =
        {
            3.15, 3.55, 3.95, 4.35, 4.75, 5.1, 5.4, 5.66, 5.86, 6.02,
            6.35, 6.72, 7.08, 7.55, 8.15, 8.85, 9.65, 10.38, 11.14, 11.95,
        };

        private static readonly double[] handstream_means =
        {
            3.2, 3.6, 4.0, 4.4, 4.8, 5.15, 5.45, 5.72, 5.92, 6.08,
            6.6, 6.9, 6.96, 7.72, 8.48, 9.18, 9.98, 10.72, 11.46, 12.2,
        };

        private static readonly double[] stamina_means =
        {
            3.2, 3.6, 4.0, 4.4, 4.8, 5.15, 5.45, 5.72, 5.92, 6.08,
            6.3, 6.7, 7.12, 7.62, 8.28, 8.98, 9.78, 10.52, 11.26, 12.0,
        };

        private static readonly double[] chordjack_means =
        {
            3.2, 3.6, 4.0, 4.42, 4.82, 5.18, 5.48, 5.75, 5.95, 6.12,
            6.35, 6.75, 7.15, 7.65, 8.25, 8.85, 9.55, 10.25, 11.0, 11.8,
        };

        private static readonly double[] tech_means =
        {
            3.25, 3.65, 4.05, 4.48, 4.88, 5.25, 5.55, 5.82, 6.02, 6.18,
            6.42, 6.82, 7.22, 7.72, 8.35, 9.02, 9.8, 10.52, 11.26, 12.0,
        };

        /// <summary>Reform ladder length used by SR→rawDan means tables (1…kappa).</summary>
        private const int reform_means_levels = 20;

        /// <summary>
        /// Keymodes that have a community xxy→dan interval table (<see cref="EzSunnyDanIntervals"/>).
        /// Outside these the 4K-calibrated <c>means</c> tables below are off-scale: the n-key
        /// MinaCalc engine rates 5K / 8K–18K on a different curve, so feeding its Overall MSD
        /// straight into <see cref="SrToRawDan(double, EzMinaSkillAxis, bool)"/> inflates the label by several levels.
        /// </summary>
        public static bool HasXxyDanTable(int keyCount) => EzSunnyDanIntervals.SupportsKeyCount(keyCount);

        /// <summary>
        /// Fitting factor for keymodes with no xxy table: <c>Overall MSD × xxySR × 0.05</c>.
        /// The star rating carries the absolute difficulty MSD loses across keymodes, and the
        /// factor lands the product back on the Reform ladder's 1…20 rawDan scale.
        /// </summary>
        public const double TABLELESS_MSD_XXY_FACTOR = 0.05;

        /// <summary>
        /// Star-fitted rawDan for a keymode without an xxy table. Null when the star rating (or
        /// the MSD) is missing — callers must leave the dan empty rather than invent one.
        /// </summary>
        public static double? FitTablelessRawDan(double overallMsd, double? xxySr)
        {
            if (!double.IsFinite(overallMsd) || overallMsd <= 0)
                return null;

            if (xxySr is not double sr || !double.IsFinite(sr) || sr <= 0)
                return null;

            return overallMsd * sr * TABLELESS_MSD_XXY_FACTOR;
        }

        /// <summary>
        /// MSD→rawDan fallback used whenever no Sunny interval matched. 4/6/7K keep the
        /// calibrated means table (the "other side" half of a dual-half row); every other
        /// keymode uses the star-fitted value. Null = no trustworthy dan for this chart.
        /// </summary>
        public static double? TryFallbackRawDan(int keyCount, double overallMsd, EzMinaSkillAxis axis, double? xxySr)
        {
            if (!double.IsFinite(overallMsd) || overallMsd <= 0)
                return null;

            return HasXxyDanTable(keyCount)
                ? SrToRawDan(overallMsd, axis)
                : FitTablelessRawDan(overallMsd, xxySr);
        }

        public static double SrToRawDan(double sr, EzMinaSkillAxis axis = EzMinaSkillAxis.Stream, bool calibrate = true)
        {
            if (!double.IsFinite(sr) || sr <= 0)
                return 1;

            // Hub: jumpstream calibration borrows handstream means.
            var calibrationAxis = axis == EzMinaSkillAxis.Jumpstream ? EzMinaSkillAxis.Handstream : axis;
            double calibratedSr = calibrate ? calibrateSrForAxis(sr, calibrationAxis) : sr;
            return rawDanFromMeans(calibratedSr, meansFor(calibrationAxis));
        }

        public static double SrToRawDan(double sr, string axisId, bool calibrate = true)
        {
            if (!EzMinaSkillAxisExtensions.TryParse(axisId, out var axis))
                axis = EzMinaSkillAxis.Stream;

            return SrToRawDan(sr, axis, calibrate);
        }

        /// <summary>Thin forward to <see cref="EzDanLadders"/>.</summary>
        public static string LabelFor(double rawDan, EzDanSide side, int keyCount)
            => EzDanLadders.For(keyCount, side).ParseLabel(rawDan);

        public static string LabelFor(double rawDan, string sideId, int keyCount)
            => LabelFor(rawDan, EzDanSideExtensions.ParseOrRc(sideId), keyCount);

        public static double? CeilingFor(EzDanSide side, int keyCount)
            => EzDanLadders.For(keyCount, side).Ceiling;

        public static double? CeilingFor(string sideId, int keyCount)
            => CeilingFor(EzDanSideExtensions.ParseOrRc(sideId), keyCount);

        public static double FloorFor(EzDanSide side, int keyCount)
            => EzDanLadders.For(keyCount, side).Floor;

        public static double FloorFor(string sideId, int keyCount)
            => FloorFor(EzDanSideExtensions.ParseOrRc(sideId), keyCount);

        private readonly record struct SrCalibration(double Slope, double Offset, double GateStart, double GateWidth);

        private static readonly Dictionary<EzMinaSkillAxis, SrCalibration> sr_calibration = new Dictionary<EzMinaSkillAxis, SrCalibration>
        {
            [EzMinaSkillAxis.JackSpeed] = new SrCalibration(0.74, 1.3, 7.4, 0.3),
            [EzMinaSkillAxis.Stream] = new SrCalibration(0.92, -0.4, 7, 0.9),
            [EzMinaSkillAxis.Jumpstream] = new SrCalibration(1.02, -0.45, 7.25, 0.55),
            [EzMinaSkillAxis.Handstream] = new SrCalibration(1.16, -2, 7.4, 0.2),
            [EzMinaSkillAxis.Stamina] = new SrCalibration(1.12, -1.5, 7.3, 0.55),
            [EzMinaSkillAxis.Chordjack] = new SrCalibration(1, 0, 7.15, 0.85),
            [EzMinaSkillAxis.Technical] = new SrCalibration(0.95, -0.9, 7.6, 0.9),
        };

        private static double calibrateSrForAxis(double sr, EzMinaSkillAxis axis)
        {
            if (!sr_calibration.TryGetValue(axis, out var calibration))
                calibration = sr_calibration[EzMinaSkillAxis.Stream];

            double targetSr = sr * calibration.Slope + calibration.Offset;
            double gate = Math.Clamp((sr - calibration.GateStart) / calibration.GateWidth, 0, 1);
            return sr + (targetSr - sr) * gate;
        }

        /// <summary>Highest RcMina radar axis in MSD (for chart skill chip / means when not Sunny).</summary>
        public static EzMinaSkillAxis DominantAxis(IReadOnlyDictionary<string, double> msdSkills)
        {
            var best = EzMinaSkillAxis.Stream;
            double bestValue = -1;

            foreach (var axis in EzMinaSkillAxisExtensions.RadarAxes)
            {
                if (!tryGetAxisMsd(msdSkills, axis, out double value) || value <= bestValue)
                    continue;

                bestValue = value;
                best = axis;
            }

            return best;
        }

        /// <summary>Look up one axis via its current MSD skill id.</summary>
        private static bool tryGetAxisMsd(IReadOnlyDictionary<string, double> msdSkills, EzMinaSkillAxis axis, out double value)
        {
            if (msdSkills.TryGetValue(axis.ToMsdSkillId(), out value))
                return true;

            value = 0;
            return false;
        }

        private static double[] meansFor(EzMinaSkillAxis axis) => axis switch
        {
            EzMinaSkillAxis.JackSpeed => jack_means,
            EzMinaSkillAxis.Jumpstream => jumpstream_means,
            EzMinaSkillAxis.Handstream => handstream_means,
            EzMinaSkillAxis.Stamina => stamina_means,
            EzMinaSkillAxis.Chordjack => chordjack_means,
            EzMinaSkillAxis.Technical => tech_means,
            _ => stream_means,
        };

        private static double rawDanFromMeans(double value, double[] means)
        {
            int maxIndex = reform_means_levels - 1;
            var capped = means.AsSpan(0, Math.Min(means.Length, maxIndex + 1));

            if (value < boundaryLower(capped, 0))
                return 1;

            int last = capped.Length - 1;
            if (value >= boundaryUpper(capped, last))
                return maxIndex + 1;

            for (int index = 0; index < capped.Length; index++)
            {
                double lower = boundaryLower(capped, index);
                double upper = boundaryUpper(capped, index);

                if (value >= lower && value < upper)
                {
                    double t = (value - lower) / Math.Max(0.001, upper - lower);
                    return index + 1 + t - 0.5;
                }
            }

            return 1;
        }

        private static double boundaryLower(ReadOnlySpan<double> means, int index)
        {
            double mean = means[index];

            if (index == 0)
                return mean - (means[1] - mean) / 2;

            return (means[index - 1] + mean) / 2;
        }

        private static double boundaryUpper(ReadOnlySpan<double> means, int index)
        {
            double mean = means[index];

            if (index == means.Length - 1)
                return mean + (mean - means[index - 1]) / 2;

            return (mean + means[index + 1]) / 2;
        }
    }
}
