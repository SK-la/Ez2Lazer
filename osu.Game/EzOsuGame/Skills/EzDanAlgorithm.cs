// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Bump when MVP dan credit / MSD→rawDan heuristic semantics change.
    /// Independent from <see cref="EzManiaSkillAlgorithm.VERSION"/>.
    /// </summary>
    public static class EzDanAlgorithm
    {
        public const int VERSION = 1;

        public const int CLEAR_QUORUM = 4;
        public const int CLEAR_WINDOW = 10;

        public const double LN_PRIMARY_MIN_RATIO = 0.45;
        public const double LN_PRIMARY_7K_MIN_RATIO = 0.375;

        public static double LnPrimaryMinRatioFor(int keyCount)
            => keyCount == 7 ? LN_PRIMARY_7K_MIN_RATIO : LN_PRIMARY_MIN_RATIO;

        public static double AccuracyBarFor(string side, int keyCount)
        {
            if (side == DanSkillSystem.SIDE_LN)
                return keyCount == 4 ? 0.97 : 0.95;

            return 0.96;
        }
    }
}
