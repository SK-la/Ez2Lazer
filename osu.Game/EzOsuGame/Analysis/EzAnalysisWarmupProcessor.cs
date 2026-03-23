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
    /// 启动阶段只做一次全量扫描，确定哪些 mania 谱面缺失/过期。
    /// 运行阶段仅在当前选中谱面命中待补算集合时，执行一次低优先级重算。
    /// </summary>
    public partial class EzAnalysisWarmupProcessor : Component
    {
        // 诊断开关：用于快速排除“启动预热导致内存无法回收”的问题。
        // 设为 true 时，无论配置如何都强制跳过预热。
        private bool forceDisableWarmupForDiagnostics { get; set; } = false;

        protected Task ProcessingTask { get; private set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private RealmAccess realmAccess { get; set; } = null!;

        [Resolved]
        private EzAnalysisCache maniaAnalysisCache { get; set; } = null!;

        [Resolved]
        private EzAnalysisPersistentStore persistentStore { get; set; } = null!;

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

        private IBindable<bool> ezAnalysisRecomputeEnabled = new BindableBool(true);
        private IBindable<bool> ezAnalysisSqliteEnabled = new BindableBool(true);

        protected virtual int TimeToSleepDuringGameplay => 30000;

        private readonly object pendingBeatmapLock = new object();
        private readonly HashSet<Guid> pendingBeatmapIds = new HashSet<Guid>();
        private readonly HashSet<Guid> inFlightBeatmapIds = new HashSet<Guid>();

        private volatile bool pendingScanCompleted;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ezAnalysisRecomputeEnabled = ezConfig.GetBindable<bool>(Ez2Setting.EzAnalysisRecEnabled);
            ezAnalysisSqliteEnabled = ezConfig.GetBindable<bool>(Ez2Setting.EzAnalysisSqliteEnabled);

            currentBeatmap.BindValueChanged(beatmap => queueSelectedBeatmapRecomputeIfRequired(beatmap.NewValue), true);

            ProcessingTask = Task.Factory.StartNew(scanBeatmapsNeedingWarmup, TaskCreationOptions.LongRunning).ContinueWith(t =>
            {
                if (t.Exception?.InnerException is ObjectDisposedException)
                {
                    Logger.Log("Finished mania analysis startup scan aborted during shutdown", Ez2ConfigManager.LOGGER_NAME, LogLevel.Verbose);
                    return;
                }

                pendingScanCompleted = true;
                Schedule(() => queueSelectedBeatmapRecomputeIfRequired(currentBeatmap.Value));

                Logger.Log("Finished background mania analysis startup scan.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
            });
        }

        private bool isStartupWarmupScanEnabled() =>
            ezAnalysisSqliteEnabled.Value
            && EzAnalysisPersistentStore.Enabled;

        private bool isRuntimeSelectedBeatmapRecomputeEnabled() =>
            ezAnalysisRecomputeEnabled.Value;

        private void scanBeatmapsNeedingWarmup()
        {
            if (forceDisableWarmupForDiagnostics)
            {
                Logger.Log("Ez analysis warmup scan is force-disabled by internal diagnostic switch.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                return;
            }

            if (!isStartupWarmupScanEnabled())
            {
                Logger.Log(ezAnalysisSqliteEnabled.Value
                        ? "Mania analysis persistence is disabled; skipping startup scan."
                        : "Ez analysis sqlite cache is disabled; skipping startup scan.",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                return;
            }

            List<(Guid id, string hash)> beatmaps = new List<(Guid id, string hash)>();

            Logger.Log("Querying for mania beatmaps requiring analysis recompute...", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);

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

                    totalWithSet++;

                    if (!isMania)
                        continue;

                    maniaWithSet++;
                    beatmaps.Add((b.ID, b.Hash));
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

            var needingRecompute = persistentStore.GetBeatmapsNeedingRecompute(beatmaps);

            if (needingRecompute.Count == 0)
            {
                Logger.Log("No beatmaps require mania analysis recompute.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                return;
            }

            lock (pendingBeatmapLock)
            {
                pendingBeatmapIds.Clear();

                foreach (Guid id in needingRecompute)
                    pendingBeatmapIds.Add(id);
            }

            Logger.Log($"Startup scan found {needingRecompute.Count} beatmaps requiring mania analysis recompute. Runtime recompute will only process the selected beatmap from this set.",
                Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);

            postWarmupSummaryNotification(needingRecompute.Count);
        }

        private void queueSelectedBeatmapRecomputeIfRequired(WorkingBeatmap? workingBeatmap)
        {
            if (!pendingScanCompleted || !isRuntimeSelectedBeatmapRecomputeEnabled())
                return;

            var beatmapInfo = workingBeatmap?.BeatmapInfo;

            if (beatmapInfo == null || beatmapInfo.Ruleset.OnlineID != 3)
                return;

            bool shouldQueue;

            lock (pendingBeatmapLock)
            {
                shouldQueue = pendingBeatmapIds.Contains(beatmapInfo.ID) && inFlightBeatmapIds.Add(beatmapInfo.ID);
            }

            if (!shouldQueue)
                return;

            Task.Factory.StartNew(() => recomputeSelectedBeatmap(beatmapInfo), TaskCreationOptions.LongRunning);
        }

        private void recomputeSelectedBeatmap(BeatmapInfo beatmapInfo)
        {
            try
            {
                if (!isRuntimeSelectedBeatmapRecomputeEnabled())
                    return;

                sleepIfRequired();

                var beatmap = realmAccess.Run(r => r.Find<BeatmapInfo>(beatmapInfo.ID)?.Detach()) ?? beatmapInfo;

                maniaAnalysisCache.WarmupPersistentOnlyAsync(beatmap, CancellationToken.None)
                                  .GetAwaiter()
                                  .GetResult();

                lock (pendingBeatmapLock)
                    pendingBeatmapIds.Remove(beatmapInfo.ID);

                ((IWorkingBeatmapCache)beatmapManager).Invalidate(beatmap);

                Logger.Log($"Completed selected-beatmap mania analysis recompute for {beatmapInfo}. Remaining pending={getPendingBeatmapCount()}.",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
            catch (Exception e)
            {
                Logger.Log($"Selected-beatmap mania analysis recompute failed on {beatmapInfo}: {e}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
            }
            finally
            {
                lock (pendingBeatmapLock)
                    inFlightBeatmapIds.Remove(beatmapInfo.ID);
            }
        }

        private int getPendingBeatmapCount()
        {
            lock (pendingBeatmapLock)
                return pendingBeatmapIds.Count;
        }

        private void postWarmupSummaryNotification(int totalCount)
        {
            if (notificationOverlay == null || totalCount <= 0)
                return;

            Schedule(() =>
            {
                try
                {
                    notificationOverlay.Post(new SimpleNotification
                    {
                        Text = $"Ez analysis scan found {totalCount} beatmaps needing recompute. Runtime recompute is limited to the selected beatmap only."
                    });
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to post mania analysis recompute summary notification.");
                }
            });
        }

        private void sleepIfRequired()
        {
            while (localUserPlayInfo?.PlayingState.Value != LocalUserPlayingState.NotPlaying || highPerformanceSessionManager?.IsSessionActive == true)
            {
                Logger.Log("Mania analysis recompute is sleeping due to active gameplay...", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                Thread.Sleep(TimeToSleepDuringGameplay);
            }
        }
    }
}
