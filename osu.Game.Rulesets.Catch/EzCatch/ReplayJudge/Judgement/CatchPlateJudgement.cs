// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.Replays;

namespace osu.Game.Rulesets.Catch.EzCatch.ReplayJudge.Judgement
{
    /// <summary>
    /// Catch 接盘判定纯函数（TSL-001）：Drawable <c>Catcher.CanCatch</c> 与 Session 同调。
    /// </summary>
    public static class CatchPlateJudgement
    {
        public static bool IsInPlate(float fruitX, float catcherX, float halfCatchWidth)
            => fruitX >= catcherX - halfCatchWidth && fruitX <= catcherX + halfCatchWidth;

        public static bool CanCatch(PalpableCatchHitObject fruit, float catcherX, float halfCatchWidth)
            => IsInPlate(fruit.EffectiveX, catcherX, halfCatchWidth);

        /// <summary>
        /// 在判定窗内是否接住。assist 关时与 Drawable 对齐：StartTime 检一次；
        /// 若未中，再检 StartTime+1ms（timeOffset&gt;0 的首次 CheckForResult，避免「下一帧任意远」误接）。
        /// </summary>
        public static bool IsCaughtInWindow(
            PalpableCatchHitObject fruit,
            System.Collections.Generic.IReadOnlyList<CatchReplayFrame> frames,
            float halfCatchWidth,
            double earlyMs,
            double lateMs)
        {
            double start = fruit.StartTime;

            if (isCaughtAt(fruit, frames, halfCatchWidth, start))
                return true;

            if (earlyMs <= 0 && lateMs <= 0)
                return isCaughtAt(fruit, frames, halfCatchWidth, start + 1);

            double windowStart = start - earlyMs;
            double windowEnd = start + lateMs;

            if (isCaughtAt(fruit, frames, halfCatchWidth, windowStart))
                return true;

            if (isCaughtAt(fruit, frames, halfCatchWidth, windowEnd))
                return true;

            for (int i = 0; i < frames.Count; i++)
            {
                double t = frames[i].Time;
                if (t <= windowStart || t >= windowEnd)
                    continue;

                if (isCaughtAt(fruit, frames, halfCatchWidth, t))
                    return true;
            }

            return false;
        }

        private static bool isCaughtAt(
            PalpableCatchHitObject fruit,
            System.Collections.Generic.IReadOnlyList<CatchReplayFrame> frames,
            float halfCatchWidth,
            double time)
            => CanCatch(fruit, InterpolateCatcherX(frames, time), halfCatchWidth);

        public static float InterpolateCatcherX(System.Collections.Generic.IReadOnlyList<CatchReplayFrame> frames, double time)
        {
            if (frames.Count == 0)
                return 0;

            if (time <= frames[0].Time)
                return frames[0].Position;

            for (int i = 1; i < frames.Count; i++)
            {
                if (time <= frames[i].Time)
                {
                    double span = frames[i].Time - frames[i - 1].Time;
                    if (span <= 0)
                        return frames[i].Position;

                    float t = (float)((time - frames[i - 1].Time) / span);
                    return frames[i - 1].Position + (frames[i].Position - frames[i - 1].Position) * t;
                }
            }

            return frames[^1].Position;
        }
    }
}
