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
        /// caller hands them to the chart-side chain instead of rating them here; see
        /// <see cref="EzLocalProfileService.ChartSideBackfillRequested"/>.
        /// </summary>
        public IReadOnlyCollection<string> MissingChartHashes { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// One load of the per-play cache for a whole pass, so a multi-player run does not re-read the table once per
        /// player. Pass the result to <see cref="ComputeAndStore"/>.
        /// </summary>
        public IReadOnlyDictionary<Guid, EzDanPlayCacheRow> LoadPlayCache()
            => profileStore?.LoadDanPlayCache() ?? new Dictionary<Guid, EzDanPlayCacheRow>();

        public void ComputeAndStore(
            string username,
            IEnumerable<ScoreInfo> scores,
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
                foreach (var score in scores)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (score.Ruleset.OnlineID != 3)
                            continue;

                        if (!score.Passed || score.Rank == ScoreRank.F)
                            continue;

                        if (score.Accuracy <= 0 || !double.IsFinite(score.Accuracy))
                            continue;

                        // Hub ez_windows: EZ widened hit windows — no dan credit.
                        if (score.Mods.Any(static m => m is ModEasy))
                            continue;

                        bool hasCachedPlay = cachedPlays.TryGetValue(score.ID, out var cachedPlay)
                                             && string.Equals(cachedPlay.BeatmapHash, score.BeatmapHash, StringComparison.Ordinal);

                        if (hasCachedPlay)
                        {
                            if (cachedPlay.Credited)
                                mergeCachedCredit(bestByChartRate, username, cachedPlay);

                            continue;
                        }

                        var beatmapInfo = score.BeatmapInfo ?? beatmapManager.QueryBeatmap(b => b.Hash == score.BeatmapHash);
                        if (beatmapInfo == null)
                            continue;

                        if (beatmapInfo.Ruleset.OnlineID != 3)
                            continue;

                        string hash = score.BeatmapHash;
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
                                // folds the play in with its credit.
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

                        double? credited = EzDanCredit.CreditedDanFor(chart.RawDan, score.Accuracy, chart.Side, chart.KeyCount);

                        if (credited is not double value)
                        {
                            cacheNoCredit(score, username, hash, rate, pendingCacheWrites);
                            continue;
                        }

                        var cacheRow = new EzDanPlayCacheRow(
                            score.ID,
                            username,
                            hash,
                            EzDanAlgorithm.VERSION,
                            true,
                            chart.KeyCount,
                            chart.Side.ToId(),
                            rate,
                            value,
                            score.Accuracy,
                            score.Date);
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

        private static void cacheNoCredit(ScoreInfo score, string username, string hash, double rate, List<EzDanPlayCacheRow> pending)
        {
            pending.Add(new EzDanPlayCacheRow(
                score.ID,
                username,
                hash,
                EzDanAlgorithm.VERSION,
                false,
                0,
                string.Empty,
                rate,
                0,
                score.Accuracy,
                score.Date));
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
