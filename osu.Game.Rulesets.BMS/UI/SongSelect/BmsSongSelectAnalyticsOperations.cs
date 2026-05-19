// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    public static class BmsSongSelectAnalyticsOperations
    {
        public static void RunAnalyticsBuild(
            Scheduler scheduler,
            BMSBeatmapManager beatmapManager,
            BmsAnalyticsSqliteRepository repository,
            AudioManager audioManager,
            INotificationOverlay? notifications,
            Action? onComplete = null,
            CancellationToken cancellationToken = default)
        {
            if (!beatmapManager.HasIndexedCharts)
            {
                notifications?.Post(new SimpleNotification { Text = "BMS 曲库为空，请先扫描曲库" });
                return;
            }

            var notification = new ProgressNotification
            {
                Text = "正在构建 BMS 分析库...",
                Progress = 0,
            };

            notifications?.Post(notification);

            using var scanCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, notification.CancellationToken);

            var progress = new Progress<BmsAnalyticsScanProgress>(p => postProgress(notification, p));

            _ = Task.Run(async () =>
            {
                try
                {
                    await BmsAnalyticsScanService.RunAsync(
                        beatmapManager,
                        repository,
                        audioManager,
                        progress,
                        scanCancellation.Token).ConfigureAwait(false);

                    if (scanCancellation.Token.IsCancellationRequested)
                    {
                        scheduler.Add(() => markCancelled(notification));
                        return;
                    }

                    scheduler.Add(() =>
                    {
                        notification.Progress = 1f;
                        notification.Text = "BMS 分析库构建完成";
                        notification.State = ProgressNotificationState.Completed;
                        notification.CompletionText = "BMS 分析库构建完成";
                        onComplete?.Invoke();
                    });
                }
                catch (OperationCanceledException)
                {
                    scheduler.Add(() => markCancelled(notification));
                }
                catch (Exception ex)
                {
                    scheduler.Add(() =>
                    {
                        Logger.Error(ex, "[BMS] analytics build failed");
                        notification.State = ProgressNotificationState.Cancelled;
                        notifications?.Post(new SimpleNotification { Text = $"分析库构建失败：{ex.Message}" });
                    });
                }
            }, scanCancellation.Token);
        }

        private static void postProgress(ProgressNotification notification, BmsAnalyticsScanProgress p)
        {
            if (notification.State is ProgressNotificationState.Cancelled or ProgressNotificationState.Completed)
                return;

            // ProgressNotification.Progress/Text already marshal via their own Scheduler.AddOnce on the update thread.
            notification.Progress = (float)Math.Clamp(p.Progress, 0, 1);
            notification.Text = p.Status;
        }

        private static void markCancelled(ProgressNotification notification)
        {
            if (notification.State == ProgressNotificationState.Completed)
                return;

            notification.State = ProgressNotificationState.Cancelled;
            notification.Text = "BMS 分析已取消";
        }
    }
}
