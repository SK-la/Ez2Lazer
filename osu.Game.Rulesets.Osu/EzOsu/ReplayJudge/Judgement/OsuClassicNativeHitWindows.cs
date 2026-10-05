// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement
{
    /// <summary>
    /// ClassicNative 轨命中窗口（OSL-013）：对齐 osu!stable OD 公式的近似表。
    /// Drawable 接线与 classic 总分公式仍 open。
    /// </summary>
    public class OsuClassicNativeHitWindows : HitWindows
    {
        // stable: 300 = 80 - 6*OD, 100 = 140 - 8*OD, 50 = 200 - 10*OD（ms，半窗）。
        public static readonly DifficultyRange GREAT_WINDOW_RANGE = new DifficultyRange(80, 50, 20);
        public static readonly DifficultyRange OK_WINDOW_RANGE = new DifficultyRange(140, 80, 40);
        public static readonly DifficultyRange MEH_WINDOW_RANGE = new DifficultyRange(200, 120, 60);

        public const double MISS_WINDOW = 400;

        private double great;
        private double ok;
        private double meh;

        public override bool IsHitResultAllowed(HitResult result)
        {
            switch (result)
            {
                case HitResult.Great:
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
            great = IBeatmapDifficultyInfo.DifficultyRange(difficulty, GREAT_WINDOW_RANGE);
            ok = IBeatmapDifficultyInfo.DifficultyRange(difficulty, OK_WINDOW_RANGE);
            meh = IBeatmapDifficultyInfo.DifficultyRange(difficulty, MEH_WINDOW_RANGE);
        }

        public override double WindowFor(HitResult result)
        {
            switch (result)
            {
                case HitResult.Great:
                    return great;

                case HitResult.Ok:
                    return ok;

                case HitResult.Meh:
                    return meh;

                case HitResult.Miss:
                    return MISS_WINDOW;

                default:
                    return 0;
            }
        }
    }
}
