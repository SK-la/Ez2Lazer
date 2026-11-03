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

        public bool CanExecute(EzDataRebuildTarget target)
            => dialogOverlay != null && dispatcher.CanDispatch(target);

        public void RequestExecute(EzDataRebuildTarget target)
        {
            if (dialogOverlay == null)
            {
                notifications?.Post(new SimpleErrorNotification { Text = EzSettingsStrings.DATA_REBUILD_DIALOG_UNAVAILABLE });
                return;
            }

            dialogOverlay.Push(new EzDataRebuildConfirmDialog(
                () => notifyDispatchResult(Dispatch(target, forceAll: false)),
                () => notifyDispatchResult(Dispatch(target, forceAll: true))));
        }

        public EzDataRebuildDispatchResult Dispatch(EzDataRebuildTarget target, bool forceAll)
            => dispatcher.Execute(target, forceAll);

        public void NotifyDispatchResult(EzDataRebuildDispatchResult result)
            => notifyDispatchResult(result);

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

        private void notifyDispatchResult(EzDataRebuildDispatchResult result)
        {
            if (result == EzDataRebuildDispatchResult.Queued)
                return;

            notifications?.Post(new SimpleErrorNotification { Text = GetErrorMessage(result) });
        }
    }
}
