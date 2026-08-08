// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Localization;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    public static class BmsSongSelectLibraryOperations
    {
        public static BmsUiBackgroundOperation RunLibraryRefresh(
            Scheduler scheduler,
            BMSBeatmapManager beatmapManager,
            Storage storage,
            RealmAccess realm,
            RulesetInfo bmsRulesetInfo,
            INotificationOverlay? notifications,
            Action<long>? onComplete = null,
            CancellationToken cancellationToken = default)
        {
            if (beatmapManager.RootPaths.Count == 0)
            {
                notifications?.Post(new SimpleNotification { Text = BmsStrings.SONG_SELECT_ADD_LIBRARY_PATH_FIRST });
                return new BmsUiBackgroundOperation(new CancellationToken(canceled: true));
            }

            var notification = new ProgressNotification
            {
                Text = BmsStrings.SONG_SELECT_SCANNING_LIBRARY,
                Progress = 0,
            };

            notifications?.Post(notification);

            var operation = new BmsUiBackgroundOperation(cancellationToken);
            var linked = CancellationTokenSource.CreateLinkedTokenSource(operation.Token, notification.CancellationToken);
            CancellationToken token = linked.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await BmsLibraryImportPipeline.RunAsync(
                        beatmapManager,
                        storage,
                        realm,
                        bmsRulesetInfo,
                        beatmapManager.RootPaths,
                        p =>
                        {
                            if (token.IsCancellationRequested || operation.IsCancelled)
                                return;

                            scheduler.Add(() =>
                            {
                                if (token.IsCancellationRequested || operation.IsCancelled)
                                    return;

                                notification.Progress = (float)p.Progress;
                                notification.Text = p.StatusMessage;
                            });
                        },
                        token).ConfigureAwait(false);

                    scheduler.Add(() =>
                    {
                        if (operation.IsCancelled)
                            return;

                        notification.Progress = 1f;
                        notification.State = ProgressNotificationState.Completed;
                        onComplete?.Invoke(result.OperationId);
                    });
                }
                catch (OperationCanceledException)
                {
                    scheduler.Add(() =>
                    {
                        if (notification.State != ProgressNotificationState.Completed)
                            notification.State = ProgressNotificationState.Cancelled;
                    });
                }
                catch (Exception ex)
                {
                    scheduler.Add(() =>
                    {
                        if (operation.IsCancelled)
                            return;

                        Logger.Error(ex, "[BMS] library import failed");
                        notification.State = ProgressNotificationState.Cancelled;
                        notifications?.Post(new SimpleNotification { Text = BmsStrings.SongSelect_RefreshFailed(ex.Message) });
                    });
                }
                finally
                {
                    linked.Dispose();
                }
            }, CancellationToken.None);

            return operation;
        }
    }
}
