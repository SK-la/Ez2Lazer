// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Threading;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    /// <summary>
    /// Optional background prefetch for the shared Pixiv follow-feed catalog.
    /// Song changes always enqueue one background download; this adds periodic prefetch when enabled.
    /// </summary>
    public partial class PixivAutoDownloadProcessor : Component
    {
        [Resolved]
        private PixivBackgroundCoordinator coordinator { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        private IBindable<bool> autoDownloadEnabled = null!;
        private ScheduledDelegate? scheduledPrefetch;
        private bool prefetchInFlight;
        private bool authFailureNotified;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (DebugUtils.IsNUnitRunning)
                return;

            autoDownloadEnabled = ezConfig.GetBindable<bool>(Ez2Setting.PixivAutoDownloadEnabled);
            autoDownloadEnabled.BindValueChanged(e => updateSchedule(e.NewValue), true);
        }

        private void updateSchedule(bool enabled)
        {
            scheduledPrefetch?.Cancel();
            scheduledPrefetch = null;
            authFailureNotified = false;

            if (!enabled || !coordinator.Auth.HasRefreshToken)
                return;

            scheduleNext(0);
        }

        private void scheduleNext(double delay)
        {
            scheduledPrefetch = Scheduler.AddDelayed(() =>
            {
                if (!autoDownloadEnabled.Value)
                    return;

                runPrefetch();
                scheduleNext(PixivConstants.AUTO_PREFETCH_INTERVAL_MS);
            }, delay);
        }

        private void runPrefetch()
        {
            if (prefetchInFlight)
                return;

            prefetchInFlight = true;

            Task.Run(() =>
            {
                try
                {
                    if (!coordinator.RunBackgroundPrefetch(out string? error))
                    {
                        if (!string.IsNullOrWhiteSpace(error))
                            Scheduler.Add(() => handleFailure(error));
                    }
                }
                finally
                {
                    prefetchInFlight = false;
                }
            });
        }

        private void handleFailure(string error)
        {
            coordinator.LogFailure("Auto prefetch", error);

            if (authFailureNotified)
                return;

            authFailureNotified = true;
            ezConfig.SetValue(Ez2Setting.PixivAutoDownloadEnabled, false);

            notifications?.Post(new SimpleNotification
            {
                Text = EzSettingsStrings.PIXIV_AUTO_DOWNLOAD_PAUSED.Format(error),
            });
        }
    }
}
