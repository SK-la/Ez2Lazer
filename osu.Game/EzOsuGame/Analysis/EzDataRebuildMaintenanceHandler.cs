// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// Runs a maintenance action chosen in <see cref="EzDataRebuildDialog"/>: immediate backfill, or a confirmed
    /// full rebuild, and reports the outcome when the queue refuses the work.
    /// </summary>
    public class EzDataRebuildMaintenanceHandler
    {
        private readonly EzDataRebuildDispatcher dispatcher;
        private readonly IDialogOverlay? dialogOverlay;
        private readonly INotificationOverlay? notifications;

        public EzDataRebuildMaintenanceHandler(
            BackgroundDataStoreProcessor? backgroundDataStoreProcessor,
            EzAnalysisWarmupProcessor? analysisWarmupProcessor,
            IDialogOverlay? dialogOverlay,
            INotificationOverlay? notifications)
            : this(new EzDataRebuildDispatcher(backgroundDataStoreProcessor, analysisWarmupProcessor), dialogOverlay, notifications)
        {
        }

        internal EzDataRebuildMaintenanceHandler(
            EzDataRebuildDispatcher dispatcher,
            IDialogOverlay? dialogOverlay,
            INotificationOverlay? notifications)
        {
            this.dispatcher = dispatcher;
            this.dialogOverlay = dialogOverlay;
            this.notifications = notifications;
        }

        public bool CanExecute(EzDataRebuildAction action) => dispatcher.CanDispatch(action);

        /// <summary>
        /// Runs the action's backfill. An action whose processor is missing still reports through
        /// <see cref="NotifyDispatchResult"/> rather than being silently dropped.
        /// </summary>
        public EzDataRebuildDispatchResult RequestBackfill(EzDataRebuildAction action, bool forceAll)
        {
            var result = dispatcher.Execute(action, forceAll);
            NotifyDispatchResult(result);
            return result;
        }

        /// <summary>
        /// Asks before running the destructive form. Without a dialog overlay the request is reported and dropped,
        /// because a full rebuild clears existing results first.
        /// </summary>
        public void RequestForceRebuild(EzDataRebuildAction action)
        {
            if (!CanExecute(action))
            {
                notifications?.Post(new SimpleErrorNotification { Text = EzSettingsStrings.DATA_REBUILD_UNAVAILABLE });
                return;
            }

            if (dialogOverlay == null)
            {
                notifications?.Post(new SimpleErrorNotification { Text = EzSettingsStrings.DATA_REBUILD_DIALOG_UNAVAILABLE });
                return;
            }

            LocalisableString target = EzDataRebuildCatalog.Find(action)?.Name ?? EzSettingsStrings.DATA_REBUILD_DIALOG_HEADER;

            dialogOverlay.Push(new EzDataRebuildForceConfirmDialog(target, () => RequestBackfill(action, forceAll: true)));
        }

        public void NotifyDispatchResult(EzDataRebuildDispatchResult result)
        {
            if (result == EzDataRebuildDispatchResult.Queued)
                return;

            notifications?.Post(new SimpleErrorNotification { Text = GetErrorMessage(result) });
        }

        internal static LocalisableString GetErrorMessage(EzDataRebuildDispatchResult result)
        {
            return result switch
            {
                EzDataRebuildDispatchResult.UnavailableProcessor => EzSettingsStrings.DATA_REBUILD_UNAVAILABLE,
                EzDataRebuildDispatchResult.AlreadyRunning => EzSettingsStrings.DATA_REBUILD_ALREADY_RUNNING,
                EzDataRebuildDispatchResult.SqliteDisabled => EzSettingsStrings.DATA_REBUILD_SQLITE_DISABLED,
                _ => EzSettingsStrings.DATA_REBUILD_UNAVAILABLE,
            };
        }
    }
}
