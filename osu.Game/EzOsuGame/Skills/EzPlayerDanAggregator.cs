// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Player dan: rate-aware chart verdict → credit clears → average window.
    /// Not LeoBlack; estimates are provisional (DATA-1 heuristic + rate).
    /// </summary>
    public sealed class EzPlayerDanAggregator
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzSkillStore skillStore;
        private readonly EzBeatmapMsdComputer msdComputer;

        public EzPlayerDanAggregator(BeatmapManager beatmapManager, EzSkillStore skillStore, EzBeatmapMsdComputer msdComputer)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
            this.msdComputer = msdComputer;
        }

        /// <summary>Credited clears collected during the last <see cref="ComputeAndStore"/> (for DATA-3 evidence).</summary>
        public IReadOnlyList<EzDanClearEvidenceRow> PendingEvidence { get; private set; } = Array.Empty<EzDanClearEvidenceRow>();

        public void ComputeAndStore(string username, IEnumerable<ScoreInfo> scores)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            var clearsByBucket = new Dictionary<(int KeyCount, string Side), List<double>>();
            var evidence = new List<EzDanClearEvidenceRow>();
            var msdCache = new Dictionary<(string Hash, int RateMilli), IReadOnlyDictionary<string, double>>();

            foreach (var score in scores)
            {
                if (score.Ruleset.OnlineID != 3)
                    continue;

                if (score.Accuracy <= 0 || !double.IsFinite(score.Accuracy))
                    continue;

                var beatmapInfo = score.BeatmapInfo ?? beatmapManager.QueryBeatmap(b => b.Hash == score.BeatmapHash);
                if (beatmapInfo == null)
                    continue;

                if (beatmapInfo.Ruleset.OnlineID != 3)
                    continue;

                float rate = EzModRate.Resolve(score.Mods);
                int rateMilli = (int)Math.Round(rate * 1000);
                var cacheKey = (beatmapInfo.Hash, rateMilli);

                if (!msdCache.TryGetValue(cacheKey, out var msd))
                {
                    if (EzModRate.IsNomodRate(rate))
                    {
                        msd = msdComputer.TryGetOrCompute(beatmapInfo);
                    }
                    else
                    {
                        var workingForMsd = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                        var playableForMsd = workingForMsd.GetPlayableBeatmap(score.Ruleset, score.Mods);
                        using var calc = new EzMinaCalcFacade();
                        var vector = calc.CalculateMsd(playableForMsd, rate);
                        if (vector.Overall <= 0 && vector.Stream <= 0)
                            continue;

                        msd = EzChartDanEstimator.VectorToMsdDict(vector);
                    }

                    if (msd == null || msd.Count == 0)
                        continue;

                    msdCache[cacheKey] = msd;
                }

                var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                var playable = working.GetPlayableBeatmap(score.Ruleset, score.Mods);
                var chart = EzChartDanEstimator.FromMsdAndPlayable(msd, playable);
                if (chart == null)
                    continue;

                double? credited = EzDanCredit.CreditedDanFor(chart.RawDan, score.Accuracy, chart.Side, chart.KeyCount);
                if (credited is not double value)
                    continue;

                var key = (chart.KeyCount, chart.Side);
                if (!clearsByBucket.TryGetValue(key, out var list))
                    clearsByBucket[key] = list = new List<double>();

                list.Add(value);

                string hash = score.BeatmapHash;
                if (string.IsNullOrWhiteSpace(hash))
                    hash = beatmapInfo.Hash;

                evidence.Add(new EzDanClearEvidenceRow
                {
                    Username = username,
                    KeyCount = chart.KeyCount,
                    Side = chart.Side,
                    BeatmapHash = hash,
                    Rate = rate,
                    CreditedDan = value,
                    Accuracy = score.Accuracy,
                    ScoredAt = score.Date,
                });
            }

            DateTimeOffset at = DateTimeOffset.UtcNow;

            foreach (((int keyCount, string side), List<double> clears) in clearsByBucket)
            {
                if (clears.Count < EzDanAlgorithm.CLEAR_QUORUM)
                    continue;

                var window = clears
                             .OrderByDescending(v => v)
                             .Take(EzDanAlgorithm.CLEAR_WINDOW)
                             .ToList();

                double rawDan = window.Average();
                string label = EzDanLabels.LabelFor(rawDan, side, keyCount);
                double? ceiling = EzDanLabels.CeilingFor(side, keyCount);

                skillStore.WriteDanEstimate(new EzDanEstimate
                {
                    Username = username,
                    KeyCount = keyCount,
                    Side = side,
                    RawDan = rawDan,
                    Label = label,
                    Clears = clears.Count,
                    BeyondTable = ceiling is double c && rawDan >= c,
                    ClearWindowHave = window.Count,
                    ClearWindowNeed = EzDanAlgorithm.CLEAR_WINDOW,
                    AlgorithmVersion = EzDanAlgorithm.VERSION,
                    ComputedAt = at,
                });
            }

            PendingEvidence = evidence;
        }
    }
}
