// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public readonly struct EzLocalProfileOnlinePullProgress
    {
        public int Processed { get; init; }
        public int Total { get; init; }
        public bool DownloadingMap { get; init; }
    }

    /// <summary>
    /// Progress UI for <see cref="EzLocalProfileOnlinePullService"/> — posted once at start so it keeps updating
    /// after the settings panel is closed (unlike scheduling finish work onto the settings drawable).
    /// </summary>
    public static class EzLocalProfileOnlinePullNotification
    {
        public static ProgressNotification Create() => new ProgressNotification
        {
            Text = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_BUSY,
            CompletionText = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_BUSY,
            State = ProgressNotificationState.Active,
        };

        public sealed class Forwarder : IProgress<EzLocalProfileOnlinePullProgress>
        {
            private const int min_report_interval_ms = 100;

            private readonly ProgressNotification notification;
            private readonly System.Diagnostics.Stopwatch sinceLastReport = System.Diagnostics.Stopwatch.StartNew();
            private int lastBasisPoints = -1;
            private bool lastDownloadingMap;

            public Forwarder(ProgressNotification notification)
            {
                this.notification = notification;
            }

            public void Report(EzLocalProfileOnlinePullProgress value)
            {
                if (notification.State is ProgressNotificationState.Cancelled or ProgressNotificationState.Completed)
                    return;

                int total = Math.Max(1, value.Total);
                int processed = Math.Clamp(value.Processed, 0, total);
                int basisPoints = (int)(10000L * processed / total);
                bool finished = processed >= total;
                bool mapFlagChanged = value.DownloadingMap != lastDownloadingMap;

                if (!mapFlagChanged && !finished
                    && !(basisPoints / 100 != lastBasisPoints / 100 && sinceLastReport.ElapsedMilliseconds >= min_report_interval_ms))
                    return;

                lastBasisPoints = basisPoints;
                lastDownloadingMap = value.DownloadingMap;
                sinceLastReport.Restart();

                notification.Text = LocalisableString.Format(
                    (value.DownloadingMap
                        ? EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_PROGRESS_MAP
                        : EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_PROGRESS).ToString(),
                    processed,
                    total);
                notification.Progress = Math.Min(0.99f, (float)processed / total);
            }
        }

        public static EzLocalProfileOnlinePullResult? Finish(
            Task<EzLocalProfileOnlinePullResult> pullTask,
            ProgressNotification notification,
            INotificationOverlay? notifications)
        {
            if (notification.State == ProgressNotificationState.Cancelled)
                return null;

            if (pullTask.IsFaulted)
            {
                notification.State = ProgressNotificationState.Cancelled;
                notifications?.Post(new SimpleErrorNotification { Text = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_FAILED });
                return null;
            }

            if (pullTask.IsCanceled)
            {
                notification.State = ProgressNotificationState.Cancelled;
                return null;
            }

            var result = pullTask.GetResultSafely();

            if (result.ErrorMessage == "need_online")
            {
                notification.State = ProgressNotificationState.Cancelled;
                notifications?.Post(new SimpleErrorNotification { Text = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_NEED_ONLINE });
                return result;
            }

            if (result.ErrorMessage == "already_pulling")
            {
                notification.State = ProgressNotificationState.Cancelled;
                notifications?.Post(new SimpleNotification { Text = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_BUSY });
                return result;
            }

            if (result.ErrorMessage == "cancelled")
            {
                notification.State = ProgressNotificationState.Cancelled;
                return result;
            }

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                notification.State = ProgressNotificationState.Cancelled;
                notifications?.Post(new SimpleErrorNotification { Text = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_FAILED });
                return result;
            }

            notification.Progress = 1f;
            notification.CompletionText = LocalisableString.Format(
                EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_DONE.ToString(),
                result.Candidates,
                result.Imported,
                result.AlreadyOwned,
                result.NoReplay,
                result.MissingBeatmap,
                result.Failed,
                result.StatsRecorded,
                result.MapsDownloaded,
                result.MapsAlreadyLocal,
                result.CollectionAdds);
            notification.State = ProgressNotificationState.Completed;
            return result;
        }
    }
}
