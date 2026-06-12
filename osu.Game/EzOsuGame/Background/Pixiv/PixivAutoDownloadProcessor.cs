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
    /// Periodically downloads one uncached illustration from the Pixiv follow feed into BG_PIXIV.
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
        private ScheduledDelegate? scheduledDownload;
        private bool downloadInFlight;
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
            scheduledDownload?.Cancel();
            scheduledDownload = null;
            authFailureNotified = false;

            if (!enabled || !coordinator.Auth.HasRefreshToken)
                return;

            scheduleNext(PixivConstants.AUTO_DOWNLOAD_INTERVAL_MS);
        }

        private void scheduleNext(double delay)
        {
            scheduledDownload = Scheduler.AddDelayed(() =>
            {
                if (!autoDownloadEnabled.Value)
                    return;

                runDownload();
                scheduleNext(PixivConstants.AUTO_DOWNLOAD_INTERVAL_MS);
            }, delay);
        }

        private void runDownload()
        {
            if (downloadInFlight)
                return;

            downloadInFlight = true;

            Task.Run(() =>
            {
                try
                {
                    if (!coordinator.TryDownloadNextUncached(out string? error))
                    {
                        if (!string.IsNullOrWhiteSpace(error))
                            Scheduler.Add(() => handleFailure(error));
                    }
                }
                finally
                {
                    downloadInFlight = false;
                }
            });
        }

        private void handleFailure(string error)
        {
            coordinator.LogFailure("Auto download", error);

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
