// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.BMS.Scoring
{
    /// <summary>
    /// Score processor for BMS ruleset.
    /// Uses EX SCORE style scoring (PGREAT=2, GREAT=1, etc.)
    /// </summary>
    public partial class BMSScoreProcessor : ManiaScoreProcessor
    {
        protected override double ComputeTotalScore(double comboProgress, double accuracyProgress, double bonusPortion)
        {
            // BMS-style EX SCORE calculation
            return 1000000 * accuracyProgress;
        }

        public override int GetBaseScoreForResult(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return 2;

                case HitResult.Great:
                    return 1;

                default:
                    return 0;
            }
        }
    }
}
