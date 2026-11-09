// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Startup;
using osu.Game.Performance;
using osu.Game.Screens.Play;

namespace osu.Game.Database
{
    /// <summary>
    /// Decides when <see cref="RealmDetachedBeatmapStore"/> may apply Realm change notifications to the detached
    /// carousel list. Realm persistence is unaffected; only UI sync is throttled.
    /// </summary>
    public sealed class DetachedBeatmapStoreSyncPolicy
    {
        private double muteUntil;

        private double drainWindowOpenUntil;

        private double lastIntervalGatingClock;

        /// <summary>
        /// Mutes detached-store drain briefly after screen stack transitions so navigation stays smooth.
        /// </summary>
        public void NotifyScreenTransition(double currentTime)
        {
            double muteUntilCandidate = currentTime + EzDetachedBeatmapStoreTuning.ScreenTransitionMuteMs;

            if (muteUntilCandidate > muteUntil)
                muteUntil = muteUntilCandidate;
        }

        /// <summary>
        /// Whether pending ops may be applied this frame.
        /// </summary>
        public bool AllowDrain(double currentTime, ILocalUserPlayInfo? localUserPlayInfo, IHighPerformanceSessionManager? highPerformanceSessionManager)
        {
            if (currentTime < muteUntil)
                return false;

            if (localUserPlayInfo != null && localUserPlayInfo.PlayingState.Value != LocalUserPlayingState.NotPlaying)
                return false;

            if (highPerformanceSessionManager?.IsSessionActive == true)
                return false;

            if (!EzDetachedBeatmapStoreTuning.UseDrainIntervalGating)
                return true;

            if (currentTime < drainWindowOpenUntil)
                return true;

            double interval = EzDetachedBeatmapStoreTuning.DrainIntervalMs;

            if (interval <= 0)
                return true;

            if (lastIntervalGatingClock <= 0)
                lastIntervalGatingClock = currentTime;

            if (currentTime - lastIntervalGatingClock < interval)
                return false;

            lastIntervalGatingClock = currentTime;
            drainWindowOpenUntil = currentTime + EzDetachedBeatmapStoreTuning.DrainWindowMs;
            return true;
        }

        public double DrainTimeBudgetMilliseconds => EzDetachedBeatmapStoreTuning.DrainTimeBudgetMs;

        public int DrainMaxOpsPerFrame => EzDetachedBeatmapStoreTuning.DrainMaxOpsPerFrame;
    }
}
