// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Database;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Chart-chain follow-up for the local score analysis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A play is folded into the SQLite slice the moment it settles into Realm (see
    /// <see cref="EzLocalProfileService.IngestSettledScore"/>), while the Realm-side skill rows are only flagged
    /// stale, so the archive stays correct without a launch-time recompute.
    /// </para>
    /// <para>
    /// This component <b>no longer auto-runs at launch</b>. It only owns the other half of the contract: the
    /// player pass reads chart-side data (MSD / CSI / ChartDan) but never computes it, so when a user-initiated
    /// compute reports charts the chain has not rated yet, this component asks the chain for them and re-runs the
    /// affected players once it has finished.
    /// </para>
    /// <para>
    /// It is not a process-level watcher — nothing runs while the game is idle, and a launch with nothing out of
    /// date does no work and posts nothing.
    /// </para>
    /// </remarks>
    public partial class EzLocalProfileStartupAlign : Component
    {
        /// <summary>Charts the player pass could not read, i.e. what the chart-side chain has to rate.</summary>
        private const EzRealmMetadataScope chart_side_scope =
            EzRealmMetadataScope.Msd | EzRealmMetadataScope.ChartSkillInfo | EzRealmMetadataScope.ChartDan;

        /// <summary>
        /// How many times one session may ask the chart chain for more rows. A transient chain failure would
        /// otherwise let "missing → queue → still missing" ping-pong for the life of the process.
        /// </summary>
        private const int chart_follow_up_budget = 3;

        [Resolved]
        private EzLocalProfileService localProfileService { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notificationOverlay { get; set; }

        [Resolved(CanBeNull = true)]
        private BackgroundDataStoreProcessor? backgroundDataStoreProcessor { get; set; }

        [Resolved(CanBeNull = true)]
        private ILocalUserPlayInfo? localUserPlayInfo { get; set; }

        private Action? onChartBackfillFinished;
        private readonly Lock chartFollowUpLock = new Lock();
        private bool chartBackfillRequested;
        private int chartFollowUpsRemaining = chart_follow_up_budget;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (DebugUtils.IsNUnitRunning)
                return;

            localProfileService.ChartSideBackfillRequested = requestChartSideBackfill;

            // 启动不再自动补算成绩分析：此前这里会在 BDSP 收工后自动跑一次对账，
            // 并因图表链回折连续触发多轮分析。现在只保留图表链回折——用户手动「计算本地成绩」
            // 报缺时排队补算，补完后再回头折一次受影响玩家（见 followUpAfterChartBackfill）。
            if (backgroundDataStoreProcessor != null)
            {
                onChartBackfillFinished = followUpAfterChartBackfill;
                backgroundDataStoreProcessor.EzRealmMetadataBackfillFinished += onChartBackfillFinished;
            }
        }

        /// <summary>
        /// The player pass hit charts with no chart-side rows: hand them to the chain that owns them, and leave the
        /// players whose own plays hit one flagged so the follow-up below re-derives exactly them.
        /// </summary>
        private void requestChartSideBackfill()
        {
            var processor = backgroundDataStoreProcessor;

            if (processor == null)
                return;

            lock (chartFollowUpLock)
            {
                if (chartFollowUpsRemaining <= 0)
                    return;

                chartBackfillRequested = true;
            }

            processor.QueueEzRealmMetadataRebuild(chart_side_scope, forceAll: false);
        }

        private void followUpAfterChartBackfill()
        {
            var processor = backgroundDataStoreProcessor;

            lock (chartFollowUpLock)
            {
                if (!chartBackfillRequested)
                    return;

                // A request that landed while the previous run was exiting starts its own backfill (and will fire
                // its own completion), so let that one drive the follow-up instead of racing it.
                if (processor?.IsEzRealmMetadataBackfillRunning == true)
                    return;

                chartBackfillRequested = false;
                chartFollowUpsRemaining--;
            }

            // The plan covers both halves of "behind": rows flagged for a play that could not be folded in yet, and
            // whatever the ledger shows the chain still owes - so the re-derive happens even if the flag was already
            // cleared by an earlier pass.
            startAlign();
        }

        private void startAlign()
        {
            Task.Factory.StartNew(runAlign, TaskCreationOptions.LongRunning);
        }

        private void runAlign()
        {
            try
            {
                // Gameplay first: a player mid-map should not have analysis competing with it for CPU.
                while (localUserPlayInfo?.PlayingState.Value is LocalUserPlayingState.Playing or LocalUserPlayingState.Break)
                    Thread.Sleep(1000);

                var plan = localProfileService.PlanStartupAlign();

                // Nothing out of date: stay silent, so a follow-up with no affected rows costs nothing and says nothing.
                if (!plan.HasWork)
                    return;

                var notification = EzLocalProfileComputeNotification.Create(EzSettingsProfile.LOCAL_PROFILE_ALIGN);
                notificationOverlay?.Post(notification);

                var progress = new EzLocalProfileComputeNotification.Forwarder(notification);

                localProfileService.AlignOnStartupAsync(plan, progress, notification.CancellationToken)
                                   .ContinueWith(t => EzLocalProfileComputeNotification.Finish(t, localProfileService, notification, notificationOverlay));
            }
            catch (Exception e)
            {
                Logger.Error(e, "[EzLocalProfile] Score analysis reconcile failed.", Ez2ConfigManager.LOGGER_NAME);
            }
        }

        private void unsubscribeChartBackfill()
        {
            if (backgroundDataStoreProcessor != null && onChartBackfillFinished != null)
            {
                backgroundDataStoreProcessor.EzRealmMetadataBackfillFinished -= onChartBackfillFinished;
                onChartBackfillFinished = null;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                unsubscribeChartBackfill();

                localProfileService.ChartSideBackfillRequested = null;
            }

            base.Dispose(isDisposing);
        }
    }
}
