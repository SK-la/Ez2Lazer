// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// Diagnostic sampler for the post-startup frame-rate dip. Once per second it reports frame count,
    /// worst frame time, allocated bytes and GC collection counts through <see cref="EzStartupTrace"/>.
    ///
    /// Exists purely to attribute a startup dip (GC pauses vs CPU contention vs main-thread work) instead
    /// of only measuring its total length. Runs for a bounded window, then removes itself.
    /// </summary>
    public partial class EzStartupFrameSampler : CompositeDrawable
    {
        private const int max_report_seconds = 30;

        private double windowStart;
        private double lastFrameTime;
        private int frames;
        private double worstFrameMs;
        private int secondsReported;

        private long lastAllocatedBytes;
        private int lastGc0;
        private int lastGc1;
        private int lastGc2;

        public EzStartupFrameSampler()
        {
            RelativeSizeAxes = Axes.Both;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (!EzStartupTuning.SampleStartupFrames)
            {
                Expire();
                return;
            }

            double now = Clock.CurrentTime;
            windowStart = now;
            lastFrameTime = now;
            lastAllocatedBytes = GC.GetTotalAllocatedBytes();
            lastGc0 = GC.CollectionCount(0);
            lastGc1 = GC.CollectionCount(1);
            lastGc2 = GC.CollectionCount(2);
        }

        protected override void Update()
        {
            base.Update();

            if (!EzStartupTuning.SampleStartupFrames)
                return;

            double now = Clock.CurrentTime;
            double elapsedMs = now - lastFrameTime;
            lastFrameTime = now;

            frames++;

            if (elapsedMs > worstFrameMs)
                worstFrameMs = elapsedMs;

            if (now - windowStart < 1000)
                return;

            secondsReported++;

            long allocated = GC.GetTotalAllocatedBytes();
            int gc0 = GC.CollectionCount(0);
            int gc1 = GC.CollectionCount(1);
            int gc2 = GC.CollectionCount(2);

            double avgMs = frames > 0 ? (now - windowStart) / frames : 0;
            double allocMb = (allocated - lastAllocatedBytes) / 1024.0 / 1024.0;

            EzStartupTrace.Log($"FrameSample {secondsReported}s frames={frames} avg={fmt(avgMs)}ms worst={fmt(worstFrameMs)}ms "
                               + $"alloc={fmt(allocMb)}MB gc0={gc0 - lastGc0} gc1={gc1 - lastGc1} gc2={gc2 - lastGc2}");

            windowStart = now;
            frames = 0;
            worstFrameMs = 0;
            lastAllocatedBytes = allocated;
            lastGc0 = gc0;
            lastGc1 = gc1;
            lastGc2 = gc2;

            if (secondsReported >= max_report_seconds)
                Expire();
        }

        private static string fmt(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
