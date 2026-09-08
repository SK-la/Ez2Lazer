// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// DI façade for the single shared local profile store and aggregation.
    /// </summary>
    public class EzLocalProfileService : IDisposable
    {
        private readonly EzLocalProfileStore store;
        private readonly EzLocalProfileAggregator aggregator;
        private readonly EzPlayerSsrAggregator? ssrAggregator;
        private readonly EzPlayerDanAggregator? danAggregator;
        private readonly Lock computeLock = new Lock();
        private CancellationTokenSource? computeCts;

        public Bindable<EzLocalProfileSnapshot> Snapshot { get; } = new Bindable<EzLocalProfileSnapshot>(new EzLocalProfileSnapshot());

        public BindableBool IsComputing { get; } = new BindableBool();

        public EzLocalProfileService(
            Storage storage,
            RealmAccess realm,
            EzAnalysisPersistentStore analysisStore,
            BeatmapManager beatmapManager,
            ScoreManager scoreManager,
            IEzReplaySession replaySession,
            EzPlayerSsrAggregator? ssrAggregator = null,
            EzPlayerDanAggregator? danAggregator = null)
        {
            store = new EzLocalProfileStore(storage);
            aggregator = new EzLocalProfileAggregator(realm, analysisStore, beatmapManager, scoreManager, replaySession);
            this.ssrAggregator = ssrAggregator;
            this.danAggregator = danAggregator;
            Snapshot.Value = store.LoadSnapshot();
        }

        public IReadOnlyList<EzLocalProfileUsernameCount> ScanUsernameCounts() => aggregator.ScanUsernameCounts();

        public IReadOnlyList<string> GetPreviouslyIncludedUsernames() => store.LoadIncludedUsernames();

        /// <summary>
        /// Display snapshot for the player filter: <see cref="EzLocalProfileConstants.ALL_PLAYERS"/> or one stored username.
        /// </summary>
        public EzLocalProfileSnapshot LoadDisplaySnapshot(string? usernameFilter) => store.LoadSnapshotForUsername(usernameFilter);

        public IReadOnlyList<EzLocalProfileDrillScoreRow> LoadDrillScores(int rulesetId, string? usernameFilter = null)
            => store.LoadDrillScores(rulesetId, usernameFilter);

        public bool HasOnlineScoreContributions() => store.LoadOnlineScoreContributions().Count > 0;

        /// <param name="usernamesToRecompute">Names whose score stats will be recalculated and overwrite their stored slice.</param>
        /// <param name="replaceOtherUsernames">
        /// If true, drop stored slices for names not in <paramref name="usernamesToRecompute"/>.
        /// If false, leave other names' slices untouched.
        /// </param>
        /// <param name="progress">Optional progress reporter for UI notifications.</param>
        /// <param name="cancellationToken">Cancels in-flight aggregation; partial results are not written.</param>
        public Task ComputeAsync(
            IReadOnlyCollection<string> usernamesToRecompute,
            bool replaceOtherUsernames = false,
            IProgress<EzLocalProfileComputeProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            lock (computeLock)
            {
                computeCts?.Cancel();
                computeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = computeCts.Token;

                IsComputing.Value = true;

                return Task.Run(() =>
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();

                        var selected = usernamesToRecompute
                                       .Where(n => !string.IsNullOrWhiteSpace(n))
                                       .Distinct(StringComparer.Ordinal)
                                       .ToList();

                        // Online-only refresh path: no local usernames selected, just rebuild display from existing partitions + online.
                        var byUser = selected.Count > 0
                            ? aggregator.AggregateByUsername(selected, progress, token)
                            : new Dictionary<string, EzLocalProfileAggregationResult>(StringComparer.Ordinal);

                        token.ThrowIfCancellationRequested();

                        // Signal UI that aggregation is done and we are persisting (may include a Realm scan).
                        progress?.Report(new EzLocalProfileComputeProgress(1, 1, Saving: true));

                        var online = store.LoadOnlineScoreContributions();
                        var localOnlineIds = aggregator.CollectLocalOnlineScoreIds();
                        store.ApplyUsernamePartitions(byUser, replaceOtherUsernames, online, localOnlineIds);

                        if (selected.Count > 0)
                            writePlayerSkills(selected, token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "[EzLocalProfile] Failed to compute local profile statistics.", Ez2ConfigManager.LOGGER_NAME);
                        throw;
                    }
                    finally
                    {
                        IsComputing.Value = false;
                    }
                }, token);
            }
        }

        private void writePlayerSkills(IReadOnlyList<string> usernames, CancellationToken token)
        {
            if (ssrAggregator == null && danAggregator == null)
                return;

            try
            {
                var maniaScores = aggregator.CollectDetachedManiaScores(usernames);

                foreach ((string username, List<ScoreInfo> scores) in maniaScores)
                {
                    token.ThrowIfCancellationRequested();

                    if (scores.Count == 0)
                        continue;

                    try
                    {
                        if (ssrAggregator != null)
                        {
                            ssrAggregator.ComputeAndStore(username, scores);

                            try
                            {
                                store.ReplaceAxisPlays(username, ssrAggregator.PendingEvidence);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                Logger.Error(ex, "[EzLocalProfile] Failed to persist axis play evidence after SSR compute.", Ez2ConfigManager.LOGGER_NAME);
                            }
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Logger.Error(ex, "[EzLocalProfile] Failed to compute player SSR skills after profile save.", Ez2ConfigManager.LOGGER_NAME);
                    }

                    try
                    {
                        if (danAggregator != null)
                        {
                            danAggregator.ComputeAndStore(username, scores);

                            try
                            {
                                store.ReplaceDanClears(username, danAggregator.PendingEvidence);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                Logger.Error(ex, "[EzLocalProfile] Failed to persist dan clear evidence after Dan compute.", Ez2ConfigManager.LOGGER_NAME);
                            }
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Logger.Error(ex, "[EzLocalProfile] Failed to compute player Dan estimates after profile save.", Ez2ConfigManager.LOGGER_NAME);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[EzLocalProfile] Failed to collect mania scores for skill/dan compute.", Ez2ConfigManager.LOGGER_NAME);
            }
        }

        public IReadOnlyList<EzDanClearEvidenceRow> GetDanClears(string username, int? keyCount = null, string? side = null)
            => store.GetDanClears(username, keyCount, side);

        public IReadOnlyList<EzAxisPlayEvidenceRow> GetAxisPlays(string username, int? keyCount = null, string? skillId = null)
            => store.GetAxisPlays(username, keyCount, skillId);

        public void ReloadFromDisk()
        {
            Snapshot.Value = store.LoadSnapshot();
        }

        public void Dispose()
        {
            computeCts?.Cancel();
            computeCts?.Dispose();
            store.Dispose();
        }
    }
}
