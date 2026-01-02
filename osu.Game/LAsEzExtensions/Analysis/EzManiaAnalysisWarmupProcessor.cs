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
        protected Task ProcessingTask { get; private set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private RealmAccess realmAccess { get; set; } = null!;

        [Resolved]
        private EzBeatmapManiaAnalysisCache maniaAnalysisCache { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        [Resolved]
        private ILocalUserPlayInfo? localUserPlayInfo { get; set; }

        [Resolved]
        private IHighPerformanceSessionManager? highPerformanceSessionManager { get; set; }

        protected virtual int TimeToSleepDuringGameplay => 30000;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ProcessingTask = Task.Factory.StartNew(() =>
            {
                Logger.Log("Beginning background mania analysis warmup..");

                populateManiaAnalysis();
            }, TaskCreationOptions.LongRunning).ContinueWith(t =>
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
            HashSet<Guid> beatmapIds = new HashSet<Guid>();

            Logger.Log("Querying for mania beatmaps to warm up analysis cache...");

            realmAccess.Run(r =>
            {
                foreach (var b in r.All<BeatmapInfo>().Where(b => b.BeatmapSet != null && !b.Hidden && b.Ruleset.OnlineID == 3))
                    beatmapIds.Add(b.ID);
            });

            if (beatmapIds.Count == 0)
                return;

            Logger.Log($"Found {beatmapIds.Count} beatmaps to precompute mania analysis for.");

            var notification = showProgressNotification(beatmapIds.Count, "Precomputing mania analysis for beatmaps", "beatmaps' mania analysis has been precomputed");

            int processedCount = 0;
            int failedCount = 0;

            foreach (Guid id in beatmapIds)
            {
                if (notification?.State == ProgressNotificationState.Cancelled)
                    break;

                updateNotificationProgress(notification, processedCount, beatmapIds.Count);

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
                    maniaAnalysisCache.GetAnalysisAsync(beatmap, beatmap.Ruleset, mods: null, CancellationToken.None)
                                     .GetAwaiter()
                                     .GetResult();

                    ++processedCount;
                }
                catch (Exception e)
                {
                    Logger.Log($"Background mania analysis warmup failed on {beatmap}: {e}");
                    ++failedCount;
                }

                if (processedCount % 50 == 0)
                    ((IWorkingBeatmapCache)beatmapManager).Invalidate(beatmap);
            }

            completeNotification(notification, processedCount, beatmapIds.Count, failedCount);
        }

        private void updateNotificationProgress(ProgressNotification? notification, int processedCount, int totalCount)
        {
            if (notification == null)
                return;

            notification.Text = notification.Text.ToString().Split('(').First().TrimEnd() + $" ({processedCount} of {totalCount})";
            notification.Progress = (float)processedCount / totalCount;

            if (processedCount % 100 == 0)
                Logger.Log(notification.Text.ToString());
        }

        private void completeNotification(ProgressNotification? notification, int processedCount, int totalCount, int failedCount)
        {
            if (notification == null)
                return;

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
        }

        private ProgressNotification? showProgressNotification(int totalCount, string running, string completed)
        {
            if (notificationOverlay == null)
                return null;

            if (totalCount < 10)
                return null;

            ProgressNotification notification = new ProgressNotification
            {
                Text = running,
                CompletionText = completed,
                State = ProgressNotificationState.Active
            };

            notificationOverlay.Post(notification);
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
