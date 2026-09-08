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
            EzPlayerDanAggregator? danAggregator = null,
            EzLocalProfileStore? sharedStore = null)
        {
            Store = sharedStore ?? new EzLocalProfileStore(storage);
            aggregator = new EzLocalProfileAggregator(realm, analysisStore, beatmapManager, scoreManager, replaySession);
            this.ssrAggregator = ssrAggregator;
            this.danAggregator = danAggregator;
            Snapshot.Value = Store.LoadSnapshot();
        }

        /// <summary>Shared store for DI (e.g. <see cref="EzSkillProvider"/> evidence reads).</summary>
        public EzLocalProfileStore Store { get; }

        public IReadOnlyList<EzLocalProfileUsernameCount> ScanUsernameCounts() => aggregator.ScanUsernameCounts();

        public IReadOnlyList<string> GetPreviouslyIncludedUsernames()
            => Store.LoadIncludedUsernames()
                    .Select(EzLocalProfileConstants.NormaliseUsername)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

        /// <summary>
        /// Display snapshot for the player filter: <see cref="EzLocalProfileConstants.ALL_PLAYERS"/> or one stored username.
        /// Guest also falls back to legacy <c>(unknown)</c> partitions.
        /// </summary>
        public EzLocalProfileSnapshot LoadDisplaySnapshot(string? usernameFilter)
        {
            if (EzLocalProfileConstants.IsGuestUsername(usernameFilter))
            {
                var guest = Store.LoadSnapshotForUsername(EzLocalProfileConstants.GUEST_USERNAME);
                if (guest.HasData)
                    return guest;

                return Store.LoadSnapshotForUsername(EzLocalProfileConstants.LEGACY_UNKNOWN_USERNAME);
            }

            return Store.LoadSnapshotForUsername(usernameFilter);
        }

        public IReadOnlyList<EzLocalProfileDrillScoreRow> LoadDrillScores(int rulesetId, string? usernameFilter = null)
            => Store.LoadDrillScores(rulesetId, usernameFilter);

        public bool HasOnlineScoreContributions() => Store.LoadOnlineScoreContributions().Count > 0;

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
                        Dictionary<string, EzLocalProfileAggregationResult> byUser;
                        Dictionary<string, List<ScoreInfo>> maniaScores;

                        if (selected.Count > 0)
                        {
                            (byUser, maniaScores) = aggregator.AggregateByUsername(selected, progress, token);
                        }
                        else
                        {
                            byUser = new Dictionary<string, EzLocalProfileAggregationResult>(StringComparer.Ordinal);
                            maniaScores = new Dictionary<string, List<ScoreInfo>>(StringComparer.Ordinal);
                        }

                        token.ThrowIfCancellationRequested();

                        // Signal UI that aggregation is done and we are persisting (may include a Realm scan).
                        progress?.Report(new EzLocalProfileComputeProgress(1, 1, Saving: true));

                        var online = Store.LoadOnlineScoreContributions();
                        var localOnlineIds = aggregator.CollectLocalOnlineScoreIds();
                        Store.ApplyUsernamePartitions(byUser, replaceOtherUsernames, online, localOnlineIds);

                        if (selected.Count > 0)
                            writePlayerSkills(maniaScores, token);
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

        private void writePlayerSkills(Dictionary<string, List<ScoreInfo>> maniaScoresByUser, CancellationToken token)
        {
            if (ssrAggregator == null && danAggregator == null)
                return;

            foreach ((string username, List<ScoreInfo> scores) in maniaScoresByUser)
            {
                token.ThrowIfCancellationRequested();

                if (scores.Count == 0)
                    continue;

                tryComputeAndPersist(
                    username,
                    () =>
                    {
                        ssrAggregator!.ComputeAndStore(username, scores);
                        Store.ReplaceAxisPlays(username, ssrAggregator.PendingEvidence);
                    },
                    ssrAggregator != null,
                    "[EzLocalProfile] Failed to compute/persist player SSR skills after profile save.");

                tryComputeAndPersist(
                    username,
                    () =>
                    {
                        danAggregator!.ComputeAndStore(username, scores);
                        Store.ReplaceDanClears(username, danAggregator.PendingEvidence);
                    },
                    danAggregator != null,
                    "[EzLocalProfile] Failed to compute/persist player Dan estimates after profile save.");
            }
        }

        private static void tryComputeAndPersist(string username, Action action, bool enabled, string errorMessage)
        {
            if (!enabled)
                return;

            try
            {
                action();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.Error(ex, $"{errorMessage} ({username})", Ez2ConfigManager.LOGGER_NAME);
            }
        }

        public IReadOnlyList<EzDanClearEvidenceRow> GetDanClears(string username, int? keyCount = null, string? side = null, int? algorithmVersion = null)
            => Store.GetDanClears(username, keyCount, side, algorithmVersion);

        public IReadOnlyList<EzAxisPlayEvidenceRow> GetAxisPlays(string username, int? keyCount = null, string? skillId = null, int? algorithmVersion = null)
            => Store.GetAxisPlays(username, keyCount, skillId, algorithmVersion);

        public void ReloadFromDisk()
        {
            Snapshot.Value = Store.LoadSnapshot();
        }

        public void Dispose()
        {
            computeCts?.Cancel();
            computeCts?.Dispose();
            Store.Dispose();
        }
    }
}
