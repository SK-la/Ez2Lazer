// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.Pattern;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.EzOsuGame.Startup;
using Realms;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Single Realm persistence facade for skill metrics (MSD / SSR / Dan / PPP / ChartSkillInfo / ChartDan / skillset cache).
    /// Does not open the game for the caller. Evidence lists stay in local-profile SQLite.
    /// </summary>
    public sealed class EzSkillStore
    {
        private readonly RealmAccess realmAccess;
        private readonly EzMetricRevisionResolver resolver;

        public EzSkillStore(RealmAccess realmAccess, EzMetricRevisionResolver? resolver = null)
        {
            this.realmAccess = realmAccess;
            this.resolver = resolver ?? EzMetricRegistries.Current;
        }

        /// <summary>Revision stamped on chart-side Mina axis rows.</summary>
        private int minaChartRevision => resolver.RevisionOf(EzChartMetricProducer.Mina.Id(), EzLocalProfileConstants.MANIA_RULESET_ID);

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
                                        && v.State == (int)EzMetricState.Current
                                        && v.AlgorithmVersion == version);

                if (rulesetOnlineId >= 0)
                    rows = rows.Where(v => v.RulesetOnlineId == rulesetOnlineId);

                return rows.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);
            });
        }

        /// <summary>
        /// Whether one system has a current value, a settled miss, or nothing at all for a chart.
        /// </summary>
        public EzMetricState GetBeatmapSystemState(string beatmapHash, string systemId, int revision)
        {
            if (string.IsNullOrEmpty(beatmapHash) || string.IsNullOrEmpty(systemId))
                return EzMetricState.Missing;

            return realmAccess.Run(r =>
            {
                bool any = false;

                foreach (var row in r.All<EzBeatmapSkillValue>().Where(v => v.BeatmapHash == beatmapHash && v.SystemId == systemId && v.AlgorithmVersion == revision))
                {
                    any = true;

                    if (row.State == (int)EzMetricState.Unrateable)
                        return EzMetricState.Unrateable;
                }

                return any ? EzMetricState.Current : EzMetricState.Missing;
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
            int bundle = EzPPPSkillVersions.ForRuleset(rulesetId);

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
                            .Where(v => v.SystemId == systemId && v.AlgorithmVersion == version && v.State == (int)EzMetricState.Current)
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
        }

        /// <summary>
        /// Replace all player skill rows for (<paramref name="username"/>, <paramref name="sliceKey"/>, <paramref name="systemId"/>).
        /// </summary>
        public void WritePlayerSystemSkills(
            string username,
            int sliceKey,
            string systemId,
            IReadOnlyDictionary<string, double> skills,
            int analyzedPlays,
            bool provisional = false,
            bool stale = false,
            DateTimeOffset? computedAt = null,
            int? algorithmVersion = null,
            IReadOnlyDictionary<string, int>? skillVersions = null,
            int rulesetOnlineId = -1)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentException.ThrowIfNullOrWhiteSpace(systemId);
            ArgumentNullException.ThrowIfNull(skills);

            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;
            int version = algorithmVersion ?? EzSkillSystems.ResolveAlgorithmVersion(systemId);

            realmAccess.Write(r =>
            {
                var existing = r.All<EzPlayerSkillValue>()
                                .Where(v => v.Username == username
                                            && v.KeyCount == sliceKey
                                            && v.SystemId == systemId
                                            && v.RulesetOnlineId == rulesetOnlineId)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);

                foreach (var (skillId, value) in skills)
                {
                    int rowVersion = rowVersionFor(systemId, skillId, version, skillVersions, rulesetOnlineId);

                    r.Add(new EzPlayerSkillValue
                    {
                        Username = username,
                        KeyCount = sliceKey,
                        SystemId = systemId,
                        RulesetOnlineId = rulesetOnlineId,
                        SkillId = skillId,
                        Value = value,
                        AnalyzedPlays = analyzedPlays,
                        Provisional = provisional,
                        Stale = stale,
                        AlgorithmVersion = rowVersion,
                        ComputedAt = at,
                    });
                }
            });
        }

        public void WriteBeatmapMsd(string beatmapHash, EzMinaSkillAxisVector vector, Guid beatmapId = default, DateTimeOffset? computedAt = null)
        {
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
        }

        /// <summary>
        /// Persist a settled-miss marker so BDSP does not reprocess the same zero-vector chart every launch.
        /// Replaces any existing <see cref="EzSkillSystems.BEATMAP_MINA"/> rows for this hash.
        /// </summary>
        public void WriteBeatmapMsdUnrateable(string beatmapHash, Guid beatmapId = default, DateTimeOffset? computedAt = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(beatmapHash);

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
                    State = (int)EzMetricState.Unrateable,
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
                                .Where(v => v.SystemId == EzSkillSystems.BEATMAP_MINA)
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
                                .Where(v => v.SystemId == EzSkillSystems.BEATMAP_MINA)
                                .AsEnumerable()
                                .Where(v => batch.Contains(v.BeatmapHash))
                                .ToList();

                    foreach (var row in rows)
                        r.Remove(row);
                });
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
                                                     && v.State == (int)EzMetricState.Unrateable)
                                         .AsEnumerable()
                                         .Select(v => v.BeatmapHash)
                                         .ToHashSet(StringComparer.Ordinal));
        }

        public IReadOnlyDictionary<string, double> GetPlayerSkills(string username, int keyCount, string systemId, int? algorithmVersion = null, int rulesetOnlineId = -1)
        {
            int version = algorithmVersion ?? EzSkillSystems.ResolveAlgorithmVersion(systemId);

            return realmAccess.Run(r =>
            {
                var rows = r.All<EzPlayerSkillValue>()
                            .Where(v => v.Username == username
                                        && v.KeyCount == keyCount
                                        && v.SystemId == systemId
                                        && v.AlgorithmVersion == version);

                if (rulesetOnlineId >= 0)
                    rows = rows.Where(v => v.RulesetOnlineId == rulesetOnlineId);

                return rows.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);
            });
        }

        /// <summary>
        /// PPP player rows written at the bundle revision of <paramref name="rulesetOnlineId"/>.
        /// </summary>
        public IReadOnlyDictionary<string, double> GetPlayerSkillsAtCurrentVersions(string username, int keyCount, string systemId, int rulesetOnlineId)
        {
            int version = EzPPPSkillVersions.ForRuleset(rulesetOnlineId);

            return realmAccess.Run(r =>
            {
                var rows = r.All<EzPlayerSkillValue>()
                            .Where(v => v.Username == username
                                        && v.KeyCount == keyCount
                                        && v.SystemId == systemId
                                        && v.RulesetOnlineId == rulesetOnlineId
                                        && v.AlgorithmVersion == version)
                            .ToList();

                var result = new Dictionary<string, double>(rows.Count, StringComparer.Ordinal);

                foreach (var row in rows)
                    result[row.SkillId] = row.Value;

                return result;
            });
        }

        /// <summary>Row metadata for a player system slice (analyzed plays / provisional / stale).</summary>
        public (int AnalyzedPlays, bool Provisional, bool Stale) GetPlayerSystemSkillMeta(
            string username,
            int keyCount,
            string systemId,
            int? algorithmVersion = null,
            int rulesetOnlineId = -1)
        {
            int version = resolvePlayerVersion(systemId, algorithmVersion, rulesetOnlineId);

            return realmAccess.Run(r =>
            {
                var rows = r.All<EzPlayerSkillValue>()
                            .Where(v => v.Username == username
                                        && v.KeyCount == keyCount
                                        && v.SystemId == systemId
                                        && v.AlgorithmVersion == version);

                if (rulesetOnlineId >= 0)
                    rows = rows.Where(v => v.RulesetOnlineId == rulesetOnlineId);

                var row = rows.FirstOrDefault();

                return row == null
                    ? (0, false, false)
                    : (row.AnalyzedPlays, row.Provisional, row.Stale);
            });
        }

        /// <summary>
        /// One player slice's values together with its metadata, in a single Realm read.
        /// </summary>
        /// <remarks>
        /// Read paths that need both used to run two transactions; the row carries the metadata, so splitting the
        /// query only doubled the cost.
        /// </remarks>
        public PlayerSystemSkillSlice GetPlayerSystemSkillSlice(
            string username,
            int keyCount,
            string systemId,
            int? algorithmVersion = null,
            int rulesetOnlineId = -1)
        {
            int version = resolvePlayerVersion(systemId, algorithmVersion, rulesetOnlineId);

            return realmAccess.Run(r =>
            {
                var rows = r.All<EzPlayerSkillValue>()
                            .Where(v => v.Username == username
                                        && v.KeyCount == keyCount
                                        && v.SystemId == systemId
                                        && v.AlgorithmVersion == version);

                if (rulesetOnlineId >= 0)
                    rows = rows.Where(v => v.RulesetOnlineId == rulesetOnlineId);

                var materialised = rows.ToList();
                var values = new Dictionary<string, double>(materialised.Count, StringComparer.Ordinal);

                foreach (var row in materialised)
                    values[row.SkillId] = row.Value;

                if (materialised.Count == 0)
                    return new PlayerSystemSkillSlice(values, 0, false, false);

                var first = materialised[0];
                return new PlayerSystemSkillSlice(values, first.AnalyzedPlays, first.Provisional, first.Stale);
            });
        }

        public readonly record struct PlayerSystemSkillSlice(IReadOnlyDictionary<string, double> Values, int AnalyzedPlays, bool Provisional, bool Stale);

        /// <summary>
        /// The revision a player slice is stored under. PP+ rows are stamped per ruleset bundle when the ruleset
        /// is known, so there is one place that decides between the bundle and the facet revision.
        /// </summary>
        private static int resolvePlayerVersion(string systemId, int? algorithmVersion, int rulesetOnlineId)
        {
            if (algorithmVersion is int explicitVersion)
                return explicitVersion;

            if (rulesetOnlineId >= 0 && systemId is EzSkillSystems.BEATMAP_PPP or EzSkillSystems.PLAYER_PPP)
                return EzPPPSkillVersions.ForRuleset(rulesetOnlineId);

            return EzSkillSystems.ResolveAlgorithmVersion(systemId);
        }

        public EzPlayerRatingSnapshot GetPlayerSsrSnapshot(string username, int keyCount, int? algorithmVersion = null)
        {
            int version = algorithmVersion ?? EzMinaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                var rows = r.All<EzPlayerSkillValue>()
                            .Where(v => v.Username == username
                                        && v.KeyCount == keyCount
                                        && v.SystemId == EzSkillSystems.PLAYER_MINA
                                        && v.RulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID
                                        && v.AlgorithmVersion == version)
                            .ToList();

                if (rows.Count == 0)
                    return new EzPlayerRatingSnapshot();

                var values = rows.ToDictionary(v => v.SkillId, v => v.Value, StringComparer.Ordinal);
                var meta = rows.FirstOrDefault(v => v.SkillId == EzMinaSkillAxis.Overall.ToSsrSkillId()) ?? rows[0];

                return new EzPlayerRatingSnapshot
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
            int version = algorithmVersion ?? EzMinaSkillAlgorithm.VERSION;

            return realmAccess.Run(r =>
            {
                return r.All<EzPlayerSkillValue>()
                        .Where(v => v.Username == username
                                    && v.SystemId == EzSkillSystems.PLAYER_MINA
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
            EzMinaSkillAxisVector vector,
            int analyzedPlays,
            bool provisional = false,
            bool stale = false,
            DateTimeOffset? computedAt = null,
            bool appendHistory = true)
        {
            DateTimeOffset at = computedAt ?? DateTimeOffset.UtcNow;
            int version = EzMinaSkillAlgorithm.VERSION;

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
            EzMinaSkillAxisVector ssr,
            int analyzedPlays,
            bool provisional,
            IReadOnlyList<(DateTimeOffset RecordedAt, EzMinaSkillAxisVector Vector)> historySamples,
            IReadOnlyList<EzPatternRating> patternRatings)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentNullException.ThrowIfNull(historySamples);
            ArgumentNullException.ThrowIfNull(patternRatings);

            DateTimeOffset at = DateTimeOffset.UtcNow;
            int version = EzMinaSkillAlgorithm.VERSION;

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
            EzMinaSkillAxisVector vector,
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
                                        && v.SystemId == EzSkillSystems.PLAYER_MINA
                                        && v.RulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID)
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
                    SystemId = EzSkillSystems.PLAYER_MINA,
                    RulesetOnlineId = EzLocalProfileConstants.MANIA_RULESET_ID,
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
            int version = algorithmVersion ?? EzMinaSkillAlgorithm.VERSION;

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
            int version = EzMinaSkillAlgorithm.VERSION;

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
            IReadOnlyList<(DateTimeOffset RecordedAt, EzMinaSkillAxisVector Vector)> samples)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentNullException.ThrowIfNull(samples);

            int version = EzMinaSkillAlgorithm.VERSION;

            realmAccess.Write(r => replacePlayerSkillHistory(r, username, keyCount, samples, version));
        }

        private static void replacePlayerSkillHistory(
            Realm r,
            string username,
            int keyCount,
            IReadOnlyList<(DateTimeOffset RecordedAt, EzMinaSkillAxisVector Vector)> samples,
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
            int version = algorithmVersion ?? EzMinaSkillAlgorithm.VERSION;

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
        }

        public void ClearChartSkillInfo()
        {
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
            realmAccess.Write(r =>
            {
                var rows = r.All<EzBeatmapSkillValue>()
                            .Where(v => v.SystemId == EzSkillSystems.BEATMAP_PPP)
                            .ToList();

                foreach (var row in rows)
                    r.Remove(row);
            });
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

                    rows[candidate.Hash] = new EzFacetRowState(complete ? EzPPPSkillVersions.FACET_REVISION : 0, false);
                }

                var facet = new EzDataStateFacet
                {
                    Id = "PPP",
                    Facet = EzAnalysisFacet.PPP,
                    CurrentRevision = EzPPPSkillVersions.FACET_REVISION,
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
                int bundle = EzPPPSkillVersions.ForRuleset(rulesetOnlineId);

                if (bundle > 0)
                    return bundle;
            }

            return fallback;
        }

        private bool isCurrentPppChart(int rulesetId, List<EzBeatmapSkillValue>? stored)
        {
            if (stored == null || stored.Count == 0 || !EzPPPSkillCatalog.SupportsRuleset(rulesetId))
                return false;

            if (resolver.ChartRegistry.Get(EzChartMetricProducer.Ppp.Id()) is not { } producer)
                return false;

            int revision = resolver.RevisionOf(EzChartMetricProducer.Ppp.Id(), rulesetId);

            if (revision <= 0)
                return false;

            var current = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var row in stored)
            {
                if (row.AlgorithmVersion == revision)
                    current[row.SkillId] = row.Value;
            }

            // Completeness is the producer's declared axis set for this ruleset, not a second list kept here.
            return producer.IsCompleteFor(EzSkillSystems.UNSLICED_KEY, rulesetId, current);
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

        /// <summary>Drop all player rows for a system across every ruleset and slice.</summary>
        public void ClearPlayerSystemSkills(string username, string systemId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

            realmAccess.Write(r =>
            {
                var existing = r.All<EzPlayerSkillValue>()
                                .Where(v => v.Username == username && v.SystemId == systemId)
                                .ToList();

                foreach (var row in existing)
                    r.Remove(row);
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
        /// Set or clear the stale flag on every stored <see cref="EzPlayerSkillValue"/> row for one player without
        /// changing any value.
        /// <para>
        /// The flag means the player's SQLite slice has moved on since these rows were written - a settled play was
        /// folded in, or the skills pass had to skip one because the chart chain had not rated its chart yet - so the
        /// numbers on screen are still the previous pass's. It is player-scoped rather than per-row because the pass
        /// that clears it re-derives that player wholesale: a row it could not re-derive (SSR values for a keymode
        /// whose plays are gone, say) must not keep reporting work no later pass will pick up.
        /// </para>
        /// <para>
        /// Dan rows (<see cref="EzDanEstimate"/> / <see cref="EzPlayerDanSkillsetValue"/>) carry no stale flag, so
        /// they can only be corrected by an actual recompute.
        /// </para>
        /// </summary>
        /// <returns>Number of rows whose flag changed.</returns>
        public int SetPlayerSkillStale(string username, bool stale)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            // Read first: a refresh pass rewrites its rows unflagged, so the usual clear has nothing to do, and the
            // write transaction (which wakes every change-notification subscriber) is the expensive half.
            bool any = realmAccess.Run(r => r.All<EzPlayerSkillValue>()
                                             .Where(v => v.Username == username && v.Stale != stale)
                                             .AsEnumerable()
                                             .Any());

            if (!any)
                return 0;

            int changed = 0;

            realmAccess.Write(r =>
            {
                foreach (var row in r.All<EzPlayerSkillValue>().Where(v => v.Username == username && v.Stale != stale).ToList())
                {
                    row.Stale = stale;
                    changed++;
                }
            });

            return changed;
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
