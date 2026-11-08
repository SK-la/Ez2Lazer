// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.EzOsuGame.Startup;
using Realms;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Single Realm persistence facade for skill metrics (MSD / SunnySR / Dan / PPP / ChartSkillInfo / ChartDan / skillset cache).
    /// Does not open the game for the caller. Evidence lists stay in local-profile SQLite.
    /// </summary>
    public sealed class EzChartSkillStore
    {
        private readonly RealmAccess realmAccess;

        public EzChartSkillStore(RealmAccess realmAccess)
        {
            this.realmAccess = realmAccess;
        }

        /// <summary>Revision stamped on chart-side Mina axis rows.</summary>
        private int minaChartRevision => EzAnalysisRevision.Msd;

        /// <summary>
        /// Current values for one system. Rows the producer settled without a value are excluded: a verdict is
        /// not an axis, so a settled miss reads as an empty set and callers ask
        /// <see cref="GetBeatmapSystemState"/> for the reason.
        /// </summary>
        public IReadOnlyDictionary<string, double> GetBeatmapSkills(string beatmapHash, string systemId, int? algorithmVersion = null, int rulesetOnlineId = -1)
        {
            int version = algorithmVersion ?? EzSkillSystems.ResolveAlgorithmVersion(systemId);

            return realmAccess.Run(r =>
            {
                var rows = r.All<EzBeatmapSkillValue>()
                            .Where(v => v.BeatmapHash == beatmapHash
                                        && v.SystemId == systemId
                                        && v.State == (int)EzSkillRowState.Current
                                        && v.AlgorithmVersion == version);

                if (rulesetOnlineId >= 0)
                    rows = rows.Where(v => v.RulesetOnlineId == rulesetOnlineId);

                return rows.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);
            });
        }

        /// <summary>
        /// Whether one system has a current value, a settled miss, or nothing at all for a chart.
        /// </summary>
        public EzSkillRowState GetBeatmapSystemState(string beatmapHash, string systemId, int revision)
        {
            if (string.IsNullOrEmpty(beatmapHash) || string.IsNullOrEmpty(systemId))
                return EzSkillRowState.Missing;

            return realmAccess.Run(r =>
            {
                bool any = false;

                foreach (var row in r.All<EzBeatmapSkillValue>().Where(v => v.BeatmapHash == beatmapHash && v.SystemId == systemId && v.AlgorithmVersion == revision))
                {
                    any = true;

                    if (row.State == (int)EzSkillRowState.Unrateable)
                        return EzSkillRowState.Unrateable;
                }

                return any ? EzSkillRowState.Current : EzSkillRowState.Missing;
            });
        }

        /// <summary>
        /// PPP rows whose <see cref="EzBeatmapSkillValue.AlgorithmVersion"/> matches the bundle revision for
        /// <paramref name="beatmapInfo"/>'s ruleset. The ruleset has to be supplied by the caller: looking it up
        /// from the hash would scan the beatmap table on every read.
        /// </summary>
        public IReadOnlyDictionary<string, double> GetBeatmapSkillsAtCurrentVersions(BeatmapInfo beatmapInfo, string systemId)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            int rulesetId = beatmapInfo.Ruleset.OnlineID;
            int bundle = EzAnalysisRevision.VERSION;

            if (bundle <= 0)
                return new Dictionary<string, double>(StringComparer.Ordinal);

            string beatmapHash = beatmapInfo.Hash;

            return realmAccess.Run(r =>
            {
                var rows = r.All<EzBeatmapSkillValue>()
                            .Where(v => v.BeatmapHash == beatmapHash && v.SystemId == systemId);

                var result = new Dictionary<string, double>(StringComparer.Ordinal);

                foreach (var row in rows)
                {
                    if (row.AlgorithmVersion == bundle)
                        result[row.SkillId] = row.Value;
                }

                return result;
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

            int version = algorithmVersion ?? EzSkillSystems.ResolveAlgorithmVersion(systemId);

            return realmAccess.Run(r =>
            {
                var result = new Dictionary<string, IReadOnlyDictionary<string, double>>(hashSet.Count, StringComparer.Ordinal);

                var rows = r.All<EzBeatmapSkillValue>()
                            .Where(v => v.SystemId == systemId && v.AlgorithmVersion == version && v.State == (int)EzSkillRowState.Current)
                            .AsEnumerable()
                            .Where(v => hashSet.Contains(v.BeatmapHash));

                foreach (var group in rows.GroupBy(v => v.BeatmapHash, StringComparer.Ordinal))
                    result[group.Key] = group.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);

                return result;
            });
        }

        public bool TryGetBeatmapSkill(string beatmapHash, string skillId, out double value, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? minaChartRevision;
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

        /// <summary>
        /// Replace all beatmap skill rows for <paramref name="systemId"/> with <paramref name="skills"/>.
        /// </summary>
        /// <param name="rulesetOnlineId">
        /// Ruleset the rows belong to. Pass a negative value when the caller cannot name one; reads then treat the
        /// rows as unqualified rather than attributing them to a ruleset.
        /// </param>
        public void WriteBeatmapSystemSkills(
            string beatmapHash,
            string systemId,
            IReadOnlyDictionary<string, double> skills,
            Guid beatmapId = default,
            DateTimeOffset? computedAt = null,
            int? algorithmVersion = null,
            IReadOnlyDictionary<string, int>? skillVersions = null,
            int rulesetOnlineId = -1)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(beatmapHash);
            ArgumentException.ThrowIfNullOrWhiteSpace(systemId);
            ArgumentNullException.ThrowIfNull(skills);

            // [EZ-REALTIME] Realm v12 写入已屏蔽（实时验证期）；恢复时取消注释。
            /*
            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;
            int version = algorithmVersion ?? EzSkillSystems.ResolveAlgorithmVersion(systemId);

            realmAccess.Write(r =>
            {
                var existing = r.All<EzBeatmapSkillValue>()
                                .Where(v => v.BeatmapHash == beatmapHash && v.SystemId == systemId)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                foreach (var (skillId, value) in skills)
                {
                    int rowVersion = rowVersionFor(systemId, skillId, version, skillVersions, rulesetOnlineId);

                    r.Add(new EzBeatmapSkillValue
                    {
                        BeatmapHash = beatmapHash,
                        BeatmapId = beatmapId,
                        SystemId = systemId,
                        RulesetOnlineId = rulesetOnlineId,
                        SkillId = skillId,
                        Value = value,
                        AlgorithmVersion = rowVersion,
                        ComputedAt = at,
                    });
                }
            });
            */
        }

        public void WriteBeatmapMsd(string beatmapHash, EzMinaSkillAxisVector vector, Guid beatmapId = default, DateTimeOffset? computedAt = null)
        {
            // [EZ-REALTIME] Realm v12 写入已屏蔽（实时验证期）；恢复时取消注释。
            /*
            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;
            int version = minaChartRevision;

            realmAccess.Write(r =>
            {
                var existing = r.All<EzBeatmapSkillValue>()
                                .Where(v => v.BeatmapHash == beatmapHash && v.SystemId == EzSkillSystems.BEATMAP_MINA)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                foreach (var (axis, value) in vector.Enumerate())
                {
                    r.Add(new EzBeatmapSkillValue
                    {
                        BeatmapHash = beatmapHash,
                        BeatmapId = beatmapId,
                        SystemId = EzSkillSystems.BEATMAP_MINA,
                        RulesetOnlineId = EzLocalProfileConstants.MANIA_RULESET_ID,
                        SkillId = axis.ToMsdSkillId(),
                        Value = value,
                        AlgorithmVersion = version,
                        ComputedAt = at,
                    });
                }
            });
            */
        }

        /// <summary>
        /// Persist a settled-miss marker so BDSP does not reprocess the same zero-vector chart every launch.
        /// Replaces any existing <see cref="EzSkillSystems.BEATMAP_MINA"/> rows for this hash.
        /// </summary>
        public void WriteBeatmapMsdUnrateable(string beatmapHash, Guid beatmapId = default, DateTimeOffset? computedAt = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(beatmapHash);

            // [EZ-REALTIME] Realm v12 写入已屏蔽（实时验证期）；恢复时取消注释。
            /*
            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;
            int version = minaChartRevision;

            realmAccess.Write(r =>
            {
                var existing = r.All<EzBeatmapSkillValue>()
                                .Where(v => v.BeatmapHash == beatmapHash && v.SystemId == EzSkillSystems.BEATMAP_MINA)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                r.Add(new EzBeatmapSkillValue
                {
                    BeatmapHash = beatmapHash,
                    BeatmapId = beatmapId,
                    SystemId = EzSkillSystems.BEATMAP_MINA,
                    RulesetOnlineId = EzLocalProfileConstants.MANIA_RULESET_ID,
                    SkillId = EzSkillSystems.UNRATEABLE,
                    Value = 1,
                    State = (int)EzSkillRowState.Unrateable,
                    AlgorithmVersion = version,
                    ComputedAt = at,
                });
            });
            */
        }

        /// <summary>
        /// Deletes persisted beatmap MSD rows. When <paramref name="hashes"/> is null, clears all MSD rows.
        /// </summary>
        public void ClearBeatmapMsd(IEnumerable<string>? hashes = null)
        {
            // [EZ-REALTIME] Realm v12 写入已屏蔽（实时验证期）；恢复时取消注释。
            if (hashes == null)
            {
                /*
                realmAccess.Write(r =>
                {
                    var rows = r.All<EzBeatmapSkillValue>()
                                .Where(v => v.SystemId == EzSkillSystems.BEATMAP_MINA)
                                .ToList();

                    foreach (var row in rows)
                        r.Remove(row);
                });
                */
                return;
            }

            var hashList = hashes as IList<string> ?? hashes.ToList();
            if (hashList.Count == 0)
                return;

            const int batch_size = 200;

            for (int i = 0; i < hashList.Count; i += batch_size)
            {
                var batch = hashList.Skip(i).Take(batch_size).ToHashSet(StringComparer.Ordinal);

                /*
                realmAccess.Write(r =>
                {
                    var rows = r.All<EzBeatmapSkillValue>()
                                .Where(v => v.SystemId == EzSkillSystems.BEATMAP_MINA)
                                .AsEnumerable()
                                .Where(v => batch.Contains(v.BeatmapHash))
                                .ToList();

                    foreach (var row in rows)
                        r.Remove(row);
                });
                */
            }
        }

        /// <summary>
        /// One read of the chart skill chain's persisted state - the rateable charts, each facet's rows, and the
        /// per-stage answers derived from them. Every consumer of the chain (the backfill stages, the status
        /// readout, the player-chain debt) reads this, so they cannot disagree about what is settled, missing or
        /// worth another pass.
        /// </summary>
        public EzChartChainState CollectChartChainState() => CollectChartChainState(null);

        public EzChartChainState CollectChartChainState(string? traceCaller)
        {
            var stopwatch = Stopwatch.StartNew();
            var chain = realmAccess.Run(EzChartChainState.Build);
            stopwatch.Stop();

            if (traceCaller != null)
                EzStartupTrace.LogDiagnostic($"ChartChainState caller={traceCaller} elapsedMs={stopwatch.ElapsedMilliseconds} rateable={chain.RateableChartCount}");

            return chain;
        }

        /// <summary>
        /// Charts whose MSD settled as unrateable at the current revision. Every later facet (CSI included) is
        /// settled for them too, so anything waiting on one of them is settled rather than missing - reporting it
        /// would re-queue the chain forever.
        /// </summary>
        /// <remarks>
        /// Scans the MSD table, so callers should treat the result as a per-pass value and only ask for it when a
        /// later facet's row is otherwise missing (the steady state has none).
        /// </remarks>
        public HashSet<string> GetUnrateableMsdHashes()
        {
            int version = minaChartRevision;

            return realmAccess.Run(r => r.All<EzBeatmapSkillValue>()
                                         .Where(v => v.SystemId == EzSkillSystems.BEATMAP_MINA
                                                     && v.AlgorithmVersion == version
                                                     && v.State == (int)EzSkillRowState.Unrateable)
                                         .AsEnumerable()
                                         .Select(v => v.BeatmapHash)
                                         .ToHashSet(StringComparer.Ordinal));
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

            // [EZ-REALTIME] Realm v12 写入已屏蔽（实时验证期）；恢复时取消注释。
            /*
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
            */
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

            // [EZ-REALTIME] Realm v12 写入已屏蔽（实时验证期）；恢复时取消注释。
            /*
            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;
            int revision = EzAnalysisRevision.ChartSkillInfo;

            realmAccess.Write(r =>
            {
                var existing = r.All<EzBeatmapChartSkillInfo>()
                                .Where(v => v.BeatmapHash == beatmapHash)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                // A settled miss is not a frame: leaving the axis rows behind would let a reader draw a chart the
                // structure pass declined to rate.
                removeStructureAxes(r, beatmapHash);

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
            */
        }

        public void ClearChartSkillInfo()
        {
            // [EZ-REALTIME] Realm v12 写入已屏蔽（实时验证期）；恢复时取消注释。
            /*
            realmAccess.Write(r =>
            {
                var rows = r.All<EzBeatmapChartSkillInfo>().ToList();

                foreach (var row in rows)
                    r.Remove(row);

                var axes = r.All<EzBeatmapSkillValue>()
                            .Where(v => v.SystemId == EzSkillSystems.BEATMAP_CSI)
                            .ToList();

                foreach (var row in axes)
                    r.Remove(row);
            });
            */
        }

        private static void removeStructureAxes(Realm r, string beatmapHash)
        {
            var axes = r.All<EzBeatmapSkillValue>()
                        .Where(v => v.BeatmapHash == beatmapHash && v.SystemId == EzSkillSystems.BEATMAP_CSI)
                        .ToList();

            foreach (var row in axes)
                r.Remove(row);
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
                var row = r.All<EzChartDan>()
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

            // Stamped with the facet revision (not chartDan.AlgorithmVersion, which stays the pure
            // dan algorithm version on the DTO) so a later MSD/CSI bump retires this row.
            // [EZ-REALTIME] Realm v12 写入已屏蔽（实时验证期）；恢复时取消注释。
            /*
            DateTimeOffset at = chartDan.ComputedAt == default ? DateTimeOffset.UtcNow : chartDan.ComputedAt;
            int version = EzAnalysisRevision.ChartDan;

            realmAccess.Write(r =>
            {
                var existing = r.All<EzChartDan>()
                                .Where(v => v.BeatmapHash == chartDan.BeatmapHash)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                r.Add(new EzChartDan
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
            */
        }

        public void ClearChartDan()
        {
            // [EZ-REALTIME] Realm v12 写入已屏蔽（实时验证期）；恢复时取消注释。
            /*
            realmAccess.Write(r =>
            {
                var rows = r.All<EzChartDan>().ToList();

                foreach (var row in rows)
                    r.Remove(row);
            });
            */
        }

        /// <summary>
        /// Coverage of every analysis system, grouped by ruleset. Reads the chart chain and the skill-value table in
        /// one run, so call it off the UI thread; it is meant for an explicit status refresh, not a hot path.
        /// </summary>
        public IReadOnlyList<EzRulesetAnalysisStatus> CollectAnalysisStatus(string? traceCaller = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var statuses = realmAccess.Run(r => EzAnalysisDataStatus.Collect(r, DateTimeOffset.UtcNow));
            stopwatch.Stop();

            if (traceCaller != null)
                EzStartupTrace.LogDiagnostic($"AnalysisDataStatus caller={traceCaller} elapsedMs={stopwatch.ElapsedMilliseconds} rulesets={statuses.Count}");

            return statuses;
        }

        /// <summary>
        /// Snapshot of the chart skill chain's completeness, counted over every rateable mania chart in Realm.
        /// <para>
        /// Reads all three facet tables in one run, so call it off the UI thread; it is meant for an explicit
        /// status refresh, not a hot path. It counts the same state the backfill stages filter on
        /// (<see cref="EzChartChainState"/>), so "Pending &gt; 0" and "the next backfill has work" cannot disagree.
        /// </para>
        /// </summary>
        public EzDataStateReport GetSkillDataStatus(DateTimeOffset? measuredAt = null)
        {
            DateTimeOffset at = measuredAt ?? DateTimeOffset.UtcNow;
            var report = CollectChartChainState().ToReport(at);
            var facets = new List<EzFacetStatus>(report.Facets.Count + 1);
            facets.AddRange(report.Facets);
            facets.Add(scanPppCharts().Status);

            return new EzDataStateReport
            {
                UniverseCount = report.UniverseCount,
                Facets = facets,
                MeasuredAt = at,
            };
        }

        /// <summary>Beatmaps whose PPP chart skills are missing a current per-skill version.</summary>
        public IReadOnlyList<Guid> CollectPppChartDebt()
            => scanPppCharts().OwedIds;

        public void ClearBeatmapPPP()
        {
            // [EZ-REALTIME] Realm v12 写入已屏蔽（实时验证期）；恢复时取消注释。
            /*
            realmAccess.Write(r =>
            {
                var rows = r.All<EzBeatmapSkillValue>()
                            .Where(v => v.SystemId == EzSkillSystems.BEATMAP_PPP)
                            .ToList();

                foreach (var row in rows)
                    r.Remove(row);
            });
            */
        }

        private PppChartScan scanPppCharts()
        {
            return realmAccess.Run(r =>
            {
                var candidates = new List<(Guid Id, string Hash, int Ruleset)>();

                foreach (var beatmap in r.All<BeatmapInfo>())
                {
                    if (beatmap.BeatmapSet == null || string.IsNullOrEmpty(beatmap.Hash))
                        continue;

                    int rulesetId = beatmap.Ruleset.OnlineID;

                    if (!EzPPPSkillCatalog.SupportsRuleset(rulesetId))
                        continue;

                    candidates.Add((beatmap.ID, beatmap.Hash, rulesetId));
                }

                var byHash = new Dictionary<string, List<EzBeatmapSkillValue>>(StringComparer.Ordinal);

                foreach (var row in r.All<EzBeatmapSkillValue>().Where(v => v.SystemId == EzSkillSystems.BEATMAP_PPP))
                {
                    if (!byHash.TryGetValue(row.BeatmapHash, out var list))
                    {
                        list = new List<EzBeatmapSkillValue>();
                        byHash[row.BeatmapHash] = list;
                    }

                    list.Add(row);
                }

                var owed = new List<Guid>();
                var rows = new Dictionary<string, EzFacetRowState>(candidates.Count, StringComparer.Ordinal);

                foreach (var candidate in candidates)
                {
                    byHash.TryGetValue(candidate.Hash, out var stored);
                    bool complete = isCurrentPppChart(candidate.Ruleset, stored);

                    if (!complete)
                        owed.Add(candidate.Id);

                    if (stored == null || stored.Count == 0)
                        continue;

                    rows[candidate.Hash] = new EzFacetRowState(complete ? EzAnalysisRevision.VERSION : 0, false);
                }

                var facet = new EzDataStateFacet
                {
                    Id = "PPP",
                    CurrentRevision = EzAnalysisRevision.VERSION,
                    UniverseCount = candidates.Count,
                    Rows = rows,
                };

                return new PppChartScan(owed, EzDataStateChecker.Count(facet));
            });
        }

        private static int rowVersionFor(
            string systemId,
            string skillId,
            int fallback,
            IReadOnlyDictionary<string, int>? skillVersions,
            int rulesetOnlineId)
        {
            if (skillVersions != null && skillVersions.TryGetValue(skillId, out int specific))
                return specific;

            if (rulesetOnlineId >= 0
                && systemId is EzSkillSystems.BEATMAP_PPP or EzSkillSystems.PLAYER_PPP)
            {
                int bundle = EzAnalysisRevision.VERSION;

                if (bundle > 0)
                    return bundle;
            }

            return fallback;
        }

        private bool isCurrentPppChart(int rulesetId, List<EzBeatmapSkillValue>? stored)
        {
            if (stored == null || stored.Count == 0 || !EzPPPSkillCatalog.SupportsRuleset(rulesetId))
                return false;

            int revision = EzAnalysisRevision.VERSION;

            if (revision <= 0)
                return false;

            var current = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var row in stored)
            {
                if (row.AlgorithmVersion == revision)
                    current[row.SkillId] = row.Value;
            }

            // Completeness is the axis set the ruleset's catalog declares, not a second list kept here.
            foreach (var spec in EzPPPSkillCatalog.MetricsFor(rulesetId))
            {
                if (!current.ContainsKey(spec.ChartSkillId))
                    return false;
            }

            return current.Count > 0;
        }

        private readonly record struct PppChartScan(List<Guid> OwedIds, EzFacetStatus Status);

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

        private static EzPersistedChartDan chartDanToDto(EzChartDan row)
            => new EzPersistedChartDan
            {
                BeatmapHash = row.BeatmapHash,
                BeatmapId = row.BeatmapId,
                // DTO keeps the pure dan algorithm version; the column holds the composed facet
                // revision, which is a persistence detail that must not leak to consumers.
                AlgorithmVersion = EzAnalysisRevision.VERSION,
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
