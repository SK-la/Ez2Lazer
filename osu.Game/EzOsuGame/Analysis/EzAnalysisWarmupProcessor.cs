// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
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
    /// 启动阶段做一次全量扫描，补齐 analysis 主体与轻量 tag 结果。
    /// 运行阶段通过单后台 worker 仅处理最新选中谱面的低优先级重算。
    /// </summary>
    public partial class EzAnalysisWarmupProcessor : Component
    {
        protected Task ProcessingTask { get; private set; } = Task.CompletedTask;

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
        private readonly object processingTaskLock = new object();

        protected virtual int TimeToSleepDuringGameplay => 30000;

        private readonly object pendingBeatmapLock = new object();
        private readonly HashSet<Guid> pendingBeatmapIds = new HashSet<Guid>();
        private readonly HashSet<Guid> inFlightBeatmapIds = new HashSet<Guid>();
        private ProgressNotification? startupWarmupProgressNotification;
        private int startupWarmupTotalCount;
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
            sqliteEnabled = enabled;

            if (enabled)
            {
                ensureBackgroundWorkersStarted();
                triggerStartupWarmupScan();
                return;
            }

            pendingScanCompleted = false;
            cancelPendingBeatmapProcessing();
        }

        private void ensureBackgroundWorkersStarted()
        {
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

        private void triggerStartupWarmupScan()
        {
            if (!sqliteEnabled)
                return;

            lock (processingTaskLock)
            {
                if (!ProcessingTask.IsCompleted)
                    return;

                pendingScanCompleted = false;

                ProcessingTask = Task.Factory.StartNew(scanBeatmapsNeedingWarmup, TaskCreationOptions.LongRunning).ContinueWith(t =>
                {
                    if (t.Exception?.InnerException is ObjectDisposedException)
                    {
                        Logger.Log("Finished analysis startup scan aborted during shutdown", Ez2ConfigManager.LOGGER_NAME, LogLevel.Verbose);
                        return;
                    }

                    if (!sqliteEnabled)
                        return;

                    pendingScanCompleted = true;
                    Schedule(() => queueSelectedBeatmapRecomputeIfRequired(currentBeatmap.Value));

                    Logger.Log("Finished background analysis startup scan.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                });
            }
        }

        private void scanBeatmapsNeedingWarmup()
        {
            if (!sqliteEnabled)
                return;

            List<(Guid id, string hash, int rulesetOnlineId)> beatmaps = new List<(Guid id, string hash, int rulesetOnlineId)>();

            realmAccess.Run(r =>
            {
                int totalBeatmaps = 0;
                int totalWithSet = 0;
                int maniaTotal = 0;
                int maniaWithSet = 0;
                int maniaHidden = 0;

                Dictionary<string, int> rulesetDistribution = new Dictionary<string, int>();

                // Align with official BackgroundDataStoreProcessor: don't exclude Hidden beatmaps here.
                // Hidden beatmaps can still be attached to sets and may contribute to cache hit rates.
                foreach (var b in r.All<BeatmapInfo>())
                {
                    totalBeatmaps++;

                    bool isMania = b.Ruleset.OnlineID == 3;
                    bool isBms = string.Equals(b.Ruleset.ShortName, "bms", StringComparison.OrdinalIgnoreCase);

                    if (isMania)
                    {
                        maniaTotal++;
                        if (b.Hidden)
                            maniaHidden++;
                    }

                    if (totalBeatmaps <= 2000)
                    {
                        string key = $"{b.Ruleset.ShortName}:{b.Ruleset.OnlineID}";
                        rulesetDistribution.TryGetValue(key, out int count);
                        rulesetDistribution[key] = count + 1;
                    }

                    if (b.BeatmapSet == null)
                        continue;

                    // External BMS libraries are volatile by nature (sync/relink), and forcing sqlite backfill
                    // every startup is noisy and expensive. BMS keeps runtime analysis on-demand.
                    if (isBms)
                        continue;

                    totalWithSet++;

                    if (isMania)
                        maniaWithSet++;

                    beatmaps.Add((b.ID, b.Hash, b.Ruleset.OnlineID));
                }

                Logger.Log($"Startup scan beatmap summary: total={totalBeatmaps}, total_with_set={totalWithSet}, mania_total={maniaTotal}, mania_with_set={maniaWithSet}, mania_hidden={maniaHidden}",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);

                if (maniaTotal == 0)
                {
                    string dist = string.Join(", ", rulesetDistribution.OrderByDescending(kvp => kvp.Value).Take(10).Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    Logger.Log($"Startup scan ruleset distribution (first 2000): {dist}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                }
            });

            if (beatmaps.Count == 0)
                return;

            if (!sqliteEnabled)
                return;

            var needingRecompute = analysisDatabase.GetBeatmapsNeedingRecompute(beatmaps);

            if (!sqliteEnabled)
                return;

            if (needingRecompute.Count == 0)
            {
                Logger.Log("No beatmaps require analysis recompute.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                return;
            }

            bool shouldSignalStartupWarmup = false;

            lock (pendingBeatmapLock)
            {
                pendingBeatmapIds.Clear();

                foreach (Guid id in needingRecompute)
                    pendingBeatmapIds.Add(id);

                if (!startupWarmupSignalPending)
                {
                    startupWarmupSignalPending = true;
                    shouldSignalStartupWarmup = true;
                }
            }

            if (shouldSignalStartupWarmup)
                startupWarmupSignal.Release();

            postWarmupSummaryNotification(needingRecompute.Count);
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
                        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, pendingBeatmapRecomputeCancellationSource.Token);
                        recomputePendingBeatmap(beatmapId, includeTagData: true, skipExistingComparison: true, linkedCancellation.Token, "startup warmup");
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
                        recomputePendingBeatmap(beatmapId.Value, includeTagData: true, skipExistingComparison: false, linkedCancellation.Token, "selected-beatmap warmup");
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void recomputePendingBeatmap(Guid beatmapId, bool includeTagData, bool skipExistingComparison, CancellationToken cancellationToken, string source)
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
                    lock (pendingBeatmapLock)
                        pendingBeatmapIds.Remove(beatmapId);

                    return;
                }

                analysisDatabase.BackfillStoredDataAsync(beatmap, includeTagData, skipExistingComparison, cancellationToken)
                                .GetAwaiter()
                                .GetResult();

                lock (pendingBeatmapLock)
                {
                    pendingBeatmapIds.Remove(beatmapId);

                    int remaining = pendingBeatmapIds.Count;

                    if (startupWarmupProgressNotification != null)
                    {
                        int processed = startupWarmupTotalCount > 0 ? startupWarmupTotalCount - remaining : 0;
                        startupWarmupProgressNotification.Text = $"Ez analysis startup scan queued {startupWarmupTotalCount} beatmaps for recompute ({processed} of {startupWarmupTotalCount})";
                        startupWarmupProgressNotification.Progress = startupWarmupTotalCount > 0 ? (float)processed / startupWarmupTotalCount : 1;

                        if (remaining <= 0)
                        {
                            startupWarmupProgressNotification.CompletionText = $"{startupWarmupTotalCount} beatmaps' analysis recompute complete";
                            startupWarmupProgressNotification.State = ProgressNotificationState.Completed;
                            startupWarmupProgressNotification = null;
                            startupWarmupTotalCount = 0;
                        }
                    }
                }

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
                foreach (Guid pendingBeatmapId in pendingBeatmapIds)
                {
                    if (!inFlightBeatmapIds.Add(pendingBeatmapId))
                        continue;

                    beatmapId = pendingBeatmapId;
                    return true;
                }
            }

            beatmapId = Guid.Empty;
            return false;
        }

        private int getPendingBeatmapCount()
        {
            lock (pendingBeatmapLock)
                return pendingBeatmapIds.Count;
        }

        private void cancelPendingBeatmapProcessing()
        {
            CancellationTokenSource previousCancellationSource;

            lock (pendingBeatmapLock)
            {
                pendingBeatmapIds.Clear();
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
                            Text = $"Ez analysis startup scan queued {totalCount} beatmaps for recompute. Background sqlite warmup is still processing {remainingCount} remaining result(s)."
                        });

                        return;
                    }

                    var notification = new ProgressNotification
                    {
                        Text = $"Ez analysis startup scan queued {totalCount} beatmaps for recompute ({totalCount - remainingCount} of {totalCount})",
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
