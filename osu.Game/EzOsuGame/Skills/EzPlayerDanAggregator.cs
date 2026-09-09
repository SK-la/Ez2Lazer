// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Player dan: chart <see cref="EzChartDanEstimator.TryEstimate"/> → credit clears → average window.
    /// Not LeoBlack; estimates are provisional (DATA-1 heuristic + rate).
    /// </summary>
    public sealed class EzPlayerDanAggregator
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzSkillStore skillStore;
        private readonly EzChartDanEstimator chartDanEstimator;

        public EzPlayerDanAggregator(BeatmapManager beatmapManager, EzSkillStore skillStore, EzChartDanEstimator chartDanEstimator)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
            this.chartDanEstimator = chartDanEstimator;
        }

        /// <summary>Credited clears collected during the last <see cref="ComputeAndStore"/> (for evidence).</summary>
        public IReadOnlyList<EzDanClearEvidenceRow> PendingEvidence { get; private set; } = Array.Empty<EzDanClearEvidenceRow>();

        public void ComputeAndStore(
            string username,
            IEnumerable<ScoreInfo> scores,
            CancellationToken cancellationToken = default,
            Action? afterEachScore = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            var clearsByBucket = new Dictionary<(int KeyCount, EzDanSide Side), List<double>>();
            var evidence = new List<EzDanClearEvidenceRow>();

            foreach (var score in scores)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
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

                    var chart = chartDanEstimator.TryEstimate(beatmapInfo, score.Mods);
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
                        Side = chart.Side.ToId(),
                        BeatmapHash = hash,
                        Rate = EzModRate.Resolve(score.Mods),
                        CreditedDan = value,
                        Accuracy = score.Accuracy,
                        ScoredAt = score.Date,
                        AlgorithmVersion = EzDanAlgorithm.VERSION,
                    });
                }
                finally
                {
                    afterEachScore?.Invoke();
                }
            }

            DateTimeOffset at = DateTimeOffset.UtcNow;

            foreach (((int keyCount, EzDanSide side), List<double> clears) in clearsByBucket)
            {
                if (clears.Count < EzDanAlgorithm.CLEAR_QUORUM)
                    continue;

                var window = clears
                             .OrderByDescending(v => v)
                             .Take(EzDanAlgorithm.CLEAR_WINDOW)
                             .ToList();

                double rawDan = window.Average();
                var ladder = EzDanLadders.For(keyCount, side);
                string label = ladder.ParseLabel(rawDan);
                double? ceiling = ladder.Ceiling;

                skillStore.WriteDanEstimate(new EzDanEstimate
                {
                    Username = username,
                    KeyCount = keyCount,
                    Side = side.ToId(),
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
