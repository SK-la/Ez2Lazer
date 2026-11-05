// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills.SkillTag
{
    /// <summary>
    /// Thresholds and predicates that read only chart structure: pattern tag floors, jack/tech
    /// classification and the LN-vs-RC line. No dan logic lives here.
    /// </summary>
    public static class EzPatternTagRules
    {
        /// <summary>Minimum analyzer score before a pattern counts as a tag.</summary>
        public const double PATTERN_TAG_MIN_SCORE = 0.5;

        public const double CHORDJACK_TAG_MIN_SCORE = 0.8;

        public const double DELAY_TAG_MIN_SCORE = 0.25;

        /// <summary>Cluster share above which a chart is filed as that cluster.</summary>
        public const double CLUSTER_SHARE_MIN = 0.4;

        /// <summary>Jack score that vetoes the tech tag.</summary>
        public const double JACK_TECH_VETO_MIN_SCORE = 0.8;

        /// <summary>Hold ratio at or above which a chart's primary identity is LN.</summary>
        public const double LN_PRIMARY_MIN_RATIO = 0.45;

        /// <summary>7K uses a lower LN line than every other key mode.</summary>
        public const double LN_PRIMARY_7K_MIN_RATIO = 0.375;

        private static readonly HashSet<int> pattern_axis_key_counts = new HashSet<int> { 6, 7, 8 };

        /// <summary>Key modes that carry pattern rating axes rather than plain rating axes.</summary>
        public static bool UsesPatternSkillAxes(int keyCount) => pattern_axis_key_counts.Contains(keyCount);

        public static double PatternTagMinScore(string patternId)
        {
            if (patternId == EzManiaRCAxis.Chordjack.ToId())
                return CHORDJACK_TAG_MIN_SCORE;

            if (patternId == EzManiaRCAxis.Delay.ToId())
                return DELAY_TAG_MIN_SCORE;

            return PATTERN_TAG_MIN_SCORE;
        }

        public static double LnPrimaryMinRatioFor(int keyCount)
            => keyCount == 7 ? LN_PRIMARY_7K_MIN_RATIO : LN_PRIMARY_MIN_RATIO;

        /// <summary>Whether the chart's primary identity is LN rather than RC.</summary>
        public static bool IsLnPrimary(int keyCount, double holdRatio)
            => holdRatio >= LnPrimaryMinRatioFor(keyCount);

        public static bool ChartIsJack(int? keyCount, double chordjackScore, double jackScore, double? jackShare)
        {
            if (keyCount == null || !UsesPatternSkillAxes(keyCount.Value))
                return chordjackScore >= CHORDJACK_TAG_MIN_SCORE;

            if (jackScore >= PATTERN_TAG_MIN_SCORE)
                return true;

            return jackShare != null
                ? jackShare >= CLUSTER_SHARE_MIN
                : chordjackScore >= CHORDJACK_TAG_MIN_SCORE;
        }

        public static bool JackVetoesTech(int? keyCount, double chordjackScore, double jackScore, double? jackShare)
        {
            if (keyCount == null || !UsesPatternSkillAxes(keyCount.Value))
                return chordjackScore >= CHORDJACK_TAG_MIN_SCORE;

            if (jackScore >= JACK_TECH_VETO_MIN_SCORE)
                return true;

            return jackShare != null
                ? jackShare >= CLUSTER_SHARE_MIN
                : chordjackScore >= CHORDJACK_TAG_MIN_SCORE;
        }
    }
}
