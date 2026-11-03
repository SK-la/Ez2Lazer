// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.Configuration
{
    public static class EzFrameLimiter
    {
        /// <summary>
        /// Stored in config when the user has not set a base rate yet. On first use, display refresh rate is applied.
        /// </summary>
        public const double UNINITIALISED_BASE = 0;

        public static int GetDisplayRefreshRate(GameHost? host)
        {
            if (host?.Window == null)
                return 60;

            int refreshRate = (int)MathF.Round(host.Window.CurrentDisplayMode.Value.RefreshRate);

            if (refreshRate <= 0)
                refreshRate = 60;

            return refreshRate;
        }

        public static void InitialiseBaseIfNeeded(Bindable<double> baseFrameLimiter, GameHost host)
        {
            if (baseFrameLimiter.Value > UNINITIALISED_BASE)
                return;

            baseFrameLimiter.Value = GetDisplayRefreshRate(host);
        }

        public static void Apply(GameHost host, FrameSync updateFrameSync, FrameSync drawFrameSync, Bindable<double> baseFrameLimiter)
        {
            InitialiseBaseIfNeeded(baseFrameLimiter, host);

            double baseHz = baseFrameLimiter.Value;

            host.MaximumUpdateHz = updateFrameSync.ToUpdateHz(baseHz, host.AllowBenchmarkUnlimitedFrames);
            host.MaximumDrawHz = drawFrameSync.ToDrawHz(baseHz, host.AllowBenchmarkUnlimitedFrames);
        }
    }
}
