// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;
using osu.Framework.Graphics.Rendering;
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
            IRenderer renderer,
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

            _ = Task.Run(async () =>
            {
                try
                {
                    await BmsAnalyticsScanService.RunAsync(
                        beatmapManager,
                        repository,
                        audioManager,
                        renderer,
                        new Progress<BmsAnalyticsScanProgress>(p => scheduler.Add(() =>
                        {
                            notification.Progress = (float)p.Progress;
                            notification.Text = p.Status;
                        })),
                        cancellationToken).ConfigureAwait(false);

                    scheduler.Add(() =>
                    {
                        notification.Progress = 1f;
                        notification.State = ProgressNotificationState.Completed;
                        notification.CompletionText = "BMS 分析库构建完成";
                        onComplete?.Invoke();
                    });
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
            }, cancellationToken);
        }
    }
}
