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
    /// Aggregates per-play SSR vectors into independent player skills (Etterna AggregateSSRs).
    /// Goal via <see cref="EzSsrGoal"/> (Wife subset when statistics exist).
    /// </summary>
    public sealed class EzPlayerSsrAggregator
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzSkillStore skillStore;

        public EzPlayerSsrAggregator(BeatmapManager beatmapManager, EzSkillStore skillStore)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
        }

        /// <summary>Per-play axis rows collected during the last <see cref="ComputeAndStore"/> (for DATA-3 evidence).</summary>
        public IReadOnlyList<EzAxisPlayEvidenceRow> PendingEvidence { get; private set; } = Array.Empty<EzAxisPlayEvidenceRow>();

        public void ComputeAndStore(
            string username,
            IEnumerable<ScoreInfo> scores,
            CancellationToken cancellationToken = default,
            Action? afterEachScore = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            var byKey = new Dictionary<int, List<EzSkillsetVector>>();
            var evidence = new List<EzAxisPlayEvidenceRow>();

            using var calc = new EzMinaCalcFacade();

            foreach (var score in scores)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (score.Ruleset.OnlineID != 3)
                        continue;

                    if (string.IsNullOrWhiteSpace(score.BeatmapHash))
                        continue;

                    var beatmapInfo = score.BeatmapInfo ?? beatmapManager.QueryBeatmap(b => b.Hash == score.BeatmapHash);
                    if (beatmapInfo == null)
                        continue;

                    var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                    var playable = working.GetPlayableBeatmap(score.Ruleset, score.Mods);
                    int keyCount = EzMinaNoteConverter.ResolveKeyCount(playable);
                    float rate = EzModRate.Resolve(score.Mods);
                    double holdRatio = EzChartDanEstimator.ComputeHoldRatio(playable);
                    double od = beatmapInfo.Difficulty.OverallDifficulty;

                    float? goal = EzSsrGoal.ForScore(score, holdRatio, od);
                    if (goal is not float goalValue)
                        continue;

                    var notes = EzMinaNoteConverter.Convert(playable);
                    if (notes.Length == 0)
                        continue;

                    var vector = calc.CalculateSsr(notes, rate, goalValue);
                    if (vector.Overall <= 0)
                        continue;

                    if (!byKey.TryGetValue(keyCount, out var list))
                        byKey[keyCount] = list = new List<EzSkillsetVector>();

                    list.Add(vector);

                    foreach (var (axis, axisValue) in vector.Enumerate())
                    {
                        if (axisValue <= 0 || !double.IsFinite(axisValue))
                            continue;

                        evidence.Add(new EzAxisPlayEvidenceRow
                        {
                            Username = username,
                            KeyCount = keyCount,
                            SkillId = axis.ToSsrSkillId(),
                            BeatmapHash = score.BeatmapHash,
                            AxisValue = axisValue,
                            Accuracy = score.Accuracy,
                            Rate = rate,
                            ScoredAt = score.Date,
                            AlgorithmVersion = EzManiaSkillAlgorithm.VERSION,
                        });
                    }
                }
                finally
                {
                    afterEachScore?.Invoke();
                }
            }

            foreach ((int keyCount, List<EzSkillsetVector> plays) in byKey)
            {
                var aggregated = EzSsrAggregator.AggregateVectors(plays);
                bool provisional = plays.Count < EzPlayerSsrSnapshot.QUALIFYING_PLAYS;
                skillStore.WritePlayerSsr(username, keyCount, aggregated, plays.Count, provisional);
            }

            PendingEvidence = evidence;
        }

        /// <summary>Accuracy-only clamp path (tests / callers without full score).</summary>
        public static float AccuracyToGoal(double accuracy) => EzSsrGoal.ForAccuracy(accuracy);
    }
}
