// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Statistics
{
    /// <summary>
    /// 通过隐藏的真实 replay 判定链生成命中事件。
    /// </summary>
    public static partial class EzScoreServer
    {
        private static readonly ConcurrentDictionary<string, IReadOnlyList<HitEvent>> cached_hit_events = new ConcurrentDictionary<string, IReadOnlyList<HitEvent>>();

        private static bool tryGetCached(ScoreInfo scoreInfo, out IReadOnlyList<HitEvent> hitEvents)
        {
            hitEvents = Array.Empty<HitEvent>();

            string? key = getCacheKey(scoreInfo);
            if (string.IsNullOrEmpty(key))
                return false;

            return cached_hit_events.TryGetValue(key, out hitEvents!);
        }

        public static void Invalidate(ScoreInfo? scoreInfo)
        {
            if (scoreInfo == null)
                return;

            string? key = getCacheKey(scoreInfo);
            if (string.IsNullOrEmpty(key))
                return;

            cached_hit_events.TryRemove(key, out _);
        }

        private static void storeCached(ScoreInfo scoreInfo, IReadOnlyList<HitEvent> hitEvents)
        {
            if (hitEvents.Count == 0)
                return;

            string? key = getCacheKey(scoreInfo);
            if (string.IsNullOrEmpty(key))
                return;

            cached_hit_events[key] = cloneHitEvents(hitEvents);
        }

        private static string? getCacheKey(ScoreInfo scoreInfo)
        {
            if (!string.IsNullOrEmpty(scoreInfo.Hash))
                return $"hash:{scoreInfo.Hash}";

            if (scoreInfo.ID != Guid.Empty)
                return $"id:{scoreInfo.ID}";

            if (scoreInfo.OnlineID > 0)
                return $"online:{scoreInfo.OnlineID}";

            if (scoreInfo.LegacyOnlineID > 0)
                return $"legacy:{scoreInfo.LegacyOnlineID}";

            return null;
        }

        private static List<HitEvent> cloneHitEvents(IReadOnlyList<HitEvent> hitEvents) => new List<HitEvent>(hitEvents);

        /// <summary>
        /// 一个隐藏的分析宿主，用来在结果页里驱动真实 replay 判定。
        /// </summary>
        public partial class AnalysisHost : CompositeDrawable
        {
            private ReplayAnalysisRunner? currentRunner;
            private TaskCompletionSource<List<HitEvent>?>? currentTask;
            private CancellationTokenRegistration cancellationRegistration;
            private long requestId;

            public override bool HandlePositionalInput => false;
            public override bool HandleNonPositionalInput => false;
            public override bool PropagatePositionalInputSubTree => false;
            public override bool PropagateNonPositionalInputSubTree => false;

            public AnalysisHost()
            {
                RelativeSizeAxes = Axes.Both;
                AlwaysPresent = true;
                Alpha = 0;
            }

            public Task<List<HitEvent>?> GenerateAsync(Score score, CancellationToken cancellationToken = default)
            {
                ArgumentNullException.ThrowIfNull(score);

                if (score.Replay == null || score.Replay.Frames.Count == 0)
                    return Task.FromResult<List<HitEvent>?>(null);

                if (tryGetCached(score.ScoreInfo, out var cached))
                    return Task.FromResult<List<HitEvent>?>(cloneHitEvents(cached));

                if (cancellationToken.IsCancellationRequested)
                    return Task.FromCanceled<List<HitEvent>?>(cancellationToken);

                var completionSource = new TaskCompletionSource<List<HitEvent>?>(TaskCreationOptions.RunContinuationsAsynchronously);
                long analysisId = Interlocked.Increment(ref requestId);

                Schedule(() => startAnalysis(analysisId, score.DeepClone(), completionSource));

                if (cancellationToken.CanBeCanceled)
                    cancellationRegistration = cancellationToken.Register(() => Schedule(() => cancelAnalysis(analysisId, completionSource, cancellationToken)));

                return completionSource.Task;
            }

            public void CancelPendingAnalysis()
            {
                long cancellationId = Interlocked.Increment(ref requestId);

                Schedule(() =>
                {
                    if (cancellationId != requestId)
                        return;

                    cancelActiveAnalysis(setTaskCancelled: true);
                });
            }

            private void startAnalysis(long analysisId, Score score, TaskCompletionSource<List<HitEvent>?> completionSource)
            {
                if (completionSource.Task.IsCompleted || analysisId != requestId)
                    return;

                if (LoadState < LoadState.Ready)
                {
                    Schedule(() => startAnalysis(analysisId, score, completionSource));
                    return;
                }

                cancelActiveAnalysis(setTaskCancelled: false);

                currentTask = completionSource;
                currentRunner = new ReplayAnalysisRunner(score, hitEvents => finishAnalysis(analysisId, completionSource, score.ScoreInfo, hitEvents));
                AddInternal(currentRunner);
            }

            private void finishAnalysis(long analysisId, TaskCompletionSource<List<HitEvent>?> completionSource, ScoreInfo scoreInfo, List<HitEvent>? hitEvents)
            {
                if (analysisId != requestId || completionSource.Task.IsCompleted)
                    return;

                cancellationRegistration.Dispose();

                if (currentRunner != null)
                {
                    RemoveInternal(currentRunner, true);
                    currentRunner = null;
                }

                currentTask = null;

                if (hitEvents != null && hitEvents.Count > 0)
                    storeCached(scoreInfo, hitEvents);

                completionSource.TrySetResult(hitEvents != null && hitEvents.Count > 0 ? cloneHitEvents(hitEvents) : null);
            }

            private void cancelAnalysis(long analysisId, TaskCompletionSource<List<HitEvent>?> completionSource, CancellationToken cancellationToken)
            {
                if (analysisId != requestId || completionSource.Task.IsCompleted)
                    return;

                cancelActiveAnalysis(setTaskCancelled: false);
                completionSource.TrySetCanceled(cancellationToken);
            }

            private void cancelActiveAnalysis(bool setTaskCancelled)
            {
                cancellationRegistration.Dispose();

                if (currentRunner != null)
                {
                    currentRunner.Cancel();
                    RemoveInternal(currentRunner, true);
                    currentRunner = null;
                }

                if (setTaskCancelled)
                    currentTask?.TrySetCanceled();

                currentTask = null;
            }

            protected override void Dispose(bool isDisposing)
            {
                if (isDisposing)
                    cancelActiveAnalysis(setTaskCancelled: true);

                base.Dispose(isDisposing);
            }
        }
    }
}
