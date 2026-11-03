// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.EzOsuGame.Skills.Dan;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Ports mania-hub <c>dan-credit.ts</c> creditedDanFor / danCreditOffset (MVP subset).
    /// </summary>
    public static class EzDanCredit
    {
        public const double CREDIT_EDGE_TOLERANCE = 1e-9;
        public const double BELOW_BAR_WINDOW_RC = 0.05;
        public const double BELOW_BAR_WINDOW_LN = 0.03;
        public const double BELOW_BAR_WINDOW_4K_LN = 0.025;
        public const double BONUS_MIN_SPAN = 0.04;

        private static readonly (double At, double Offset)[] above_bar_rc =
        {
            (0, 0), (0.25, 0), (0.675, 0.2), (0.75, 0.7), (0.875, 1.1), (1, 1.5),
        };

        private static readonly (double At, double Offset)[] below_bar_rc =
        {
            (0, 0), (0.2, -0.5075), (0.8, -1.25), (1, -1.5),
        };

        private static readonly (double At, double Offset)[] below_bar_ln =
        {
            (0, -0.26), (1.0 / 3.0, -1.25), (2.0 / 3.0, -1.5), (1, -1.75),
        };

        private static readonly (double At, double Offset)[] above_bar_4k_ln =
        {
            (0, 0), (0.01, 0), (0.015, 0.15), (0.02, 0.3), (0.025, 0.5), (0.027, 0.7),
        };

        private static readonly (double At, double Offset)[] below_bar_4k_ln =
        {
            (0, -0.3), (0.2, -0.9), (1, -1.55),
        };

        public static double? CreditedDanFor(double chartDan, double accuracy, string sideId, int keyCount)
            => CreditedDanFor(chartDan, accuracy, EzDanSideExtensions.ParseOrRc(sideId), keyCount);

        public static double? CreditedDanFor(double chartDan, double accuracy, EzDanSide side, int keyCount)
        {
            double bar = EzDanAlgorithm.AccuracyBarFor(side, keyCount);
            double? offset = creditOffset(accuracy, bar, side, keyCount);
            if (offset is not double o)
                return null;

            double credited = chartDan + o;
            var ladder = EzDanLadders.For(keyCount, side);
            if (ladder.Ceiling is double c)
                credited = Math.Min(credited, c);

            return Math.Max(credited, ladder.Floor);
        }

        private static double? creditOffset(double accuracy, double bar, EzDanSide side, int keyCount)
        {
            if (!double.IsFinite(accuracy))
                return null;

            double window = belowBarWindow(side, keyCount);
            double delta = accuracy - bar;

            if (!(delta >= -window - CREDIT_EDGE_TOLERANCE))
                return null;

            if (delta < -CREDIT_EDGE_TOLERANCE)
            {
                var below = belowBarAnchors(side, keyCount);
                double s = Math.Min(1, -delta / window);
                double offset = interpolate(below, s);
                double nearCap = nearBarCap(side, keyCount);
                return Math.Min(offset, -nearCap);
            }

            if (side == EzDanSide.Ln && keyCount == 4)
                return interpolate(above_bar_4k_ln, Math.Max(0, delta));

            double headroom = Math.Max(1 - bar, BONUS_MIN_SPAN);
            double t = headroom > 0 ? Math.Min(1, Math.Max(0, delta) / headroom) : 1;
            return interpolate(above_bar_rc, t);
        }

        private static double belowBarWindow(EzDanSide side, int keyCount)
        {
            if (side != EzDanSide.Ln)
                return BELOW_BAR_WINDOW_RC;

            return keyCount == 4 ? BELOW_BAR_WINDOW_4K_LN : BELOW_BAR_WINDOW_LN;
        }

        private static double nearBarCap(EzDanSide side, int keyCount)
        {
            if (side == EzDanSide.Rc)
                return 0;

            return keyCount == 4 ? 0.3 : 0.26;
        }

        private static (double At, double Offset)[] belowBarAnchors(EzDanSide side, int keyCount)
        {
            if (side == EzDanSide.Ln && keyCount == 4)
                return below_bar_4k_ln;
            if (side == EzDanSide.Ln)
                return below_bar_ln;

            return below_bar_rc;
        }

        private static double interpolate((double At, double Offset)[] anchors, double at)
        {
            if (at <= anchors[0].At)
                return anchors[0].Offset;

            for (int i = 1; i < anchors.Length; i++)
            {
                if (at <= anchors[i].At)
                {
                    double span = anchors[i].At - anchors[i - 1].At;
                    double t = span > 0 ? (at - anchors[i - 1].At) / span : 1;
                    return anchors[i - 1].Offset + (anchors[i].Offset - anchors[i - 1].Offset) * t;
                }
            }

            return anchors[^1].Offset;
        }
    }
}
