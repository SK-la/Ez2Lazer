// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.LAsEzExtensions.Analysis.Persistence;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Performance;
using osu.Game.Screens.Play;
using Realms;

namespace osu.Game.LAsEzExtensions.Analysis
{
    /// <summary>
    /// 在启动阶段后台预热 mania analysis 缓存。
    /// 机制对齐官方 <see cref="BackgroundDataStoreProcessor"/>：
    /// - 使用 <see cref="ProgressNotification"/> 展示进度并允许用户取消。
    /// - 游戏进行中 / 高性能会话期间自动 sleep，避免抢占。
    /// - 作为长任务在后台运行，不阻塞启动。
    /// </summary>
    public partial class EzManiaAnalysisWarmupProcessor : Component
    {
        private const string logger_name = "mania_analysis";

        protected Task ProcessingTask { get; private set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private RealmAccess realmAccess { get; set; } = null!;

        [Resolved]
        private EzBeatmapManiaAnalysisCache maniaAnalysisCache { get; set; } = null!;

        [Resolved]
        private EzManiaAnalysisPersistentStore persistentStore { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notificationOverlay { get; set; }

        [Resolved]
        private ILocalUserPlayInfo? localUserPlayInfo { get; set; }

        [Resolved]
        private IHighPerformanceSessionManager? highPerformanceSessionManager { get; set; }

        protected virtual int TimeToSleepDuringGameplay => 30000;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ProcessingTask = Task.Factory.StartNew(populateManiaAnalysis, TaskCreationOptions.LongRunning).ContinueWith(t =>
            {
                if (t.Exception?.InnerException is ObjectDisposedException)
                {
                    Logger.Log("Finished mania analysis warmup aborted during shutdown");
                    return;
                }

                Logger.Log("Finished background mania analysis warmup!");
            });
        }

        private void populateManiaAnalysis()
        {
            if (!EzManiaAnalysisPersistentStore.Enabled)
            {
                Logger.Log("Mania analysis persistence is disabled; skipping warmup.", logger_name, LogLevel.Important);
                return;
            }

            List<(Guid id, string hash)> beatmaps = new List<(Guid id, string hash)>();

            Logger.Log("Querying for mania beatmaps to warm up analysis cache...", logger_name, LogLevel.Important);

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

                    bool isMania = b.Ruleset.OnlineID == 3 || string.Equals(b.Ruleset.ShortName, "mania", StringComparison.OrdinalIgnoreCase);

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

                Logger.Log($"Warmup beatmap query summary: total={totalBeatmaps}, total_with_set={totalWithSet}, mania_total={maniaTotal}, mania_with_set={maniaWithSet}, mania_hidden={maniaHidden}", logger_name, LogLevel.Important);

                if (maniaTotal == 0)
                {
                    string dist = string.Join(", ", rulesetDistribution.OrderByDescending(kvp => kvp.Value).Take(10).Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    Logger.Log($"Warmup beatmap ruleset distribution (first 2000): {dist}", logger_name, LogLevel.Important);
                }
            });

            if (beatmaps.Count == 0)
                return;

            // 增量：只重算缺失/过期（hash/version 不匹配）的条目。
            // 与官方 star rating 进度通知一致：只在确实需要做“预计算”时展示一条 ProgressNotification。
            var needingRecompute = persistentStore.GetBeatmapsNeedingRecompute(beatmaps);

            if (needingRecompute.Count == 0)
            {
                Logger.Log("No beatmaps require mania analysis warmup.", logger_name, LogLevel.Important);
                return;
            }

            Logger.Log($"Found {needingRecompute.Count} beatmaps which require mania analysis warmup.", logger_name, LogLevel.Important);

            Logger.Log($"Starting mania analysis warmup. total={needingRecompute.Count}", logger_name, LogLevel.Important);

            var notification = showProgressNotification(needingRecompute.Count, "Precomputing mania analysis for beatmaps", "beatmaps' mania analysis has been precomputed");

            int processedCount = 0;
            int failedCount = 0;

            foreach (Guid id in needingRecompute)
            {
                if (notification?.State == ProgressNotificationState.Cancelled)
                    break;

                updateNotificationProgress(notification, processedCount, needingRecompute.Count);

                sleepIfRequired();

                var beatmap = realmAccess.Run(r => r.Find<BeatmapInfo>(id)?.Detach());

                if (beatmap == null)
                {
                    ++failedCount;
                    continue;
                }

                try
                {
                    // 仅预热 no-mod 的缓存项：与官方 star 预计算一致（基础值持久化/复用），modded 部分仍按需计算。
                    // 关键：warmup 绝不阻塞可见项。
                    // - 等待当前所有高优先级（可见项）计算完成后再启动 warmup。
                    // - 启用持久化时，仅预热 SQLite，不把所有结果长期留在内存缓存里。
                    maniaAnalysisCache.WarmupPersistentOnlyAsync(beatmap, CancellationToken.None)
                                      .GetAwaiter()
                                      .GetResult();

                    ++processedCount;
                }
                catch (Exception e)
                {
                    Logger.Log($"Background mania analysis warmup failed on {beatmap}: {e}", logger_name, LogLevel.Important);
                    ++failedCount;
                }

                if (processedCount % 50 == 0)
                    ((IWorkingBeatmapCache)beatmapManager).Invalidate(beatmap);
            }

            completeNotification(notification, processedCount, needingRecompute.Count, failedCount);
        }

        private void updateNotificationProgress(ProgressNotification? notification, int processedCount, int totalCount)
        {
            if (notification == null)
                return;

            Schedule(() =>
            {
                notification.Text = notification.Text.ToString().Split('(').First().TrimEnd() + $" ({processedCount} of {totalCount})";
                notification.Progress = (float)processedCount / totalCount;
            });

            if (processedCount % 100 == 0)
                Logger.Log($"Warmup progress: {processedCount} of {totalCount}");
        }

        private void completeNotification(ProgressNotification? notification, int processedCount, int totalCount, int failedCount)
        {
            if (notification == null)
                return;

            Schedule(() =>
            {
                if (processedCount == totalCount)
                {
                    notification.CompletionText = $"{processedCount} {notification.CompletionText}";
                    notification.Progress = 1;
                    notification.State = ProgressNotificationState.Completed;
                }
                else
                {
                    notification.Text = $"{processedCount} of {totalCount} {notification.CompletionText}";

                    if (failedCount > 0)
                        notification.Text += $" Check logs for issues with {failedCount} failed items.";

                    notification.State = ProgressNotificationState.Cancelled;
                }
            });
        }

        private ProgressNotification? showProgressNotification(int totalCount, string running, string completed)
        {
            if (notificationOverlay == null)
            {
                Logger.Log("INotificationOverlay is null; mania analysis warmup progress notification will not be shown.", logger_name, LogLevel.Important);
                return null;
            }

            if (totalCount <= 0)
                return null;

            ProgressNotification notification = new ProgressNotification
            {
                Text = running,
                CompletionText = completed,
                State = ProgressNotificationState.Active
            };

            Schedule(() =>
            {
                try
                {
                    notificationOverlay?.Post(notification);
                    Logger.Log("Posted mania analysis warmup progress notification.", logger_name, LogLevel.Important);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to post mania analysis warmup notification.");
                }
            });
            return notification;
        }

        private void sleepIfRequired()
        {
            while (localUserPlayInfo?.PlayingState.Value != LocalUserPlayingState.NotPlaying || highPerformanceSessionManager?.IsSessionActive == true)
            {
                Logger.Log("Mania analysis warmup sleeping due to active gameplay...");
                Thread.Sleep(TimeToSleepDuringGameplay);
            }
        }
    }
}
