// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.LAsEzExtensions.Configuration
{
    /// <summary>
    /// Stores an A/B loop time range for the current application session.
    /// Values are in milliseconds and are not persisted after exiting the game.
    /// </summary>
    public static class LoopTimeRangeStore
    {
        public static readonly Bindable<double> START_TIME_MS = new Bindable<double>();
        public static readonly Bindable<double> END_TIME_MS = new Bindable<double>();

        public static void Set(double startTimeMs, double endTimeMs)
        {
            if (endTimeMs <= startTimeMs)
                return;

            START_TIME_MS.Value = startTimeMs;
            END_TIME_MS.Value = endTimeMs;
        }

        public static bool TryGet(out double startTimeMs, out double endTimeMs)
        {
            startTimeMs = START_TIME_MS.Value;
            endTimeMs = END_TIME_MS.Value;

            return endTimeMs > startTimeMs;
        }
    }
}
