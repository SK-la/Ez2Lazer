// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using Realms;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Single Realm persistence facade for mania skill metrics (MSD / SSR / Dan / ChartSkillInfo / ChartDan / skillset cache).
    /// Does not open the game for the caller. Evidence lists stay in local-profile SQLite.
    /// </summary>
    public sealed class EzSkillStore
    {
        private readonly RealmAccess realmAccess;

        public EzSkillStore(RealmAccess realmAccess)
        {
            this.realmAccess = realmAccess;
        }

        public IReadOnlyDictionary<string, double> GetBeatmapSkills(string beatmapHash, string systemId, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                var rows = r.All<EzBeatmapSkillValue>()
                            .Where(v => v.BeatmapHash == beatmapHash
                                        && v.SystemId == systemId
                                        && v.AlgorithmVersion == version);

                return rows.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);
            });
        }

        /// <summary>
        /// Batch-read beatmap MSD (or other system) rows for many hashes in one Realm run.
        /// Missing hashes are omitted from the result.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> GetBeatmapSkillsForHashes(
            IEnumerable<string> beatmapHashes,
            string systemId,
            int? algorithmVersion = null)
        {
            ArgumentNullException.ThrowIfNull(beatmapHashes);

            var hashSet = beatmapHashes
                          .Where(static h => !string.IsNullOrEmpty(h))
                          .ToHashSet(StringComparer.Ordinal);

            if (hashSet.Count == 0)
                return new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.Ordinal);

            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                var result = new Dictionary<string, IReadOnlyDictionary<string, double>>(hashSet.Count, StringComparer.Ordinal);

                var rows = r.All<EzBeatmapSkillValue>()
                            .Where(v => v.SystemId == systemId && v.AlgorithmVersion == version)
                            .AsEnumerable()
                            .Where(v => hashSet.Contains(v.BeatmapHash));

                foreach (var group in rows.GroupBy(v => v.BeatmapHash, StringComparer.Ordinal))
                    result[group.Key] = group.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);

                return result;
            });
        }

        public bool TryGetBeatmapSkill(string beatmapHash, string skillId, out double value, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;
            double found = double.NaN;

            realmAccess.Run(r =>
            {
                var row = r.All<EzBeatmapSkillValue>()
                           .FirstOrDefault(v => v.BeatmapHash == beatmapHash
                                                && v.SkillId == skillId
                                                && v.AlgorithmVersion == version);
                if (row != null)
                    found = row.Value;
            });

            if (double.IsNaN(found))
            {
                value = 0;
                return false;
            }

            value = found;
            return true;
        }

        public void WriteBeatmapMsd(string beatmapHash, EzSkillsetVector vector, Guid beatmapId = default, DateTimeOffset? computedAt = null, double? holdRatio = null)
        {
            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;
            int version = EzManiaSkillAlgorithm.VERSION;

            realmAccess.Write(r =>
            {
                var existing = r.All<EzBeatmapSkillValue>()
                                .Where(v => v.BeatmapHash == beatmapHash && v.SystemId == EzSkillSystems.BEATMAP_MSD)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                foreach (var (axis, value) in vector.Enumerate())
                {
                    r.Add(new EzBeatmapSkillValue
                    {
                        BeatmapHash = beatmapHash,
                        BeatmapId = beatmapId,
                        SystemId = EzSkillSystems.BEATMAP_MSD,
                        SkillId = axis.ToMsdSkillId(),
                        Value = value,
                        AlgorithmVersion = version,
                        ComputedAt = at,
                    });
                }

                if (holdRatio is double ratio && double.IsFinite(ratio))
                {
                    r.Add(new EzBeatmapSkillValue
                    {
                        BeatmapHash = beatmapHash,
                        BeatmapId = beatmapId,
                        SystemId = EzSkillSystems.BEATMAP_MSD,
                        SkillId = EzSkillSystems.MsdHoldRatioSkillId,
                        Value = Math.Clamp(ratio, 0, 1),
                        AlgorithmVersion = version,
                        ComputedAt = at,
                    });
                }
            });
        }

        /// <summary>
        /// Persist a settled-miss marker so BDSP does not reprocess the same zero-vector chart every launch.
        /// Replaces any existing <see cref="EzSkillSystems.BEATMAP_MSD"/> rows for this hash.
        /// </summary>
        public void WriteBeatmapMsdUnrateable(string beatmapHash, Guid beatmapId = default, DateTimeOffset? computedAt = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(beatmapHash);

            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;
            int version = EzManiaSkillAlgorithm.VERSION;

            realmAccess.Write(r =>
            {
                var existing = r.All<EzBeatmapSkillValue>()
                                .Where(v => v.BeatmapHash == beatmapHash && v.SystemId == EzSkillSystems.BEATMAP_MSD)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                r.Add(new EzBeatmapSkillValue
                {
                    BeatmapHash = beatmapHash,
                    BeatmapId = beatmapId,
                    SystemId = EzSkillSystems.BEATMAP_MSD,
                    SkillId = EzSkillSystems.MsdUnrateableSkillId,
                    Value = 1,
                    AlgorithmVersion = version,
                    ComputedAt = at,
                });
            });
        }

        /// <summary>
        /// Deletes persisted beatmap MSD rows. When <paramref name="hashes"/> is null, clears all MSD rows.
        /// </summary>
        public void ClearBeatmapMsd(IEnumerable<string>? hashes = null)
        {
            if (hashes == null)
            {
                realmAccess.Write(r =>
                {
                    var rows = r.All<EzBeatmapSkillValue>()
                                .Where(v => v.SystemId == EzSkillSystems.BEATMAP_MSD)
                                .ToList();

                    foreach (var row in rows)
                        r.Remove(row);
                });
                return;
            }

            var hashList = hashes as IList<string> ?? hashes.ToList();
            if (hashList.Count == 0)
                return;

            const int batch_size = 200;

            for (int i = 0; i < hashList.Count; i += batch_size)
            {
                var batch = hashList.Skip(i).Take(batch_size).ToHashSet(StringComparer.Ordinal);

                realmAccess.Write(r =>
                {
                    var rows = r.All<EzBeatmapSkillValue>()
                                .Where(v => v.SystemId == EzSkillSystems.BEATMAP_MSD)
                                .AsEnumerable()
                                .Where(v => batch.Contains(v.BeatmapHash))
                                .ToList();

                    foreach (var row in rows)
                        r.Remove(row);
                });
            }
        }

        /// <summary>
        /// Beatmap hashes that already have a complete current-version MSD cache (all axes + hold ratio).
        /// </summary>
        public HashSet<string> GetCompleteBeatmapMsdHashes(int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                var byHash = r.All<EzBeatmapSkillValue>()
                              .Where(v => v.SystemId == EzSkillSystems.BEATMAP_MSD
                                          && v.AlgorithmVersion == version)
                              .AsEnumerable()
                              .GroupBy(v => v.BeatmapHash, StringComparer.Ordinal);

                var complete = new HashSet<string>(StringComparer.Ordinal);

                foreach (var group in byHash)
                {
                    var skills = group.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);
                    if (EzBeatmapMsdComputer.IsCurrentMsdCache(skills))
                        complete.Add(group.Key);
                }

                return complete;
            });
        }

        /// <summary>
        /// Hashes that BDSP should not reprocess: complete MSD cache or settled <c>__unrateable</c> miss.
        /// </summary>
        public HashSet<string> GetSettledBeatmapMsdHashes(int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                var byHash = r.All<EzBeatmapSkillValue>()
                              .Where(v => v.SystemId == EzSkillSystems.BEATMAP_MSD
                                          && v.AlgorithmVersion == version)
                              .AsEnumerable()
                              .GroupBy(v => v.BeatmapHash, StringComparer.Ordinal);

                var settled = new HashSet<string>(StringComparer.Ordinal);

                foreach (var group in byHash)
                {
                    var skills = group.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);
                    if (EzBeatmapMsdComputer.IsSettledMsdCache(skills))
                        settled.Add(group.Key);
                }

                return settled;
            });
        }

        /// <summary>
        /// Charts whose MSD settled as unrateable at the current revision. The chain derives no ChartDan from
        /// them (see <c>BackgroundDataStoreProcessor.populateMissingChartDan</c>), so a play waiting on a ChartDan
        /// row for one of these is settled rather than missing - reporting it would re-queue the chain forever.
        /// </summary>
        /// <remarks>
        /// Scans the MSD table, so callers should treat the result as a per-pass value and only ask for it when a
        /// ChartDan row is otherwise missing (the steady state has none).
        /// </remarks>
        public HashSet<string> GetUnrateableChartDanHashes()
        {
            int version = EzManiaSkillAlgorithm.VERSION;

            return realmAccess.Run(r => r.All<EzBeatmapSkillValue>()
                                         .Where(v => v.SystemId == EzSkillSystems.BEATMAP_MSD
                                                     && v.AlgorithmVersion == version
                                                     && v.SkillId == EzSkillSystems.MsdUnrateableSkillId)
                                         .AsEnumerable()
                                         .Select(v => v.BeatmapHash)
                                         .ToHashSet(StringComparer.Ordinal));
        }

        public IReadOnlyDictionary<string, double> GetPlayerSkills(string username, int keyCount, string systemId, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                var rows = r.All<EzPlayerSkillValue>()
                            .Where(v => v.Username == username
                                        && v.KeyCount == keyCount
                                        && v.SystemId == systemId
                                        && v.AlgorithmVersion == version);

                return rows.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);
            });
        }

        public EzPlayerSsrSnapshot GetPlayerSsrSnapshot(string username, int keyCount, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                var rows = r.All<EzPlayerSkillValue>()
                            .Where(v => v.Username == username
                                        && v.KeyCount == keyCount
                                        && v.SystemId == EzSkillSystems.PLAYER_SSR
                                        && v.AlgorithmVersion == version)
                            .ToList();

                if (rows.Count == 0)
                    return new EzPlayerSsrSnapshot();

                var values = rows.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);
                var meta = rows.FirstOrDefault(v => v.SkillId == EzMinaSkillAxis.Overall.ToSsrSkillId()) ?? rows[0];

                return new EzPlayerSsrSnapshot
                {
                    Values = values,
                    AnalyzedPlays = meta.AnalyzedPlays,
                    Provisional = meta.Provisional,
                    Stale = meta.Stale,
                    ComputedAt = meta.ComputedAt,
                };
            });
        }

        public IReadOnlyList<int> GetPlayerSsrKeyCounts(string username, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                return r.All<EzPlayerSkillValue>()
                        .Where(v => v.Username == username
                                    && v.SystemId == EzSkillSystems.PLAYER_SSR
                                    && v.AlgorithmVersion == version)
                        .AsEnumerable()
                        .Select(v => v.KeyCount)
                        .Distinct()
                        .OrderBy(k => k)
                        .ToList();
            });
        }

        public void WritePlayerSsr(
            string username,
            int keyCount,
            EzSkillsetVector vector,
            int analyzedPlays,
            bool provisional = false,
            bool stale = false,
            DateTimeOffset? computedAt = null,
            bool appendHistory = true)
        {
            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;
            int version = EzManiaSkillAlgorithm.VERSION;

            realmAccess.Write(r => addPlayerSsr(r, username, keyCount, vector, analyzedPlays, provisional, stale, at, version, appendHistory));
        }

        /// <summary>
        /// Write one player×keymode's SSR values, history samples and pattern ratings in a single transaction.
        /// </summary>
        /// <remarks>
        /// The individual writers each open their own Realm transaction, and a per-player skill refresh touches
        /// several keymodes; grouping them here keeps the write traffic proportional to the player, not to
        /// (keymodes × tables).
        /// </remarks>
        public void WritePlayerSkillBundle(
            string username,
            int keyCount,
            EzSkillsetVector ssr,
            int analyzedPlays,
            bool provisional,
            IReadOnlyList<(DateTimeOffset RecordedAt, EzSkillsetVector Vector)> historySamples,
            IReadOnlyList<EzPatternRating> patternRatings)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentNullException.ThrowIfNull(historySamples);
            ArgumentNullException.ThrowIfNull(patternRatings);

            DateTimeOffset at = DateTimeOffset.UtcNow;
            int version = EzManiaSkillAlgorithm.VERSION;

            realmAccess.Write(r =>
            {
                // SSR values without the per-axis history points: the curve is replaced below from the sampled
                // chronological series, so the UtcNow stamping an append would do is exactly what we must not keep.
                addPlayerSsr(r, username, keyCount, ssr, analyzedPlays, provisional, stale: false, at, version, appendHistory: false);
                replacePlayerSkillHistory(r, username, keyCount, historySamples, version);
                writePlayerPatternRatings(r, username, keyCount, patternRatings, provisional, stale: false, at, version);
            });
        }

        private static void addPlayerSsr(
            Realm r,
            string username,
            int keyCount,
            EzSkillsetVector vector,
            int analyzedPlays,
            bool provisional,
            bool stale,
            DateTimeOffset at,
            int version,
            bool appendHistory)
        {
            var existing = r.All<EzPlayerSkillValue>()
                            .Where(v => v.Username == username
                                        && v.KeyCount == keyCount
                                        && v.SystemId == EzSkillSystems.PLAYER_SSR)
                            .ToList();

            foreach (var row in existing)
                r.Remove(row);

            foreach (var (axis, value) in vector.Enumerate())
            {
                string skillId = axis.ToSsrSkillId();

                r.Add(new EzPlayerSkillValue
                {
                    Username = username,
                    KeyCount = keyCount,
                    SystemId = EzSkillSystems.PLAYER_SSR,
                    SkillId = skillId,
                    Value = value,
                    AnalyzedPlays = analyzedPlays,
                    Provisional = provisional,
                    Stale = stale,
                    AlgorithmVersion = version,
                    ComputedAt = at,
                });

                if (appendHistory && value > 0)
                {
                    r.Add(new EzPlayerSkillHistoryPoint
                    {
                        Username = username,
                        KeyCount = keyCount,
                        SkillId = skillId,
                        Value = value,
                        RecordedAt = at,
                        AlgorithmVersion = version,
                    });
                }
            }
        }

        public IReadOnlyList<EzPatternRating> GetPlayerPatternRatings(string username, int keyCount, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                var rows = r.All<EzPlayerSkillValue>()
                            .Where(v => v.Username == username
                                        && v.KeyCount == keyCount
                                        && v.SystemId == EzSkillSystems.PLAYER_PATTERN
                                        && v.AlgorithmVersion == version)
                            .ToList();

                var list = new List<EzPatternRating>(rows.Count);

                foreach (var row in rows)
                {
                    if (!EzPatternRatings.TryParseSkillId(row.SkillId, out string patternId))
                        continue;

                    list.Add(new EzPatternRating(patternId, row.Value, row.AnalyzedPlays));
                }

                return list.OrderByDescending(static p => p.Rating).ToList();
            });
        }

        /// <summary>
        /// Replace pattern ratings for one username×keymode (hub mode.patterns).
        /// Uses existing <see cref="EzPlayerSkillValue"/> — DATA-Skills-PatternRatings.
        /// </summary>
        public void WritePlayerPatternRatings(
            string username,
            int keyCount,
            IReadOnlyList<EzPatternRating> ratings,
            bool provisional = false,
            bool stale = false,
            DateTimeOffset? computedAt = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentNullException.ThrowIfNull(ratings);

            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;
            int version = EzManiaSkillAlgorithm.VERSION;

            realmAccess.Write(r => writePlayerPatternRatings(r, username, keyCount, ratings, provisional, stale, at, version));
        }

        private static void writePlayerPatternRatings(
            Realm r,
            string username,
            int keyCount,
            IReadOnlyList<EzPatternRating> ratings,
            bool provisional,
            bool stale,
            DateTimeOffset at,
            int version)
        {
            var existing = r.All<EzPlayerSkillValue>()
                            .Where(v => v.Username == username
                                        && v.KeyCount == keyCount
                                        && v.SystemId == EzSkillSystems.PLAYER_PATTERN)
                            .ToList();

            foreach (var row in existing)
                r.Remove(row);

            foreach (var rating in ratings)
            {
                if (string.IsNullOrWhiteSpace(rating.Id) || !(rating.Rating > 0))
                    continue;

                r.Add(new EzPlayerSkillValue
                {
                    Username = username,
                    KeyCount = keyCount,
                    SystemId = EzSkillSystems.PLAYER_PATTERN,
                    SkillId = EzPatternRatings.ToSkillId(rating.Id),
                    Value = rating.Rating,
                    AnalyzedPlays = rating.Plays,
                    Provisional = provisional,
                    Stale = stale,
                    AlgorithmVersion = version,
                    ComputedAt = at,
                });
            }
        }

        /// <summary>
        /// Replace all history points for <paramref name="username"/> + <paramref name="keyCount"/>
        /// with chronologically sampled skill snapshots (typically keyed by score play time).
        /// </summary>
        public void ReplacePlayerSkillHistory(
            string username,
            int keyCount,
            IReadOnlyList<(DateTimeOffset RecordedAt, EzSkillsetVector Vector)> samples)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentNullException.ThrowIfNull(samples);

            int version = EzManiaSkillAlgorithm.VERSION;

            realmAccess.Write(r => replacePlayerSkillHistory(r, username, keyCount, samples, version));
        }

        private static void replacePlayerSkillHistory(
            Realm r,
            string username,
            int keyCount,
            IReadOnlyList<(DateTimeOffset RecordedAt, EzSkillsetVector Vector)> samples,
            int version)
        {
            var existing = r.All<EzPlayerSkillHistoryPoint>()
                            .Where(v => v.Username == username && v.KeyCount == keyCount)
                            .ToList();

            foreach (var row in existing)
                r.Remove(row);

            foreach (var (recordedAt, vector) in samples)
            {
                foreach (var (axis, value) in vector.Enumerate())
                {
                    if (value <= 0 || !double.IsFinite(value))
                        continue;

                    r.Add(new EzPlayerSkillHistoryPoint
                    {
                        Username = username,
                        KeyCount = keyCount,
                        SkillId = axis.ToSsrSkillId(),
                        Value = value,
                        RecordedAt = recordedAt,
                        AlgorithmVersion = version,
                    });
                }
            }
        }

        public IReadOnlyList<EzPlayerSkillHistoryPoint> GetPlayerSkillHistory(
            string username,
            int keyCount,
            string skillId,
            int maxPoints = 64,
            int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzManiaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                return r.All<EzPlayerSkillHistoryPoint>()
                        .Where(v => v.Username == username
                                    && v.KeyCount == keyCount
                                    && v.SkillId == skillId
                                    && v.AlgorithmVersion == version)
                        .ToList()
                        .OrderByDescending(v => v.RecordedAt)
                        .Take(maxPoints)
                        .Select(v => v.Detach())
                        .Reverse()
                        .ToList();
            });
        }

        public EzDanEstimate? GetDanEstimate(string username, int keyCount, string side, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzDanAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                var row = r.All<EzDanEstimate>()
                           .FirstOrDefault(v => v.Username == username
                                                && v.KeyCount == keyCount
                                                && v.Side == side
                                                && v.AlgorithmVersion == version);
                return row?.Detach();
            });
        }

        public void WriteDanEstimate(EzDanEstimate estimate)
        {
            ArgumentNullException.ThrowIfNull(estimate);

            realmAccess.Write(r =>
            {
                var existing = r.All<EzDanEstimate>()
                                .Where(v => v.Username == estimate.Username
                                            && v.KeyCount == estimate.KeyCount
                                            && v.Side == estimate.Side)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                r.Add(new EzDanEstimate
                {
                    Username = estimate.Username,
                    KeyCount = estimate.KeyCount,
                    Side = estimate.Side,
                    RawDan = estimate.RawDan,
                    Label = estimate.Label,
                    Clears = estimate.Clears,
                    BeyondTable = estimate.BeyondTable,
                    CourseName = estimate.CourseName,
                    CourseAccuracy = estimate.CourseAccuracy,
                    ClearWindowHave = estimate.ClearWindowHave,
                    ClearWindowNeed = estimate.ClearWindowNeed,
                    AlgorithmVersion = estimate.AlgorithmVersion != 0 ? estimate.AlgorithmVersion : EzDanAlgorithm.VERSION,
                    ComputedAt = estimate.ComputedAt == default ? DateTimeOffset.UtcNow : estimate.ComputedAt,
                });
            });
        }

        /// <summary>
        /// Typed Realm ChartSkillInfo (EZ≥9). Miss → callers recompute; no SQLite JSON fallback.
        /// Filtered on <see cref="EzAnalysisRevision.ChartSkillInfo"/>, so a bump of the CSI version
        /// or of an upstream facet (MSD) reads as a miss.
        /// </summary>
        public bool TryGetChartSkillInfo(string beatmapHash, out EzChartSkillInfo? info)
        {
            info = null;

            if (string.IsNullOrEmpty(beatmapHash))
                return false;

            int revision = EzAnalysisRevision.ChartSkillInfo;
            EzChartSkillInfo? fromRealm = null;

            realmAccess.Run(r =>
            {
                var row = r.All<EzBeatmapChartSkillInfo>()
                           .FirstOrDefault(v => v.BeatmapHash == beatmapHash
                                                && v.InfoVersion == revision);
                if (row != null)
                    fromRealm = chartSkillInfoToDto(row);
            });

            if (fromRealm == null)
                return false;

            info = fromRealm;
            return true;
        }

        public void UpsertChartSkillInfo(string beatmapHash, EzChartSkillInfo info, Guid beatmapId = default, DateTimeOffset? computedAt = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(beatmapHash);
            ArgumentNullException.ThrowIfNull(info);

            // Never persist Unavailable stubs — they fake “already processed” and block BDSP retries.
            if (info.IsUnavailable)
                return;

            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;

            realmAccess.Write(r =>
            {
                var existing = r.All<EzBeatmapChartSkillInfo>()
                                .Where(v => v.BeatmapHash == beatmapHash)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                r.Add(new EzBeatmapChartSkillInfo
                {
                    BeatmapHash = beatmapHash,
                    BeatmapId = beatmapId,
                    InfoVersion = EzAnalysisRevision.ChartSkillInfo,
                    ComputedAt = at,
                    PatternTagsJoined = joinPatterns(info.Patterns),
                    JackDemand = info.JackDemand,
                    JackShare = info.JackShare ?? -1,
                    StreamShare = info.StreamShare ?? -1,
                    TechCategory = info.TechCategory,
                    ClusterTrill = info.ClusterTrill,
                    HandstreamCluster = info.HandstreamCluster,
                    HandstreamEndurance = info.HandstreamEndurance,
                    TechScore = info.TechScore,
                    ChordjackScore = info.ChordjackScore,
                    JackScore = info.JackScore ?? -1,
                    MotionRhythmBreak = info.Motion?.RhythmBreak ?? -1,
                    MotionCrossHandTrill = info.Motion?.CrossHandTrill ?? -1,
                    MotionMiniJack = info.Motion?.MiniJack ?? -1,
                    MotionSameHand = info.Motion?.SameHand ?? -1,
                    LnRatio = info.LnRatio ?? -1,
                    Vibro = info.Vibro,
                    DanEligible = info.DanEligible,
                    LengthSeconds = info.LengthSeconds ?? -1,
                    KeyCount = info.KeyCount ?? -1,
                });
            });
        }

        /// <summary>
        /// Hashes with a current-revision ChartSkillInfo row that is not an Unavailable stub.
        /// </summary>
        public HashSet<string> GetPersistedChartSkillInfoHashes()
        {
            int revision = EzAnalysisRevision.ChartSkillInfo;

            return realmAccess.Run(r =>
            {
                return r.All<EzBeatmapChartSkillInfo>()
                        .Where(v => v.InfoVersion == revision && v.KeyCount >= 0)
                        .AsEnumerable()
                        .Select(v => v.BeatmapHash)
                        .Where(static h => !string.IsNullOrEmpty(h))
                        .ToHashSet(StringComparer.Ordinal);
            });
        }

        /// <summary>
        /// Hashes that BDSP should not reprocess for CSI: a complete row or a settled
        /// <c>KeyCount &lt; 0</c> stub (deterministically unrateable, e.g. unsupported keymode /
        /// no loadable playable). Mirrors <see cref="GetSettledBeatmapMsdHashes"/>.
        /// </summary>
        public HashSet<string> GetSettledChartSkillInfoHashes()
        {
            int revision = EzAnalysisRevision.ChartSkillInfo;

            return realmAccess.Run(r =>
            {
                return r.All<EzBeatmapChartSkillInfo>()
                        .Where(v => v.InfoVersion == revision)
                        .AsEnumerable()
                        .Select(v => v.BeatmapHash)
                        .Where(static h => !string.IsNullOrEmpty(h))
                        .ToHashSet(StringComparer.Ordinal);
            });
        }

        /// <summary>
        /// Records a deterministic CSI miss (<c>KeyCount = -1</c>) so BDSP stops retrying a chart
        /// that cannot be analysed (unsupported keymode, no loadable playable, empty object list).
        /// Not a valid CSI cache: <see cref="TryGetChartSkillInfo"/> returns it and callers must treat
        /// <see cref="EzChartSkillInfo.IsUnavailable"/> as a miss. Transient failures (exceptions) must
        /// NOT call this — those stay unstamped so the next run retries.
        /// </summary>
        public void WriteChartSkillInfoUnavailable(string beatmapHash, Guid beatmapId = default, DateTimeOffset? computedAt = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(beatmapHash);

            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;
            int revision = EzAnalysisRevision.ChartSkillInfo;

            realmAccess.Write(r =>
            {
                var existing = r.All<EzBeatmapChartSkillInfo>()
                                .Where(v => v.BeatmapHash == beatmapHash)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                r.Add(new EzBeatmapChartSkillInfo
                {
                    BeatmapHash = beatmapHash,
                    BeatmapId = beatmapId,
                    InfoVersion = revision,
                    ComputedAt = at,
                    DanEligible = false,
                    KeyCount = -1,
                });
            });
        }

        public void ClearChartSkillInfo()
        {
            realmAccess.Write(r =>
            {
                var rows = r.All<EzBeatmapChartSkillInfo>().ToList();

                foreach (var row in rows)
                    r.Remove(row);
            });
        }

        /// <summary>
        /// Typed Realm ChartDan (EZ≥10). Filtered on <see cref="EzAnalysisRevision.ChartDan"/>, so a bump
        /// of the dan algorithm, CSI, or MSD reads as a miss and BDSP recomputes it.
        /// </summary>
        public bool TryGetChartDan(string beatmapHash, out EzPersistedChartDan? chartDan)
        {
            chartDan = null;

            if (string.IsNullOrEmpty(beatmapHash))
                return false;

            int revision = EzAnalysisRevision.ChartDan;
            EzPersistedChartDan? fromRealm = null;

            realmAccess.Run(r =>
            {
                var row = r.All<EzBeatmapChartDan>()
                           .FirstOrDefault(v => v.BeatmapHash == beatmapHash
                                                && v.AlgorithmVersion == revision);
                if (row != null)
                    fromRealm = chartDanToDto(row);
            });

            if (fromRealm == null)
                return false;

            chartDan = fromRealm;
            return true;
        }

        public void UpsertChartDan(EzPersistedChartDan chartDan)
        {
            ArgumentNullException.ThrowIfNull(chartDan);
            ArgumentException.ThrowIfNullOrWhiteSpace(chartDan.BeatmapHash);

            DateTimeOffset at = chartDan.ComputedAt == default ? DateTimeOffset.UtcNow : chartDan.ComputedAt;

            // Stamped with the facet revision (not chartDan.AlgorithmVersion, which stays the pure
            // dan algorithm version on the DTO) so a later MSD/CSI bump retires this row.
            int version = EzAnalysisRevision.ChartDan;

            realmAccess.Write(r =>
            {
                var existing = r.All<EzBeatmapChartDan>()
                                .Where(v => v.BeatmapHash == chartDan.BeatmapHash)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                r.Add(new EzBeatmapChartDan
                {
                    BeatmapHash = chartDan.BeatmapHash,
                    BeatmapId = chartDan.BeatmapId,
                    AlgorithmVersion = version,
                    KeyCount = chartDan.KeyCount,
                    HoldRatio = chartDan.HoldRatio,
                    OverallMsd = chartDan.OverallMsd,
                    RcRawDan = chartDan.RcRawDan,
                    RcLabel = chartDan.RcLabel,
                    LnRawDan = chartDan.LnRawDan,
                    LnLabel = chartDan.LnLabel,
                    RcSkillsetLabelsJoined = EzPersistedChartDan.JoinSkillsetLabels(chartDan.RcSkillsetLabels),
                    LnSkillsetLabelsJoined = EzPersistedChartDan.JoinSkillsetLabels(chartDan.LnSkillsetLabels),
                    ComputedAt = at,
                });
            });
        }

        /// <summary>Hashes with a current-revision ChartDan row (current dan algorithm + CSI + MSD).</summary>
        public HashSet<string> GetPersistedChartDanHashes()
        {
            int revision = EzAnalysisRevision.ChartDan;

            return realmAccess.Run(r =>
            {
                return r.All<EzBeatmapChartDan>()
                        .Where(v => v.AlgorithmVersion == revision)
                        .AsEnumerable()
                        .Select(v => v.BeatmapHash)
                        .Where(static h => !string.IsNullOrEmpty(h))
                        .ToHashSet(StringComparer.Ordinal);
            });
        }

        public void ClearChartDan()
        {
            realmAccess.Write(r =>
            {
                var rows = r.All<EzBeatmapChartDan>().ToList();

                foreach (var row in rows)
                    r.Remove(row);
            });
        }

        /// <summary>
        /// Mania chart hashes the chart-side chain will rate at all. MSD is gated on the CS-derived column count and
        /// both later stages wait on MSD, so a chart outside the engine's keymode range is skipped by the whole chain
        /// and can never be something a play is "waiting" on.
        /// </summary>
        public HashSet<string> GetRateableChartHashes() => realmAccess.Run(collectRateableChartHashes);

        private static HashSet<string> collectRateableChartHashes(Realm r)
        {
            var hashes = new HashSet<string>(StringComparer.Ordinal);

            foreach (var b in r.All<BeatmapInfo>())
            {
                if (b.BeatmapSet == null || b.Ruleset.OnlineID != 3 || string.IsNullOrEmpty(b.Hash))
                    continue;

                // The chain drops keymodes its engine cannot rate at candidate time, so those charts are not
                // pending work and must not be counted as missing - otherwise "all current" is unreachable.
                if (!EzChartChainCoverage.IsRateableKeyCount((int)Math.Round(b.Difficulty.CircleSize)))
                    continue;

                hashes.Add(b.Hash);
            }

            return hashes;
        }

        /// <summary>
        /// Snapshot of the chart skill chain's completeness, counted over every mania chart in Realm.
        /// <para>
        /// Reads all three facet tables in one run, so call it off the UI thread; it is meant for an
        /// explicit status refresh, not a hot path. Results come straight from the persisted
        /// <see cref="EzAnalysisRevision"/> stamps, which is also exactly what the backfill filters on,
        /// so "Pending &gt; 0" and "the next backfill has work" cannot disagree.
        /// </para>
        /// </summary>
        public EzSkillDataStatus GetSkillDataStatus(DateTimeOffset? measuredAt = null)
        {
            int msdRevision = EzAnalysisRevision.Msd;
            int csiRevision = EzAnalysisRevision.ChartSkillInfo;
            int danRevision = EzAnalysisRevision.ChartDan;

            return realmAccess.Run(r =>
            {
                var chartHashes = collectRateableChartHashes(r);

                var msdByHash = r.All<EzBeatmapSkillValue>()
                                 .Where(v => v.SystemId == EzSkillSystems.BEATMAP_MSD)
                                 .AsEnumerable()
                                 .GroupBy(v => v.BeatmapHash, StringComparer.Ordinal);

                var csiByHash = r.All<EzBeatmapChartSkillInfo>()
                                 .AsEnumerable()
                                 .GroupBy(v => v.BeatmapHash, StringComparer.Ordinal);

                var danByHash = r.All<EzBeatmapChartDan>()
                                 .AsEnumerable()
                                 .GroupBy(v => v.BeatmapHash, StringComparer.Ordinal);

                // No MSD means no ChartDan, so a chart whose MSD settled as unrateable is settled for Dan too.
                // Counting it as missing would leave the Dan facet permanently pending, because the chain skips
                // those on purpose (see BackgroundDataStoreProcessor.populateMissingChartDan).
                var unrateableDanHashes = new HashSet<string>(StringComparer.Ordinal);

                foreach (var group in msdByHash)
                {
                    if (!group.Any(v => v.AlgorithmVersion == msdRevision))
                        continue;

                    var skills = group.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);

                    if (EzBeatmapMsdComputer.IsUnrateableMsd(skills))
                        unrateableDanHashes.Add(group.Key);
                }

                return new EzSkillDataStatus
                {
                    TotalCharts = chartHashes.Count,
                    Msd = EzSkillDataStatusCounting.Count(chartHashes, msdByHash, msdRevision, static v => v.AlgorithmVersion, EzSkillDataStatusCounting.IsMsdStub),
                    ChartSkillInfo = EzSkillDataStatusCounting.Count(chartHashes, csiByHash, csiRevision, static v => v.InfoVersion, EzSkillDataStatusCounting.IsChartSkillInfoStub),
                    // ChartDan has no settled-miss row form: a row exists only when a dan was resolved.
                    ChartDan = EzSkillDataStatusCounting.Count(chartHashes, danByHash, danRevision, static v => v.AlgorithmVersion, static _ => false, unrateableDanHashes),
                    MeasuredAt = measuredAt ?? DateTimeOffset.UtcNow,
                };
            });
        }

        /// <summary>
        /// Batch-read typed ChartSkillInfo rows for many hashes in one Realm run.
        /// Missing / wrong-version hashes are omitted.
        /// </summary>
        public IReadOnlyDictionary<string, EzChartSkillInfo> GetChartSkillInfoForHashes(IEnumerable<string> beatmapHashes)
        {
            ArgumentNullException.ThrowIfNull(beatmapHashes);

            var hashSet = beatmapHashes
                          .Where(static h => !string.IsNullOrEmpty(h))
                          .ToHashSet(StringComparer.Ordinal);

            if (hashSet.Count == 0)
                return new Dictionary<string, EzChartSkillInfo>(StringComparer.Ordinal);

            int revision = EzAnalysisRevision.ChartSkillInfo;

            return realmAccess.Run(r =>
            {
                var result = new Dictionary<string, EzChartSkillInfo>(hashSet.Count, StringComparer.Ordinal);

                var rows = r.All<EzBeatmapChartSkillInfo>()
                            .Where(v => v.InfoVersion == revision)
                            .AsEnumerable()
                            .Where(v => hashSet.Contains(v.BeatmapHash));

                foreach (var row in rows)
                {
                    var dto = chartSkillInfoToDto(row);
                    if (dto.IsUnavailable)
                        continue;

                    result[row.BeatmapHash] = dto;
                }

                return result;
            });
        }

        /// <summary>
        /// True when at least one non-sentinel skillset tile exists for this username/key/side/version.
        /// Legacy <see cref="EzDanSkillsetBuckets.CACHE_EMPTY_SENTINEL"/> rows are removed and treated as miss.
        /// <para>
        /// Hot path is read-only: this runs on every DualPanel refresh (twice per side), and a write transaction
        /// per call was measurable update-thread cost. Sentinel cleanup only happens when such rows exist.
        /// </para>
        /// </summary>
        public bool HasDanSkillsetCache(string username, int keyCount, string side, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzDanAlgorithm.VERSION;
            bool hasReal = false;
            List<Guid>? legacySentinelIds = null;

            realmAccess.Run(r =>
            {
                var rows = r.All<EzPlayerDanSkillsetValue>()
                            .Where(v => v.Username == username
                                        && v.KeyCount == keyCount
                                        && v.Side == side
                                        && v.AlgorithmVersion == version)
                            .ToList();

                hasReal = false;

                foreach (var row in rows)
                {
                    if (row.SkillsetId == EzDanSkillsetBuckets.CACHE_EMPTY_SENTINEL)
                        (legacySentinelIds ??= new List<Guid>()).Add(row.ID);
                    else
                        hasReal = true;
                }
            });

            if (legacySentinelIds != null)
                removeDanSkillsetSentinels(legacySentinelIds);

            return hasReal;
        }

        /// <summary>One-off cleanup of legacy empty-sentinel rows found on the read path.</summary>
        private void removeDanSkillsetSentinels(List<Guid> ids)
        {
            realmAccess.Write(r =>
            {
                foreach (Guid id in ids)
                {
                    var row = r.Find<EzPlayerDanSkillsetValue>(id);

                    if (row != null && row.SkillsetId == EzDanSkillsetBuckets.CACHE_EMPTY_SENTINEL)
                        r.Remove(row);
                }
            });
        }

        /// <summary>Cached skillset tiles (excludes legacy empty sentinel). Empty list means miss.</summary>
        public IReadOnlyList<EzPlayerDanSkillsetValue> GetDanSkillsetValues(
            string username,
            int keyCount,
            string side,
            int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzDanAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                return r.All<EzPlayerDanSkillsetValue>()
                        .Where(v => v.Username == username
                                    && v.KeyCount == keyCount
                                    && v.Side == side
                                    && v.AlgorithmVersion == version
                                    && v.SkillsetId != EzDanSkillsetBuckets.CACHE_EMPTY_SENTINEL)
                        .ToList()
                        .Select(v => v.Detach())
                        .ToList();
            });
        }

        /// <summary>
        /// Replace skillset cache for one username/key/side.
        /// Empty <paramref name="verdicts"/> only deletes existing rows (keeps miss — no empty sentinel).
        /// </summary>
        public void WriteDanSkillsetVerdicts(
            string username,
            int keyCount,
            string side,
            IReadOnlyDictionary<string, EzDanSkillsetVerdict> verdicts,
            DateTimeOffset? computedAt = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentNullException.ThrowIfNull(verdicts);

            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;
            int version = EzDanAlgorithm.VERSION;

            realmAccess.Write(r =>
            {
                var existing = r.All<EzPlayerDanSkillsetValue>()
                                .Where(v => v.Username == username
                                            && v.KeyCount == keyCount
                                            && v.Side == side)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                if (verdicts.Count == 0)
                    return;

                foreach (var (id, verdict) in verdicts)
                {
                    r.Add(new EzPlayerDanSkillsetValue
                    {
                        Username = username,
                        KeyCount = keyCount,
                        Side = side,
                        SkillsetId = string.IsNullOrEmpty(verdict.SkillsetId) ? id : verdict.SkillsetId,
                        RawDan = verdict.RawDan,
                        Label = verdict.Label,
                        Clears = verdict.Clears,
                        AlgorithmVersion = version,
                        ComputedAt = at,
                    });
                }
            });
        }

        /// <summary>Drop all skillset cache rows for a username (all keys/sides).</summary>
        public void ClearDanSkillsetValues(string username)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            realmAccess.Write(r =>
            {
                var existing = r.All<EzPlayerDanSkillsetValue>()
                                .Where(v => v.Username == username)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);
            });
        }

        /// <summary>Drop side GetDan estimates for a username (all keys/sides/versions).</summary>
        public void ClearDanEstimates(string username)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            realmAccess.Write(r =>
            {
                var existing = r.All<EzDanEstimate>()
                                .Where(v => v.Username == username)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);
            });
        }

        /// <summary>
        /// Drop every Realm-side player skill row for <paramref name="username"/> — SSR / pattern values,
        /// skill history, side Dan estimates and Dan skillset tiles, across all keymodes and versions.
        /// Used to clear the archive-wide <c>All</c> sentinel when the players it summarised change (an exclusion,
        /// or a re-derivation from a now-empty included set). The SQLite counterpart is
        /// <c>EzLocalProfileStore</c>, which is not touched here.
        /// </summary>
        /// <returns>Total number of rows removed.</returns>
        public int DeletePlayerSkillData(string username)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            int removed = 0;

            realmAccess.Write(r =>
            {
                foreach (var row in r.All<EzPlayerSkillValue>().Where(v => v.Username == username).ToList())
                {
                    r.Remove(row);
                    removed++;
                }

                foreach (var row in r.All<EzPlayerSkillHistoryPoint>().Where(v => v.Username == username).ToList())
                {
                    r.Remove(row);
                    removed++;
                }

                foreach (var row in r.All<EzDanEstimate>().Where(v => v.Username == username).ToList())
                {
                    r.Remove(row);
                    removed++;
                }

                foreach (var row in r.All<EzPlayerDanSkillsetValue>().Where(v => v.Username == username).ToList())
                {
                    r.Remove(row);
                    removed++;
                }
            });

            return removed;
        }

        /// <summary>
        /// Flag every stored <see cref="EzPlayerSkillValue"/> row for one player (SSR / pattern) as stale without
        /// changing any value. Used when a newly settled play has been folded into the SQLite slice but the player's
        /// Realm-side skill rows have not been recomputed yet: the UI keeps showing the old numbers and marks them
        /// stale, and the startup warmup refreshes them.
        /// </summary>
        /// <remarks>
        /// Dan rows (<see cref="EzDanEstimate"/> / <see cref="EzPlayerDanSkillsetValue"/>) carry no stale flag, so
        /// they can only be corrected by an actual recompute.
        /// </remarks>
        /// <returns>Number of rows newly flagged.</returns>
        public int MarkPlayerSkillStale(string username)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            int flagged = 0;

            realmAccess.Write(r =>
            {
                foreach (var row in r.All<EzPlayerSkillValue>().Where(v => v.Username == username && !v.Stale).ToList())
                {
                    row.Stale = true;
                    flagged++;
                }
            });

            return flagged;
        }

        /// <summary>
        /// Clear the stale flag on every stored row for one player without changing any value. Called once a refresh
        /// pass has covered that player's slice: the flag is player-scoped, so a row the pass could not re-derive
        /// (SSR/pattern values for a keymode whose plays are gone, say) must not keep reporting work that no later
        /// pass will ever pick up.
        /// </summary>
        /// <returns>Number of rows newly un-flagged.</returns>
        public int ClearPlayerSkillStale(string username)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            // Read first: a refresh pass rewrites its rows unstale, so the usual call has nothing to clear, and the
            // write transaction (which wakes every change-notification subscriber) is the expensive half.
            bool anyStale = realmAccess.Run(r => r.All<EzPlayerSkillValue>()
                                                  .Where(v => v.Username == username && v.Stale)
                                                  .AsEnumerable()
                                                  .Any());

            if (!anyStale)
                return 0;

            int cleared = 0;

            realmAccess.Write(r =>
            {
                foreach (var row in r.All<EzPlayerSkillValue>().Where(v => v.Username == username && v.Stale).ToList())
                {
                    row.Stale = false;
                    cleared++;
                }
            });

            return cleared;
        }

        /// <summary>
        /// Players with at least one skill row flagged stale, i.e. their SQLite slice has moved on since the
        /// Realm-side skills were written. This is the scope a reconcile reads to decide whose skills need refreshing
        /// (a play folded in after the last successful skill pass); it is also the manual compute's default scope.
        /// </summary>
        public IReadOnlyList<string> GetStalePlayerSkillUsernames()
        {
            return realmAccess.Run(r => r.All<EzPlayerSkillValue>()
                                        .Where(v => v.Stale)
                                        .AsEnumerable()
                                        .Select(v => v.Username)
                                        .Where(n => !string.IsNullOrEmpty(n))
                                        .Distinct(StringComparer.Ordinal)
                                        .ToList());
        }

        /// <summary>
        /// The stale players with the row count and newest write time behind the flag. Same rows
        /// <see cref="GetStalePlayerSkillUsernames"/> reports; this shape exists so the status readout can show why a
        /// player is listed (how much is flagged, and how far back the values it is showing were written).
        /// </summary>
        public IReadOnlyList<EzStalePlayerSkill> GetStalePlayerSkillDetails()
        {
            return realmAccess.Run(r => r.All<EzPlayerSkillValue>()
                                        .Where(v => v.Stale)
                                        .AsEnumerable()
                                        .Where(v => !string.IsNullOrEmpty(v.Username))
                                        .GroupBy(v => v.Username, StringComparer.Ordinal)
                                        .Select(g => new EzStalePlayerSkill(g.Key, g.Count(), g.Max(v => v.ComputedAt)))
                                        .OrderBy(s => s.Username, StringComparer.Ordinal)
                                        .ToList());
        }

        /// <summary>Legacy row writer; prefer <see cref="WriteDanSkillsetVerdicts"/>.</summary>
        public void WriteDanSkillsetValues(string username, int keyCount, string side, IEnumerable<EzPlayerDanSkillsetValue> values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentNullException.ThrowIfNull(values);

            var list = values as IList<EzPlayerDanSkillsetValue> ?? values.ToList();
            var map = new Dictionary<string, EzDanSkillsetVerdict>(StringComparer.Ordinal);

            foreach (var value in list)
            {
                if (value.SkillsetId == EzDanSkillsetBuckets.CACHE_EMPTY_SENTINEL)
                    continue;

                map[value.SkillsetId] = new EzDanSkillsetVerdict(value.SkillsetId, value.RawDan, value.Label, value.Clears);
            }

            WriteDanSkillsetVerdicts(username, keyCount, side, map);
        }

        private const char pattern_separator = '\u001f';

        private static string joinPatterns(string[]? patterns)
        {
            if (patterns == null || patterns.Length == 0)
                return string.Empty;

            return string.Join(pattern_separator, patterns.Where(static t => !string.IsNullOrWhiteSpace(t)));
        }

        private static string[] splitPatterns(string? joined)
        {
            if (string.IsNullOrEmpty(joined))
                return [];

            return joined.Split(pattern_separator, StringSplitOptions.RemoveEmptyEntries);
        }

        private static double? optionalDouble(double value)
            => value < 0 ? null : value;

        private static EzChartSkillInfo chartSkillInfoToDto(EzBeatmapChartSkillInfo row)
        {
            EzMotionFeatures? motion = null;

            if (row.MotionRhythmBreak >= 0
                || row.MotionCrossHandTrill >= 0
                || row.MotionMiniJack >= 0
                || row.MotionSameHand >= 0)
            {
                motion = new EzMotionFeatures
                {
                    RhythmBreak = row.MotionRhythmBreak < 0 ? 0 : row.MotionRhythmBreak,
                    CrossHandTrill = row.MotionCrossHandTrill < 0 ? 0 : row.MotionCrossHandTrill,
                    MiniJack = row.MotionMiniJack < 0 ? 0 : row.MotionMiniJack,
                    SameHand = row.MotionSameHand < 0 ? 0 : row.MotionSameHand,
                };
            }

            return new EzChartSkillInfo
            {
                Patterns = splitPatterns(row.PatternTagsJoined),
                JackDemand = row.JackDemand,
                JackShare = optionalDouble(row.JackShare),
                StreamShare = optionalDouble(row.StreamShare),
                TechCategory = row.TechCategory,
                ClusterTrill = row.ClusterTrill,
                HandstreamCluster = row.HandstreamCluster,
                HandstreamEndurance = row.HandstreamEndurance,
                TechScore = row.TechScore,
                ChordjackScore = row.ChordjackScore,
                JackScore = optionalDouble(row.JackScore),
                Motion = motion,
                LnRatio = optionalDouble(row.LnRatio),
                Vibro = row.Vibro,
                DanEligible = row.DanEligible,
                LengthSeconds = optionalDouble(row.LengthSeconds),
                KeyCount = row.KeyCount,
            };
        }

        private static EzPersistedChartDan chartDanToDto(EzBeatmapChartDan row)
            => new EzPersistedChartDan
            {
                BeatmapHash = row.BeatmapHash,
                BeatmapId = row.BeatmapId,
                // DTO keeps the pure dan algorithm version; the column holds the composed facet
                // revision, which is a persistence detail that must not leak to consumers.
                AlgorithmVersion = EzDanAlgorithm.VERSION,
                KeyCount = row.KeyCount,
                HoldRatio = row.HoldRatio,
                OverallMsd = row.OverallMsd,
                RcRawDan = row.RcRawDan,
                RcLabel = row.RcLabel,
                LnRawDan = row.LnRawDan,
                LnLabel = row.LnLabel,
                RcSkillsetLabels = EzPersistedChartDan.ParseSkillsetLabels(row.RcSkillsetLabelsJoined),
                LnSkillsetLabels = EzPersistedChartDan.ParseSkillsetLabels(row.LnSkillsetLabelsJoined),
                ComputedAt = row.ComputedAt,
            };
    }
}
