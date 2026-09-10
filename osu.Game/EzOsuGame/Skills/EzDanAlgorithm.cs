// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Bump when player-dan aggregation / side headline fold / clear credit semantics change
    /// (not <see cref="osu.Game.Database.RealmAccess.EZ_REALM_SCHEMA_VERSION"/>).
    /// Independent from <see cref="EzManiaSkillAlgorithm.VERSION"/>. Reads via <see cref="EzSkillStore.GetDanEstimate"/> filter on this value.
    /// v2: side <c>GetDan</c> uses hub anchor/mean fold (<see cref="EzDanSideHeadline"/>).
    /// </summary>
    public static class EzDanAlgorithm
    {
        public const int VERSION = 2;

        // --- Player clear aggregation (hub clear window) ---
        public const int CLEAR_QUORUM = 4;
        public const int CLEAR_WINDOW = 10;

        /// <summary>Minimum rated skillset tiles before averaging / anchoring a side headline.</summary>
        public const int SKILLSET_AVERAGE_MIN_BUCKETS = 2;

        /// <summary>Hub <c>DAN_ANCHOR_CLAMP</c>: max levels another tile may pull the 7K LN headline.</summary>
        public const double ANCHOR_CLAMP = 2;

        /// <summary>Hub <c>DAN_ANCHOR_PULL</c>: fraction of mean capped distance applied to the anchor.</summary>
        public const double ANCHOR_PULL = 0.5;

        public const double ROUNDING_EPSILON = 1e-9;

        // --- Chart side classification by hold ratio ---
        public const double LN_PRIMARY_MIN_RATIO = 0.45;
        public const double LN_PRIMARY_7K_MIN_RATIO = 0.375;

        public static double LnPrimaryMinRatioFor(int keyCount)
            => keyCount == 7 ? LN_PRIMARY_7K_MIN_RATIO : LN_PRIMARY_MIN_RATIO;

        /// <summary>
        /// Whether DualPanel / Sunny may print a chart aggregate for <paramref name="side"/>.
        /// Matches primary identity: LN only when hold ratio reaches <see cref="LnPrimaryMinRatioFor"/>;
        /// RC when below that line (or hold unknown — callers should pass 0 when missing).
        /// </summary>
        public static bool AllowsChartSideHalf(EzDanSide side, int keyCount, double holdRatio)
        {
            bool lnPrimary = holdRatio >= LnPrimaryMinRatioFor(keyCount);
            return side == EzDanSide.Ln ? lnPrimary : !lnPrimary;
        }

        public static double AccuracyBarFor(EzDanSide side, int keyCount)
        {
            if (side == EzDanSide.Ln)
                return keyCount == 4 ? 0.97 : 0.95;

            return 0.96;
        }

        public static double AccuracyBarFor(string sideId, int keyCount)
            => AccuracyBarFor(EzDanSideExtensions.ParseOrRc(sideId), keyCount);
    }
}
