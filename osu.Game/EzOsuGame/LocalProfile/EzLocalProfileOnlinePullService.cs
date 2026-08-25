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
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Experimental pull of own online scores (BP / most-played) into local score storage via official API.
    /// </summary>
    public class EzLocalProfileOnlinePullService : IDisposable
    {
        public const int BATCH_SIZE = 50;
        public const int BEST_TOTAL = 100;
        public const string COLLECTION_BP = "BP";
        public const string COLLECTION_MOST_PLAYED = "玩过的图";

        /// <summary>Legacy alias used by dialog copy.</summary>
        public const int DEFAULT_MOST_PLAYED_BATCH = BATCH_SIZE;

        private static readonly TimeSpan request_delay = TimeSpan.FromSeconds(1);

        private readonly IAPIProvider api;
        private readonly ScoreManager scoreManager;
        private readonly BeatmapManager beatmapManager;
        private readonly RealmAccess realm;
        private readonly EzLocalProfileStore store;
        private readonly object pullLock = new object();
        private CancellationTokenSource? pullCts;
        private bool isDisposed;

        public BindableBool IsPulling { get; } = new BindableBool();

        public EzLocalProfileOnlinePullService(
            IAPIProvider api,
            ScoreManager scoreManager,
            BeatmapManager beatmapManager,
            RealmAccess realm,
            Storage storage)
        {
            this.api = api;
            this.scoreManager = scoreManager;
            this.beatmapManager = beatmapManager;
            this.realm = realm;
            store = new EzLocalProfileStore(storage);
        }

        public int PeekPullOffset(EzLocalProfileOnlinePullKind kind, int rulesetId) =>
            store.GetPullOffset(kind, rulesetId);

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
            int batchSize = request.BatchSize > 0 ? request.BatchSize : BATCH_SIZE;
            int offset = Math.Max(0, request.StartOffset);

            List<SoloScoreInfo> candidates = request.Kind switch
            {
                EzLocalProfileOnlinePullKind.Best => await fetchBestBatchAsync(userId, ruleset, offset, batchSize, token).ConfigureAwait(false),
                EzLocalProfileOnlinePullKind.MostPlayed => await fetchMostPlayedScoresAsync(userId, ruleset, offset, batchSize, token).ConfigureAwait(false),
                _ => throw new InvalidOperationException("unknown_kind"),
            };

            int nextOffset = offset + batchSize;
            store.SetPullOffset(request.Kind, rulesetId, nextOffset);
            result.OffsetAfter = nextOffset;
            result.Candidates = candidates.Count;

            string? collectionName = request.DownloadMissingBeatmaps
                ? (request.Kind == EzLocalProfileOnlinePullKind.Best ? COLLECTION_BP : COLLECTION_MOST_PLAYED)
                : null;

            var downloadedSetIds = new HashSet<int>();

            foreach (var solo in candidates)
            {
                token.ThrowIfCancellationRequested();

                if (request.DownloadMissingBeatmaps)
                    await ensureBeatmapAndCollectionAsync(solo, collectionName!, downloadedSetIds, result, token).ConfigureAwait(false);

                await processCandidateAsync(solo, request.IncludeInStatsWithoutImport, result, token).ConfigureAwait(false);
                await Task.Delay(request_delay, token).ConfigureAwait(false);
            }

            return result;
        }

        private async Task<List<SoloScoreInfo>> fetchBestBatchAsync(
            int userId,
            RulesetInfo ruleset,
            int offset,
            int batchSize,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            // BP is capped around 100 online; still honour requested offset/limit for two×50 batches.
            var req = new GetUserScoresRequest(userId, ScoreType.Best, new PaginationParameters(offset, batchSize), ruleset);
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
                    // incomplete metadata — still try with mode filter
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

        private async Task ensureBeatmapAndCollectionAsync(
            SoloScoreInfo solo,
            string collectionName,
            HashSet<int> downloadedSetIds,
            EzLocalProfileOnlinePullResult result,
            CancellationToken token)
        {
            int beatmapOnlineId = solo.BeatmapID > 0 ? solo.BeatmapID : solo.Beatmap?.OnlineID ?? 0;
            int setOnlineId = solo.Beatmap?.OnlineBeatmapSetID
                              ?? solo.Beatmap?.BeatmapSet?.OnlineID
                              ?? 0;

            if (beatmapOnlineId <= 0 && setOnlineId <= 0)
                return;

            var local = beatmapOnlineId > 0
                ? beatmapManager.QueryBeatmap(b => b.OnlineID == beatmapOnlineId)
                : null;

            if (local == null && setOnlineId > 0)
            {
                if (downloadedSetIds.Add(setOnlineId))
                {
                    try
                    {
                        bool ok = await downloadBeatmapSetAsync(setOnlineId, token).ConfigureAwait(false);
                        if (ok)
                            result.MapsDownloaded++;
                        else
                            result.Failed++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[EzLocalProfile] Beatmapset {setOnlineId} download failed: {ex.Message}", Ez2ConfigManager.LOGGER_NAME);
                        result.Failed++;
                    }

                    await Task.Delay(request_delay, token).ConfigureAwait(false);
                }

                local = beatmapOnlineId > 0
                    ? beatmapManager.QueryBeatmap(b => b.OnlineID == beatmapOnlineId)
                    : null;
            }
            else if (local != null)
            {
                int localSetId = local.BeatmapSet?.OnlineID ?? setOnlineId;
                if (localSetId > 0 && downloadedSetIds.Add(localSetId))
                    result.MapsAlreadyLocal++;
                else if (localSetId <= 0)
                    result.MapsAlreadyLocal++;
            }

            string? hash = local?.MD5Hash;
            if (string.IsNullOrEmpty(hash))
                hash = solo.Beatmap?.Checksum;

            if (!string.IsNullOrEmpty(hash) && tryAddHashToCollection(collectionName, hash))
                result.CollectionAdds++;
        }

        private async Task<bool> downloadBeatmapSetAsync(int setOnlineId, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var set = new APIBeatmapSet { OnlineID = setOnlineId };
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var download = new DownloadBeatmapSetRequest(set, noVideo: true);

            download.Success += path => tcs.TrySetResult(path);
            download.Failure += ex => tcs.TrySetException(ex);

            await api.PerformAsync(download).ConfigureAwait(false);

            if (download.CompletionState == APIRequestCompletionState.Failed && !tcs.Task.IsCompleted)
                tcs.TrySetException(new InvalidOperationException("Beatmap download failed."));

            string path;

            using (token.Register(() => tcs.TrySetCanceled(token)))
                path = await tcs.Task.ConfigureAwait(false);

            try
            {
                var notification = new ProgressNotification
                {
                    State = ProgressNotificationState.Active,
                    Text = $"Importing beatmapset {setOnlineId}…",
                };

                var imported = (await beatmapManager.Import(notification, new[] { new ImportTask(path) }, new ImportParameters { Batch = true }).ConfigureAwait(false)).ToList();
                return imported.Count > 0;
            }
            finally
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

        private bool tryAddHashToCollection(string collectionName, string md5Hash)
        {
            return realm.Write(r =>
            {
                var collection = r.All<BeatmapCollection>().FirstOrDefault(c => c.Name == collectionName) ?? r.Add(new BeatmapCollection(collectionName));

                if (collection.BeatmapMD5Hashes.Contains(md5Hash))
                    return false;

                collection.BeatmapMD5Hashes.Add(md5Hash);
                collection.LastModified = DateTimeOffset.UtcNow;
                return true;
            });
        }

        private async Task processCandidateAsync(
            SoloScoreInfo solo,
            bool includeInStatsWithoutImport,
            EzLocalProfileOnlinePullResult result,
            CancellationToken token)
        {
            long onlineId = solo.OnlineID;

            if (onlineId <= 0)
            {
                result.Failed++;
                return;
            }

            if (includeInStatsWithoutImport)
            {
                store.UpsertOnlineScoreContribution(CreateContribution(solo));
                result.StatsRecorded++;
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

        public static EzLocalProfileOnlineScoreContribution CreateContribution(SoloScoreInfo solo)
        {
            var beatmap = solo.Beatmap;
            long keys = countKeysFromStatistics(solo.MaximumStatistics);
            if (keys <= 0)
                keys = countKeysFromStatistics(solo.Statistics);

            return new EzLocalProfileOnlineScoreContribution(
                solo.OnlineID,
                solo.RulesetID,
                solo.Rank,
                beatmap?.StarRating ?? 0,
                beatmap?.CircleSize ?? 0,
                beatmap?.ApproachRate ?? 0,
                keys);
        }

        private static long countKeysFromStatistics(IReadOnlyDictionary<HitResult, int> statistics)
        {
            long total = 0;

            foreach (var (hitResult, count) in statistics)
            {
                if (hitResult.IsScorable() && !hitResult.IsBonus())
                    total += count;
            }

            return total;
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
