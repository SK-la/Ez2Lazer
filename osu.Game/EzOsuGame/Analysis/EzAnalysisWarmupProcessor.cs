// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Performance;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 启动时仅在 SQLite 主库需要版本/ schema 升级时自动补算；已匹配最新版文件时不自动预热。
    /// 其余 SQLite 维护由设置页手动触发；Realm 基线仍由 BackgroundDataStoreProcessor 负责。
    /// </summary>
    public partial class EzAnalysisWarmupProcessor : Component
    {
        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private RealmAccess realmAccess { get; set; } = null!;

        [Resolved]
        private EzAnalysisDatabase analysisDatabase { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> currentBeatmap { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notificationOverlay { get; set; }

        [Resolved]
        private ILocalUserPlayInfo? localUserPlayInfo { get; set; }

        [Resolved]
        private IHighPerformanceSessionManager? highPerformanceSessionManager { get; set; }

        private IBindable<bool> sqliteEnabledBindable = null!;
        private bool sqliteEnabled;
        private bool backgroundWorkersStarted;
        private readonly object sqliteManualRebuildLock = new object();
        private bool sqliteMainRebuildQueued;
        private bool sqliteSongsBranchesRebuildQueued;

        protected virtual int TimeToSleepDuringGameplay => 30000;
        private readonly object pendingBeatmapLock = new object();
        private readonly Queue<Guid> pendingBeatmapQueue = new Queue<Guid>();
        private readonly HashSet<Guid> inFlightBeatmapIds = new HashSet<Guid>();
        private ProgressNotification? startupWarmupProgressNotification;
        private int startupWarmupTotalCount;
        private int startupWarmupProcessedCount;
        private int lastStartupWarmupProgressReported = -1;

        private const int warmup_progress_update_interval = 50;
        private readonly SemaphoreSlim startupWarmupSignal = new SemaphoreSlim(0, 1);
        private readonly SemaphoreSlim selectedBeatmapRecomputeSignal = new SemaphoreSlim(0, 1);
        private readonly CancellationTokenSource startupWarmupCancellationSource = new CancellationTokenSource();
        private readonly CancellationTokenSource selectedBeatmapRecomputeCancellationSource = new CancellationTokenSource();
        private CancellationTokenSource pendingBeatmapRecomputeCancellationSource = new CancellationTokenSource();

        private Task startupWarmupTask = Task.CompletedTask;
        private Task selectedBeatmapRecomputeTask = Task.CompletedTask;
        private Guid? queuedSelectedBeatmapId;
        private bool startupWarmupSignalPending;
        private bool selectedBeatmapRecomputeSignalPending;

        private volatile bool pendingScanCompleted;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            sqliteEnabledBindable = ezConfig.GetBindable<bool>(Ez2Setting.EzAnalysisSqliteEnabled);

            currentBeatmap.BindValueChanged(beatmap => queueSelectedBeatmapRecomputeIfRequired(beatmap.NewValue), true);
            sqliteEnabledBindable.BindValueChanged(enabled => onSqliteEnabledChanged(enabled.NewValue), true);
        }

        private void onSqliteEnabledChanged(bool enabled)
        {
            if (DebugUtils.IsNUnitRunning)
            {
                sqliteEnabled = false;
                pendingScanCompleted = false;
                cancelPendingBeatmapProcessing();
                return;
            }

            sqliteEnabled = enabled;

            if (enabled)
            {
                ensureBackgroundWorkersStarted();
                pendingScanCompleted = true;
                Schedule(() => queueSelectedBeatmapRecomputeIfRequired(currentBeatmap.Value));
                tryQueueAutomaticSqliteUpgradeWarmup();
                return;
            }

            pendingScanCompleted = false;
            cancelPendingBeatmapProcessing();
        }

        private void ensureBackgroundWorkersStarted()
        {
            if (DebugUtils.IsNUnitRunning)
                return;

            if (backgroundWorkersStarted)
                return;

            backgroundWorkersStarted = true;

            startupWarmupTask = Task.Factory.StartNew(() => processStartupWarmupQueue(startupWarmupCancellationSource.Token),
                startupWarmupCancellationSource.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            selectedBeatmapRecomputeTask = Task.Factory.StartNew(() => processSelectedBeatmapRecomputeQueue(selectedBeatmapRecomputeCancellationSource.Token),
                selectedBeatmapRecomputeCancellationSource.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private void tryQueueAutomaticSqliteUpgradeWarmup()
        {
            if (!sqliteEnabled || !analysisDatabase.ShouldRunAutomaticSqliteWarmup())
                return;

            Task.Factory.StartNew(() =>
            {
                try
                {
                    Logger.Log("Automatic SQLite upgrade warmup started.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);

                    analysisDatabase.EnsureInitialised();
                    executeSqliteMainRebuild(forceAll: false);
                    executeSqliteSongsBranchesRebuild(forceAll: false);

                    Logger.Log("Automatic SQLite upgrade warmup finished.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Automatic SQLite upgrade warmup failed.", Ez2ConfigManager.LOGGER_NAME);
                }
            }, TaskCreationOptions.LongRunning);
        }

        public EzDataRebuildDispatchResult QueueSqliteMainRebuild(bool forceAll)
        {
            if (!sqliteEnabled)
                return EzDataRebuildDispatchResult.SqliteDisabled;

            lock (sqliteManualRebuildLock)
            {
                if (sqliteMainRebuildQueued)
                {
                    Logger.Log("SQLite main rebuild is already queued; ignoring duplicate request.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                    return EzDataRebuildDispatchResult.AlreadyRunning;
                }

                sqliteMainRebuildQueued = true;
            }

            Task.Factory.StartNew(() =>
            {
                try
                {
                    executeSqliteMainRebuild(forceAll);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "SQLite main rebuild failed.", Ez2ConfigManager.LOGGER_NAME);
                }
                finally
                {
                    lock (sqliteManualRebuildLock)
                        sqliteMainRebuildQueued = false;
                }
            }, TaskCreationOptions.LongRunning);

            return EzDataRebuildDispatchResult.Queued;
        }

        public EzDataRebuildDispatchResult QueueSqliteSongsBranchesRebuild(bool forceAll)
        {
            if (!sqliteEnabled)
                return EzDataRebuildDispatchResult.SqliteDisabled;

            lock (sqliteManualRebuildLock)
            {
                if (sqliteSongsBranchesRebuildQueued)
                {
                    Logger.Log("SQLite songs branch rebuild is already queued; ignoring duplicate request.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                    return EzDataRebuildDispatchResult.AlreadyRunning;
                }

                sqliteSongsBranchesRebuildQueued = true;
            }

            Task.Factory.StartNew(() =>
            {
                try
                {
                    executeSqliteSongsBranchesRebuild(forceAll);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "SQLite songs branch rebuild failed.", Ez2ConfigManager.LOGGER_NAME);
                }
                finally
                {
                    lock (sqliteManualRebuildLock)
                        sqliteSongsBranchesRebuildQueued = false;
                }
            }, TaskCreationOptions.LongRunning);

            return EzDataRebuildDispatchResult.Queued;
        }

        private void executeSqliteMainRebuild(bool forceAll)
        {
            if (!sqliteEnabled)
                return;

            if (forceAll)
                analysisDatabase.TrySetForceRecompute(true);

            IReadOnlyList<Guid> needingRecompute = forceAll
                ? collectAllBeatmapIdsForSqlite()
                : collectBeatmapsNeedingMainSqliteBackfill();

            if (needingRecompute.Count == 0)
            {
                Logger.Log("No beatmaps require analysis recompute.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                return;
            }

            Logger.Log($"Manual SQLite main rebuild queued {needingRecompute.Count} beatmap(s) (forceAll={forceAll}).",
                Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);

            queueBeatmapsForMainSqliteBackfill(needingRecompute);
        }

        private void executeSqliteSongsBranchesRebuild(bool forceAll)
        {
            if (!sqliteEnabled)
                return;

            var refreshPlans = forceAll
                ? analysisDatabase.GetAllSongsBranchesForceRefreshPlans()
                : analysisDatabase.GetSongsBranchesNeedingRefresh();

            runSongsBranchRefreshPlans(refreshPlans);
        }

        private List<(Guid id, string hash, int rulesetOnlineId)> collectBeatmapDescriptorsForSqlite()
        {
            var beatmaps = new List<(Guid id, string hash, int rulesetOnlineId)>();

            realmAccess.Run(r =>
            {
                foreach (var b in r.All<BeatmapInfo>())
                {
                    if (b.BeatmapSet == null)
                        continue;

                    if (b.BeatmapSet is { IsExternallyHosted: true })
                        continue;

                    beatmaps.Add((b.ID, b.Hash, b.Ruleset.OnlineID));
                }
            });

            return beatmaps;
        }

        private IReadOnlyList<Guid> collectBeatmapsNeedingMainSqliteBackfill()
        {
            var beatmaps = collectBeatmapDescriptorsForSqlite();

            if (beatmaps.Count == 0 || !sqliteEnabled)
                return Array.Empty<Guid>();

            return analysisDatabase.GetBeatmapsNeedingRecompute(beatmaps);
        }

        private IReadOnlyList<Guid> collectAllBeatmapIdsForSqlite()
        {
            var beatmapIds = new List<Guid>();

            realmAccess.Run(r =>
            {
                foreach (var b in r.All<BeatmapInfo>())
                {
                    if (b.BeatmapSet == null)
                        continue;

                    if (b.BeatmapSet is { IsExternallyHosted: true })
                        continue;

                    beatmapIds.Add(b.ID);
                }
            });

            return beatmapIds;
        }

        private void queueBeatmapsForMainSqliteBackfill(IReadOnlyList<Guid> beatmapIds)
        {
            bool shouldSignalStartupWarmup = false;

            lock (pendingBeatmapLock)
            {
                pendingBeatmapQueue.Clear();
                startupWarmupProcessedCount = 0;
                lastStartupWarmupProgressReported = -1;

                foreach (Guid id in beatmapIds)
                    pendingBeatmapQueue.Enqueue(id);

                if (!startupWarmupSignalPending)
                {
                    startupWarmupSignalPending = true;
                    shouldSignalStartupWarmup = true;
                }
            }

            if (shouldSignalStartupWarmup)
                startupWarmupSignal.Release();

            postWarmupSummaryNotification(beatmapIds.Count);
        }

        private void runSongsBranchRefreshPlans(IReadOnlyList<EzAnalysisDatabase.SongsBranchRefreshPlan> refreshPlans)
        {
            if (!sqliteEnabled)
                return;

            if (refreshPlans.Count == 0)
            {
                Logger.Log("No songs branches require xxy/PP refresh.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                return;
            }

            int totalBeatmaps = refreshPlans.Sum(plan => plan.Branch.Metadata.BeatmapCount);
            Logger.Log($"Songs branch refresh queued for {refreshPlans.Count} branch(es), ~{totalBeatmaps} beatmap(s).",
                Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);

            postSongsBranchRefreshNotification(refreshPlans.Count, totalBeatmaps);

            int totalBranches = refreshPlans.Count;

            for (int branchIndex = 0; branchIndex < totalBranches; branchIndex++)
            {
                if (!sqliteEnabled)
                    return;

                var plan = refreshPlans[branchIndex];

                try
                {
                    using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(startupWarmupCancellationSource.Token);
                    int index = branchIndex;
                    analysisDatabase.RefreshSongsBranchAsync(plan,
                        (processed, total) => updateSongsBranchRefreshProgress(index + 1, totalBranches, plan.Branch.Metadata.DisplayName, processed, total),
                        linkedCancellation.Token).Wait(linkedCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Songs branch refresh failed for \"{plan.Branch.Metadata.DisplayName}\".", Ez2ConfigManager.LOGGER_NAME);
                }

                updateSongsBranchRefreshProgress(branchIndex + 1, totalBranches, plan.Branch.Metadata.DisplayName, 0, 0);
            }
        }

        private ProgressNotification? songsBranchRefreshProgressNotification;
        private int lastSongsBranchBeatmapProgressReported = -1;

        private void postSongsBranchRefreshNotification(int branchCount, int totalBeatmaps)
        {
            if (notificationOverlay == null || branchCount <= 0)
                return;

            Schedule(() =>
            {
                try
                {
                    if (totalBeatmaps < 10)
                    {
                        notificationOverlay.Post(new SimpleNotification
                        {
                            Text = $"Ez songs branch refresh queued for {branchCount} branch(es)."
                        });
                        return;
                    }

                    var notification = new ProgressNotification
                    {
                        Text = $"Ez songs branch refresh queued for {branchCount} branch(es) (~{totalBeatmaps} beatmaps)",
                        CompletionText = "Songs branch refresh complete",
                        CancelRequested = () => true,
                        State = ProgressNotificationState.Active
                    };

                    songsBranchRefreshProgressNotification = notification;
                    lastSongsBranchBeatmapProgressReported = -1;
                    notificationOverlay.Post(notification);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to post songs branch refresh notification.");
                }
            });
        }

        private void updateSongsBranchRefreshProgress(int processedBranches, int totalBranches, string branchDisplayName, int processedBeatmaps, int branchBeatmapTotal)
        {
            if (songsBranchRefreshProgressNotification == null)
                return;

            if (processedBeatmaps > 0 && branchBeatmapTotal > 0)
            {
                bool shouldUpdate = processedBeatmaps >= branchBeatmapTotal
                                    || processedBeatmaps - lastSongsBranchBeatmapProgressReported >= warmup_progress_update_interval;

                if (!shouldUpdate)
                    return;

                lastSongsBranchBeatmapProgressReported = processedBeatmaps;
            }
            else
            {
                lastSongsBranchBeatmapProgressReported = -1;
            }

            Schedule(() =>
            {
                try
                {
                    if (processedBeatmaps > 0 && branchBeatmapTotal > 0)
                    {
                        songsBranchRefreshProgressNotification.Text =
                            $"Refreshing songs branch \"{branchDisplayName}\" ({processedBeatmaps} of {branchBeatmapTotal}) — branch {processedBranches} of {totalBranches}";
                        songsBranchRefreshProgressNotification.Progress = (float)processedBeatmaps / branchBeatmapTotal;
                    }
                    else if (processedBranches >= totalBranches)
                    {
                        songsBranchRefreshProgressNotification.CompletionText = "Songs branch refresh complete";
                        songsBranchRefreshProgressNotification.State = ProgressNotificationState.Completed;
                        songsBranchRefreshProgressNotification = null;
                    }
                }
                catch
                {
                }
            });
        }

        private void queueSelectedBeatmapRecomputeIfRequired(WorkingBeatmap? workingBeatmap)
        {
            if (!pendingScanCompleted || !sqliteEnabled)
                return;

            var beatmapInfo = workingBeatmap?.BeatmapInfo;

            if (beatmapInfo == null)
                return;

            bool shouldSignal = false;

            lock (pendingBeatmapLock)
            {
                if (!analysisDatabase.NeedsOnDemandBackfill(beatmapInfo) || inFlightBeatmapIds.Contains(beatmapInfo.ID))
                    return;

                queuedSelectedBeatmapId = beatmapInfo.ID;

                if (!selectedBeatmapRecomputeSignalPending)
                {
                    selectedBeatmapRecomputeSignalPending = true;
                    shouldSignal = true;
                }
            }

            if (shouldSignal)
                selectedBeatmapRecomputeSignal.Release();
        }

        private void processStartupWarmupQueue(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    startupWarmupSignal.Wait(cancellationToken);

                    lock (pendingBeatmapLock)
                        startupWarmupSignalPending = false;

                    while (sqliteEnabled && tryBeginAnyPendingBeatmap(out Guid beatmapId))
                    {
                        try
                        {
                            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, pendingBeatmapRecomputeCancellationSource.Token);
                            recomputePendingBeatmap(beatmapId, skipExistingComparison: true, linkedCancellation.Token, source: "manual sqlite rebuild");
                        }
                        catch (ObjectDisposedException)
                        {
                            return;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void processSelectedBeatmapRecomputeQueue(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    selectedBeatmapRecomputeSignal.Wait(cancellationToken);

                    Guid? beatmapId = null;

                    lock (pendingBeatmapLock)
                    {
                        selectedBeatmapRecomputeSignalPending = false;

                        if (queuedSelectedBeatmapId is Guid queuedBeatmapId)
                        {
                            queuedSelectedBeatmapId = null;

                            if (inFlightBeatmapIds.Add(queuedBeatmapId))
                                beatmapId = queuedBeatmapId;
                        }
                    }

                    if (beatmapId.HasValue)
                    {
                        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, pendingBeatmapRecomputeCancellationSource.Token);
                        recomputePendingBeatmap(beatmapId.Value, skipExistingComparison: false, linkedCancellation.Token, source: "selected-beatmap warmup");
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void recomputePendingBeatmap(Guid beatmapId, bool skipExistingComparison, CancellationToken cancellationToken, string source)
        {
            BeatmapInfo? beatmap = null;

            try
            {
                if (!sqliteEnabled)
                    return;

                sleepIfRequired(cancellationToken);

                beatmap = realmAccess.Run(r => r.Find<BeatmapInfo>(beatmapId)?.Detach());

                if (beatmap == null)
                {
                    updateStartupWarmupProgressNotification();
                    return;
                }

                analysisDatabase.BackfillStoredDataAsync(beatmap, skipExistingComparison, cancellationToken)
                                .GetAwaiter()
                                .GetResult();

                updateStartupWarmupProgressNotification();

                ((IWorkingBeatmapCache)beatmapManager).Invalidate(beatmap);

                Logger.Log($"Completed {source} analysis recompute for {beatmap}. Remaining pending={getPendingBeatmapCount()}.",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Logger.Log($"{source} analysis recompute failed on {beatmap?.BeatmapSet?.Metadata.Title}: {e}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
            }
            finally
            {
                lock (pendingBeatmapLock)
                    inFlightBeatmapIds.Remove(beatmapId);
            }
        }

        private bool tryBeginAnyPendingBeatmap(out Guid beatmapId)
        {
            lock (pendingBeatmapLock)
            {
                while (pendingBeatmapQueue.Count > 0)
                {
                    beatmapId = pendingBeatmapQueue.Dequeue();

                    if (inFlightBeatmapIds.Add(beatmapId))
                        return true;
                }
            }

            beatmapId = Guid.Empty;
            return false;
        }

        private int getPendingBeatmapCount()
        {
            lock (pendingBeatmapLock)
                return pendingBeatmapQueue.Count + inFlightBeatmapIds.Count;
        }

        private void updateStartupWarmupProgressNotification()
        {
            ProgressNotification? notification;
            int processed;
            int total;

            lock (pendingBeatmapLock)
            {
                startupWarmupProcessedCount++;
                processed = startupWarmupProcessedCount;
                total = startupWarmupTotalCount;
                notification = startupWarmupProgressNotification;

                if (notification == null)
                    return;

                bool isComplete = total > 0 && processed >= total;
                bool shouldUpdate = total <= 10
                                    || isComplete
                                    || processed - lastStartupWarmupProgressReported >= warmup_progress_update_interval;

                if (!shouldUpdate)
                    return;

                lastStartupWarmupProgressReported = processed;

                if (isComplete)
                {
                    startupWarmupProgressNotification = null;
                    startupWarmupTotalCount = 0;
                    startupWarmupProcessedCount = 0;
                    lastStartupWarmupProgressReported = -1;
                }
            }

            Schedule(() =>
            {
                try
                {
                    if (notification.State == ProgressNotificationState.Cancelled)
                        return;

                    notification.Text = $"Ez analysis rebuild queued {total} beatmaps for recompute ({processed} of {total})";
                    notification.Progress = total > 0 ? (float)processed / total : 1;

                    if (processed >= total)
                    {
                        notification.CompletionText = $"{total} beatmaps' analysis recompute complete";
                        notification.State = ProgressNotificationState.Completed;
                    }
                }
                catch
                {
                }
            });
        }

        private void cancelPendingBeatmapProcessing()
        {
            CancellationTokenSource previousCancellationSource;

            lock (pendingBeatmapLock)
            {
                pendingBeatmapQueue.Clear();
                startupWarmupProcessedCount = 0;
                lastStartupWarmupProgressReported = -1;
                queuedSelectedBeatmapId = null;
                startupWarmupSignalPending = false;
                selectedBeatmapRecomputeSignalPending = false;
                startupWarmupProgressNotification = null;
                startupWarmupTotalCount = 0;
                previousCancellationSource = pendingBeatmapRecomputeCancellationSource;
                pendingBeatmapRecomputeCancellationSource = new CancellationTokenSource();
            }

            previousCancellationSource.Cancel();
            previousCancellationSource.Dispose();
        }

        private void postWarmupSummaryNotification(int totalCount)
        {
            if (notificationOverlay == null || totalCount <= 0)
                return;

            Schedule(() =>
            {
                try
                {
                    int remainingCount = getPendingBeatmapCount();

                    if (remainingCount <= 0)
                        return;

                    // For small workloads, a simple notification is sufficient.
                    if (totalCount < 10)
                    {
                        notificationOverlay.Post(new SimpleNotification
                        {
                            Text = $"Ez analysis rebuild queued {totalCount} beatmaps for recompute. Background sqlite processing is still working through {remainingCount} remaining result(s)."
                        });

                        return;
                    }

                    var notification = new ProgressNotification
                    {
                        Text = $"Ez analysis rebuild queued {totalCount} beatmaps for recompute ({totalCount - remainingCount} of {totalCount})",
                        CompletionText = "beatmaps' analysis recompute is complete",
                        CancelRequested = () =>
                        {
                            cancelPendingBeatmapProcessing();
                            return true;
                        },
                        State = ProgressNotificationState.Active
                    };

                    lock (pendingBeatmapLock)
                    {
                        startupWarmupProgressNotification = notification;
                        startupWarmupTotalCount = totalCount;
                        startupWarmupProcessedCount = 0;
                        lastStartupWarmupProgressReported = -1;
                    }

                    notificationOverlay.Post(notification);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to post analysis recompute summary notification.");
                }
            });
        }

        private void sleepIfRequired(CancellationToken cancellationToken)
        {
            while (localUserPlayInfo?.PlayingState.Value != LocalUserPlayingState.NotPlaying || highPerformanceSessionManager?.IsSessionActive == true)
            {
                Logger.Log("Analysis recompute is sleeping due to active gameplay...", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

                cancellationToken.WaitHandle.WaitOne(TimeToSleepDuringGameplay);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                startupWarmupCancellationSource.Cancel();
                selectedBeatmapRecomputeCancellationSource.Cancel();
                pendingBeatmapRecomputeCancellationSource.Cancel();

                try
                {
                    startupWarmupTask.Wait(1000);
                }
                catch
                {
                }

                try
                {
                    selectedBeatmapRecomputeTask.Wait(1000);
                }
                catch
                {
                }

                startupWarmupCancellationSource.Dispose();
                startupWarmupSignal.Dispose();
                selectedBeatmapRecomputeCancellationSource.Dispose();
                selectedBeatmapRecomputeSignal.Dispose();
                pendingBeatmapRecomputeCancellationSource.Dispose();

                lock (pendingBeatmapLock)
                {
                    if (startupWarmupProgressNotification != null)
                    {
                        try
                        {
                            startupWarmupProgressNotification.CompleteSilently();
                        }
                        catch
                        {
                        }

                        startupWarmupProgressNotification = null;
                        startupWarmupTotalCount = 0;
                    }
                }
            }

            base.Dispose(isDisposing);
        }
    }
}
