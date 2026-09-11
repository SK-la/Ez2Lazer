// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Aggregates per-play SSR vectors into independent player skills (Etterna AggregateSSRs).
    /// Goal via <see cref="EzSsrGoal"/> (Wife subset when statistics exist).
    /// </summary>
    public sealed class EzPlayerSsrAggregator
    {
        /// <summary>Max skill-history samples stored per keymode (trend chart cap).</summary>
        public const int HISTORY_MAX_POINTS = 64;

        /// <summary>
        /// Rolling play count for history AggregateSSRs (recent window so the curve can fall).
        /// Career-prefix Aggregate is structurally monotonic.
        /// </summary>
        public const int HISTORY_ROLLING_PLAYS = 50;

        private readonly BeatmapManager beatmapManager;
        private readonly EzSkillStore skillStore;

        public EzPlayerSsrAggregator(BeatmapManager beatmapManager, EzSkillStore skillStore)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
        }

        /// <summary>Per-play axis rows collected during the last <see cref="ComputeAndStore"/> (for DATA-3 evidence).</summary>
        public IReadOnlyList<EzAxisPlayEvidenceRow> PendingEvidence { get; private set; } = Array.Empty<EzAxisPlayEvidenceRow>();

        private readonly record struct TimedPlay(DateTimeOffset ScoredAt, EzSkillsetVector Vector);

        private readonly record struct PatternPlay(double Overall, IReadOnlyList<string> Patterns);

        public void ComputeAndStore(
            string username,
            IEnumerable<ScoreInfo> scores,
            CancellationToken cancellationToken = default,
            Action? afterEachScore = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            var byKey = new Dictionary<int, List<TimedPlay>>();
            var patternPlaysByKey = new Dictionary<int, List<PatternPlay>>();
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
                    EzSkillsetVector vector;

                    if (EzMinaCalcFacade.SupportsNoteArrayKeyCount(keyCount) && notes.Length > 0)
                    {
                        vector = calc.CalculateSsr(notes, rate, goalValue);
                    }
                    else if (EzMinaCalcFacade.SupportsOsuTextKeyCount(keyCount))
                    {
                        // Note-array API is 4K-only; encode playable (mods applied) for 6K/7K.
                        var sb = new StringBuilder();
                        using (var writer = new StringWriter(sb))
                            new LegacyBeatmapEncoder(playable, working.Skin, null).Encode(writer);

                        string osuText = sb.ToString();
                        if (string.IsNullOrWhiteSpace(osuText))
                            continue;

                        vector = calc.CalculateSsrFromOsuText(osuText, score.BeatmapHash + ".osu", rate, goalValue);
                    }
                    else
                    {
                        continue;
                    }

                    if (vector.Overall <= 0)
                        continue;

                    if (!byKey.TryGetValue(keyCount, out var list))
                        byKey[keyCount] = list = new List<TimedPlay>();

                    list.Add(new TimedPlay(score.Date, vector));

                    string[] patterns = Array.Empty<string>();

                    // Hub pattern ratings need chart pattern tags (ChartSkillInfo). Prefer stored rows
                    // (DATA-ChartSkillInfo-Batch). On miss, compute in-memory for this play only —
                    // TODO(DATA-Skills-PatternRatings): do not Upsert per-score here; if SSR recompute
                    // still races empty ChartSkillInfo in the wild, batch-ensure hashes before Aggregate.
                    if (skillStore.TryGetChartSkillInfo(score.BeatmapHash, out var chart) && chart is { IsUnavailable: false })
                    {
                        patterns = chart.Patterns;
                    }
                    else
                    {
                        try
                        {
                            var msd = skillStore.GetBeatmapSkills(score.BeatmapHash, EzSkillSystems.BEATMAP_MSD);
                            var computed = EzChartSkillInfoComputer.Compute(
                                EzChartSkillInfoComputer.FromPlayable(playable),
                                msd,
                                rate);
                            patterns = computed.Patterns;
                        }
                        catch
                        {
                            // leave empty — play contributes no pattern axes
                        }
                    }

                    if (patterns.Length > 0)
                    {
                        if (!patternPlaysByKey.TryGetValue(keyCount, out var patternList))
                            patternPlaysByKey[keyCount] = patternList = new List<PatternPlay>();

                        patternList.Add(new PatternPlay(vector.Overall, patterns));

                        foreach (string patternId in patterns.Distinct(StringComparer.Ordinal))
                        {
                            if (string.IsNullOrWhiteSpace(patternId))
                                continue;

                            evidence.Add(new EzAxisPlayEvidenceRow
                            {
                                Username = username,
                                KeyCount = keyCount,
                                SkillId = EzPatternRatings.ToSkillId(patternId),
                                BeatmapHash = score.BeatmapHash,
                                AxisValue = vector.Overall,
                                Accuracy = score.Accuracy,
                                Rate = rate,
                                ScoredAt = score.Date,
                                AlgorithmVersion = EzManiaSkillAlgorithm.VERSION,
                            });
                        }
                    }

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

            foreach ((int keyCount, List<TimedPlay> timed) in byKey)
            {
                timed.Sort(static (a, b) => a.ScoredAt.CompareTo(b.ScoredAt));

                var plays = timed.Select(t => t.Vector).ToList();
                var aggregated = EzSsrAggregator.AggregateVectors(plays);
                bool provisional = plays.Count < EzPlayerSsrSnapshot.QUALIFYING_PLAYS;

                // Final rating write must not stamp UtcNow history points — career curve is rebuilt below.
                skillStore.WritePlayerSsr(username, keyCount, aggregated, plays.Count, provisional, appendHistory: false);
                skillStore.ReplacePlayerSkillHistory(username, keyCount, buildChronologicalHistorySamples(timed));

                IReadOnlyList<PatternPlay> patternPlays = patternPlaysByKey.TryGetValue(keyCount, out var pp)
                    ? pp
                    : Array.Empty<PatternPlay>();

                var patternRatings = EzPatternRatings.AggregateModePatternRatings(
                    patternPlays.Select(static p => (p.Overall, p.Patterns)));

                skillStore.WritePlayerPatternRatings(username, keyCount, patternRatings, provisional);
            }

            PendingEvidence = evidence;
        }

        /// <summary>
        /// Running AggregateSSRs over a rolling window of recent plays, timestamped with that play's
        /// <see cref="ScoreInfo.Date"/>. Evenly samples up to <see cref="HISTORY_MAX_POINTS"/> points
        /// across the career (always includes first and last). Unlike a career-prefix Aggregate,
        /// values can fall when strong early plays leave the window.
        /// </summary>
        public static IReadOnlyList<(DateTimeOffset RecordedAt, EzSkillsetVector Vector)> BuildChronologicalHistorySamples(
            IReadOnlyList<(DateTimeOffset ScoredAt, EzSkillsetVector Vector)> orderedPlays,
            int maxPoints = HISTORY_MAX_POINTS,
            int rollingPlays = HISTORY_ROLLING_PLAYS)
        {
            if (orderedPlays.Count == 0 || maxPoints < 1)
                return Array.Empty<(DateTimeOffset, EzSkillsetVector)>();

            if (rollingPlays < 1)
                rollingPlays = 1;

            var indices = SampleIndices(orderedPlays.Count, maxPoints);
            var samples = new List<(DateTimeOffset, EzSkillsetVector)>(indices.Count);

            foreach (int i in indices)
            {
                int start = Math.Max(0, i - rollingPlays + 1);
                int count = i - start + 1;
                var window = new List<EzSkillsetVector>(count);

                for (int j = start; j <= i; j++)
                    window.Add(orderedPlays[j].Vector);

                samples.Add((orderedPlays[i].ScoredAt, EzSsrAggregator.AggregateVectors(window)));
            }

            return samples;
        }

        private static IReadOnlyList<(DateTimeOffset RecordedAt, EzSkillsetVector Vector)> buildChronologicalHistorySamples(
            IReadOnlyList<TimedPlay> orderedPlays)
            => BuildChronologicalHistorySamples(
                orderedPlays.Select(t => (t.ScoredAt, t.Vector)).ToList());

        /// <summary>Evenly spaced indices in <c>[0, count)</c>, always including endpoints when count &gt; 1.</summary>
        internal static List<int> SampleIndices(int count, int maxPoints)
        {
            if (count <= 0)
                return new List<int>();

            if (count <= maxPoints)
            {
                var all = new List<int>(count);
                for (int i = 0; i < count; i++)
                    all.Add(i);
                return all;
            }

            var indices = new List<int>(maxPoints);

            for (int k = 0; k < maxPoints; k++)
            {
                int i = (int)Math.Round(k * (count - 1) / (double)(maxPoints - 1));
                if (indices.Count == 0 || indices[^1] != i)
                    indices.Add(i);
            }

            return indices;
        }

        /// <summary>Accuracy-only clamp path (tests / callers without full score).</summary>
        public static float AccuracyToGoal(double accuracy) => EzSsrGoal.ForAccuracy(accuracy);
    }
}
