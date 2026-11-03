// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Player dan clears: chart <see cref="EzChartDanEstimator.TryEstimate"/> → credit.
    /// Side <see cref="EzDanEstimate"/> / GetDan is written only by
    /// <see cref="EzSkillProvider"/> <c>writeSideHeadline</c> (hub fold), not here.
    /// Hub <c>collectDanClears</c> subset: fail/EZ reject, (hash,rate) best-only.
    /// </summary>
    public sealed class EzPlayerDanAggregator
    {
        private const int cache_flush_batch = 64;

        private readonly BeatmapManager beatmapManager;
        private readonly EzChartDanEstimator chartDanEstimator;
        private readonly EzSkillStore skillStore;
        private readonly EzLocalProfileStore? profileStore;

        public EzPlayerDanAggregator(BeatmapManager beatmapManager, EzChartDanEstimator chartDanEstimator, EzSkillStore skillStore, EzLocalProfileStore? profileStore = null)
        {
            this.beatmapManager = beatmapManager;
            this.chartDanEstimator = chartDanEstimator;
            this.skillStore = skillStore;
            this.profileStore = profileStore;
        }

        /// <summary>Credited clears collected during the last <see cref="ComputeAndStore"/> (for evidence).</summary>
        public IReadOnlyList<EzDanClearEvidenceRow> PendingEvidence { get; private set; } = Array.Empty<EzDanClearEvidenceRow>();

        /// <summary>
        /// Chart hashes this pass needed a persisted <see cref="EzPersistedChartDan"/> for but found none. The
        /// caller re-derives the players whose own plays hit one once the chart-side chain has rated it; see
        /// <see cref="EzLocalProfileService.CollectChartChainDebt"/>.
        /// </summary>
        public IReadOnlyCollection<string> MissingChartHashes { get; private set; } = Array.Empty<string>();

        private HashSet<string>? unrateableMsdHashes;

        /// <summary>
        /// Charts whose MSD settled as unrateable, so no ChartDan row can ever arrive for them. Loaded on first
        /// need (a ChartDan row that is otherwise missing) and reused for the rest of the pass, because it is only
        /// ever consulted after such a miss and a chart that later gains a row is answered by that row instead.
        /// </summary>
        private HashSet<string> unrateableChartsForPass
            => unrateableMsdHashes ??= skillStore.GetUnrateableMsdHashes();

        /// <summary>
        /// Marks the start of a new skills pass, dropping lookups that were memoised against the previous one.
        /// </summary>
        public void BeginSkillPass() => unrateableMsdHashes = null;

        /// <summary>
        /// One load of the per-play cache for a whole pass, so a multi-player run does not re-read the table once per
        /// player. Pass the result to <see cref="ComputeAndStore"/>.
        /// </summary>
        public IReadOnlyDictionary<Guid, EzDanPlayCacheRow> LoadPlayCache()
            => profileStore?.LoadDanPlayCache() ?? new Dictionary<Guid, EzDanPlayCacheRow>();

        /// <param name="plays">
        /// The player's mania plays as reduced rows (the row collector filters to mania). A play the per-play cache
        /// can answer is folded from its cache row and never resolved as a score.
        /// </param>
        /// <param name="resolveScore">
        /// Resolves a play the per-play cache does not answer — see
        /// <see cref="EzLocalProfileAggregator.CreateWindowedScoreResolver"/>.
        /// </param>
        public void ComputeAndStore(
            string username,
            IEnumerable<EzSkillPlayRow> plays,
            Func<Guid, ScoreInfo?>? resolveScore,
            CancellationToken cancellationToken = default,
            Action? afterEachScore = null,
            IReadOnlyDictionary<Guid, EzDanPlayCacheRow>? loadedPlayCache = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            // Best credited clear per (beatmap hash, rate) — hub collectDanClears dedupe.
            var bestByChartRate = new Dictionary<(string Hash, double Rate), EzDanClearEvidenceRow>();
            var missingCharts = new HashSet<string>(StringComparer.Ordinal);

            // Per-play Dan cache: an already-evaluated play skips the chart-dan estimate (incremental backfill).
            // The caller can hand one in so a multi-player pass loads the table once instead of once per player.
            var cachedPlays = loadedPlayCache ?? profileStore?.LoadDanPlayCache() ?? new Dictionary<Guid, EzDanPlayCacheRow>();
            var pendingCacheWrites = new List<EzDanPlayCacheRow>();

            try
            {
                foreach (var play in plays)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (!play.Passed || play.Rank == ScoreRank.F)
                            continue;

                        if (play.Accuracy <= 0 || !double.IsFinite(play.Accuracy))
                            continue;

                        bool hasCachedPlay = cachedPlays.TryGetValue(play.ScoreId, out var cachedPlay)
                                             && string.Equals(cachedPlay.BeatmapHash, play.BeatmapHash, StringComparison.Ordinal);

                        if (hasCachedPlay)
                        {
                            // The EZ gate is already in the recorded verdict: a credited row cannot come from an EZ
                            // play, and an uncredited one has nothing to fold in — so a cached play never reads mods.
                            if (cachedPlay.Credited)
                                mergeCachedCredit(bestByChartRate, username, cachedPlay);

                            continue;
                        }

                        var score = resolveScore?.Invoke(play.ScoreId);

                        if (score == null)
                            continue;

                        // Hub ez_windows: EZ widened hit windows — no dan credit.
                        if (score.Mods.Any(static m => m is ModEasy))
                            continue;

                        var beatmapInfo = score.BeatmapInfo ?? beatmapManager.QueryBeatmap(b => b.Hash == play.BeatmapHash);
                        if (beatmapInfo == null)
                            continue;

                        if (beatmapInfo.Ruleset.OnlineID != 3)
                            continue;

                        string hash = play.BeatmapHash;
                        if (string.IsNullOrWhiteSpace(hash))
                            hash = beatmapInfo.Hash;

                        double rate = EzModRate.Resolve(score.Mods);
                        bool chartAffectingMods = EzModRate.AffectsChartSkills(score.Mods);

                        // Fast path: with no chart-affecting mod, the persisted nomod ChartDan baseline is
                        // exactly what TryEstimate would derive, so credit straight off it and skip the
                        // WorkingBeatmap/playable build. Anything whole-chart (rate / key conversion /
                        // difficulty) still needs the live estimate.
                        EzChartDanVerdict? chart = null;

                        if (!chartAffectingMods
                            && skillStore.TryGetChartDan(hash, out var persisted) && persisted != null)
                        {
                            var side = persisted.HoldRatio >= EzDanAlgorithm.LnPrimaryMinRatioFor(persisted.KeyCount)
                                ? EzDanSide.Ln
                                : EzDanSide.Rc;

                            chart = persisted.ToVerdict(side);
                        }

                        if (chart == null)
                        {
                            if (!chartAffectingMods)
                            {
                                // This pass never rates a chart: it reads the chart-side row or the play simply
                                // waits. A missing row is reported so the chain computes it, and the next pass
                                // folds the play in with its credit - except where the chain will never produce
                                // one, which is settled rather than missing (see EzChartChainCoverage).
                                if (EzChartChainCoverage.IsRateableChart(beatmapInfo) && !unrateableChartsForPass.Contains(hash))
                                    missingCharts.Add(hash);

                                continue;
                            }

                            // A rate / key-conversion mod yields a chart variant no persisted NoMod row can answer,
                            // so the live estimate is the only correct source. It reads the playable and writes
                            // nothing (see EzChartDanEstimator.TryComputeLiveSnapshot).
                            chart = chartDanEstimator.TryEstimate(beatmapInfo, score.Mods);
                        }

                        if (chart == null)
                        {
                            // Do not cache: a missing chart-dan estimate may be transient
                            // (beatmap analysis not computed yet), so retry it on the next run.
                            continue;
                        }

                        double? credited = EzDanCredit.CreditedDanFor(chart.RawDan, play.Accuracy, chart.Side, chart.KeyCount);

                        if (credited is not double value)
                        {
                            cacheNoCredit(play, username, hash, rate, pendingCacheWrites);
                            continue;
                        }

                        var cacheRow = new EzDanPlayCacheRow(
                            play.ScoreId,
                            username,
                            hash,
                            EzDanAlgorithm.VERSION,
                            true,
                            chart.KeyCount,
                            chart.Side.ToId(),
                            rate,
                            value,
                            play.Accuracy,
                            play.ScoredAt);
                        pendingCacheWrites.Add(cacheRow);
                        mergeCachedCredit(bestByChartRate, username, cacheRow);
                    }
                    finally
                    {
                        if (pendingCacheWrites.Count >= cache_flush_batch)
                            flushCache(pendingCacheWrites);

                        afterEachScore?.Invoke();
                    }
                }
            }
            finally
            {
                flushCache(pendingCacheWrites);
            }

            PendingEvidence = bestByChartRate.Values
                                             .OrderByDescending(r => r.CreditedDan)
                                             .ThenByDescending(r => r.ScoredAt)
                                             .ToList();
            MissingChartHashes = missingCharts;
        }

        private static void mergeCachedCredit(
            Dictionary<(string Hash, double Rate), EzDanClearEvidenceRow> bestByChartRate,
            string username,
            EzDanPlayCacheRow cachedPlay)
        {
            var key = (cachedPlay.BeatmapHash, Math.Round(cachedPlay.Rate, 4));

            var row = new EzDanClearEvidenceRow
            {
                Username = username,
                KeyCount = cachedPlay.KeyCount,
                Side = cachedPlay.Side,
                BeatmapHash = cachedPlay.BeatmapHash,
                Rate = cachedPlay.Rate,
                CreditedDan = cachedPlay.CreditedDan,
                Accuracy = cachedPlay.Accuracy,
                ScoredAt = cachedPlay.ScoredAt,
                AlgorithmVersion = cachedPlay.AlgorithmVersion,
            };

            if (!bestByChartRate.TryGetValue(key, out var existing)
                || row.CreditedDan > existing.CreditedDan
                || (Math.Abs(row.CreditedDan - existing.CreditedDan) < 1e-9 && row.ScoredAt > existing.ScoredAt))
            {
                bestByChartRate[key] = row;
            }
        }

        private static void cacheNoCredit(EzSkillPlayRow play, string username, string hash, double rate, List<EzDanPlayCacheRow> pending)
        {
            pending.Add(new EzDanPlayCacheRow(
                play.ScoreId,
                username,
                hash,
                EzDanAlgorithm.VERSION,
                false,
                0,
                string.Empty,
                rate,
                0,
                play.Accuracy,
                play.ScoredAt));
        }

        private void flushCache(List<EzDanPlayCacheRow> pending)
        {
            if (profileStore == null || pending.Count == 0)
                return;

            profileStore.UpsertDanPlayCache(pending);
            pending.Clear();
        }
    }
}
