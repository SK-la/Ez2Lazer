// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Input;

namespace osu.Game.Rulesets.Catch
{
    /// <summary>
    /// 从 L/R 双转盘状态中选出当前 active 的 processor（单盘/双盘通用）。
    /// </summary>
    internal static class CatchScratchAxisResolver
    {
        internal static ScratchAxisProcessor? ResolveActive(ScratchAxisProcessor left, ScratchAxisProcessor right)
        {
            bool leftPressed = left.IsPressed.Value;
            bool rightPressed = right.IsPressed.Value;

            if (!leftPressed && !rightPressed)
                return null;

            if (leftPressed && !rightPressed)
                return left;

            if (rightPressed && !leftPressed)
                return right;

            return left.LastMotionTime >= right.LastMotionTime ? left : right;
        }
    }
}
