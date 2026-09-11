// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills.LeoBlack
{
    /// <summary>Hub <c>patterns/config.js</c>.</summary>
    internal static class EzLeoBlackConfig
    {
        public static readonly IReadOnlyDictionary<string, double> CORE_RATING_MULTIPLIER =
            new Dictionary<string, double>
            {
                ["Stream"] = 1.0 / 3.0,
                ["Chordstream"] = 0.65,
                ["Jacks"] = 0.9,
                ["Coordination"] = 0.75,
                ["Density"] = 0.9,
                ["Wildcard"] = 1.0,
            };

        private static readonly Dictionary<string, double> rc_subtype_base = new Dictionary<string, double>
        {
            ["Rolls"] = 1.0 / 3.0,
            ["Trills"] = 1.0 / 3.0,
            ["Minitrills"] = 1.0 / 3.0,
            ["Handstream"] = 0.65,
            ["Split Trill"] = 0.65,
            ["Jumptrill"] = 0.65,
            ["Jumpstream"] = 0.65,
            ["Brackets"] = 0.65,
            ["Double Stream"] = 0.65,
            ["Dense Chordstream"] = 0.65,
            ["Light Chordstream"] = 0.65,
            ["Chord Rolls"] = 0.65,
            ["Longjacks"] = 0.9,
            ["Quadstream"] = 0.9,
            ["Gluts"] = 0.9,
            ["Chordjacks"] = 0.9,
            ["Minijacks"] = 0.9,
        };

        private static readonly Dictionary<string, double> ln_subtype_base = new Dictionary<string, double>
        {
            ["Column Lock"] = 1.5,
            ["Release"] = 0.73,
            ["Shield"] = 0.8,
            ["JS Density"] = 1.0,
            ["HS Density"] = 1.0,
            ["DS Density"] = 1.0,
            ["LCS Density"] = 1.0,
            ["DCS Density"] = 1.0,
            ["Inverse"] = 1.5,
            ["Jacky WC"] = 0.55,
            ["Speedy WC"] = 0.8,
        };

        public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> SUBTYPE_RATING_MULTIPLIER_BY_MODE;

        static EzLeoBlackConfig()
        {
            var rc = merge(rc_subtype_base, ln_subtype_base);
            var ln = merge(rc_subtype_base, new Dictionary<string, double>
            {
                ["Column Lock"] = 1.5,
                ["Release"] = 1.0,
                ["Shield"] = 0.8,
                ["JS Density"] = 0.9,
                ["HS Density"] = 0.9,
                ["DS Density"] = 0.9,
                ["LCS Density"] = 0.9,
                ["DCS Density"] = 0.9,
                ["Inverse"] = 1.5,
                ["Jacky WC"] = 0.55,
                ["Speedy WC"] = 0.8,
            });
            var hb = merge(rc_subtype_base, new Dictionary<string, double>
            {
                ["Column Lock"] = 1.5,
                ["Release"] = 0.3,
                ["Shield"] = 0.8,
                ["JS Density"] = 0.9,
                ["HS Density"] = 0.9,
                ["DS Density"] = 0.9,
                ["LCS Density"] = 0.9,
                ["DCS Density"] = 0.9,
                ["Inverse"] = 0.0,
                ["Jacky WC"] = 0.65,
                ["Speedy WC"] = 0.45,
            });
            var mix = merge(rc_subtype_base, new Dictionary<string, double>
            {
                ["Column Lock"] = 1.5,
                ["Release"] = 0.3,
                ["Shield"] = 0.8,
                ["JS Density"] = 0.9,
                ["HS Density"] = 0.9,
                ["DS Density"] = 0.9,
                ["LCS Density"] = 0.9,
                ["DCS Density"] = 0.9,
                ["Inverse"] = 0.0,
                ["Jacky WC"] = 0.45,
                ["Speedy WC"] = 0.45,
            });

            SUBTYPE_RATING_MULTIPLIER_BY_MODE = new Dictionary<string, IReadOnlyDictionary<string, double>>
            {
                ["RC"] = rc,
                ["LN"] = ln,
                ["HB"] = hb,
                ["Mix"] = mix,
            };
        }

        public const double RC_CORE_LN_SCALE = 0.3;
        public const double RC_LN_CORE_SCALE = 0.0;
        public const double RELEASE_WITH_DW_MULTIPLIER = 0.8;
        public const double LN_MODE_LOW_THRESHOLD = 0.15;
        public const double LN_MODE_HIGH_THRESHOLD = 0.9;
        public const double HB_ROW_RATIO_THRESHOLD = 0.1;
        public const double BPM_CLUSTER_THRESHOLD = 5.0;
        public const double CLUSTER_TIMED_MIN_MSPB = 40.0;
        public const double PATTERN_STABILITY_THRESHOLD = 5.0;
        public const double IMPORTANT_CLUSTER_RATIO = 0.5;
        public const double CATEGORY_JS_HS_SECONDARY_RATIO = 0.4;
        public const double SV_AMOUNT_THRESHOLD = 2000.0;
        public const double SV_SPEED_EPS = 0.05;
        public const double SV_EXTREME_BPM_MIN = 20.0;
        public const double SV_EXTREME_BPM_MAX = 450.0;
        public const double SV_EXTREME_BPM_RATIO = 4.0;
        public const double CLUSTER_SPECIFIC_NAME_MIN_RATIO = 0.0;
        public const bool ENABLE_MULTI_LABEL_SAME_WINDOW = true;

        public static readonly string[] COORDINATION_SPECIFIC_ORDER = ["Column Lock", "Shield", "Release"];
        public static readonly string[] DENSITY_SPECIFIC_ORDER = ["Inverse", "JS Density", "HS Density", "DS Density", "DCS Density", "LCS Density"];
        public static readonly string[] WILDCARD_SPECIFIC_ORDER = ["Speedy WC", "Jacky WC"];

        public const double JACKY_MIN_BPM = 90.0;
        public const double SHIELD_MAX_BEAT_RATIO = 0.25;
        public const double INVERSE_GAP_TOLERANCE_MS = 5.0;
        public const int INVERSE_MIN_FILLED_LANES = 3;
        public const int RELEASE_SCAN_ROWS = 4;
        public const int RELEASE_MIN_TAIL_ROWS = 4;
        public const int RELEASE_ROLL_POINTS = 2;
        public const int RELEASE_FULL_MATCH_ROWS = 5;
        public const int JACKY_CONTEXT_WINDOW = 6;
        public const double JACKY_FALLBACK_MAX_MSPB = 185.0;

        public static string ModeTagFromLnRatio(double lnRatio)
        {
            if (!double.IsFinite(lnRatio))
                return "Mix";

            if (lnRatio <= LN_MODE_LOW_THRESHOLD)
                return "RC";

            if (lnRatio >= LN_MODE_HIGH_THRESHOLD)
                return "LN";

            return "Mix";
        }

        private static Dictionary<string, double> merge(Dictionary<string, double> a, Dictionary<string, double> b)
        {
            var d = new Dictionary<string, double>(a);

            foreach (var kv in b)
                d[kv.Key] = kv.Value;

            return d;
        }
    }
}
