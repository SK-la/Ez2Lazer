// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Experimental pull of own online scores (BP / most-played) into local score storage via official API.
    /// </summary>
    public class EzLocalProfileOnlinePullService : IDisposable
    {
        public const int BEST_LIMIT = 100;
        public const int DEFAULT_MOST_PLAYED_BATCH = 50;
        private static readonly TimeSpan request_delay = TimeSpan.FromSeconds(1);

        private readonly IAPIProvider api;
        private readonly ScoreManager scoreManager;
        private readonly EzLocalProfileStore store;
        private readonly object pullLock = new object();
        private CancellationTokenSource? pullCts;
        private bool isDisposed;

        public BindableBool IsPulling { get; } = new BindableBool();

        public EzLocalProfileOnlinePullService(IAPIProvider api, ScoreManager scoreManager, Storage storage)
        {
            this.api = api;
            this.scoreManager = scoreManager;
            store = new EzLocalProfileStore(storage);
        }

        public int PeekMostPlayedOffset(int rulesetId) => store.GetMostPlayedOffset(rulesetId);

        public Task<EzLocalProfileOnlinePullResult> PullAsync(EzLocalProfileOnlinePullRequest request, CancellationToken cancellationToken = default)
        {
            lock (pullLock)
            {
                if (IsPulling.Value)
                    return Task.FromResult(new EzLocalProfileOnlinePullResult { ErrorMessage = "already_pulling" });

                pullCts?.Cancel();
                pullCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = pullCts.Token;
                IsPulling.Value = true;

                return Task.Run(async () =>
                {
                    try
                    {
                        return await pullCoreAsync(request, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return new EzLocalProfileOnlinePullResult { ErrorMessage = "cancelled" };
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "[EzLocalProfile] Online score pull failed.", Ez2ConfigManager.LOGGER_NAME);
                        return new EzLocalProfileOnlinePullResult { ErrorMessage = ex.Message };
                    }
                    finally
                    {
                        IsPulling.Value = false;
                    }
                }, token);
            }
        }

        private async Task<EzLocalProfileOnlinePullResult> pullCoreAsync(EzLocalProfileOnlinePullRequest request, CancellationToken token)
        {
            var result = new EzLocalProfileOnlinePullResult();

            if (!api.IsLoggedIn || api.IsLocalOnly)
            {
                result.ErrorMessage = "need_online";
                return result;
            }

            int userId = api.LocalUser.Value.Id;

            if (userId <= 1)
            {
                result.ErrorMessage = "need_online";
                return result;
            }

            var ruleset = request.Ruleset ?? throw new ArgumentNullException(nameof(request.Ruleset));
            int rulesetId = ruleset.OnlineID;
            int batchSize = request.MostPlayedBatchSize > 0 ? request.MostPlayedBatchSize : DEFAULT_MOST_PLAYED_BATCH;

            List<SoloScoreInfo> candidates;

            switch (request.Kind)
            {
                case EzLocalProfileOnlinePullKind.Best:
                    candidates = await fetchBestAsync(userId, ruleset, token).ConfigureAwait(false);
                    result.MostPlayedOffsetAfter = store.GetMostPlayedOffset(rulesetId);
                    break;

                case EzLocalProfileOnlinePullKind.MostPlayed:
                {
                    int offset = request.ResetMostPlayedOffset ? 0 : store.GetMostPlayedOffset(rulesetId);
                    if (request.ResetMostPlayedOffset)
                        store.SetMostPlayedOffset(rulesetId, 0);

                    candidates = await fetchMostPlayedScoresAsync(userId, ruleset, offset, batchSize, token).ConfigureAwait(false);
                    int nextOffset = offset + batchSize;
                    store.SetMostPlayedOffset(rulesetId, nextOffset);
                    result.MostPlayedOffsetAfter = nextOffset;
                    break;
                }

                default:
                    result.ErrorMessage = "unknown_kind";
                    return result;
            }

            result.Candidates = candidates.Count;

            foreach (var solo in candidates)
            {
                token.ThrowIfCancellationRequested();
                await processCandidateAsync(solo, result, token).ConfigureAwait(false);
                await Task.Delay(request_delay, token).ConfigureAwait(false);
            }

            return result;
        }

        private async Task<List<SoloScoreInfo>> fetchBestAsync(int userId, RulesetInfo ruleset, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var req = new GetUserScoresRequest(userId, ScoreType.Best, new PaginationParameters(BEST_LIMIT), ruleset);
            await api.PerformAsync(req).ConfigureAwait(false);

            if (req.CompletionState != APIRequestCompletionState.Completed || req.Response == null)
                throw new InvalidOperationException("Failed to fetch best scores.");

            return req.Response;
        }

        private async Task<List<SoloScoreInfo>> fetchMostPlayedScoresAsync(
            int userId,
            RulesetInfo ruleset,
            int offset,
            int batchSize,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var mpReq = new GetUserMostPlayedBeatmapsRequest(userId, new PaginationParameters(offset, batchSize));
            await api.PerformAsync(mpReq).ConfigureAwait(false);

            if (mpReq.CompletionState != APIRequestCompletionState.Completed || mpReq.Response == null)
                throw new InvalidOperationException("Failed to fetch most-played beatmaps.");

            var scores = new List<SoloScoreInfo>();

            foreach (var entry in mpReq.Response)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    if (entry.BeatmapInfo.Ruleset.OnlineID != ruleset.OnlineID)
                        continue;
                }
                catch
                {
                    // Beatmap metadata may be incomplete; still try the score request with mode filter.
                }

                await Task.Delay(request_delay, token).ConfigureAwait(false);

                var scoreReq = new GetUserBeatmapScoreRequest(entry.BeatmapID, userId, ruleset);
                await api.PerformAsync(scoreReq).ConfigureAwait(false);

                if (scoreReq.CompletionState != APIRequestCompletionState.Completed || scoreReq.Response?.Score == null)
                    continue;

                if (scoreReq.Response.Score.RulesetID != ruleset.OnlineID)
                    continue;

                scores.Add(scoreReq.Response.Score);
            }

            return scores;
        }

        private async Task processCandidateAsync(
            SoloScoreInfo solo,
            EzLocalProfileOnlinePullResult result,
            CancellationToken token)
        {
            long onlineId = solo.OnlineID;

            if (onlineId <= 0)
            {
                result.Failed++;
                return;
            }

            if (scoreManager.Query(s => s.OnlineID == onlineId) != null)
            {
                result.AlreadyOwned++;
                return;
            }

            if (!solo.HasReplay)
            {
                result.NoReplay++;
                return;
            }

            string? path = null;

            try
            {
                path = await downloadReplayAsync(solo, token).ConfigureAwait(false);

                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    result.Failed++;
                    return;
                }

                var notification = new ProgressNotification
                {
                    State = ProgressNotificationState.Active,
                    Text = $"Importing online score {onlineId}…",
                };

                var imported = (await scoreManager.Import(notification, new[] { new ImportTask(path) }, new ImportParameters { Batch = true }).ConfigureAwait(false)).ToList();

                if (imported.Count > 0)
                    result.Imported++;
                else if (scoreManager.Query(s => s.OnlineID == onlineId) != null)
                    result.AlreadyOwned++;
                else
                    result.MissingBeatmap++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Log($"[EzLocalProfile] Import failed for score {onlineId}: {ex.Message}", Ez2ConfigManager.LOGGER_NAME);
                result.Failed++;
            }
            finally
            {
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        private async Task<string?> downloadReplayAsync(IScoreInfo scoreInfo, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var download = new DownloadReplayRequest(scoreInfo);

            download.Success += path => tcs.TrySetResult(path);
            download.Failure += ex => tcs.TrySetException(ex);

            await api.PerformAsync(download).ConfigureAwait(false);

            if (download.CompletionState == APIRequestCompletionState.Failed && !tcs.Task.IsCompleted)
                tcs.TrySetException(new InvalidOperationException("Replay download failed."));

            using (token.Register(() => tcs.TrySetCanceled(token)))
                return await tcs.Task.ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            pullCts?.Cancel();
            pullCts?.Dispose();
            store.Dispose();
        }
    }
}
