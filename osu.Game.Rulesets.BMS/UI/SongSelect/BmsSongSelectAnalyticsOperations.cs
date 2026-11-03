// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Localization;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    public static class BmsSongSelectAnalyticsOperations
    {
        public static BmsUiBackgroundOperation RunAnalyticsBuild(
            Scheduler scheduler,
            BMSBeatmapManager beatmapManager,
            BmsAnalyticsSqliteRepository repository,
            AudioManager audioManager,
            RealmAccess realm,
            INotificationOverlay? notifications,
            EzAnalysisDatabase? analysisDatabase = null,
            Action? onComplete = null,
            CancellationToken cancellationToken = default)
        {
            if (!beatmapManager.HasIndexedCharts)
            {
                notifications?.Post(new SimpleNotification { Text = BmsStrings.ANALYTICS_LIBRARY_EMPTY });
                return new BmsUiBackgroundOperation(new CancellationToken(canceled: true));
            }

            if (BmsAnalyticsScanService.IsRunning)
            {
                notifications?.Post(new SimpleNotification { Text = BmsStrings.ANALYTICS_BUILDING });
                return new BmsUiBackgroundOperation(new CancellationToken(canceled: true));
            }

            var notification = new ProgressNotification
            {
                Text = BmsStrings.ANALYTICS_BUILDING,
                Progress = 0,
            };

            notifications?.Post(notification);

            var operation = new BmsUiBackgroundOperation(cancellationToken);
            var linked = CancellationTokenSource.CreateLinkedTokenSource(operation.Token, notification.CancellationToken);
            CancellationToken token = linked.Token;
            var progress = new Progress<BmsAnalyticsScanProgress>(p =>
            {
                if (operation.IsCancelled)
                    return;

                postProgress(notification, p);
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    await BmsAnalyticsScanService.RunAsync(
                        beatmapManager,
                        repository,
                        audioManager,
                        progress,
                        token,
                        realm,
                        analysisDatabase).ConfigureAwait(false);

                    if (token.IsCancellationRequested || operation.IsCancelled)
                    {
                        scheduler.Add(() => markCancelled(notification));
                        return;
                    }

                    scheduler.Add(() =>
                    {
                        if (operation.IsCancelled)
                            return;

                        notification.Progress = 1f;
                        notification.Text = BmsStrings.ANALYTICS_BUILD_COMPLETE;
                        notification.State = ProgressNotificationState.Completed;
                        notification.CompletionText = BmsStrings.ANALYTICS_BUILD_COMPLETE;
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
                        if (operation.IsCancelled)
                            return;

                        Logger.Error(ex, "[BMS] analytics build failed");
                        notification.State = ProgressNotificationState.Cancelled;
                        notifications?.Post(new SimpleNotification { Text = BmsStrings.Analytics_BuildFailed(ex.Message) });
                    });
                }
                finally
                {
                    linked.Dispose();
                }
            }, CancellationToken.None);

            return operation;
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
            notification.Text = BmsStrings.ANALYTICS_CANCELLED;
        }
    }
}
