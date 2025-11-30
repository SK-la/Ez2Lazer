// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject
{
    public class Ez2AcHitWindows : HitWindows
    {
        private double speedMultiplier = 1;

        public static DifficultyRange PerfectRange = new DifficultyRange(22.4D, 19.4D, 13.9D);
        public static DifficultyRange GreatRange = new DifficultyRange(64, 49, 34);
        public static DifficultyRange GoodRange = new DifficultyRange(97, 82, 67);
        public static DifficultyRange OkRange = new DifficultyRange(127, 112, 97);
        public static DifficultyRange MehRange = new DifficultyRange(151, 136, 121);
        public static DifficultyRange MissRange = new DifficultyRange(188, 173, 158);
        public static DifficultyRange PoolRange = new DifficultyRange(200, 300, 500);

        public double SpeedMultiplier
        {
            get => speedMultiplier;
            set
            {
                speedMultiplier = value;
                updateWindows();
            }
        }

        private double difficultyMultiplier = 1;

        /// <summary>
        /// Multiplier used to make the gameplay more or less difficult.
        /// <list type="bullet">
        /// <item>When the <see cref="DifficultyMultiplier"/> is above 1, the hit windows decrease to make the gameplay harder.</item>
        /// <item>When the <see cref="DifficultyMultiplier"/> is below 1, the hit windows increase to make the gameplay easier.</item>
        /// </list>
        /// </summary>
        public double DifficultyMultiplier
        {
            get => difficultyMultiplier;
            set
            {
                difficultyMultiplier = value;
                updateWindows();
            }
        }

        private double totalMultiplier => speedMultiplier / difficultyMultiplier;

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
                case HitResult.IgnoreHit:
                case HitResult.IgnoreMiss:
                case HitResult.Pool:
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

        public void SetSpecialDifficultyRange(DifficultyRange[] difficultyRangeArray)
        {
            PerfectRange = difficultyRangeArray[0];
            GreatRange = difficultyRangeArray[1];
            GoodRange = difficultyRangeArray[2];
            OkRange = difficultyRangeArray[3];
            MehRange = difficultyRangeArray[4];
            MissRange = difficultyRangeArray[5];
            PoolRange = difficultyRangeArray[6];
            updateWindows();
        }

        private void updateWindows()
        {
            perfect = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, PerfectRange) * totalMultiplier) + 0.5;
            great = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, GreatRange) * totalMultiplier) + 0.5;
            good = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, GoodRange) * totalMultiplier) + 0.5;
            ok = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, OkRange) * totalMultiplier) + 0.5;
            meh = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, MehRange) * totalMultiplier) + 0.5;
            miss = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, MissRange) * totalMultiplier) + 0.5;
            pool = Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, PoolRange) * totalMultiplier) + 0.5;
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

                case HitResult.Miss:
                    return miss;

                case HitResult.Pool:
                    return pool;

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }
    }
}
