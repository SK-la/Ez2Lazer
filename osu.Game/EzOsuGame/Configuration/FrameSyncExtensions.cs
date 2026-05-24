// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Configuration;

namespace osu.Game.EzOsuGame.Configuration
{
    public static class FrameSyncExtensions
    {
        public static double ToUpdateHz(this FrameSync frameSync, int refreshRate, bool allowBenchmarkUnlimited)
        {
            if (refreshRate <= 0)
                refreshRate = 60;

            int updateLimiter = frameSync switch
            {
                FrameSync.VSync => refreshRate * 2,
                FrameSync.Limit2x => refreshRate * 2,
                FrameSync.Limit4x => refreshRate * 4,
                FrameSync.Limit8x => refreshRate * 8,
                FrameSync.Unlimited => int.MaxValue,
                _ => refreshRate * 2,
            };

            if (!allowBenchmarkUnlimited)
                updateLimiter = Math.Min(8000, updateLimiter);

            return updateLimiter;
        }
    }
}
