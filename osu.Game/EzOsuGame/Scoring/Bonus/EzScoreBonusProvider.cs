// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring.Bonus
{
    /// <summary>
    /// 进程内附加分缓存：游玩结束 / 手动重算时直接存入，其余本地成绩按需串行回放补算。不写 Realm，重启后重新补算。
    /// </summary>
    public static class EzScoreBonusProvider
    {
        private static readonly ConcurrentDictionary<string, Task<EzScoreBonusSet?>> entries = new ConcurrentDictionary<string, Task<EzScoreBonusSet?>>();

        // 回放 Session 较重，补算串行执行，避免选歌界面一次性占满线程池。
        private static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        private const int busy_poll_interval_ms = 1000;

        public static void Store(ScoreInfo score, EzScoreBonusSet? bonus)
        {
            string? key = keyOf(score);

            if (key != null)
                entries[key] = Task.FromResult(bonus);
        }

        /// <summary>
        /// 已有结果（成绩自带或缓存已完成）时同步返回 true；<paramref name="bonus"/> 为 null 表示该成绩无法计算。
        /// </summary>
        public static bool TryGet(ScoreInfo score, out EzScoreBonusSet? bonus)
        {
            if (score.EzBonus != null)
            {
                bonus = score.EzBonus;
                return true;
            }

            string? key = keyOf(score);

            if (key != null && entries.TryGetValue(key, out var task) && task.IsCompletedSuccessfully)
            {
                bonus = task.GetResultSafely();
                return true;
            }

            bonus = null;
            return false;
        }

        /// <param name="isBusy">返回 true 时补算等待（例如游玩中），避免与 gameplay 抢资源。</param>
        public static Task<EzScoreBonusSet?> GetAsync(ScoreInfo score, ScoreManager scoreManager, BeatmapManager beatmapManager, IEzReplaySession replaySession,
                                                      Func<bool>? isBusy = null, CancellationToken cancellationToken = default)
        {
            if (score.EzBonus != null)
                return Task.FromResult(score.EzBonus);

            string? key = keyOf(score);

            if (key == null)
                return Task.FromResult<EzScoreBonusSet?>(null);

            var detached = score.Detach();

            return entries.GetOrAdd(key, k => Task.Run(async () =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    while (isBusy?.Invoke() == true)
                        await Task.Delay(busy_poll_interval_ms, cancellationToken).ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();

                    return await EzScoreRecalculationService.CalculateBonusAsync(scoreManager, beatmapManager, replaySession, detached, cancellationToken)
                                                            .ConfigureAwait(false);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Logger.Log($"Score bonus calculation failed for {k}: {e.Message}");
                    return null;
                }
                finally
                {
                    gate.Release();
                }
            }, cancellationToken)).ContinueWith(t =>
            {
                // 取消的补算不留在缓存里，下次显示时重新排队。
                if (t.IsCanceled)
                    entries.TryRemove(key, out _);

                return t;
            }, TaskScheduler.Default).Unwrap();
        }

        private static string? keyOf(ScoreInfo score)
        {
            if (score.ID != Guid.Empty)
                return $"id:{score.ID}";

            return string.IsNullOrEmpty(score.Hash) ? null : $"hash:{score.Hash}";
        }
    }
}
