// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Menu;

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// Diagnostic sampler for the post-startup frame-rate dip. Accumulates metrics for a fixed wall-clock window,
    /// emits one summary through <see cref="EzStartupTrace"/>, then ends its lifetime. At most once per process.
    /// </summary>
    public partial class EzStartupFrameSampler : CompositeDrawable
    {
        private const int window_seconds = 30;
        private const string startup_trace_env = "EZ2LAZER_STARTUP_TRACE";

        private static bool sessionAttached;

        private double samplingStartTime;
        private double windowStart;
        private double lastUpdateTime;
        private int secondsElapsed;
        private int totalUpdates;
        private double worstUpdateGapMs;

        private long windowStartAllocatedBytes;
        private int windowStartGc0;
        private int windowStartGc1;
        private int windowStartGc2;

        private bool finished;

        public static bool IsEnabledForSession =>
            EzStartupTuning.EnableStartupFrameSampling
            || Environment.GetEnvironmentVariable(startup_trace_env) == "1";

        /// <summary>
        /// Returns whether <see cref="MainMenu"/> should attach the one session sampler (debug + opt-in only).
        /// </summary>
        public static bool TryClaimSessionAttach()
        {
            if (!DebugUtils.IsDebugBuild || !IsEnabledForSession || sessionAttached)
                return false;

            sessionAttached = true;
            return true;
        }

        public EzStartupFrameSampler()
        {
            RelativeSizeAxes = Axes.Both;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            double now = Clock.CurrentTime;
            samplingStartTime = windowStart = lastUpdateTime = now;
            windowStartAllocatedBytes = GC.GetTotalAllocatedBytes();
            windowStartGc0 = GC.CollectionCount(0);
            windowStartGc1 = GC.CollectionCount(1);
            windowStartGc2 = GC.CollectionCount(2);
        }

        protected override void Update()
        {
            if (finished)
                return;

            base.Update();

            double now = Clock.CurrentTime;
            double elapsedMs = now - lastUpdateTime;
            lastUpdateTime = now;

            totalUpdates++;

            if (elapsedMs > worstUpdateGapMs)
                worstUpdateGapMs = elapsedMs;

            if (now - windowStart < 1000)
                return;

            secondsElapsed++;
            windowStart = now;

            if (secondsElapsed >= window_seconds)
                finishSampling();
        }

        private void finishSampling()
        {
            if (finished)
                return;

            finished = true;

            long allocated = GC.GetTotalAllocatedBytes();
            int gc0 = GC.CollectionCount(0);
            int gc1 = GC.CollectionCount(1);
            int gc2 = GC.CollectionCount(2);

            double wallMs = Math.Max(1, Clock.CurrentTime - samplingStartTime);
            double avgMs = totalUpdates > 0 ? wallMs / totalUpdates : 0;
            double allocMb = (allocated - windowStartAllocatedBytes) / 1024.0 / 1024.0;

            EzStartupTrace.LogDiagnostic($"FrameSample summary window={window_seconds}s frames={totalUpdates} avg={fmt(avgMs)}ms "
                                         + $"worst={fmt(worstUpdateGapMs)}ms alloc={fmt(allocMb)}MB "
                                         + $"gc0={gc0 - windowStartGc0} gc1={gc1 - windowStartGc1} gc2={gc2 - windowStartGc2}");

            LifetimeEnd = Time.Current;
            Expire();
        }

        private static string fmt(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
