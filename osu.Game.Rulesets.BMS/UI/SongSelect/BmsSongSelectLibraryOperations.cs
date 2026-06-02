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
        public static void RunLibraryRefresh(
            Scheduler scheduler,
            BMSBeatmapManager beatmapManager,
            Storage storage,
            RealmAccess realm,
            RulesetInfo bmsRulesetInfo,
            INotificationOverlay? notifications,
            Action? onComplete = null)
        {
            if (beatmapManager.RootPaths.Count == 0)
            {
                notifications?.Post(new SimpleNotification { Text = BmsStrings.SONG_SELECT_ADD_LIBRARY_PATH_FIRST });
                return;
            }

            var notification = new ProgressNotification
            {
                Text = BmsStrings.SONG_SELECT_SCANNING_LIBRARY,
                Progress = 0,
            };

            notifications?.Post(notification);

            _ = Task.Run(async () =>
            {
                try
                {
                    await BmsLibraryImportPipeline.RunAsync(
                        beatmapManager,
                        storage,
                        realm,
                        bmsRulesetInfo,
                        beatmapManager.RootPaths,
                        p => scheduler.Add(() =>
                        {
                            notification.Progress = (float)p.Progress;
                            notification.Text = p.StatusMessage;
                        })).ConfigureAwait(false);

                    scheduler.Add(() =>
                    {
                        notification.Progress = 1f;
                        notification.State = ProgressNotificationState.Completed;
                        onComplete?.Invoke();
                    });
                }
                catch (Exception ex)
                {
                    scheduler.Add(() =>
                    {
                        Logger.Error(ex, "[BMS] library import failed");
                        notification.State = ProgressNotificationState.Cancelled;
                        notifications?.Post(new SimpleNotification { Text = BmsStrings.SongSelect_RefreshFailed(ex.Message) });
                    });
                }
            });
        }
    }
}
