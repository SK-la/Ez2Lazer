// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Startup backfill for the local score analysis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A play is folded into the SQLite slice the moment it settles into Realm (see
    /// <see cref="EzLocalProfileService.IngestSettledScore"/>), while the Realm-side skill rows are only flagged
    /// stale. This component closes that gap once per launch, after <see cref="BackgroundDataStoreProcessor"/> has
    /// finished its own startup work: it folds in plays that never made it into the slice (a crash or force close
    /// before the write landed) and refreshes the players whose skills were flagged.
    /// </para>
    /// <para>
    /// It is a launch-time reconcile, not a process-level watcher — nothing runs while the game is idle, and a
    /// launch with nothing out of date does no work and posts nothing.
    /// </para>
    /// </remarks>
    public partial class EzLocalProfileStartupAlign : Component
    {
        [Resolved]
        private EzLocalProfileService localProfileService { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notificationOverlay { get; set; }

        [Resolved(CanBeNull = true)]
        private BackgroundDataStoreProcessor? backgroundDataStoreProcessor { get; set; }

        [Resolved(CanBeNull = true)]
        private ILocalUserPlayInfo? localUserPlayInfo { get; set; }

        private Action? onBdspFinished;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (DebugUtils.IsNUnitRunning)
                return;

            if (backgroundDataStoreProcessor == null || backgroundDataStoreProcessor.IsStartupProcessingFinished)
            {
                startAlign();
                return;
            }

            onBdspFinished = startAlign;
            backgroundDataStoreProcessor.StartupProcessingFinished += onBdspFinished;
        }

        private void startAlign()
        {
            unsubscribeBdsp();
            Task.Factory.StartNew(runAlign, TaskCreationOptions.LongRunning);
        }

        private void runAlign()
        {
            try
            {
                // Gameplay first: a player mid-map should not have analysis competing with it for CPU.
                while (localUserPlayInfo?.PlayingState.Value is LocalUserPlayingState.Playing or LocalUserPlayingState.Break)
                    Thread.Sleep(1000);

                var plan = localProfileService.PlanStartupAlign();

                // Nothing out of date: stay silent so a normal launch costs nothing and says nothing.
                if (!plan.HasWork)
                    return;

                var notification = EzLocalProfileComputeNotification.Create(EzSettingsProfile.LOCAL_PROFILE_STARTUP_ALIGN);
                notificationOverlay?.Post(notification);

                var progress = new EzLocalProfileComputeNotification.Forwarder(notification);

                localProfileService.AlignOnStartupAsync(plan, progress, notification.CancellationToken)
                                   .ContinueWith(t => EzLocalProfileComputeNotification.Finish(t, localProfileService, notification, notificationOverlay));
            }
            catch (Exception e)
            {
                Logger.Error(e, "[EzLocalProfile] Startup align failed.", Ez2ConfigManager.LOGGER_NAME);
            }
        }

        private void unsubscribeBdsp()
        {
            if (backgroundDataStoreProcessor != null && onBdspFinished != null)
            {
                backgroundDataStoreProcessor.StartupProcessingFinished -= onBdspFinished;
                onBdspFinished = null;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                unsubscribeBdsp();

            base.Dispose(isDisposing);
        }
    }
}
