// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement
{
    /// <summary>
    /// Spinner 判定纯函数（OSL-011）：Drawable 与 Session 同调。
    /// </summary>
    public static class OsuSpinnerJudgement
    {
        /// <summary>将相邻采样角差归一到 (-180, 180] 并乘 gameplayRate。</summary>
        public static float NormaliseRotationDelta(float angleAtStart, float angleAtEnd, double gameplayRate)
        {
            float delta = angleAtEnd - angleAtStart;

            if (delta > 180)
                delta -= 360;
            if (delta < -180)
                delta += 360;

            return (float)(delta * System.Math.Abs(gameplayRate));
        }

        public static HitResult BodyResultForProgress(double progress, HitResult minResult)
        {
            if (progress >= 1)
                return HitResult.Great;
            if (progress > .9)
                return HitResult.Ok;
            if (progress > .75)
                return HitResult.Meh;

            return minResult;
        }
    }
}
