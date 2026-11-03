// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.StableClassic
{
    /// <summary>
    /// osu!stable std 原生判定窗口（classic，非 ScoreV2）。
    /// 半窗 ms：300 = 80 - 6*OD，100 = 140 - 8*OD，50 = 200 - 10*OD，取 (int) 截断；
    /// Easy / HardRock 对 OD 的改动已由谱面难度折入。判定用严格 &lt;（lazer 基类为 &lt;=）。
    /// </summary>
    /// <remarks>
    /// 仅供 StableClassic 路径使用。
    /// </remarks>
    public class StableStdHitWindows : HitWindows
    {
        /// <summary>stable std 的 miss 半窗（ms）。</summary>
        public const double MISS_WINDOW = 400;

        private double great;
        private double ok;
        private double meh;

        public override bool IsHitResultAllowed(HitResult result)
            => result is HitResult.Great or HitResult.Ok or HitResult.Meh or HitResult.Miss;

        public override void SetDifficulty(double difficulty)
        {
            double od = Math.Clamp(difficulty, 0, 10);

            // stable 逐档 (int) 截断，非四舍五入；这里 windows 为半窗。
            great = (int)(80 - 6 * od);
            ok = (int)(140 - 8 * od);
            meh = (int)(200 - 10 * od);
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

        public override HitResult ResultFor(double timeOffset)
        {
            double abs = Math.Abs(timeOffset);

            if (abs < great)
                return HitResult.Great;
            if (abs < ok)
                return HitResult.Ok;
            if (abs < meh)
                return HitResult.Meh;
            if (abs <= MISS_WINDOW)
                return HitResult.Miss;

            return HitResult.None;
        }
    }
}
