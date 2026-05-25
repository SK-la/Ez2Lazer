// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Configuration;

namespace osu.Game.EzOsuGame.Configuration
{
    public static class FrameSyncExtensions
    {
        public static double ToUpdateHz(this FrameSync frameSync, double baseHz, bool allowBenchmarkUnlimited)
            => applyLimit(frameSync, baseHz, allowBenchmarkUnlimited, forDraw: false);

        public static double ToDrawHz(this FrameSync frameSync, double baseHz, bool allowBenchmarkUnlimited)
            => applyLimit(frameSync, baseHz, allowBenchmarkUnlimited, forDraw: true);

        private static double applyLimit(FrameSync frameSync, double baseHz, bool allowBenchmarkUnlimited, bool forDraw)
        {
            int baseValue = Math.Max(1, (int)Math.Round(baseHz));
            int limiter = baseValue;

            switch (frameSync)
            {
                case FrameSync.VSync:
                    limiter = forDraw ? int.MaxValue : baseValue;
                    break;

                case FrameSync.Limit2x:
                    limiter = baseValue * 2;
                    break;

                case FrameSync.Limit4x:
                    limiter = baseValue * 4;
                    break;

                case FrameSync.Limit8x:
                    limiter = baseValue * 8;
                    break;

                case FrameSync.Unlimited:
                    limiter = int.MaxValue;
                    break;
            }

            if (!allowBenchmarkUnlimited)
                limiter = Math.Min(8000, limiter);

            return limiter;
        }
    }
}
