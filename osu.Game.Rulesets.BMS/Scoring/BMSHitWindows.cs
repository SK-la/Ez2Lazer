// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.BMS.Scoring
{
    /// <summary>
    /// BMS hit windows based on LR2/beatoraja timing.
    /// </summary>
    public class BMSHitWindows : HitWindows
    {
        // Based on LR2 RANK 2 (Normal) timing
        private double perfect = 18;
        private double great = 40;
        private double good = 100;
        private double ok = 200;
        private double meh = 200;
        private double miss = 1000;

        public override bool IsHitResultAllowed(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                case HitResult.Great:
                case HitResult.Good:
                case HitResult.Ok:
                case HitResult.Meh:
                case HitResult.Miss:
                    return true;

                default:
                    return false;
            }
        }

        public override void SetDifficulty(double difficulty)
        {
            // BMS uses fixed timing windows, not affected by difficulty
            // But we can adjust based on RANK if needed later
        }

        public override double WindowFor(HitResult result)
        {
            return result switch
            {
                HitResult.Perfect => perfect,
                HitResult.Great => great,
                HitResult.Good => good,
                HitResult.Ok => ok,
                HitResult.Meh => meh,
                HitResult.Miss => miss,
                _ => 0
            };
        }
    }
}
