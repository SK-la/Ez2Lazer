// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public class EzCustomHitWindows : HitWindows
    {
        public static DifficultyRange PerfectRange = new DifficultyRange(22.4D, 19.4D, 13.9D);
        public static DifficultyRange GreatRange = new DifficultyRange(64, 49, 34);
        public static DifficultyRange GoodRange = new DifficultyRange(97, 82, 67);
        public static DifficultyRange OkRange = new DifficultyRange(127, 112, 97);
        public static DifficultyRange MehRange = new DifficultyRange(151, 136, 121);
        public static DifficultyRange MissRange = new DifficultyRange(188, 173, 158);
        public static DifficultyRange PoolRange = new DifficultyRange(300, 500, 800);

        private double overallDifficulty;

        private double perfect;
        private double great;
        private double good;
        private double ok;
        private double meh;
        private double miss;
        private double pool;

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
            }

            return false;
        }

        public override void SetDifficulty(double difficulty)
        {
            overallDifficulty = difficulty;
            updateWindows();
        }

        public void SetSpecialDifficultyRange(double perfect, double great, double good, double ok, double meh, double miss, double? pool = 0)
        {
            PerfectRange = new DifficultyRange(perfect, perfect, perfect);
            GreatRange = new DifficultyRange(great, great, great);
            GoodRange = new DifficultyRange(good, good, good);
            OkRange = new DifficultyRange(ok, ok, ok);
            MehRange = new DifficultyRange(meh, meh, meh);
            MissRange = new DifficultyRange(miss, miss, miss);
            PoolRange = new DifficultyRange(pool ?? 0, pool ?? 0, pool ?? 0);
            updateWindows();
        }

        private void updateWindows()
        {
            perfect = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, PerfectRange)) + 0.5;
            great = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, GreatRange)) + 0.5;
            good = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, GoodRange)) + 0.5;
            ok = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, OkRange)) + 0.5;
            meh = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, MehRange)) + 0.5;
            miss = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, MissRange)) + 0.5;
            pool = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, PoolRange)) + 0.5;
        }

        public override double WindowFor(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return perfect;

                case HitResult.Great:
                    return great;

                case HitResult.Good:
                    return good;

                case HitResult.Ok:
                    return ok;

                case HitResult.Meh:
                    return meh;

                // case HitResult.Pool:
                //     return pool;

                case HitResult.Miss:
                    return miss;

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }
    }
}
