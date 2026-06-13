// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Threading;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    /// <summary>
    /// Periodic prefetch when <see cref="Ez2Setting.PixivAutoDownloadEnabled"/> is on.
    /// Song changes use <see cref="PixivBackgroundCoordinator.EnqueueSongChangeDownload"/> separately.
    /// Network failures are logged only; the next attempt is the next scheduled tick or a song change.
    /// </summary>
    public partial class PixivAutoDownloadProcessor : Component
    {
        [Resolved]
        private PixivBackgroundCoordinator coordinator { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        private IBindable<bool> autoDownloadEnabled = null!;
        private ScheduledDelegate? scheduledPrefetch;
        private bool prefetchInFlight;

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
                    coordinator.RunBackgroundPrefetch(out string? error);

                    if (!string.IsNullOrWhiteSpace(error))
                        coordinator.LogFailure("Auto prefetch", error);
                }
                finally
                {
                    prefetchInFlight = false;
                }
            });
        }
    }
}
