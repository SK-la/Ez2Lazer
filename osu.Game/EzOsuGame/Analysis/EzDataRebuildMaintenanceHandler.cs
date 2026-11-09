// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
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
        /// Runs the backfill for every selected action. Actions sharing a queue are merged, so a selection of
        /// Mina + CSI queues one Realm pass instead of reporting the second request as already running.
        /// </summary>
        public EzDataRebuildDispatchOutcome RequestBackfill(IReadOnlyList<EzDataRebuildAction> actions, bool forceAll)
        {
            var outcome = dispatcher.Execute(actions, forceAll);

            foreach (var failure in outcome.Failures)
                notifications?.Post(new SimpleErrorNotification { Text = GetErrorMessage(failure.Result) });

            return outcome;
        }

        public EzDataRebuildDispatchResult RequestBackfill(EzDataRebuildAction action, bool forceAll)
            => RequestBackfill(new[] { action }, forceAll).FirstFailure;

        /// <summary>
        /// Asks before running the destructive form. Without a dialog overlay the request is reported and dropped,
        /// because a full rebuild clears existing results first.
        /// </summary>
        public void RequestForceRebuild(IReadOnlyList<EzDataRebuildAction> actions)
        {
            if (actions.Count == 0)
                return;

            var snapshot = actions.ToArray();

            if (!snapshot.Any(CanExecute))
            {
                notifications?.Post(new SimpleErrorNotification { Text = EzSettingsStrings.DATA_REBUILD_UNAVAILABLE });
                return;
            }

            if (dialogOverlay == null)
            {
                notifications?.Post(new SimpleErrorNotification { Text = EzSettingsStrings.DATA_REBUILD_DIALOG_UNAVAILABLE });
                return;
            }

            LocalisableString target = EzDataRebuildCatalog.DisplayNames(snapshot);
            dialogOverlay.Push(new EzDataRebuildForceConfirmDialog(target, () => RequestBackfill(snapshot, forceAll: true)));
        }

        public void RequestForceRebuild(EzDataRebuildAction action) => RequestForceRebuild(new[] { action });

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
