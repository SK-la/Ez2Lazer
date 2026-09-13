// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Mods;
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

        private const int cache_flush_batch = 64;

        private readonly BeatmapManager beatmapManager;
        private readonly EzSkillStore skillStore;
        private readonly EzLocalProfileStore? profileStore;

        public EzPlayerSsrAggregator(BeatmapManager beatmapManager, EzSkillStore skillStore, EzLocalProfileStore? profileStore = null)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
            this.profileStore = profileStore;
        }

        /// <summary>Per-play axis rows collected during the last <see cref="ComputeAndStore"/> (for DATA-3 evidence).</summary>
        public IReadOnlyList<EzAxisPlayEvidenceRow> PendingEvidence { get; private set; } = Array.Empty<EzAxisPlayEvidenceRow>();

        /// <summary>
        /// Chart hashes this pass needed a stored <see cref="EzChartSkillInfo"/> for but found none. The caller hands
        /// them to the chart-side chain instead of rating them here; see
        /// <see cref="EzLocalProfileService.ChartSideBackfillRequested"/>.
        /// </summary>
        public IReadOnlyCollection<string> MissingChartHashes { get; private set; } = Array.Empty<string>();

        private readonly record struct TimedPlay(DateTimeOffset ScoredAt, EzSkillsetVector Vector);

        private readonly record struct PatternPlay(double Overall, IReadOnlyList<string> Patterns);

        /// <summary>
        /// One load of the per-play cache for a whole pass, so a multi-player run does not re-read (and re-parse the
        /// vectors of) the table once per player. Pass the result to <see cref="ComputeAndStore"/>.
        /// </summary>
        public IReadOnlyDictionary<Guid, EzSsrPlayCacheRow> LoadPlayCache()
            => profileStore?.LoadSsrPlayCache() ?? new Dictionary<Guid, EzSsrPlayCacheRow>();

        public void ComputeAndStore(
            string username,
            IEnumerable<ScoreInfo> scores,
            CancellationToken cancellationToken = default,
            Action? afterEachScore = null,
            IReadOnlyDictionary<Guid, EzSsrPlayCacheRow>? loadedPlayCache = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            var byKey = new Dictionary<int, List<TimedPlay>>();
            var patternPlaysByKey = new Dictionary<int, List<PatternPlay>>();
            var evidence = new List<EzAxisPlayEvidenceRow>();
            var missingCharts = new HashSet<string>(StringComparer.Ordinal);
            var patternsByHash = new Dictionary<string, string[]>(StringComparer.Ordinal);

            // Per-play SSR cache: an already-rated play skips the MinaCalc engine (incremental backfill, not full recompute).
            // The caller can hand one in so a multi-player pass loads the table once instead of once per player.
            var cachedPlays = loadedPlayCache ?? profileStore?.LoadSsrPlayCache() ?? new Dictionary<Guid, EzSsrPlayCacheRow>();
            var pendingCacheWrites = new List<EzSsrPlayCacheRow>();

            using var calc = new EzNKeyMsdEngine();

            try
            {
                foreach (var score in scores)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (score.Ruleset.OnlineID != 3)
                            continue;

                        if (string.IsNullOrWhiteSpace(score.BeatmapHash))
                            continue;

                        bool hasCachedPlay = cachedPlays.TryGetValue(score.ID, out var cachedPlay)
                                             && string.Equals(cachedPlay.BeatmapHash, score.BeatmapHash, StringComparison.Ordinal);

                        // A cached negative marks a play the engine deliberately produced nothing for; never retry it.
                        if (hasCachedPlay && !cachedPlay.HasSsr)
                            continue;

                        float rate = EzModRate.Resolve(score.Mods);
                        int keyCount;
                        EzSkillsetVector vector;

                        if (hasCachedPlay)
                        {
                            // The cache row carries everything the fold needs — keymode, rate and vector — so no
                            // beatmap is built at all. Building one per already-rated play was the single largest
                            // allocation source of a re-run.
                            keyCount = cachedPlay.KeyCount;
                            vector = cachedPlay.Vector;
                        }
                        else
                        {
                            var beatmapInfo = score.BeatmapInfo ?? beatmapManager.QueryBeatmap(b => b.Hash == score.BeatmapHash);
                            if (beatmapInfo == null)
                                continue;

                            var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                            var playable = working.GetPlayableBeatmap(score.Ruleset, score.Mods);
                            keyCount = EzMinaNoteConverter.ResolveKeyCount(playable);

                            double holdRatio = EzChartDanEstimator.ComputeHoldRatio(playable);
                            double od = beatmapInfo.Difficulty.OverallDifficulty;

                            float? goal = EzSsrGoal.ForScore(score, holdRatio, od);

                            if (goal is not float goalValue)
                            {
                                cacheNegative(score, username, keyCount, rate, pendingCacheWrites);
                                continue;
                            }

                            var notes = EzMinaNoteConverter.Convert(playable);

                            if (!calc.SupportsKeyCount(keyCount) || notes.Length < EzNKeyMsdEngine.MIN_RATEABLE_ROWS)
                            {
                                cacheNegative(score, username, keyCount, rate, pendingCacheWrites);
                                continue;
                            }

                            try
                            {
                                vector = calc.CalculateSsr(notes, keyCount, rate, goalValue);
                            }
                            catch (EzMsdEngineException e)
                            {
                                // A chart the engine cannot rate (e.g. a chord wider than its column
                                // limit) simply contributes no SSR; the engine rebuilds itself for the next play.
                                Logger.Log($"[EzSkills] SSR compute failed for {score.BeatmapHash} (keys={keyCount}): {e.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                                cacheNegative(score, username, keyCount, rate, pendingCacheWrites);
                                continue;
                            }

                            if (vector.Overall <= 0)
                            {
                                cacheNegative(score, username, keyCount, rate, pendingCacheWrites);
                                continue;
                            }

                            pendingCacheWrites.Add(new EzSsrPlayCacheRow(
                                score.ID,
                                username,
                                score.BeatmapHash,
                                keyCount,
                                rate,
                                score.Date,
                                true,
                                vector,
                                EzManiaSkillAlgorithm.VERSION));
                        }

                        string[] patterns = resolvePatterns(score.BeatmapHash, patternsByHash, missingCharts);

                        addScoredPlay(username, score, keyCount, rate, vector, patterns, byKey, patternPlaysByKey, evidence);
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

            foreach ((int keyCount, List<TimedPlay> timed) in byKey)
            {
                timed.Sort(static (a, b) => a.ScoredAt.CompareTo(b.ScoredAt));

                var plays = timed.Select(t => t.Vector).ToList();
                var aggregated = EzSsrAggregator.AggregateVectors(plays);
                bool provisional = plays.Count < EzPlayerSsrSnapshot.QUALIFYING_PLAYS;

                IReadOnlyList<PatternPlay> patternPlays = patternPlaysByKey.TryGetValue(keyCount, out var pp)
                    ? pp
                    : Array.Empty<PatternPlay>();

                var patternRatings = EzPatternRatings.AggregateModePatternRatings(
                    patternPlays.Select(static p => (p.Overall, p.Patterns)));

                // One transaction for the keymode: the SSR values, the replaced history curve and the pattern
                // ratings. Issued separately they were three transactions per keymode per player.
                skillStore.WritePlayerSkillBundle(
                    username,
                    keyCount,
                    aggregated,
                    plays.Count,
                    provisional,
                    buildChronologicalHistorySamples(timed),
                    patternRatings);
            }

            PendingEvidence = evidence;
            MissingChartHashes = missingCharts;
        }

        /// <summary>
        /// Pattern tags come from the chart-side CSI row only: this pass never computes or writes chart-side data.
        /// A chart the chain has not rated yet contributes no pattern axes and is reported for a chain backfill;
        /// a chart the chain settled as unrateable contributes nothing and is not reported (a retry changes nothing).
        /// </summary>
        private string[] resolvePatterns(string beatmapHash, Dictionary<string, string[]> memo, HashSet<string> missing)
        {
            if (memo.TryGetValue(beatmapHash, out var cached))
                return cached;

            string[] patterns = Array.Empty<string>();

            if (skillStore.TryGetChartSkillInfo(beatmapHash, out var chart))
            {
                if (chart is { IsUnavailable: false })
                    patterns = chart.Patterns;
            }
            else if (isChartChainRateable(beatmapHash))
            {
                missing.Add(beatmapHash);
            }

            memo[beatmapHash] = patterns;
            return patterns;
        }

        /// <summary>
        /// Whether the chain will ever produce a CSI row for this chart. A chart whose CS-derived keymode the engine
        /// cannot rate is dropped by the chain before MSD, so its CSI stage never runs for it - a play on it is
        /// settled, and reporting the chart would re-queue the chain on every pass (see
        /// <see cref="EzChartChainCoverage"/>).
        /// </summary>
        private bool isChartChainRateable(string beatmapHash)
        {
            var beatmapInfo = beatmapManager.QueryBeatmap(b => b.Hash == beatmapHash);

            // No beatmap left to rate either.
            return beatmapInfo != null && EzChartChainCoverage.IsRateableChart(beatmapInfo);
        }

        private static void cacheNegative(ScoreInfo score, string username, int keyCount, float rate, List<EzSsrPlayCacheRow> pending)
        {
            pending.Add(new EzSsrPlayCacheRow(
                score.ID,
                username,
                score.BeatmapHash,
                keyCount,
                rate,
                score.Date,
                false,
                default,
                EzManiaSkillAlgorithm.VERSION));
        }

        private void flushCache(List<EzSsrPlayCacheRow> pending)
        {
            if (profileStore == null || pending.Count == 0)
                return;

            profileStore.UpsertSsrPlayCache(pending);
            pending.Clear();
        }

        private void addScoredPlay(
            string username,
            ScoreInfo score,
            int keyCount,
            float rate,
            EzSkillsetVector vector,
            string[] patterns,
            Dictionary<int, List<TimedPlay>> byKey,
            Dictionary<int, List<PatternPlay>> patternPlaysByKey,
            List<EzAxisPlayEvidenceRow> evidence)
        {
            if (!byKey.TryGetValue(keyCount, out var list))
                byKey[keyCount] = list = new List<TimedPlay>();

            list.Add(new TimedPlay(score.Date, vector));

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
