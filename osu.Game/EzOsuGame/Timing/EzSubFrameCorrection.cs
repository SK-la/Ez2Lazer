// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using System.Threading;

namespace osu.Game.EzOsuGame.Timing
{
    /// <summary>
    /// Provides sub-frame timing correction for gameplay judgment.
    /// <para>
    /// Problem: When a key is pressed between UpdateThread frames, the judgment uses
    /// the previous frame's FSC ManualClock value, introducing up to ±(frame_time/2) error
    /// after player calibration. At low/variable frame rates (300-500fps), this is ±1.5-5ms.
    /// </para>
    /// <para>
    /// Solution: Record the wall-clock time when the FSC ManualClock is updated, and when
    /// the key is pressed. The difference gives the sub-frame correction.
    /// </para>
    /// </summary>
    public static class EzSubFrameCorrection
    {
        /// <summary>
        /// Wall-clock timestamp when the FSC ManualClock was last set.
        /// Written by FrameStabilityContainer.updateClock() on UpdateThread.
        /// </summary>
        private static long fscUpdateTimestamp;

        /// <summary>
        /// Whether sub-frame correction is enabled.
        /// </summary>
        public static bool Enabled = true;

        /// <summary>
        /// Called by FrameStabilityContainer when it updates the ManualClock.
        /// </summary>
        public static void RecordFscUpdate()
        {
            Interlocked.Exchange(ref fscUpdateTimestamp, Stopwatch.GetTimestamp());
        }

        /// <summary>
        /// Compute the sub-frame correction in milliseconds.
        /// This is the elapsed time between the FSC clock update and the key press,
        /// multiplied by the playback rate [correction = (keyPress - fscUpdate) * rate].
        /// </summary>
        /// <param name="keyWallTimestamp">The wall-clock timestamp of the key press,
        /// from <see cref="osu.Framework.Input.InputManager.EzSubFrameTimestamp"/>.</param>
        /// <param name="rate">The current playback rate (e.g. 1.0 for normal, 1.5 for DT).</param>
        /// <returns>Correction in milliseconds to add to Time.Current for more accurate judgment.
        /// Always >= 0 and clamped to a reasonable maximum.</returns>
        public static double GetCorrectionMs(long keyWallTimestamp, double rate = 1.0)
        {
            if (!Enabled || keyWallTimestamp <= 0)
                return 0;

            long fscTs = Interlocked.Read(ref fscUpdateTimestamp);

            if (fscTs <= 0 || keyWallTimestamp <= fscTs)
                return 0;

            double wallMs = (double)(keyWallTimestamp - fscTs) / Stopwatch.Frequency * 1000.0;

            // Clamp: correction should be positive and at most ~1 frame (reasonable max ~20ms).
            // Beyond this likely indicates a stale timestamp or other anomaly.
            return wallMs > 20.0 ? 0 : wallMs * rate;
        }
    }
}
