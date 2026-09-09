// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Player dan clears: chart <see cref="EzChartDanEstimator.TryEstimate"/> → credit.
    /// Side <see cref="EzDanEstimate"/> / GetDan is written only by
    /// <see cref="EzSkillProvider"/> <c>writeSideHeadline</c> (hub fold), not here.
    /// </summary>
    public sealed class EzPlayerDanAggregator
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzChartDanEstimator chartDanEstimator;

        public EzPlayerDanAggregator(BeatmapManager beatmapManager, EzChartDanEstimator chartDanEstimator)
        {
            this.beatmapManager = beatmapManager;
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

            PendingEvidence = evidence;
        }
    }
}
