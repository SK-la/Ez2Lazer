// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using static System.String;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 本地持久化的 mania analysis 存储。
    ///
    /// 目标：对齐官方“有版本号、可增量补齐”的后台预处理体验，但不写回主谱面库的 Realm（client.realm），
    /// 以降低误操作/迁移导致主库损坏的风险。
    ///
    /// 存储键：BeatmapInfo.ID（Guid）+ BeatmapInfo.Hash（SHA-256）。
    /// - 只要 beatmap 内容变化（Hash 变化），对应条目会自动失效并重算。
    /// - AnalysisVersion 用于你这边算法变更时整体失效。
    ///
    /// 注意：此处使用 SQLite（而不是额外 Realm 文件），因为向 osu.Game 程序集新增 RealmObject 类型
    /// 会改变 client.realm 的 schema 并要求迁移；而 SQLite 独立文件更安全、易恢复。
    /// </summary>
    public class EzAnalysisPersistentStore : IDisposable
    {
        [Flags]
        internal enum MissingDataKind
        {
            None = 0,
            Common = 1 << 0,
            Mania = 1 << 1,
        }

        /// <summary>
        /// One chart's stored analysis, mirroring what <see cref="Beatmaps.BeatmapInfo.EzRatingData"/> holds.
        /// </summary>
        /// <remarks>
        /// A scalar that has never been computed is <c>-1</c> in Realm; here it is <c>null</c>, because the column is
        /// also absent on a branch written before the scalar existed. Both read as "not computed", which is the only
        /// question a reader asks of them.
        /// </remarks>
        public readonly record struct SongsBranchRow(
            Guid BeatmapId,
            string BeatmapHash,
            string BeatmapMd5,
            double SunnySR,
            double? Pp,
            double? OfficialSr = null,
            double? MinaSr = null,
            double? MinaPp = null,
            double? PppSr = null,
            double? PppPp = null,
            bool SunnyPpComputed = false);

        /// <summary>One axis of a system whose result is an axis set rather than one number (CSI, dan).</summary>
        public readonly record struct SongsBranchSkillRow(
            Guid BeatmapId,
            string SystemId,
            int RulesetOnlineId,
            string SkillId,
            double Value,
            int AlgorithmVersion);

        public readonly record struct SongsBranchDescriptor(string DatabasePath, string RelativePath, SongsBranchMetadata Metadata);

        public readonly record struct SourceCollectionSnapshot(
            Guid CollectionId,
            string Name,
            long LastModifiedUnixMilliseconds,
            IReadOnlyList<string> BeatmapMd5Hashes);

        public readonly record struct SongsBranchMetadata(
            int RulesetOnlineId,
            string RulesetShortName,
            string ModsFingerprint,
            string ModsDisplay,
            int BeatmapCount,
            long CreatedAtUnixMilliseconds,
            string DisplayName,
            string ModsJson = "",
            bool HiddenApplied = false,
            Guid SourceCollectionId = default,
            string SourceCollectionName = "",
            long SourceCollectionLastModifiedUnixMilliseconds = 0,
            int SourceCollectionBeatmapCount = 0);

        /// <summary>
        /// 持久化总开关（默认关闭）：未来考虑是否允许用户通过配置关闭此功能以避免额外的磁盘读写。
        /// </summary>
        public static bool Enabled = true;

        public const string LEGACY_DATABASE_FILENAME_PREFIX = "ez-analysis_v";

        public const string SONGS_BRANCH_DATABASE_DIRECTORY = "EzData";

        private const string songs_branch_kind = "songs_branch";
        private const string legacy_sunny_sr_branch_kind = "sunny_sr_branch";

        /// <summary>
        /// Version of the songs branch file layout.
        /// </summary>
        /// <remarks>
        /// v4 mirrors the whole analysis layer instead of only the Sunny star and the official pp: every scalar rating
        /// gets a column, and CSI / dan - which are not scalars - get rows in <see cref="table_songs_branch_skill"/>.
        /// A branch written by an older version is migrated additively and then refreshed, because the columns it
        /// never had read as "not computed".
        /// </remarks>
        private const int songs_branch_schema_version = 4;

        /// <summary>
        /// 主分析库文件版本。仅 kps / KPC 表结构或列统计口径变更时递增。
        /// v7 自 v6 继承 kps 数据并移除 legacy 列；v8 只继承 kps（mania 列统计改为 dense 补列，旧 sparse 行不带过去，之后按谱面懒回填）。
        /// 分支库 schema v1 迁移后需全量重算 ssr/PP；v2 及以后迁移时保留既有版本 meta 并复用结果。
        /// </summary>
        public const int ANALYSIS_VERSION = 8;

        public static string DatabaseFilename => $"{LEGACY_DATABASE_FILENAME_PREFIX}{ANALYSIS_VERSION}.sqlite";

        public string GetMainDatabasePath() => storage.GetFullPath(DatabaseFilename, true);

        /// <summary>
        /// 旧版稳定文件名（中间分支）；任何新版主库首次启动时会从此文件迁移一次。
        /// </summary>
        public const string LEGACY_STABLE_DATABASE_FILENAME = "ez-analysis.sqlite";

        // 列定义（songs branch 快照库）
        private const string col_beatmap_id = "beatmap_id";
        private const string col_beatmap_hash = "beatmap_hash";
        private const string col_beatmap_md5 = "beatmap_md5";
        private const string col_sunny_sr = "sunny_sr";
        private const string col_pp = "pp";
        private const string col_official_sr = "official_sr";
        private const string col_mina_sr = "mina_sr";
        private const string col_mina_pp = "mina_pp";
        private const string col_ppp_sr = "ppp_sr";
        private const string col_ppp_pp = "ppp_pp";

        private const string table_songs_branch_skill = "songs_branch_skill";
        private const string col_skill_beatmap_id = "beatmap_id";
        private const string col_skill_system_id = "system_id";
        private const string col_skill_ruleset_online_id = "ruleset_online_id";
        private const string col_skill_skill_id = "skill_id";
        private const string col_skill_value = "value";
        private const string col_skill_algorithm_version = "algorithm_version";

        private readonly Storage storage;
        private readonly Lock initLock = new Lock();
        private static readonly IReadOnlyDictionary<Guid, double> empty_sunny_sr_values = new Dictionary<Guid, double>();
        private static readonly IReadOnlyDictionary<Guid, double> empty_pp_values = new Dictionary<Guid, double>();

        private bool initialised;
        private bool initialisationFailed;
        private string dbPath = Empty;

        private record PendingWrite(Guid Id, string Hash, string Md5, int RulesetOnlineId, EzAnalysisResult Analysis, long Timestamp);

        private readonly record struct MemoEntry(bool Found, string Hash, string Md5, int RulesetOnlineId, EzAnalysisResult Result);

        private readonly ConcurrentDictionary<Guid, PendingWrite> pendingWrites = new ConcurrentDictionary<Guid, PendingWrite>();

        /// <summary>
        /// Upper bound on <see cref="sharedReadMemo"/> entries. At the bound the oldest inserts are dropped in a
        /// batch, so a long scroll keeps answering for the charts it re-binds rather than emptying the memo and
        /// re-reading every panel.
        /// </summary>
        private const int shared_read_memo_capacity = 2048;

        /// <summary>
        /// Entries dropped at once when the bound is reached. Batching amortises the eviction over many inserts.
        /// </summary>
        private readonly int sharedReadMemoEvictionBatch;

        private readonly ConcurrentDictionary<Guid, MemoEntry> sharedReadMemo = new ConcurrentDictionary<Guid, MemoEntry>();

        /// <summary>
        /// Insertion order for <see cref="sharedReadMemo"/>. A chart that is bound again is naturally the newest
        /// insert for its slot, so first-in-first-out already keeps whatever is on screen.
        /// </summary>
        private readonly ConcurrentQueue<Guid> sharedReadMemoOrder = new ConcurrentQueue<Guid>();

        private long sharedReadMemoGeneration;

        /// <summary>
        /// Charts whose stored row changed, with the write generation that changed it. A memo entry is dropped only
        /// when its chart appears here; dropping the whole memo on every flush made a long write storm re-read every
        /// panel it had already answered.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, long> dirtyEntries = new ConcurrentDictionary<Guid, long>();

        /// <summary>Upper bound on <see cref="dirtyEntries"/>; older stamps are dropped wholesale.</summary>
        private const int dirty_entries_capacity = 16384;
        private CancellationTokenSource? writeCts;
        private Task? backgroundWriterTask;
        private bool isDisposed;

        /// <summary>
        /// Bumped whenever a pending write actually lands in SQLite. A <see cref="ReadSession"/> memoises stored
        /// rows for the length of one aggregation, so it watches this to drop its memo when the data moved under it.
        /// </summary>
        private long writeGeneration;

        // 常量化的表名与 meta key，避免在代码中散落硬编码字符串。
        private const string meta_key_requires_post_migration_refresh = "requires_post_migration_refresh";
        // The system's declared id is the key, so the branch stamp and the Realm applied-version table answer the
        // same question with the same vocabulary.
        private static readonly string meta_key_sunny_sr_version = EzAnalysisSystem.SSR.ToStorageId();
        private static readonly string meta_key_pp_version = EzAnalysisSystem.Official.ToStorageId();
        private const string meta_key_analysis_version = "analysis_version";
        private const string meta_key_kind = "kind";
        private const string meta_key_schema_version = "schema_version";
        private const string meta_key_ruleset_online_id = "ruleset_online_id";
        private const string meta_key_ruleset_short_name = "ruleset_short_name";
        private const string meta_key_mods_fingerprint = "mods_fingerprint";
        private const string meta_key_mods_display = "mods_display";
        private const string meta_key_beatmap_count = "beatmap_count";
        private const string meta_key_created_at = "created_at";
        private const string meta_key_display_name = "display_name";
        private const string meta_key_mods_json = "mods_json";
        private const string meta_key_hidden_applied = "hidden_applied";
        private const string meta_key_source_collection_id = "source_collection_id";
        private const string meta_key_source_collection_name = "source_collection_name";
        private const string meta_key_source_collection_last_modified = "source_collection_last_modified";
        private const string meta_key_source_collection_beatmap_count = "source_collection_beatmap_count";
        private const string meta_key_content_hash = "content_hash";

        // songs branch tables
        private const string table_songs_branch_entry = "songs_branch_entry";
        private const string legacy_table_sunny_sr_branch = "sunny_sr_branch";
        private const string table_songs_branch_hidden_preexisting = "hidden_preexisting_beatmap";
        private const string table_songs_branch_source_collection = "source_collection_beatmap";

        // Meta 表的创建由 ensureMetaTableExists 方法负责，避免在字符串中多处复制 SQL。

        private static bool getMetaBool(SqliteConnection connection, string key)
        {
            string? v = tryGetMeta(connection, key);
            return string.Equals(v, "1", StringComparison.Ordinal);
        }

        public EzAnalysisPersistentStore(Storage storage)
            : this(storage, shared_read_memo_capacity)
        {
        }

        /// <summary>
        /// Builds a store whose read memo is bounded by <paramref name="sharedReadMemoCapacity"/>. The smaller bound
        /// exists so the eviction policy can be exercised without filling two thousand entries.
        /// </summary>
        internal EzAnalysisPersistentStore(Storage storage, int sharedReadMemoCapacity)
        {
            this.storage = storage;
            this.sharedReadMemoCapacity = sharedReadMemoCapacity;
            sharedReadMemoEvictionBatch = Math.Max(1, sharedReadMemoCapacity / 4);
        }

        private readonly int sharedReadMemoCapacity;

        /// <summary>
        /// Creates/migrates the database and starts the background writer. Called once for the session, from the
        /// startup maintenance path and from the first write.
        /// </summary>
        /// <remarks>
        /// A failure is terminal for the session rather than retried: the previous attempt deleted the file and
        /// recursed, which could exhaust the stack while a corrupted file kept failing. Nothing in the analysis
        /// pipeline depends on this store, so leaving it unavailable is the safe outcome.
        /// </remarks>
        public void Initialise()
        {
            if (!Enabled)
                return;

            lock (initLock)
            {
                if (initialised || initialisationFailed)
                    return;

                dbPath = storage.GetFullPath(DatabaseFilename, true);

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

                    Logger.Log($"EzManiaAnalysisPersistentStore path: {dbPath}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);

                    EzAnalysisSchemaManager.TryMigrateFromPreviousMainDatabase(dbPath);

                    using var connection = openConnection();

                    EzAnalysisSchemaManager.InitializeMainDatabase(connection);

                    initialised = true;

                    // Start background writer for pending writes.
                    startBackgroundWriter();
                }
                catch (Exception e)
                {
                    initialisationFailed = true;
                    Logger.Error(e, $"EzManiaAnalysisPersistentStore failed to initialise at {dbPath}; analysis storage is unavailable for this session.", Ez2ConfigManager.LOGGER_NAME);
                }
            }
        }

        /// <summary>
        /// Prepares a read without creating or migrating anything. A read that finds no database simply reports a
        /// miss; schema creation, legacy migration and the rebuild's <c>VACUUM</c> all belong to
        /// <see cref="Initialise"/>, which runs on the maintenance path rather than on a UI read.
        /// </summary>
        private bool tryPrepareRead()
        {
            if (!Enabled || initialisationFailed)
                return false;

            if (initialised)
                return true;

            string path = storage.GetFullPath(DatabaseFilename, true);

            if (!File.Exists(path))
                return false;

            dbPath = path;
            return true;
        }

        public bool TryGet(BeatmapInfo beatmap, out EzAnalysisResult result)
        {
            result = default;

            if (!Enabled)
                return false;

            try
            {
                if (!tryPrepareRead())
                    return false;

                pendingWrites.TryGetValue(beatmap.ID, out var pending);

                using var connection = openConnection();

                if (!tryGetRawData(connection, beatmap, out var storedAnalysis))
                {
                    if (pending is not null && string.Equals(pending.Hash, beatmap.Hash, StringComparison.Ordinal))
                    {
                        result = pending.Analysis;
                        return true;
                    }

                    return false;
                }

                result = pending is not null && string.Equals(pending.Hash, beatmap.Hash, StringComparison.Ordinal)
                    ? mergeAnalysisResult(storedAnalysis, pending.Analysis)
                    : storedAnalysis;

                // Validate the analysis result to ensure it's reasonable
                if (!isValidAnalysisResult(result))
                {
                    Logger.Log($"[EzManiaAnalysisPersistentStore] Invalid analysis result for {beatmap.ID}, ignoring cached data.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                    return false;
                }

                // missingRequiredSunnySR = requireSunnySR && sunnySR == null;

                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TryGet failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        /// <summary>
        /// Open a memoising read scope over the analysis database, or <see langword="null"/> when the store is
        /// disabled. The caller owns the returned session and must dispose it.
        /// </summary>
        /// <remarks>
        /// A bulk aggregation walks thousands of scores but only a fraction of distinct charts. Without a session
        /// each score re-opens the database and re-parses that chart's KPS columns/JSON; with one, each chart is
        /// read and parsed once for the whole run. Not thread-safe: one session belongs to one worker.
        /// </remarks>
        public ReadSession? OpenReadSession()
        {
            if (!tryPrepareRead())
                return null;

            return new ReadSession(this, openConnection());
        }

        /// <summary>
        /// Memoising counterpart of <see cref="TryGet"/>, for readers that revisit the same charts but cannot hold a
        /// session: song-select panels re-read a chart's KPS columns and JSON every time a carousel panel is bound,
        /// and scrolling back re-binds the exact same charts.
        /// </summary>
        /// <remarks>
        /// Thread-safe, and deliberately does not keep a connection open — the database file gets deleted and replaced
        /// when the user switches songs branch. Shares <see cref="ReadSession"/>'s validity rules, so a memo hit is
        /// indistinguishable from an uncached read. Bounded, because the key space is whatever the user scrolls past.
        /// </remarks>
        public bool TryGetMemoised(BeatmapInfo beatmap, out EzAnalysisResult result)
        {
            result = default;

            if (!Enabled)
                return false;

            try
            {
                ArgumentNullException.ThrowIfNull(beatmap);

                if (tryReadMemo(sharedReadMemo, ref sharedReadMemoGeneration, beatmap, out var entry))
                    return resolveMemoEntry(beatmap, entry, out result);

                if (!tryPrepareRead())
                    return false;

                using var connection = openConnection();
                bool found = tryGetRawData(connection, beatmap, out var stored);

                entry = new MemoEntry(found, beatmap.Hash, beatmap.MD5Hash, beatmap.Ruleset.OnlineID, stored);

                if (!sharedReadMemo.ContainsKey(beatmap.ID) && sharedReadMemo.Count >= sharedReadMemoCapacity)
                    evictOldestSharedReadMemoEntries();

                sharedReadMemo[beatmap.ID] = entry;
                sharedReadMemoOrder.Enqueue(beatmap.ID);

                return resolveMemoEntry(beatmap, entry, out result);
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TryGetMemoised failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        /// <summary>
        /// Drops the oldest inserts until the memo is back under its target. Ids already dropped by the dirty-entry
        /// path are skipped rather than counted, so a batch always makes progress.
        /// </summary>
        private void evictOldestSharedReadMemoEntries()
        {
            int target = Math.Max(1, sharedReadMemoCapacity - sharedReadMemoEvictionBatch);
            while (sharedReadMemo.Count > target && sharedReadMemoOrder.TryDequeue(out Guid id))
                sharedReadMemo.TryRemove(id, out _);

            // The order queue can also outlive its entries (a full memo drop, a chart replaced under a new hash).
            // Leaving the leftovers would only cost memory, so they are trimmed once the memo is clear.
            if (sharedReadMemo.IsEmpty)
            {
                while (sharedReadMemoOrder.TryDequeue(out _))
                {
                }
            }
        }

        /// <summary>
        /// Entries currently held by the shared read memo. Exposed for tests only: the bound and its eviction
        /// policy are what is being pinned, and both are otherwise invisible from outside.
        /// </summary>
        internal int SharedReadMemoCount => sharedReadMemo.Count;

        /// <summary>
        /// Looks up a memo entry whose fingerprint still matches the chart, dropping the whole memo first if a pending
        /// write has since landed. Returns <see langword="false"/> when the caller must read SQLite.
        /// </summary>
        private bool tryReadMemo(IDictionary<Guid, MemoEntry> memo, ref long observedGeneration, BeatmapInfo beatmap, out MemoEntry entry)
        {
            long generation = Interlocked.Read(ref writeGeneration);

            // A flushed batch can add or replace a row the memo already answered from; drop only those charts.
            if (generation != observedGeneration)
            {
                if (dirtyEntries.IsEmpty)
                {
                    // The change log was pruned, so we cannot tell which charts moved. Fall back to a full drop.
                    memo.Clear();
                }
                else
                {
                    foreach (var (id, stamp) in dirtyEntries)
                    {
                        if (stamp <= generation)
                            memo.Remove(id);
                    }
                }

                observedGeneration = generation;
            }

            if (memo.TryGetValue(beatmap.ID, out var existing)
                && string.Equals(existing.Hash, beatmap.Hash, StringComparison.Ordinal)
                && string.Equals(existing.Md5, beatmap.MD5Hash, StringComparison.Ordinal)
                && existing.RulesetOnlineId == beatmap.Ruleset.OnlineID)
            {
                entry = existing;
                return true;
            }

            entry = default;
            return false;
        }

        /// <summary>
        /// Records the charts a committed batch wrote, so only their memo entries are dropped.
        /// </summary>
        private void markDirty(IEnumerable<Guid> ids, long generation)
        {
            foreach (Guid id in ids)
                dirtyEntries[id] = generation;

            if (dirtyEntries.Count <= dirty_entries_capacity)
                return;

            // Rare: the change log outgrew its bound. Clearing it makes readers fall back to a full memo drop,
            // which over-invalidates (a re-read) instead of missing an invalidation (stale rows).
            dirtyEntries.Clear();
        }

        /// <summary>
        /// Pending-write overlay plus the validity gate, applied on every read and never memoised.
        /// </summary>
        private bool resolveMemoEntry(BeatmapInfo beatmap, in MemoEntry entry, out EzAnalysisResult result)
        {
            result = default;

            if (!entry.Found)
            {
                // A pending write may exist for a chart with no stored row yet (the uncached TryGet path serves
                // exactly this case). Checked on every read, never memoised, so it cannot be missed.
                if (pendingWrites.TryGetValue(beatmap.ID, out var pending)
                    && string.Equals(pending.Hash, beatmap.Hash, StringComparison.Ordinal))
                {
                    result = pending.Analysis;
                    return true;
                }

                return false;
            }

            result = entry.Result;

            if (pendingWrites.TryGetValue(beatmap.ID, out var overlay)
                && string.Equals(overlay.Hash, beatmap.Hash, StringComparison.Ordinal))
                result = mergeAnalysisResult(result, overlay.Analysis);

            if (!isValidAnalysisResult(result))
            {
                Logger.Log($"[EzManiaAnalysisPersistentStore] Invalid analysis result for {beatmap.ID}, ignoring cached data.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                return false;
            }

            return true;
        }

        /// <summary>
        /// One connection + a per-chart memo for a single aggregation pass. Mirrors <see cref="TryGet"/>'s
        /// validation (hash / md5 / ruleset identity, pending-write overlay, validity gate) so a memo hit is
        /// indistinguishable from an uncached read.
        /// </summary>
        public sealed class ReadSession : IDisposable
        {
            private readonly EzAnalysisPersistentStore owner;
            private readonly SqliteConnection connection;
            private readonly Dictionary<Guid, MemoEntry> memo = new Dictionary<Guid, MemoEntry>();
            private long observedGeneration;

            internal ReadSession(EzAnalysisPersistentStore owner, SqliteConnection connection)
            {
                this.owner = owner;
                this.connection = connection;
                observedGeneration = Interlocked.Read(ref owner.writeGeneration);
            }

            /// <summary>
            /// Memoised counterpart of <see cref="TryGet"/>. A negative is memoised too, because on a large library
            /// the un-analysed charts are exactly the ones hit repeatedly by chart-blind score ordering.
            /// </summary>
            public bool TryGet(BeatmapInfo beatmap, out EzAnalysisResult result)
            {
                result = default;
                ArgumentNullException.ThrowIfNull(beatmap);

                try
                {
                    if (!owner.tryReadMemo(memo, ref observedGeneration, beatmap, out var entry))
                    {
                        bool found = owner.tryGetRawData(connection, beatmap, out var stored);
                        entry = new MemoEntry(found, beatmap.Hash, beatmap.MD5Hash, beatmap.Ruleset.OnlineID, stored);
                        memo[beatmap.ID] = entry;
                    }

                    return owner.resolveMemoEntry(beatmap, entry, out result);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "EzManiaAnalysisPersistentStore ReadSession.TryGet failed.", Ez2ConfigManager.LOGGER_NAME);
                    return false;
                }
            }

            public void Dispose() => connection.Dispose();
        }

        /// <summary>
        /// NoMod 基线 ssr：从 <see cref="BeatmapInfo.EzRatingData"/> 聚合（非 SQLite）。
        /// </summary>
        public IReadOnlyDictionary<Guid, double> GetBaselineSunnySRFromRealm(IEnumerable<BeatmapInfo> beatmaps)
        {
            var beatmapList = beatmaps.Distinct().ToList();

            if (beatmapList.Count == 0)
                return empty_sunny_sr_values;

            var resolvedValues = new Dictionary<Guid, double>(beatmapList.Count);

            foreach (var beatmap in beatmapList)
            {
                if (beatmap.EzRatingData.SunnySR >= 0)
                    resolvedValues[beatmap.ID] = beatmap.EzRatingData.SunnySR;
            }

            return resolvedValues;
        }

        /// <summary>
        /// 对比新计算结果和 SQLite 中的旧数据，如果有差异则更新。
        /// 主要场景：KPS / mania 列统计变化（算法修复等）。基线 ssr/ PP 已迁到 Realm，不在此比较。
        /// </summary>
        public void StoreIfDifferent(BeatmapInfo beatmap, EzAnalysisResult analysis)
        {
            if (!Enabled)
                return;

            var commonSummary = analysis.CommonSummary;

            // Validate the analysis result before storing
            if (!isValidAnalysisResult(analysis))
            {
                Logger.Log($"[EzManiaAnalysisPersistentStore] Refusing to store invalid analysis result for {beatmap.ID}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                return;
            }

            // 跳过失败的分析结果
            if (commonSummary == null)
                return;

            try
            {
                Initialise();

                using var connection = openConnection();

                // 尝试从 SQLite 读取旧数据
                if (!tryGetRawData(connection, beatmap, out var storedAnalysis))
                {
                    // 缓存不存在，直接存储
                    Store(beatmap, analysis);
                    return;
                }

                // 对比两个结果是否有差异
                MissingDataKind filledMissingData = GetMissingData(storedAnalysis, beatmap.Ruleset.OnlineID) &
                                                    ~GetMissingData(analysis, beatmap.Ruleset.OnlineID);

                if (filledMissingData != MissingDataKind.None || hasDifference(storedAnalysis, analysis))
                {
                    Logger.Log($"[EzManiaAnalysisPersistentStore] Data difference detected for {beatmap.ID}, updating SQLite.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                    Store(beatmap, analysis);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore StoreIfDifferent failed.");
            }
        }

        /// <summary>
        /// 从数据库读取原始数据（不验证 hash/version）。
        /// </summary>
        private bool tryGetRawData(SqliteConnection connection, BeatmapInfo beatmap, out EzAnalysisResult result)
        {
            result = default;

            try
            {
                pendingWrites.TryGetValue(beatmap.ID, out var pending);

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $@"
SELECT entry.{EzAnalysisSchemaManager.COL_BEATMAP_HASH},
       entry.{EzAnalysisSchemaManager.COL_BEATMAP_MD5},
       entry.{EzAnalysisSchemaManager.COL_RULESET_ONLINE_ID},
       entry.{EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT},
       entry.{EzAnalysisSchemaManager.COL_AVERAGE_KPS},
       entry.{EzAnalysisSchemaManager.COL_MAX_KPS},
       entry.{EzAnalysisSchemaManager.COL_KPS_LIST_JSON},
       mania.{EzAnalysisSchemaManager.COL_UPDATED_AT},
       mania.{EzAnalysisSchemaManager.COL_COLUMN_COUNTS_JSON},
       mania.{EzAnalysisSchemaManager.COL_HOLD_NOTE_COUNTS_JSON}
FROM {EzAnalysisSchemaManager.TABLE_ENTRY} entry
LEFT JOIN {EzAnalysisSchemaManager.TABLE_MANIA} mania
    ON mania.{EzAnalysisSchemaManager.COL_BEATMAP_ID} = entry.{EzAnalysisSchemaManager.COL_BEATMAP_ID}
WHERE entry.{EzAnalysisSchemaManager.COL_BEATMAP_ID} = $id
LIMIT 1;
";
                    cmd.Parameters.AddWithValue("$id", beatmap.ID.ToString());

                    using var reader = cmd.ExecuteReader();

                    if (!reader.Read())
                    {
                        if (pending is not null && string.Equals(pending.Hash, beatmap.Hash, StringComparison.Ordinal))
                        {
                            result = pending.Analysis;
                            return true;
                        }

                        return false;
                    }

                    string storedHash = reader.GetString(0);
                    string storedMd5 = reader.GetString(1);
                    int storedRulesetOnlineId = reader.GetInt32(2);
                    long commonUpdatedAt = reader.GetInt64(3);

                    if (!string.Equals(storedHash, beatmap.Hash, StringComparison.Ordinal))
                        return false;

                    if (!IsNullOrEmpty(storedMd5) && !string.Equals(storedMd5, beatmap.MD5Hash, StringComparison.Ordinal))
                        return false;

                    if (storedRulesetOnlineId != beatmap.Ruleset.OnlineID)
                        return false;

                    if (commonUpdatedAt <= 0)
                        return false;

                    double averageKps = reader.GetDouble(4);
                    double maxKps = reader.GetDouble(5);
                    string kpsListJson = reader.GetString(6);
                    // 主库仅持久化 kps/KPC；ssr/PP 基线由 Realm 字段提供，不在此 DTO 中注入。
                    long maniaUpdatedAt = reader.IsDBNull(7) ? 0 : reader.GetInt64(7);

                    string columnCountsJson = reader.IsDBNull(8) ? "{}" : reader.GetString(8);
                    string holdNoteCountsJson = reader.IsDBNull(9) ? "{}" : reader.GetString(9);

                    var columnCounts = JsonSerializer.Deserialize<Dictionary<int, int>>(columnCountsJson) ?? new Dictionary<int, int>();
                    var holdNoteCounts = JsonSerializer.Deserialize<Dictionary<int, int>>(holdNoteCountsJson) ?? new Dictionary<int, int>();
                    var kpsList = JsonSerializer.Deserialize<List<double>>(kpsListJson) ?? new List<double>();

                    EzManiaSummary? maniaSummary = storedRulesetOnlineId == 3 && maniaUpdatedAt > 0
                        ? new EzManiaSummary(columnCounts, holdNoteCounts, sunnySR: null)
                        : null;

                    result = new EzAnalysisResult(new KpsSummary(averageKps, maxKps, kpsList), maniaSummary);

                    if (pending is not null && string.Equals(pending.Hash, beatmap.Hash, StringComparison.Ordinal))
                        result = mergeAnalysisResult(result, pending.Analysis);

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 比较两个分析结果是否有差异（仅 kps / KPC；ssr/ PP 来自 Realm，不在此比较）。
        /// </summary>
        private bool hasDifference(EzAnalysisResult stored, EzAnalysisResult computed)
        {
            var storedCommonSummary = stored.CommonSummary;
            var computedCommonSummary = computed.CommonSummary;
            var storedManiaSummary = stored.ManiaSummary;
            var computedManiaSummary = computed.ManiaSummary;

            // 检查 KPS 相关数据
            if (!stored.AverageKps.Equals(computed.AverageKps) || !stored.MaxKps.Equals(computed.MaxKps))
                return true;

            if (!(storedCommonSummary?.KpsList ?? Array.Empty<double>()).SequenceEqual(computedCommonSummary?.KpsList ?? Array.Empty<double>()))
                return true;

            var storedColumnCounts = storedManiaSummary?.ColumnCounts ?? new Dictionary<int, int>();
            var computedColumnCounts = computedManiaSummary?.ColumnCounts ?? new Dictionary<int, int>();

            // 检查列统计
            if (storedColumnCounts.Count != computedColumnCounts.Count)
                return true;

            foreach (var kvp in computedColumnCounts)
            {
                if (!storedColumnCounts.TryGetValue(kvp.Key, out int storedCount) || storedCount != kvp.Value)
                    return true;
            }

            bool storedHasHolds = storedManiaSummary?.HasHoldNoteCounts ?? false;
            bool computedHasHolds = computedManiaSummary?.HasHoldNoteCounts ?? false;

            if (storedHasHolds != computedHasHolds)
                return true;

            if (!storedHasHolds)
                return false;

            var storedHoldNoteCounts = storedManiaSummary!.Value.HoldNoteCounts;
            var computedHoldNoteCounts = computedManiaSummary!.Value.HoldNoteCounts;

            // 检查长按统计
            if (storedHoldNoteCounts.Count != computedHoldNoteCounts.Count)
                return true;

            foreach (var kvp in computedHoldNoteCounts)
            {
                if (!storedHoldNoteCounts.TryGetValue(kvp.Key, out int storedCount) || storedCount != kvp.Value)
                    return true;
            }

            return false;
        }

        public void Store(BeatmapInfo beatmap, EzAnalysisResult analysis)
        {
            if (!Enabled)
                return;

            var commonSummary = analysis.CommonSummary;

            // Validate the analysis result before storing
            if (!isValidAnalysisResult(analysis))
            {
                Logger.Log($"[EzManiaAnalysisPersistentStore] Refusing to store invalid analysis result for {beatmap.ID}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                return;
            }

            // 跳过无效的分析结果（commonSummary 为 null）。
            // 允许保存 0-note 谱面的分析结果（KpsList 为空），避免启动时重复预热。
            if (commonSummary == null)
                return;

            // Enqueue pending write and return quickly. Background writer will flush to SQLite.
            try
            {
                Initialise();

                var pending = snapshotPendingWrite(beatmap, analysis);
                pendingWrites[pending.Id] = pending;
            }
            catch (Exception e)
            {
                // If enqueue fails for some reason, fallback to synchronous write to avoid data loss.
                Logger.Error(e, "EzManiaAnalysisPersistentStore enqueue Store failed, falling back to sync write.");

                try
                {
                    // synchronous fallback
                    Initialise();
                    using var connection = openConnection();
                    writePendingEntryToConnection(connection, snapshotPendingWrite(beatmap, analysis));
                    markDirty(new[] { beatmap.ID }, Interlocked.Increment(ref writeGeneration));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "EzManiaAnalysisPersistentStore Store fallback failed.");
                }
            }
        }

        public bool NeedsOnDemandBackfill(BeatmapInfo beatmap)
        {
            if (!Enabled)
                return false;

            EzAnalysisResult? storedAnalysis = TryGet(beatmap, out var existingAnalysis)
                ? existingAnalysis
                : null;

            return GetMissingData(storedAnalysis, beatmap.Ruleset.OnlineID) != MissingDataKind.None;
        }

        public IReadOnlyList<Guid> GetBeatmapsNeedingRecompute(IEnumerable<(Guid id, string hash, int rulesetOnlineId)> beatmaps) => GetBeatmapsNeedingRecompute(beatmaps, progress: null);

        public IReadOnlyList<Guid> GetBeatmapsNeedingRecompute(IEnumerable<(Guid id, string hash, int rulesetOnlineId)> beatmaps, Action<int, int>? progress)
        {
            if (!Enabled)
                return Array.Empty<Guid>();

            try
            {
                Initialise();

                var beatmapList = beatmaps as IList<(Guid id, string hash, int rulesetOnlineId)> ?? beatmaps.ToList();

                var existing = new Dictionary<Guid, (string hash, int rulesetOnlineId, long commonUpdatedAt)>();
                var maniaUpdated = new HashSet<Guid>();

                using (var connection = openConnection())
                {
                    using (var entryCommand = connection.CreateCommand())
                    {
                        entryCommand.CommandText = $@"
SELECT {EzAnalysisSchemaManager.COL_BEATMAP_ID},
       {EzAnalysisSchemaManager.COL_BEATMAP_HASH},
       {EzAnalysisSchemaManager.COL_RULESET_ONLINE_ID},
       {EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT}
FROM {EzAnalysisSchemaManager.TABLE_ENTRY};
";

                        using var reader = entryCommand.ExecuteReader();

                        while (reader.Read())
                        {
                            if (!Guid.TryParse(reader.GetString(0), out var id))
                                continue;

                            existing[id] = (reader.GetString(1), reader.GetInt32(2), reader.GetInt64(3));
                        }
                    }

                    using (var maniaCommand = connection.CreateCommand())
                    {
                        maniaCommand.CommandText = $@"
SELECT {EzAnalysisSchemaManager.COL_BEATMAP_ID}
FROM {EzAnalysisSchemaManager.TABLE_MANIA}
WHERE {EzAnalysisSchemaManager.COL_UPDATED_AT} > 0;
";

                        using var maniaReader = maniaCommand.ExecuteReader();

                        while (maniaReader.Read())
                        {
                            if (Guid.TryParse(maniaReader.GetString(0), out var id))
                                maniaUpdated.Add(id);
                        }
                    }
                }

                List<Guid> needing = new List<Guid>();

                int processed = 0;
                int total = beatmapList.Count;

                foreach (var (id, hash, rulesetOnlineId) in beatmapList)
                {
                    processed++;

                    if (processed == 1 || processed % 200 == 0)
                        progress?.Invoke(processed, total);

                    if (!existing.TryGetValue(id, out var row))
                    {
                        needing.Add(id);
                        continue;
                    }

                    MissingDataKind missingData = getMissingData(row.commonUpdatedAt, maniaUpdated.Contains(id), rulesetOnlineId);

                    if (!string.Equals(row.hash, hash, StringComparison.Ordinal)
                        || row.rulesetOnlineId != rulesetOnlineId
                        || missingData != MissingDataKind.None)
                        needing.Add(id);
                }

                progress?.Invoke(total, total);

                // 可选清理：删除已不存在的条目（避免无限增长）。
                // 这里用 HashSet 做 membership 判断，避免每条都查库。
                HashSet<Guid> live = beatmapList.Select(b => b.id).ToHashSet();
                var toDelete = existing.Keys.Where(id => !live.Contains(id)).ToList();

                if (toDelete.Count > 0)
                {
                    using var connection = openConnection();
                    using var transaction = connection.BeginTransaction();
                    using var deleteMania = connection.CreateCommand();
                    deleteMania.Transaction = transaction;
                    deleteMania.CommandText = $"DELETE FROM {EzAnalysisSchemaManager.TABLE_MANIA} WHERE {EzAnalysisSchemaManager.COL_BEATMAP_ID} = $id;";

                    using var deleteEntry = connection.CreateCommand();
                    deleteEntry.Transaction = transaction;
                    deleteEntry.CommandText = $"DELETE FROM {EzAnalysisSchemaManager.TABLE_ENTRY} WHERE {EzAnalysisSchemaManager.COL_BEATMAP_ID} = $id;";

                    var idParam = deleteEntry.CreateParameter();
                    idParam.ParameterName = "$id";
                    deleteEntry.Parameters.Add(idParam);

                    var maniaIdParam = deleteMania.CreateParameter();
                    maniaIdParam.ParameterName = "$id";
                    deleteMania.Parameters.Add(maniaIdParam);

                    foreach (var id in toDelete)
                    {
                        idParam.Value = id.ToString();
                        maniaIdParam.Value = id.ToString();
                        deleteMania.ExecuteNonQuery();
                        deleteEntry.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }

                return needing;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetBeatmapsNeedingRecompute failed.");
                return Array.Empty<Guid>();
            }
        }

        public string CreateSongsBranchDatabasePath(string rulesetShortName)
        {
            string safeRulesetName = IsNullOrWhiteSpace(rulesetShortName) ? "ruleset" : rulesetShortName.Trim();
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            string uniqueSuffix = Guid.NewGuid().ToString("N")[..8];

            return storage.GetFullPath(Path.Combine(SONGS_BRANCH_DATABASE_DIRECTORY, $"songs_{safeRulesetName}_{timestamp}_{uniqueSuffix}.sqlite"), true);
        }

        public string CreateSongsBranchDatabasePath(SongsBranchMetadata metadata)
        {
            string safeCollectionName = createSafeBranchDisplayName(IsNullOrWhiteSpace(metadata.SourceCollectionName) ? "collection" : metadata.SourceCollectionName);
            string safeModsDisplay = createSafeBranchModsDisplay(metadata.ModsDisplay);
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            string uniqueSuffix = Guid.NewGuid().ToString("N")[..8];

            return storage.GetFullPath(Path.Combine(SONGS_BRANCH_DATABASE_DIRECTORY, $"songs_{safeCollectionName}_{safeModsDisplay}_{timestamp}_{uniqueSuffix}.sqlite"), true);
        }

        public void StoreSongsBranch(string databasePath, SongsBranchMetadata metadata, IEnumerable<SongsBranchRow> rows, SourceCollectionSnapshot? sourceCollection = null,
                                     int sunnySRAlgorithmVersion = 0, int ppAlgorithmVersion = 0, CancellationToken cancellationToken = default)
            => StoreSongsBranch(databasePath, metadata, rows, Array.Empty<SongsBranchSkillRow>(), sourceCollection,
                sunnySRAlgorithmVersion, ppAlgorithmVersion, cancellationToken);

        public void StoreSongsBranch(string databasePath, SongsBranchMetadata metadata, IEnumerable<SongsBranchRow> rows, IEnumerable<SongsBranchSkillRow> skillRows,
                                     SourceCollectionSnapshot? sourceCollection = null,
                                     int sunnySRAlgorithmVersion = 0, int ppAlgorithmVersion = 0, CancellationToken cancellationToken = default)
        {
            if (!Enabled)
                return;

            string fullPath = Path.GetFullPath(databasePath);
            bool success = false;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                // songs branch 是独立导出的快照库，这里允许整库重建；主分析库补写仍然按 beatmap 增量 upsert。
                if (File.Exists(fullPath))
                    File.Delete(fullPath);

                using var connection = openConnection(fullPath);

                cancellationToken.ThrowIfCancellationRequested();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA temp_store=MEMORY;
";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = connection.CreateCommand())
                {
                    ensureMetaTableExists(connection);

                    cmd.CommandText = $@"
CREATE TABLE IF NOT EXISTS {table_songs_branch_entry} (
    {col_beatmap_id} TEXT PRIMARY KEY,
    {col_beatmap_hash} TEXT NOT NULL,
    {col_beatmap_md5} TEXT NOT NULL,
    {col_sunny_sr} REAL NOT NULL,
    {col_pp} REAL NULL,
    {col_official_sr} REAL NULL,
    {col_mina_sr} REAL NULL,
    {col_mina_pp} REAL NULL,
    {col_ppp_sr} REAL NULL,
    {col_ppp_pp} REAL NULL
);

-- The systems whose result is an axis set rather than one number. They are rows, not columns, so adding an axis to a
-- system does not change this schema.
CREATE TABLE IF NOT EXISTS {table_songs_branch_skill} (
    {col_skill_beatmap_id} TEXT NOT NULL,
    {col_skill_system_id} TEXT NOT NULL,
    {col_skill_ruleset_online_id} INTEGER NOT NULL,
    {col_skill_skill_id} TEXT NOT NULL,
    {col_skill_value} REAL NOT NULL,
    {col_skill_algorithm_version} INTEGER NOT NULL,
    PRIMARY KEY ({col_skill_beatmap_id}, {col_skill_system_id}, {col_skill_ruleset_online_id}, {col_skill_skill_id})
);

CREATE INDEX IF NOT EXISTS idx_songs_branch_skill_system
    ON {table_songs_branch_skill}({col_skill_system_id}, {col_skill_ruleset_online_id});

CREATE TABLE IF NOT EXISTS {table_songs_branch_hidden_preexisting} (
    {col_beatmap_id} TEXT PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS {table_songs_branch_source_collection} (
    {col_beatmap_md5} TEXT PRIMARY KEY
);
";
                    cmd.ExecuteNonQuery();
                }

                long sourceCollectionLastModified = sourceCollection?.LastModifiedUnixMilliseconds ?? metadata.SourceCollectionLastModifiedUnixMilliseconds;
                int sourceCollectionBeatmapCount = sourceCollection?.BeatmapMd5Hashes.Count ?? metadata.SourceCollectionBeatmapCount;

                setMeta(connection, meta_key_kind, songs_branch_kind);
                setMeta(connection, meta_key_schema_version, songs_branch_schema_version.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, meta_key_analysis_version, ANALYSIS_VERSION.ToString(CultureInfo.InvariantCulture));

                if (sunnySRAlgorithmVersion > 0)
                    setMeta(connection, meta_key_sunny_sr_version, sunnySRAlgorithmVersion.ToString(CultureInfo.InvariantCulture));

                if (ppAlgorithmVersion > 0)
                    setMeta(connection, meta_key_pp_version, ppAlgorithmVersion.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, meta_key_ruleset_online_id, metadata.RulesetOnlineId.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, meta_key_ruleset_short_name, metadata.RulesetShortName);
                setMeta(connection, meta_key_mods_fingerprint, metadata.ModsFingerprint);
                setMeta(connection, meta_key_mods_display, metadata.ModsDisplay);
                setMeta(connection, meta_key_beatmap_count, metadata.BeatmapCount.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, meta_key_created_at, metadata.CreatedAtUnixMilliseconds.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, meta_key_display_name, metadata.DisplayName);
                setMeta(connection, meta_key_mods_json, metadata.ModsJson);
                setMeta(connection, meta_key_hidden_applied, metadata.HiddenApplied ? "1" : "0");
                setMeta(connection, meta_key_source_collection_id, metadata.SourceCollectionId == Guid.Empty ? Empty : metadata.SourceCollectionId.ToString());
                setMeta(connection, meta_key_source_collection_name, metadata.SourceCollectionName);
                setMeta(connection, meta_key_source_collection_last_modified, sourceCollectionLastModified.ToString(CultureInfo.InvariantCulture));
                setMeta(connection, meta_key_source_collection_beatmap_count, sourceCollectionBeatmapCount.ToString(CultureInfo.InvariantCulture));

                IReadOnlyList<string> contentHashMd5s = sourceCollection?.BeatmapMd5Hashes
                                                                        .Where(hash => !IsNullOrWhiteSpace(hash))
                                                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                                                        .OrderBy(hash => hash, StringComparer.OrdinalIgnoreCase)
                                                                        .ToList()
                                                        ?? (IReadOnlyList<string>)Array.Empty<string>();

                setMeta(connection, meta_key_content_hash, ComputeSongsBranchContentHash(
                    metadata.SourceCollectionId == Guid.Empty ? sourceCollection?.CollectionId ?? Guid.Empty : metadata.SourceCollectionId,
                    sourceCollectionLastModified,
                    sourceCollectionBeatmapCount,
                    contentHashMd5s,
                    sunnySRAlgorithmVersion > 0 ? sunnySRAlgorithmVersion : 0,
                    ppAlgorithmVersion > 0 ? ppAlgorithmVersion : 0));

                using var transaction = connection.BeginTransaction();

                if (sourceCollection is SourceCollectionSnapshot sourceCollectionSnapshot)
                {
                    using var insertSourceCollection = connection.CreateCommand();
                    insertSourceCollection.Transaction = transaction;
                    insertSourceCollection.CommandText = $@"
INSERT INTO {table_songs_branch_source_collection}({col_beatmap_md5})
VALUES($md5)
ON CONFLICT({col_beatmap_md5}) DO NOTHING;
";

                    var sourceCollectionMd5Param = insertSourceCollection.CreateParameter();
                    sourceCollectionMd5Param.ParameterName = "$md5";
                    insertSourceCollection.Parameters.Add(sourceCollectionMd5Param);

                    foreach (string beatmapMd5 in contentHashMd5s)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        sourceCollectionMd5Param.Value = beatmapMd5;
                        insertSourceCollection.ExecuteNonQuery();
                    }
                }

                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = $@"
INSERT INTO {table_songs_branch_entry}(
    {col_beatmap_id},
    {col_beatmap_hash},
    {col_beatmap_md5},
    {col_sunny_sr},
    {col_pp},
    {col_official_sr},
    {col_mina_sr},
    {col_mina_pp},
    {col_ppp_sr},
    {col_ppp_pp}
)
VALUES(
    $id,
    $hash,
    $md5,
    $sunny_sr,
    $pp,
    $official_sr,
    $mina_sr,
    $mina_pp,
    $ppp_sr,
    $ppp_pp
)
ON CONFLICT({col_beatmap_id}) DO UPDATE SET
    {col_beatmap_hash} = excluded.{col_beatmap_hash},
    {col_beatmap_md5} = excluded.{col_beatmap_md5},
    {col_sunny_sr} = excluded.{col_sunny_sr},
    {col_pp} = COALESCE(excluded.{col_pp}, {table_songs_branch_entry}.{col_pp}),
    {col_official_sr} = COALESCE(excluded.{col_official_sr}, {table_songs_branch_entry}.{col_official_sr}),
    {col_mina_sr} = COALESCE(excluded.{col_mina_sr}, {table_songs_branch_entry}.{col_mina_sr}),
    {col_mina_pp} = COALESCE(excluded.{col_mina_pp}, {table_songs_branch_entry}.{col_mina_pp}),
    {col_ppp_sr} = COALESCE(excluded.{col_ppp_sr}, {table_songs_branch_entry}.{col_ppp_sr}),
    {col_ppp_pp} = COALESCE(excluded.{col_ppp_pp}, {table_songs_branch_entry}.{col_ppp_pp});
";

                var idParam = insert.CreateParameter();
                idParam.ParameterName = "$id";
                insert.Parameters.Add(idParam);

                var hashParam = insert.CreateParameter();
                hashParam.ParameterName = "$hash";
                insert.Parameters.Add(hashParam);

                var md5Param = insert.CreateParameter();
                md5Param.ParameterName = "$md5";
                insert.Parameters.Add(md5Param);

                var sunnySRParam = insert.CreateParameter();
                sunnySRParam.ParameterName = "$sunny_sr";
                insert.Parameters.Add(sunnySRParam);

                var ppParam = insert.CreateParameter();
                ppParam.ParameterName = "$pp";
                insert.Parameters.Add(ppParam);

                var officialSrParam = insert.CreateParameter();
                officialSrParam.ParameterName = "$official_sr";
                insert.Parameters.Add(officialSrParam);

                var minaSrParam = insert.CreateParameter();
                minaSrParam.ParameterName = "$mina_sr";
                insert.Parameters.Add(minaSrParam);

                var minaPpParam = insert.CreateParameter();
                minaPpParam.ParameterName = "$mina_pp";
                insert.Parameters.Add(minaPpParam);

                var pppSrParam = insert.CreateParameter();
                pppSrParam.ParameterName = "$ppp_sr";
                insert.Parameters.Add(pppSrParam);

                var pppPpParam = insert.CreateParameter();
                pppPpParam.ParameterName = "$ppp_pp";
                insert.Parameters.Add(pppPpParam);

                foreach (var row in rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    idParam.Value = row.BeatmapId.ToString();
                    hashParam.Value = row.BeatmapHash;
                    md5Param.Value = row.BeatmapMd5;
                    sunnySRParam.Value = row.SunnySR;
                    ppParam.Value = valueOrNull(row.Pp);
                    officialSrParam.Value = valueOrNull(row.OfficialSr);
                    minaSrParam.Value = valueOrNull(row.MinaSr);
                    minaPpParam.Value = valueOrNull(row.MinaPp);
                    pppSrParam.Value = valueOrNull(row.PppSr);
                    pppPpParam.Value = valueOrNull(row.PppPp);
                    insert.ExecuteNonQuery();
                }

                writeSkillRows(connection, transaction, skillRows, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                transaction.Commit();
                success = true;
            }
            finally
            {
                if (!success)
                {
                    try
                    {
                        SqliteConnection.ClearAllPools();

                        if (File.Exists(fullPath))
                            File.Delete(fullPath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static object valueOrNull(double? value) => value is double actual ? actual : DBNull.Value;

        /// <summary>
        /// Reads a nullable column, treating "absent" and "not computed" as the same answer.
        /// </summary>
        private static double? readNullableDouble(SqliteDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);

        /// <summary>
        /// Replaces the axis rows of the systems whose result is not a scalar.
        /// </summary>
        /// <remarks>
        /// Replaced rather than merged: a chart's axis set is the complete answer of one compute, so a row left from an
        /// earlier set (an axis the system no longer produces) would be read as a current axis of the chart.
        /// </remarks>
        private static void writeSkillRows(SqliteConnection connection, SqliteTransaction transaction,
                                           IEnumerable<SongsBranchSkillRow> skillRows, CancellationToken cancellationToken)
        {
            using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = $"DELETE FROM {table_songs_branch_skill};";
            delete.ExecuteNonQuery();

            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = $@"
INSERT OR REPLACE INTO {table_songs_branch_skill}(
    {col_skill_beatmap_id},
    {col_skill_system_id},
    {col_skill_ruleset_online_id},
    {col_skill_skill_id},
    {col_skill_value},
    {col_skill_algorithm_version}
)
VALUES($id, $system, $ruleset, $skill, $value, $version);
";

            var idParam = insert.CreateParameter();
            idParam.ParameterName = "$id";
            insert.Parameters.Add(idParam);

            var systemParam = insert.CreateParameter();
            systemParam.ParameterName = "$system";
            insert.Parameters.Add(systemParam);

            var rulesetParam = insert.CreateParameter();
            rulesetParam.ParameterName = "$ruleset";
            insert.Parameters.Add(rulesetParam);

            var skillParam = insert.CreateParameter();
            skillParam.ParameterName = "$skill";
            insert.Parameters.Add(skillParam);

            var valueParam = insert.CreateParameter();
            valueParam.ParameterName = "$value";
            insert.Parameters.Add(valueParam);

            var versionParam = insert.CreateParameter();
            versionParam.ParameterName = "$version";
            insert.Parameters.Add(versionParam);

            foreach (var row in skillRows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                idParam.Value = row.BeatmapId.ToString();
                systemParam.Value = row.SystemId;
                rulesetParam.Value = row.RulesetOnlineId;
                skillParam.Value = row.SkillId;
                valueParam.Value = row.Value;
                versionParam.Value = row.AlgorithmVersion;
                insert.ExecuteNonQuery();
            }
        }

        public IReadOnlyDictionary<Guid, double> GetSongsBranchValues(string databasePath, IEnumerable<BeatmapInfo> beatmaps)
        {
            if (!Enabled || IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return empty_sunny_sr_values;

            try
            {
                var beatmapList = beatmaps.Distinct().ToList();

                if (beatmapList.Count == 0)
                    return empty_sunny_sr_values;

                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return empty_sunny_sr_values;

                var beatmapsById = beatmapList.ToDictionary(b => b.ID);
                var resolvedValues = new Dictionary<Guid, double>(beatmapList.Count);
                var idsToQuery = beatmapList.Select(b => b.ID).ToList();

                for (int offset = 0; offset < idsToQuery.Count; offset += 800)
                {
                    using var cmd = connection.CreateCommand();

                    int batchCount = Math.Min(800, idsToQuery.Count - offset);
                    var parameterNames = new List<string>(batchCount);

                    for (int i = 0; i < batchCount; i++)
                    {
                        string parameterName = $"$id{i}";
                        parameterNames.Add(parameterName);
                        cmd.Parameters.AddWithValue(parameterName, idsToQuery[offset + i].ToString());
                    }

                    cmd.CommandText = $@"
SELECT beatmap_id, beatmap_hash, beatmap_md5, sunny_sr
FROM {table_songs_branch_entry}
WHERE beatmap_id IN ({Join(", ", parameterNames)});
";

                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        if (!Guid.TryParse(reader.GetString(0), out var beatmapId))
                            continue;

                        if (!beatmapsById.TryGetValue(beatmapId, out var beatmap))
                            continue;

                        string storedHash = reader.GetString(1);
                        string storedMd5 = reader.GetString(2);

                        if (!string.Equals(storedHash, beatmap.Hash, StringComparison.Ordinal))
                            continue;

                        if (!IsNullOrEmpty(storedMd5) && !string.Equals(storedMd5, beatmap.MD5Hash, StringComparison.Ordinal))
                            continue;

                        resolvedValues[beatmapId] = reader.IsDBNull(3) ? 0 : reader.GetDouble(3);
                    }
                }

                return resolvedValues;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetSongsBranchValues failed.", Ez2ConfigManager.LOGGER_NAME);
                return empty_sunny_sr_values;
            }
        }

        public IReadOnlyDictionary<Guid, double> GetSongsBranchPpValues(string databasePath, IEnumerable<BeatmapInfo> beatmaps)
        {
            if (!Enabled || IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return empty_pp_values;

            try
            {
                var beatmapList = beatmaps.Distinct().ToList();

                if (beatmapList.Count == 0)
                    return empty_pp_values;

                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return empty_pp_values;

                var beatmapsById = beatmapList.ToDictionary(b => b.ID);
                var resolvedValues = new Dictionary<Guid, double>(beatmapList.Count);
                var idsToQuery = beatmapList.Select(b => b.ID).ToList();

                for (int offset = 0; offset < idsToQuery.Count; offset += 800)
                {
                    using var cmd = connection.CreateCommand();

                    int batchCount = Math.Min(800, idsToQuery.Count - offset);
                    var parameterNames = new List<string>(batchCount);

                    for (int i = 0; i < batchCount; i++)
                    {
                        string parameterName = $"$id{i}";
                        parameterNames.Add(parameterName);
                        cmd.Parameters.AddWithValue(parameterName, idsToQuery[offset + i].ToString());
                    }

                    cmd.CommandText = $@"
SELECT beatmap_id, beatmap_hash, beatmap_md5, {col_pp}
FROM {table_songs_branch_entry}
WHERE beatmap_id IN ({Join(", ", parameterNames)});
";

                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        if (!Guid.TryParse(reader.GetString(0), out var beatmapId))
                            continue;

                        if (!beatmapsById.TryGetValue(beatmapId, out var beatmap))
                            continue;

                        string storedHash = reader.GetString(1);
                        string storedMd5 = reader.GetString(2);

                        if (!string.Equals(storedHash, beatmap.Hash, StringComparison.Ordinal))
                            continue;

                        if (!IsNullOrEmpty(storedMd5) && !string.Equals(storedMd5, beatmap.MD5Hash, StringComparison.Ordinal))
                            continue;

                        if (!reader.IsDBNull(3))
                            resolvedValues[beatmapId] = reader.GetDouble(3);
                    }
                }

                return resolvedValues;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetSongsBranchPpValues failed.", Ez2ConfigManager.LOGGER_NAME);
                return empty_pp_values;
            }
        }

        public IReadOnlySet<string> GetSongsBranchCollectionBeatmapMd5Hashes(string databasePath)
        {
            var beatmapMd5Hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!Enabled || IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return beatmapMd5Hashes;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return beatmapMd5Hashes;

                try
                {
                    using var collectionCommand = connection.CreateCommand();
                    collectionCommand.CommandText = $@"
SELECT beatmap_md5
FROM {table_songs_branch_source_collection};
";

                    using var collectionReader = collectionCommand.ExecuteReader();

                    while (collectionReader.Read())
                    {
                        string beatmapMd5 = collectionReader.GetString(0);

                        if (!IsNullOrWhiteSpace(beatmapMd5))
                            beatmapMd5Hashes.Add(beatmapMd5);
                    }
                }
                catch (SqliteException)
                {
                }

                if (beatmapMd5Hashes.Count > 0)
                    return beatmapMd5Hashes;

                using var branchCommand = connection.CreateCommand();
                branchCommand.CommandText = $@"
SELECT beatmap_md5
FROM {table_songs_branch_entry};
";

                using var branchReader = branchCommand.ExecuteReader();

                while (branchReader.Read())
                {
                    string beatmapMd5 = branchReader.GetString(0);

                    if (!IsNullOrWhiteSpace(beatmapMd5))
                        beatmapMd5Hashes.Add(beatmapMd5);
                }

                return beatmapMd5Hashes;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetSongsBranchCollectionBeatmapMd5Hashes failed.", Ez2ConfigManager.LOGGER_NAME);
                return beatmapMd5Hashes;
            }
        }

        public IReadOnlySet<string> GetSongsBranchCollectionMatchingMd5Hashes(string databasePath, IEnumerable<string> candidateMd5Hashes)
        {
            var matchedMd5Hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!Enabled || IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return matchedMd5Hashes;

            var candidates = candidateMd5Hashes.Where(hash => !IsNullOrWhiteSpace(hash))
                                               .Distinct(StringComparer.OrdinalIgnoreCase)
                                               .ToList();

            if (candidates.Count == 0)
                return matchedMd5Hashes;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return matchedMd5Hashes;

                bool queriedSourceCollection = false;

                try
                {
                    queryMatchedMd5Hashes(connection, table_songs_branch_source_collection, candidates, matchedMd5Hashes);
                    queriedSourceCollection = true;
                }
                catch (SqliteException)
                {
                }

                if (!queriedSourceCollection || matchedMd5Hashes.Count == 0)
                    queryMatchedMd5Hashes(connection, table_songs_branch_entry, candidates, matchedMd5Hashes);

                return matchedMd5Hashes;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetSongsBranchCollectionMatchingMd5Hashes failed.", Ez2ConfigManager.LOGGER_NAME);
                return matchedMd5Hashes;
            }
        }

        private static void readSourceCollectionMd5Hashes(SqliteConnection connection, List<string> output)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $@"
SELECT {col_beatmap_md5}
FROM {table_songs_branch_source_collection};
";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                string beatmapMd5 = reader.GetString(0);

                if (!IsNullOrWhiteSpace(beatmapMd5))
                    output.Add(beatmapMd5);
            }
        }

        private static void queryMatchedMd5Hashes(SqliteConnection connection, string tableName, IReadOnlyList<string> candidates, HashSet<string> output)
        {
            for (int offset = 0; offset < candidates.Count; offset += 800)
            {
                using var command = connection.CreateCommand();

                int batchCount = Math.Min(800, candidates.Count - offset);
                var parameterNames = new List<string>(batchCount);

                for (int i = 0; i < batchCount; i++)
                {
                    string parameterName = $"$md5{i}";
                    parameterNames.Add(parameterName);
                    command.Parameters.AddWithValue(parameterName, candidates[offset + i]);
                }

                command.CommandText = $@"
SELECT {col_beatmap_md5}
FROM {tableName}
WHERE {col_beatmap_md5} IN ({Join(", ", parameterNames)});
";

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string beatmapMd5 = reader.GetString(0);

                    if (!IsNullOrWhiteSpace(beatmapMd5))
                        output.Add(beatmapMd5);
                }
            }
        }

        public IReadOnlyList<SongsBranchDescriptor> GetAvailableSongsBranches()
        {
            if (!Enabled)
                return Array.Empty<SongsBranchDescriptor>();

            var descriptors = new List<SongsBranchDescriptor>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (storage.ExistsDirectory(SONGS_BRANCH_DATABASE_DIRECTORY))
            {
                foreach (string relativePath in storage.GetFiles(SONGS_BRANCH_DATABASE_DIRECTORY, "*.sqlite"))
                {
                    string absolutePath = storage.GetFullPath(relativePath);

                    if (!seenPaths.Add(absolutePath))
                        continue;

                    if (TryGetSongsBranchDescriptor(absolutePath, out var descriptor))
                        descriptors.Add(descriptor);
                }
            }

            return descriptors
                   .OrderByDescending(d => d.Metadata.CreatedAtUnixMilliseconds)
                   .ThenByDescending(d => d.RelativePath, StringComparer.Ordinal)
                   .ToList();
        }

        /// <summary>
        /// 读取分支库 meta 中的 ssr算法版本；缺失时返回 0（表示 legacy，默认视为当前算法）。
        /// </summary>
        public bool TryGetSongsBranchStoredSunnySRVersion(string databasePath, out int storedVersion)
        {
            storedVersion = 0;

            if (!Enabled || IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return false;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return false;

                if (!int.TryParse(tryGetMeta(connection, meta_key_sunny_sr_version), NumberStyles.Integer, CultureInfo.InvariantCulture, out storedVersion))
                    storedVersion = 0;

                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TryGetSongsBranchStoredSunnySRVersion failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        /// <summary>
        /// 为 legacy 分支库补写 ssr/ PP 算法版本 meta（不触发重算）。已有版本且落后时不修改。
        /// </summary>
        public void EnsureSongsBranchVersionMeta(string databasePath, int currentSunnySRVersion, int currentPpVersion)
        {
            if (!Enabled || IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return;

            if (currentSunnySRVersion <= 0 && currentPpVersion <= 0)
                return;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return;

                if (currentSunnySRVersion > 0 && IsNullOrEmpty(tryGetMeta(connection, meta_key_sunny_sr_version)))
                    setMeta(connection, meta_key_sunny_sr_version, currentSunnySRVersion.ToString(CultureInfo.InvariantCulture));

                if (currentPpVersion > 0 && IsNullOrEmpty(tryGetMeta(connection, meta_key_pp_version)))
                    setMeta(connection, meta_key_pp_version, currentPpVersion.ToString(CultureInfo.InvariantCulture));

                setMeta(connection, meta_key_analysis_version, ANALYSIS_VERSION.ToString(CultureInfo.InvariantCulture));

                if (IsNullOrEmpty(tryGetMeta(connection, meta_key_content_hash))
                    && tryReadSongsBranchMetadata(connection, out var metadata))
                {
                    int storedSunnySR = int.TryParse(tryGetMeta(connection, meta_key_sunny_sr_version), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSunnySR)
                        ? parsedSunnySR
                        : currentSunnySRVersion;
                    int storedPp = int.TryParse(tryGetMeta(connection, meta_key_pp_version), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPp)
                        ? parsedPp
                        : currentPpVersion;

                    var md5Hashes = new List<string>();

                    try
                    {
                        readSourceCollectionMd5Hashes(connection, md5Hashes);
                    }
                    catch (SqliteException)
                    {
                    }

                    setMeta(connection, meta_key_content_hash, ComputeSongsBranchContentHash(
                        metadata.SourceCollectionId,
                        metadata.SourceCollectionLastModifiedUnixMilliseconds,
                        metadata.SourceCollectionBeatmapCount,
                        md5Hashes,
                        storedSunnySR,
                        storedPp));
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore EnsureSongsBranchVersionMeta failed.", Ez2ConfigManager.LOGGER_NAME);
            }
        }

        public void EnsureSongsBranchSunnySRVersionMeta(string databasePath, int currentSunnySRVersion) => EnsureSongsBranchVersionMeta(databasePath, currentSunnySRVersion, 0);

        public void EnsureSongsBranchPpVersionMeta(string databasePath, int currentPpVersion) => EnsureSongsBranchVersionMeta(databasePath, 0, currentPpVersion);

        public bool SongsBranchNeedsSunnySRRefresh(int storedSunnySRVersion, int currentSunnySRVersion)
        {
            if (currentSunnySRVersion <= 0)
                return false;

            // legacy 分支库（无 meta）默认按当前算法生成，首次打开会 stamp 而不重算（v1 迁移除外，见 requires_post_migration_refresh）
            if (storedSunnySRVersion <= 0)
                return false;

            return storedSunnySRVersion < currentSunnySRVersion;
        }

        /// <summary>
        /// When the stored content hash matches the current algorithm + source-collection snapshot, the branch needs no refresh.
        /// </summary>
        public bool SongsBranchContentHashMatches(string databasePath, SongsBranchMetadata metadata, int currentSunnySRVersion, int currentPpVersion)
        {
            if (!Enabled || IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return false;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return false;

                string? storedHash = tryGetMeta(connection, meta_key_content_hash);

                if (IsNullOrEmpty(storedHash))
                    return false;

                var md5Hashes = new List<string>();

                try
                {
                    readSourceCollectionMd5Hashes(connection, md5Hashes);
                }
                catch (SqliteException)
                {
                }

                string expected = ComputeSongsBranchContentHash(
                    metadata.SourceCollectionId,
                    metadata.SourceCollectionLastModifiedUnixMilliseconds,
                    metadata.SourceCollectionBeatmapCount,
                    md5Hashes,
                    currentSunnySRVersion,
                    currentPpVersion);

                return string.Equals(storedHash, expected, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore SongsBranchContentHashMatches failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        public static string ComputeSongsBranchContentHash(
            Guid sourceCollectionId,
            long sourceCollectionLastModifiedUnixMilliseconds,
            int sourceCollectionBeatmapCount,
            IEnumerable<string> beatmapMd5Hashes,
            int ssrVersion,
            int ppVersion)
        {
            var builder = new StringBuilder(256);
            builder.Append(sourceCollectionId.ToString("D"));
            builder.Append('|');
            builder.Append(sourceCollectionLastModifiedUnixMilliseconds.ToString(CultureInfo.InvariantCulture));
            builder.Append('|');
            builder.Append(sourceCollectionBeatmapCount.ToString(CultureInfo.InvariantCulture));
            builder.Append('|');
            builder.Append(ssrVersion.ToString(CultureInfo.InvariantCulture));
            builder.Append('|');
            builder.Append(ppVersion.ToString(CultureInfo.InvariantCulture));
            builder.Append('|');
            builder.Append(songs_branch_schema_version.ToString(CultureInfo.InvariantCulture));
            builder.Append('|');
            builder.Append(ANALYSIS_VERSION.ToString(CultureInfo.InvariantCulture));

            foreach (string md5 in beatmapMd5Hashes
                                   .Where(hash => !IsNullOrWhiteSpace(hash))
                                   .Select(hash => hash.ToLowerInvariant())
                                   .Distinct(StringComparer.Ordinal)
                                   .OrderBy(hash => hash, StringComparer.Ordinal))
            {
                builder.Append('|');
                builder.Append(md5);
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        public static string ComputeCollectionContentHash(long lastModifiedUnixMilliseconds, IEnumerable<string> beatmapMd5Hashes)
        {
            var builder = new StringBuilder(128);
            builder.Append(lastModifiedUnixMilliseconds.ToString(CultureInfo.InvariantCulture));

            foreach (string md5 in beatmapMd5Hashes
                                   .Where(hash => !IsNullOrWhiteSpace(hash))
                                   .Select(hash => hash.ToLowerInvariant())
                                   .Distinct(StringComparer.Ordinal)
                                   .OrderBy(hash => hash, StringComparer.Ordinal))
            {
                builder.Append('|');
                builder.Append(md5);
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        public bool TryGetSongsBranchRequiresPostMigrationRefresh(string databasePath, out bool requiresRefresh)
        {
            requiresRefresh = false;

            if (!Enabled || IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return false;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return false;

                requiresRefresh = getMetaBool(connection, meta_key_requires_post_migration_refresh);
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TryGetSongsBranchRequiresPostMigrationRefresh failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        public void ClearSongsBranchPostMigrationRefresh(string databasePath)
        {
            if (!Enabled || IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return;

                deleteMeta(connection, meta_key_requires_post_migration_refresh);
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore ClearSongsBranchPostMigrationRefresh failed.", Ez2ConfigManager.LOGGER_NAME);
            }
        }

        public IReadOnlyList<SongsBranchRow> ReadAllSongsBranchRows(string databasePath)
        {
            if (!Enabled || IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return Array.Empty<SongsBranchRow>();

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return Array.Empty<SongsBranchRow>();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = $@"
SELECT {col_beatmap_id}, {col_beatmap_hash}, {col_beatmap_md5}, {col_sunny_sr}, {col_pp},
       {col_official_sr}, {col_mina_sr}, {col_mina_pp}, {col_ppp_sr}, {col_ppp_pp}
FROM {table_songs_branch_entry};";

                var rows = new List<SongsBranchRow>();

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (!Guid.TryParse(reader.GetString(0), out Guid beatmapId))
                        continue;

                    rows.Add(new SongsBranchRow(
                        beatmapId,
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                        readNullableDouble(reader, 4),
                        readNullableDouble(reader, 5),
                        readNullableDouble(reader, 6),
                        readNullableDouble(reader, 7),
                        readNullableDouble(reader, 8),
                        readNullableDouble(reader, 9)));
                }

                return rows;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore ReadAllSongsBranchRows failed.", Ez2ConfigManager.LOGGER_NAME);
                return Array.Empty<SongsBranchRow>();
            }
        }

        public bool SongsBranchNeedsPpRefresh(int storedPpVersion, int currentPpVersion)
        {
            if (currentPpVersion <= 0)
                return false;

            if (storedPpVersion <= 0)
                return false;

            return storedPpVersion < currentPpVersion;
        }

        public bool TryGetSongsBranchStoredPpVersion(string databasePath, out int storedVersion)
        {
            storedVersion = 0;

            if (!Enabled || IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return false;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return false;

                if (!int.TryParse(tryGetMeta(connection, meta_key_pp_version), NumberStyles.Integer, CultureInfo.InvariantCulture, out storedVersion))
                    storedVersion = 0;

                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TryGetSongsBranchStoredPpVersion failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        public bool UpdateSongsBranchRows(string databasePath, IEnumerable<SongsBranchRow> rows, int? newSunnySRAlgorithmVersion, int? newPpAlgorithmVersion)
        {
            if (!Enabled || IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return false;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return false;

                using var transaction = connection.BeginTransaction();

                using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = $@"
UPDATE {table_songs_branch_entry}
SET {col_sunny_sr} = $ssr,
    {col_pp} = $pp
WHERE {col_beatmap_id} = $id;
";

                var idParam = update.CreateParameter();
                idParam.ParameterName = "$id";
                update.Parameters.Add(idParam);

                var ssrParam = update.CreateParameter();
                ssrParam.ParameterName = "$ssr";
                update.Parameters.Add(ssrParam);

                var ppParam = update.CreateParameter();
                ppParam.ParameterName = "$pp";
                update.Parameters.Add(ppParam);

                foreach (var row in rows)
                {
                    idParam.Value = row.BeatmapId.ToString();
                    ssrParam.Value = row.SunnySR;
                    ppParam.Value = row.Pp.HasValue ? row.Pp.Value : DBNull.Value;
                    update.ExecuteNonQuery();
                }

                transaction.Commit();

                if (newSunnySRAlgorithmVersion is > 0)
                    setMeta(connection, meta_key_sunny_sr_version, newSunnySRAlgorithmVersion.Value.ToString(CultureInfo.InvariantCulture));

                if (newPpAlgorithmVersion is > 0)
                    setMeta(connection, meta_key_pp_version, newPpAlgorithmVersion.Value.ToString(CultureInfo.InvariantCulture));

                setMeta(connection, meta_key_analysis_version, ANALYSIS_VERSION.ToString(CultureInfo.InvariantCulture));
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore UpdateSongsBranchRows failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        // 备份分支库隐藏用法
        //         public bool TrySetSongsBranchHideState(string databasePath, bool hiddenApplied, IEnumerable<Guid> preexistingHiddenBeatmapIds)
        //         {
        //             if (!Enabled || string.IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
        //                 return false;
        //
        //             try
        //             {
        //                 using var connection = openConnection(databasePath);
        //
        //                 if (!prepareSongsBranchConnection(connection))
        //                     return false;
        //
        //                 ensureSongsBranchStateTables(connection);
        //
        //                 using var transaction = connection.BeginTransaction();
        //
        //                 setMeta(connection, "hidden_applied", hiddenApplied ? "1" : "0");
        //
        //                 using (var delete = connection.CreateCommand())
        //                 {
        //                     delete.Transaction = transaction;
        //                     delete.CommandText = "DELETE FROM hidden_preexisting_beatmap;";
        //                     delete.ExecuteNonQuery();
        //                 }
        //
        //                 using var insert = connection.CreateCommand();
        //                 insert.Transaction = transaction;
        //                 insert.CommandText = @"
        // INSERT INTO hidden_preexisting_beatmap(beatmap_id)
        // VALUES($id)
        // ON CONFLICT(beatmap_id) DO NOTHING;
        // ";
        //
        //                 var idParam = insert.CreateParameter();
        //                 idParam.ParameterName = "$id";
        //                 insert.Parameters.Add(idParam);
        //
        //                 foreach (Guid beatmapId in preexistingHiddenBeatmapIds.Distinct())
        //                 {
        //                     idParam.Value = beatmapId.ToString();
        //                     insert.ExecuteNonQuery();
        //                 }
        //
        //                 transaction.Commit();
        //                 return true;
        //             }
        //             catch (Exception e)
        //             {
        //                 Logger.Error(e, "EzManiaAnalysisPersistentStore TrySetSongsBranchHideState failed.", Ez2ConfigManager.LOGGER_NAME);
        //                 return false;
        //             }
        //         }

        public bool TrySetCollectionHideState(Guid collectionId, bool hiddenApplied, IEnumerable<Guid> preexistingHiddenBeatmapIds, IEnumerable<string> beatmapMd5Hashes,
                                              long lastModifiedUnixMilliseconds = 0)
        {
            if (!Enabled || collectionId == Guid.Empty)
                return false;

            try
            {
                Initialise();

                using var connection = openConnection();
                ensureCollectionHideTables(connection);

                string contentHash = hiddenApplied
                    ? ComputeCollectionContentHash(lastModifiedUnixMilliseconds, beatmapMd5Hashes)
                    : Empty;

                using var transaction = connection.BeginTransaction();

                using (var upsertState = connection.CreateCommand())
                {
                    upsertState.Transaction = transaction;
                    upsertState.CommandText = @"
INSERT INTO collection_hidden_state(collection_id, hidden_applied, content_hash)
VALUES($collection_id, $hidden_applied, $content_hash)
ON CONFLICT(collection_id) DO UPDATE SET
    hidden_applied = excluded.hidden_applied,
    content_hash = excluded.content_hash;
";
                    upsertState.Parameters.AddWithValue("$collection_id", collectionId.ToString());
                    upsertState.Parameters.AddWithValue("$hidden_applied", hiddenApplied ? 1 : 0);
                    upsertState.Parameters.AddWithValue("$content_hash", contentHash);
                    upsertState.ExecuteNonQuery();
                }

                using (var deletePreexisting = connection.CreateCommand())
                {
                    deletePreexisting.Transaction = transaction;
                    deletePreexisting.CommandText = "DELETE FROM collection_hidden_preexisting_beatmap WHERE collection_id = $collection_id;";
                    deletePreexisting.Parameters.AddWithValue("$collection_id", collectionId.ToString());
                    deletePreexisting.ExecuteNonQuery();
                }

                using (var deleteMd5 = connection.CreateCommand())
                {
                    deleteMd5.Transaction = transaction;
                    deleteMd5.CommandText = "DELETE FROM collection_hidden_beatmap_md5 WHERE collection_id = $collection_id;";
                    deleteMd5.Parameters.AddWithValue("$collection_id", collectionId.ToString());
                    deleteMd5.ExecuteNonQuery();
                }

                using (var insertPreexisting = connection.CreateCommand())
                {
                    insertPreexisting.Transaction = transaction;
                    insertPreexisting.CommandText = @"
INSERT INTO collection_hidden_preexisting_beatmap(collection_id, beatmap_id)
VALUES($collection_id, $beatmap_id)
ON CONFLICT(collection_id, beatmap_id) DO NOTHING;
";

                    insertPreexisting.Parameters.AddWithValue("$collection_id", collectionId.ToString());
                    var beatmapIdParam = insertPreexisting.CreateParameter();
                    beatmapIdParam.ParameterName = "$beatmap_id";
                    insertPreexisting.Parameters.Add(beatmapIdParam);

                    foreach (Guid beatmapId in preexistingHiddenBeatmapIds.Distinct())
                    {
                        beatmapIdParam.Value = beatmapId.ToString();
                        insertPreexisting.ExecuteNonQuery();
                    }
                }

                using (var insertMd5 = connection.CreateCommand())
                {
                    insertMd5.Transaction = transaction;
                    insertMd5.CommandText = @"
INSERT INTO collection_hidden_beatmap_md5(collection_id, beatmap_md5)
VALUES($collection_id, $beatmap_md5)
ON CONFLICT(collection_id, beatmap_md5) DO NOTHING;
";

                    insertMd5.Parameters.AddWithValue("$collection_id", collectionId.ToString());
                    var beatmapMd5Param = insertMd5.CreateParameter();
                    beatmapMd5Param.ParameterName = "$beatmap_md5";
                    insertMd5.Parameters.Add(beatmapMd5Param);

                    foreach (string beatmapMd5 in beatmapMd5Hashes.Where(hash => !IsNullOrWhiteSpace(hash)).Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        beatmapMd5Param.Value = beatmapMd5;
                        insertMd5.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TrySetCollectionHideState failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        /// <summary>
        /// 强制使全部已缓存 kps / KPC 失效（开发/维护用）。
        /// </summary>
        public bool TrySetForceRecompute(bool force)
        {
            if (!Enabled)
                return false;

            try
            {
                Initialise();

                using var connection = openConnection();

                if (force)
                    EzAnalysisSchemaManager.InvalidateAllCachedAnalysisTimestamps(connection);

                EzAnalysisSchemaManager.SetMeta(connection, EzAnalysisSchemaManager.META_KEY_FORCE_RECOMPUTE, "0");
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TrySetForceRecompute failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        /// <summary>
        /// 读取当前（主）数据库中是否启用了强制重建开关。
        /// </summary>
        public bool IsForceRecomputeEnabled()
        {
            try
            {
                Initialise();
                using var connection = openConnection();
                return EzAnalysisSchemaManager.GetMetaBool(connection, EzAnalysisSchemaManager.META_KEY_FORCE_RECOMPUTE);
            }
            catch
            {
                return false;
            }
        }

        public IReadOnlySet<Guid> GetHiddenCollectionIds()
        {
            var collectionIds = new HashSet<Guid>();

            if (!Enabled)
                return collectionIds;

            try
            {
                Initialise();

                using var connection = openConnection();
                ensureCollectionHideTables(connection);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
SELECT collection_id
FROM collection_hidden_state
WHERE hidden_applied = 1;
";

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (Guid.TryParse(reader.GetString(0), out Guid collectionId))
                        collectionIds.Add(collectionId);
                }

                return collectionIds;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetHiddenCollectionIds failed.", Ez2ConfigManager.LOGGER_NAME);
                return collectionIds;
            }
        }

        public IReadOnlySet<Guid> GetCollectionPreexistingHiddenBeatmapIds(Guid collectionId)
        {
            var beatmapIds = new HashSet<Guid>();

            if (!Enabled || collectionId == Guid.Empty)
                return beatmapIds;

            try
            {
                Initialise();

                using var connection = openConnection();
                ensureCollectionHideTables(connection);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
SELECT beatmap_id
FROM collection_hidden_preexisting_beatmap
WHERE collection_id = $collection_id;
";
                cmd.Parameters.AddWithValue("$collection_id", collectionId.ToString());

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (Guid.TryParse(reader.GetString(0), out Guid beatmapId))
                        beatmapIds.Add(beatmapId);
                }

                return beatmapIds;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetCollectionPreexistingHiddenBeatmapIds failed.", Ez2ConfigManager.LOGGER_NAME);
                return beatmapIds;
            }
        }

        public IReadOnlySet<string> GetHiddenCollectionBeatmapMd5Hashes(Guid collectionId)
        {
            var beatmapMd5Hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!Enabled || collectionId == Guid.Empty)
                return beatmapMd5Hashes;

            try
            {
                Initialise();

                using var connection = openConnection();
                ensureCollectionHideTables(connection);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
SELECT beatmap_md5
FROM collection_hidden_beatmap_md5
WHERE collection_id = $collection_id;
";
                cmd.Parameters.AddWithValue("$collection_id", collectionId.ToString());

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    string beatmapMd5 = reader.GetString(0);

                    if (!IsNullOrWhiteSpace(beatmapMd5))
                        beatmapMd5Hashes.Add(beatmapMd5);
                }

                return beatmapMd5Hashes;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetHiddenCollectionBeatmapMd5Hashes failed.", Ez2ConfigManager.LOGGER_NAME);
                return beatmapMd5Hashes;
            }
        }

        public IReadOnlySet<Guid> GetSongsBranchPreexistingHiddenBeatmapIds(string databasePath)
        {
            var beatmapIds = new HashSet<Guid>();

            if (!Enabled || IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return beatmapIds;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return beatmapIds;

                ensureSongsBranchStateTables(connection);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
SELECT beatmap_id
FROM hidden_preexisting_beatmap;
";

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (Guid.TryParse(reader.GetString(0), out Guid beatmapId))
                        beatmapIds.Add(beatmapId);
                }

                return beatmapIds;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore GetSongsBranchPreexistingHiddenBeatmapIds failed.", Ez2ConfigManager.LOGGER_NAME);
                return beatmapIds;
            }
        }

        public bool DeleteSongsBranch(string databasePath)
        {
            if (!Enabled || IsNullOrEmpty(databasePath))
                return false;

            try
            {
                string fullPath = Path.GetFullPath(databasePath);

                if (!File.Exists(fullPath))
                    return false;

                SqliteConnection.ClearAllPools();
                File.Delete(fullPath);
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore DeleteSongsBranch failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        public bool TryGetSongsBranchDescriptor(string databasePath, out SongsBranchDescriptor descriptor)
        {
            descriptor = default;

            if (!Enabled || IsNullOrEmpty(databasePath) || !File.Exists(databasePath))
                return false;

            if (!isCurrentSongsBranchPath(databasePath))
                return false;

            try
            {
                using var connection = openConnection(databasePath);

                if (!prepareSongsBranchConnection(connection))
                    return false;

                if (!tryReadSongsBranchMetadata(connection, out var metadata))
                    return false;

                descriptor = createSongsBranchDescriptor(databasePath, metadata);
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore TryGetSongsBranchDescriptor failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        private SqliteConnection openConnection() => openConnection(dbPath);

        private static SqliteConnection openConnection(string databasePath)
        {
            // 这里每次操作打开一个连接，避免跨线程复用连接导致的问题。
            var connection = new SqliteConnection($"Data Source={databasePath};Cache=Shared;Mode=ReadWriteCreate");
            connection.Open();

            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys=ON;";
            pragma.ExecuteNonQuery();

            return connection;
        }

        private static void setMeta(SqliteConnection connection, string key, string value)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO meta(key, value)
VALUES($k, $v)
ON CONFLICT(key) DO UPDATE SET value = excluded.value;
";
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$v", value);
            cmd.ExecuteNonQuery();
        }

        private static void deleteMeta(SqliteConnection connection, string key)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM meta WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.ExecuteNonQuery();
        }

        private static string? tryGetMeta(SqliteConnection connection, string key)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT value
FROM meta
WHERE key = $key
LIMIT 1;
";
            cmd.Parameters.AddWithValue("$key", key);
            return cmd.ExecuteScalar() as string;
        }

        private static void ensureMetaTableExists(SqliteConnection connection)
        {
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);";
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // ignore failures; callers will handle missing meta via tryGetMeta/setMeta error paths
            }
        }

        private static bool prepareSongsBranchConnection(SqliteConnection connection)
        {
            ensureMetaTableExists(connection);

            if (!looksLikeSongsBranchDatabase(connection))
                return false;

            ensureSongsBranchSchemaCurrent(connection);
            return isValidSongsBranchConnection(connection);
        }

        private static bool looksLikeSongsBranchDatabase(SqliteConnection connection)
        {
            string? kind = tryGetMeta(connection, meta_key_kind);

            if (string.Equals(kind, songs_branch_kind, StringComparison.Ordinal)
                || string.Equals(kind, legacy_sunny_sr_branch_kind, StringComparison.Ordinal))
                return true;

            return songsBranchEntryTableExists(connection, legacy_table_sunny_sr_branch)
                   || songsBranchEntryTableExists(connection, table_songs_branch_entry);
        }

        private static void ensureSongsBranchSchemaCurrent(SqliteConnection connection)
        {
            if (!looksLikeSongsBranchDatabase(connection))
                return;

            if (isCurrentSongsBranchSchema(connection))
                return;

            int legacySchemaVersion = readLegacySongsBranchSchemaVersion(connection);
            bool requiresFullRecompute = legacySongsBranchRequiresFullRecompute(connection, legacySchemaVersion);

            using var transaction = connection.BeginTransaction();

            if (songsBranchEntryTableExists(connection, legacy_table_sunny_sr_branch))
            {
                if (songsBranchEntryTableExists(connection, table_songs_branch_entry))
                {
                    bool legacyHasPp = songsBranchEntryColumnExists(connection, legacy_table_sunny_sr_branch, col_pp);
                    string legacyPpSelect = legacyHasPp ? col_pp : "NULL";

                    using (var copy = connection.CreateCommand())
                    {
                        copy.Transaction = transaction;
                        copy.CommandText = $@"
INSERT OR REPLACE INTO {table_songs_branch_entry}(
    {col_beatmap_id},
    {col_beatmap_hash},
    {col_beatmap_md5},
    {col_sunny_sr},
    {col_pp}
)
SELECT
    {col_beatmap_id},
    {col_beatmap_hash},
    {col_beatmap_md5},
    {col_sunny_sr},
    {legacyPpSelect}
FROM {legacy_table_sunny_sr_branch};
";
                        copy.ExecuteNonQuery();
                    }

                    using (var drop = connection.CreateCommand())
                    {
                        drop.Transaction = transaction;
                        drop.CommandText = $"DROP TABLE {legacy_table_sunny_sr_branch};";
                        drop.ExecuteNonQuery();
                    }
                }
                else
                {
                    using var rename = connection.CreateCommand();
                    rename.Transaction = transaction;
                    rename.CommandText = $"ALTER TABLE {legacy_table_sunny_sr_branch} RENAME TO {table_songs_branch_entry};";
                    rename.ExecuteNonQuery();
                }
            }

            if (songsBranchEntryTableExists(connection, table_songs_branch_entry))
            {
                foreach (string column in new[] { col_pp, col_official_sr, col_mina_sr, col_mina_pp, col_ppp_sr, col_ppp_pp })
                {
                    if (songsBranchEntryColumnExists(connection, table_songs_branch_entry, column))
                        continue;

                    using var addColumn = connection.CreateCommand();
                    addColumn.Transaction = transaction;
                    addColumn.CommandText = $"ALTER TABLE {table_songs_branch_entry} ADD COLUMN {column} REAL NULL;";
                    addColumn.ExecuteNonQuery();
                }
            }

            using (var ensureHidden = connection.CreateCommand())
            {
                ensureHidden.Transaction = transaction;
                ensureHidden.CommandText = $@"
CREATE TABLE IF NOT EXISTS {table_songs_branch_hidden_preexisting} (
    {col_beatmap_id} TEXT PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS {table_songs_branch_source_collection} (
    {col_beatmap_md5} TEXT PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS {table_songs_branch_skill} (
    {col_skill_beatmap_id} TEXT NOT NULL,
    {col_skill_system_id} TEXT NOT NULL,
    {col_skill_ruleset_online_id} INTEGER NOT NULL,
    {col_skill_skill_id} TEXT NOT NULL,
    {col_skill_value} REAL NOT NULL,
    {col_skill_algorithm_version} INTEGER NOT NULL,
    PRIMARY KEY ({col_skill_beatmap_id}, {col_skill_system_id}, {col_skill_ruleset_online_id}, {col_skill_skill_id})
);

CREATE INDEX IF NOT EXISTS idx_songs_branch_skill_system
    ON {table_songs_branch_skill}({col_skill_system_id}, {col_skill_ruleset_online_id});
";
                ensureHidden.ExecuteNonQuery();
            }

            setMeta(connection, meta_key_kind, songs_branch_kind);
            setMeta(connection, meta_key_schema_version, songs_branch_schema_version.ToString(CultureInfo.InvariantCulture));

            if (requiresFullRecompute)
            {
                deleteMeta(connection, meta_key_sunny_sr_version);
                deleteMeta(connection, meta_key_pp_version);
                setMeta(connection, meta_key_requires_post_migration_refresh, "1");
            }
            else
            {
                deleteMeta(connection, meta_key_requires_post_migration_refresh);
            }

            transaction.Commit();

            Logger.Log(
                requiresFullRecompute
                    ? $"[EzAnalysisPersistentStore] Migrated legacy songs branch schema v{legacySchemaVersion} to kind={songs_branch_kind}, schema v{songs_branch_schema_version}; scheduled full ssr/PP refresh."
                    : $"[EzAnalysisPersistentStore] Migrated legacy songs branch schema v{legacySchemaVersion} to kind={songs_branch_kind}, schema v{songs_branch_schema_version}; reusing cached ssr/PP.",
                Ez2ConfigManager.LOGGER_NAME,
                LogLevel.Important);
        }

        private static int readLegacySongsBranchSchemaVersion(SqliteConnection connection)
        {
            if (int.TryParse(tryGetMeta(connection, meta_key_schema_version), NumberStyles.Integer, CultureInfo.InvariantCulture, out int schemaVersion))
                return schemaVersion;

            return 0;
        }

        /// <summary>
        /// legacy schema v1（或无 pp 列的旧库）迁移后需全量重算；v2 及以后复用既有结果。
        /// </summary>
        private static bool legacySongsBranchRequiresFullRecompute(SqliteConnection connection, int legacySchemaVersion)
        {
            if (legacySchemaVersion >= 2)
                return false;

            if (legacySchemaVersion == 1)
                return true;

            if (songsBranchEntryTableExists(connection, legacy_table_sunny_sr_branch))
                return !songsBranchEntryColumnExists(connection, legacy_table_sunny_sr_branch, col_pp);

            if (songsBranchEntryTableExists(connection, table_songs_branch_entry))
                return !songsBranchEntryColumnExists(connection, table_songs_branch_entry, col_pp);

            return true;
        }

        private static bool isCurrentSongsBranchSchema(SqliteConnection connection)
        {
            string? kind = tryGetMeta(connection, meta_key_kind);

            if (!string.Equals(kind, songs_branch_kind, StringComparison.Ordinal))
                return false;

            if (!int.TryParse(tryGetMeta(connection, meta_key_schema_version), NumberStyles.Integer, CultureInfo.InvariantCulture, out int schemaVersion)
                || schemaVersion != songs_branch_schema_version)
                return false;

            return songsBranchEntryTableExists(connection, table_songs_branch_entry)
                   && !songsBranchEntryTableExists(connection, legacy_table_sunny_sr_branch);
        }

        private static bool isValidSongsBranchConnection(SqliteConnection connection) => isCurrentSongsBranchSchema(connection);

        private static bool songsBranchEntryTableExists(SqliteConnection connection, string tableName)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
            cmd.Parameters.AddWithValue("$name", tableName);
            return cmd.ExecuteScalar() != null;
        }

        private static bool songsBranchEntryColumnExists(SqliteConnection connection, string tableName, string columnName)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({tableName});";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool tryReadSongsBranchMetadata(SqliteConnection connection, out SongsBranchMetadata metadata)
        {
            metadata = default;

            string? rulesetOnlineIdText = tryGetMeta(connection, meta_key_ruleset_online_id);
            string? rulesetShortName = tryGetMeta(connection, meta_key_ruleset_short_name);
            string? modsFingerprint = tryGetMeta(connection, meta_key_mods_fingerprint);
            string? modsDisplay = tryGetMeta(connection, meta_key_mods_display);
            string? beatmapCountText = tryGetMeta(connection, meta_key_beatmap_count);
            string? createdAtText = tryGetMeta(connection, meta_key_created_at);
            string? displayName = tryGetMeta(connection, meta_key_display_name);
            string? modsJson = tryGetMeta(connection, meta_key_mods_json);
            string? hiddenAppliedText = tryGetMeta(connection, meta_key_hidden_applied);
            string? sourceCollectionIdText = tryGetMeta(connection, meta_key_source_collection_id);
            string? sourceCollectionName = tryGetMeta(connection, meta_key_source_collection_name);
            string? sourceCollectionLastModifiedText = tryGetMeta(connection, meta_key_source_collection_last_modified);
            string? sourceCollectionBeatmapCountText = tryGetMeta(connection, meta_key_source_collection_beatmap_count);

            if (!int.TryParse(rulesetOnlineIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rulesetOnlineId))
                return false;

            if (!int.TryParse(beatmapCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int beatmapCount))
                beatmapCount = 0;

            if (!long.TryParse(createdAtText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long createdAtUnixMilliseconds))
                createdAtUnixMilliseconds = 0;

            rulesetShortName ??= "ruleset";
            modsFingerprint ??= Empty;
            modsDisplay ??= "NoMod";
            sourceCollectionName ??= Empty;
            displayName ??= IsNullOrWhiteSpace(sourceCollectionName)
                ? $"songs | {modsDisplay}"
                : $"{sourceCollectionName} | {modsDisplay}";
            modsJson ??= Empty;
            bool hiddenApplied = string.Equals(hiddenAppliedText, "1", StringComparison.Ordinal);
            Guid sourceCollectionId = Guid.TryParse(sourceCollectionIdText, out Guid parsedSourceCollectionId) ? parsedSourceCollectionId : Guid.Empty;
            long sourceCollectionLastModifiedUnixMilliseconds =
                long.TryParse(sourceCollectionLastModifiedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedSourceCollectionLastModified)
                    ? parsedSourceCollectionLastModified
                    : 0;
            int sourceCollectionBeatmapCount = int.TryParse(sourceCollectionBeatmapCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSourceCollectionBeatmapCount)
                ? parsedSourceCollectionBeatmapCount
                : 0;

            metadata = new SongsBranchMetadata(
                rulesetOnlineId,
                rulesetShortName,
                modsFingerprint,
                modsDisplay,
                beatmapCount,
                createdAtUnixMilliseconds,
                displayName,
                modsJson,
                hiddenApplied,
                sourceCollectionId,
                sourceCollectionName,
                sourceCollectionLastModifiedUnixMilliseconds,
                sourceCollectionBeatmapCount);
            return true;
        }

        private static void ensureSongsBranchStateTables(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
CREATE TABLE IF NOT EXISTS {table_songs_branch_hidden_preexisting} (
    beatmap_id TEXT PRIMARY KEY
);
";
            cmd.ExecuteNonQuery();
        }

        private static void ensureCollectionHideTables(SqliteConnection connection)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS collection_hidden_state (
    collection_id TEXT PRIMARY KEY,
    hidden_applied INTEGER NOT NULL DEFAULT 0,
    content_hash TEXT NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS collection_hidden_preexisting_beatmap (
    collection_id TEXT NOT NULL,
    beatmap_id TEXT NOT NULL,
    PRIMARY KEY(collection_id, beatmap_id)
);

CREATE TABLE IF NOT EXISTS collection_hidden_beatmap_md5 (
    collection_id TEXT NOT NULL,
    beatmap_md5 TEXT NOT NULL,
    PRIMARY KEY(collection_id, beatmap_md5)
);
";
                cmd.ExecuteNonQuery();
            }

            ensureCollectionHideContentHashColumn(connection);
        }

        private static void ensureCollectionHideContentHashColumn(SqliteConnection connection)
        {
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA table_info(collection_hidden_state);";

            bool hasContentHash = false;

            using (var reader = pragma.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(1), "content_hash", StringComparison.OrdinalIgnoreCase))
                    {
                        hasContentHash = true;
                        break;
                    }
                }
            }

            if (hasContentHash)
                return;

            using var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE collection_hidden_state ADD COLUMN content_hash TEXT NOT NULL DEFAULT '';";
            alter.ExecuteNonQuery();
        }

        /// <summary>
        /// Returns true when hide is already applied and the stored content hash matches the live collection snapshot.
        /// </summary>
        public bool IsCollectionHideContentCurrent(Guid collectionId, long lastModifiedUnixMilliseconds, IEnumerable<string> beatmapMd5Hashes)
        {
            if (!Enabled || collectionId == Guid.Empty)
                return false;

            try
            {
                Initialise();

                using var connection = openConnection();
                ensureCollectionHideTables(connection);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
SELECT hidden_applied, content_hash
FROM collection_hidden_state
WHERE collection_id = $collection_id
LIMIT 1;
";
                cmd.Parameters.AddWithValue("$collection_id", collectionId.ToString());

                using var reader = cmd.ExecuteReader();

                if (!reader.Read())
                    return false;

                if (reader.GetInt64(0) != 1)
                    return false;

                string storedHash = reader.IsDBNull(1) ? Empty : reader.GetString(1);

                if (IsNullOrEmpty(storedHash))
                    return false;

                string expected = ComputeCollectionContentHash(lastModifiedUnixMilliseconds, beatmapMd5Hashes);
                return string.Equals(storedHash, expected, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore IsCollectionHideContentCurrent failed.", Ez2ConfigManager.LOGGER_NAME);
                return false;
            }
        }

        private bool isCurrentSongsBranchPath(string databasePath)
        {
            string fullPath = Path.GetFullPath(databasePath);
            string branchRoot = storage.GetFullPath(SONGS_BRANCH_DATABASE_DIRECTORY, true);
            string relativePath = Path.GetRelativePath(branchRoot, fullPath);

            if (IsNullOrEmpty(relativePath))
                return false;

            return !relativePath.StartsWith("..", StringComparison.Ordinal)
                   && !Path.IsPathRooted(relativePath);
        }

        private SongsBranchDescriptor createSongsBranchDescriptor(string databasePath, SongsBranchMetadata metadata)
        {
            string fullPath = Path.GetFullPath(databasePath);
            string rootPath = storage.GetFullPath(Empty, true);
            string relativePath = Path.GetRelativePath(rootPath, fullPath).Replace('\\', '/');

            return new SongsBranchDescriptor(fullPath, relativePath, metadata);
        }

        private static string createSafeBranchModsDisplay(string? value)
        {
            if (IsNullOrWhiteSpace(value))
                return "NoMod";

            var builder = new StringBuilder(value.Length);

            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                }
                else if (c == '+')
                {
                    builder.Append('+');
                }
            }

            string result = builder.ToString().Trim('+');

            while (result.Contains("++", StringComparison.Ordinal))
                result = result.Replace("++", "+", StringComparison.Ordinal);

            return IsNullOrEmpty(result) ? "NoMod" : result;
        }

        private static string createSafeBranchDisplayName(string value)
        {
            string trimmed = value.Trim();

            if (IsNullOrEmpty(trimmed))
                return "songs_branch";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(trimmed.Length);

            foreach (char c in trimmed)
            {
                if (Array.IndexOf(invalidChars, c) >= 0)
                    continue;

                builder.Append(c);
            }

            string result = builder.ToString().Trim().TrimEnd('.');
            return IsNullOrEmpty(result) ? "songs_branch" : result;
        }

        private void startBackgroundWriter()
        {
            if (backgroundWriterTask != null)
                return;

            writeCts = new CancellationTokenSource();
            backgroundWriterTask = Task.Run(() => backgroundWriterLoop(writeCts.Token));
        }

        private async Task backgroundWriterLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(500, token).ConfigureAwait(false);

                    if (pendingWrites.IsEmpty)
                        continue;

                    var batch = pendingWrites.ToArray();

                    try
                    {
                        token.ThrowIfCancellationRequested();

                        using var connection = openConnection();
                        using var transaction = connection.BeginTransaction();
                        using var writer = new PendingWriteWriter(connection, transaction);

                        int errorsLogged = 0;

                        foreach (var kv in batch)
                        {
                            token.ThrowIfCancellationRequested();

                            var id = kv.Key;
                            var pw = kv.Value;

                            try
                            {
                                writer.Write(pw);

                                // Only remove if the pending entry we wrote is still the latest.
                                if (pendingWrites.TryGetValue(id, out var latest) && latest.Timestamp == pw.Timestamp)
                                    pendingWrites.TryRemove(id, out _);
                            }
                            catch (Exception e)
                            {
                                if (e is Realms.Exceptions.RealmClosedException)
                                    pendingWrites.TryRemove(id, out _);

                                if (errorsLogged == 0)
                                    Logger.Error(e, "EzManiaAnalysisPersistentStore background write failed for entry.");

                                errorsLogged++;
                            }
                        }

                        transaction.Commit();
                        markDirty(batch.Select(kv => kv.Key), Interlocked.Increment(ref writeGeneration));
                    }
                    catch (Exception e)
                    {
                        // Log and continue; writer will retry on next loop.
                        Logger.Error(e, "EzManiaAnalysisPersistentStore background batch write failed.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // expected on cancellation
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore background writer crashed.");
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            var cancellationSource = writeCts;
            var writerTask = backgroundWriterTask;

            writeCts = null;
            backgroundWriterTask = null;

            cancellationSource?.Cancel();

            if (writerTask != null)
            {
                try
                {
                    writerTask.Wait(3000);
                }
                catch
                {
                }
            }

            // Only dispose the source once the loop has actually stopped: disposing it under a running writer turns
            // its next wait into an ObjectDisposedException.
            if (writerTask == null || writerTask.IsCompleted)
                cancellationSource?.Dispose();

            flushPendingWrites();
        }

        /// <summary>
        /// Writes whatever the background loop did not reach before it stopped. The store is the only holder of
        /// these results, so dropping them would silently discard analysis that had already completed.
        /// </summary>
        private void flushPendingWrites()
        {
            if (pendingWrites.IsEmpty)
                return;

            try
            {
                if (!initialised)
                {
                    Initialise();

                    if (!initialised)
                        return;
                }

                using var connection = openConnection();
                using var transaction = connection.BeginTransaction();
                using var writer = new PendingWriteWriter(connection, transaction);
                var flushed = pendingWrites.ToArray();

                foreach (var kv in flushed)
                {
                    writer.Write(kv.Value);
                    pendingWrites.TryRemove(kv.Key, out _);
                }

                transaction.Commit();
                markDirty(flushed.Select(kv => kv.Key), Interlocked.Increment(ref writeGeneration));
            }
            catch (Exception e)
            {
                Logger.Error(e, "EzManiaAnalysisPersistentStore final flush failed.");
            }
        }

        private static PendingWrite snapshotPendingWrite(BeatmapInfo beatmap, EzAnalysisResult analysis) => new PendingWrite(
            beatmap.ID,
            beatmap.Hash,
            beatmap.MD5Hash,
            beatmap.Ruleset.OnlineID,
            analysis,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        /// <summary>
        /// Statements a <see cref="PendingWriteWriter"/> prepares once per batch. Built from the schema constants at
        /// type load, so the SQL text is assembled once rather than per row.
        /// </summary>
        private static readonly string entry_upsert_sql = $@"
INSERT INTO {EzAnalysisSchemaManager.TABLE_ENTRY}(
    {EzAnalysisSchemaManager.COL_BEATMAP_ID},
    {EzAnalysisSchemaManager.COL_BEATMAP_HASH},
    {EzAnalysisSchemaManager.COL_BEATMAP_MD5},
    {EzAnalysisSchemaManager.COL_RULESET_ONLINE_ID},
    {EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT},
    {EzAnalysisSchemaManager.COL_AVERAGE_KPS},
    {EzAnalysisSchemaManager.COL_MAX_KPS},
    {EzAnalysisSchemaManager.COL_KPS_LIST_JSON}
)
VALUES(
    $id,
    $hash,
    $md5,
    $ruleset,
    $common_updated_at,
    $avg,
    $max,
    $kps
)
ON CONFLICT({EzAnalysisSchemaManager.COL_BEATMAP_ID}) DO UPDATE SET
    {EzAnalysisSchemaManager.COL_BEATMAP_HASH} = excluded.{EzAnalysisSchemaManager.COL_BEATMAP_HASH},
    {EzAnalysisSchemaManager.COL_BEATMAP_MD5} = excluded.{EzAnalysisSchemaManager.COL_BEATMAP_MD5},
    {EzAnalysisSchemaManager.COL_RULESET_ONLINE_ID} = excluded.{EzAnalysisSchemaManager.COL_RULESET_ONLINE_ID},
    {EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT} = excluded.{EzAnalysisSchemaManager.COL_COMMON_UPDATED_AT},
    {EzAnalysisSchemaManager.COL_AVERAGE_KPS} = excluded.{EzAnalysisSchemaManager.COL_AVERAGE_KPS},
    {EzAnalysisSchemaManager.COL_MAX_KPS} = excluded.{EzAnalysisSchemaManager.COL_MAX_KPS},
    {EzAnalysisSchemaManager.COL_KPS_LIST_JSON} = excluded.{EzAnalysisSchemaManager.COL_KPS_LIST_JSON};
";

        private static readonly string mania_upsert_sql = $@"
INSERT INTO {EzAnalysisSchemaManager.TABLE_MANIA}(
    {EzAnalysisSchemaManager.COL_BEATMAP_ID},
    {EzAnalysisSchemaManager.COL_UPDATED_AT},
    {EzAnalysisSchemaManager.COL_COLUMN_COUNTS_JSON},
    {EzAnalysisSchemaManager.COL_HOLD_NOTE_COUNTS_JSON}
)
VALUES(
    $id,
    $updated_at,
    $column_counts_json,
    $hold_note_counts_json
)
ON CONFLICT({EzAnalysisSchemaManager.COL_BEATMAP_ID}) DO UPDATE SET
    {EzAnalysisSchemaManager.COL_UPDATED_AT} = excluded.{EzAnalysisSchemaManager.COL_UPDATED_AT},
    {EzAnalysisSchemaManager.COL_COLUMN_COUNTS_JSON} = excluded.{EzAnalysisSchemaManager.COL_COLUMN_COUNTS_JSON},
    {EzAnalysisSchemaManager.COL_HOLD_NOTE_COUNTS_JSON} = excluded.{EzAnalysisSchemaManager.COL_HOLD_NOTE_COUNTS_JSON};
";

        private static readonly string mania_delete_sql = $@"
DELETE FROM {EzAnalysisSchemaManager.TABLE_MANIA}
WHERE {EzAnalysisSchemaManager.COL_BEATMAP_ID} = $id;
";

        private void writePendingEntryToConnection(SqliteConnection connection, PendingWrite pending, SqliteTransaction? transaction = null)
        {
            using var writer = new PendingWriteWriter(connection, transaction);
            writer.Write(pending);
        }

        /// <summary>
        /// One connection's prepared statements for a pending-write batch.
        /// </summary>
        /// <remarks>
        /// A backfill writes thousands of rows through one connection and transaction. Building the three statements
        /// per row put the SQL text through the parser and re-bound every parameter each time; preparing them once
        /// per batch leaves only the parameter values to set.
        /// </remarks>
        private sealed class PendingWriteWriter : IDisposable
        {
            private readonly SqliteCommand entryUpsert;
            private readonly SqliteCommand maniaUpsert;
            private readonly SqliteCommand maniaDelete;
            private readonly SqliteParameter entryId;
            private readonly SqliteParameter entryHash;
            private readonly SqliteParameter entryMd5;
            private readonly SqliteParameter entryRuleset;
            private readonly SqliteParameter entryUpdatedAt;
            private readonly SqliteParameter entryAverage;
            private readonly SqliteParameter entryMax;
            private readonly SqliteParameter entryKps;
            private readonly SqliteParameter maniaId;
            private readonly SqliteParameter maniaUpdatedAt;
            private readonly SqliteParameter maniaColumnCounts;
            private readonly SqliteParameter maniaHoldCounts;

            public PendingWriteWriter(SqliteConnection connection, SqliteTransaction? transaction)
            {
                entryUpsert = connection.CreateCommand();
                entryUpsert.Transaction = transaction;
                entryUpsert.CommandText = entry_upsert_sql;
                entryId = entryUpsert.Parameters.Add("$id", SqliteType.Text);
                entryHash = entryUpsert.Parameters.Add("$hash", SqliteType.Text);
                entryMd5 = entryUpsert.Parameters.Add("$md5", SqliteType.Text);
                entryRuleset = entryUpsert.Parameters.Add("$ruleset", SqliteType.Integer);
                entryUpdatedAt = entryUpsert.Parameters.Add("$common_updated_at", SqliteType.Integer);
                entryAverage = entryUpsert.Parameters.Add("$avg", SqliteType.Real);
                entryMax = entryUpsert.Parameters.Add("$max", SqliteType.Real);
                entryKps = entryUpsert.Parameters.Add("$kps", SqliteType.Text);

                maniaUpsert = connection.CreateCommand();
                maniaUpsert.Transaction = transaction;
                maniaUpsert.CommandText = mania_upsert_sql;
                maniaId = maniaUpsert.Parameters.Add("$id", SqliteType.Text);
                maniaUpdatedAt = maniaUpsert.Parameters.Add("$updated_at", SqliteType.Integer);
                maniaColumnCounts = maniaUpsert.Parameters.Add("$column_counts_json", SqliteType.Text);
                maniaHoldCounts = maniaUpsert.Parameters.Add("$hold_note_counts_json", SqliteType.Text);

                maniaDelete = connection.CreateCommand();
                maniaDelete.Transaction = transaction;
                maniaDelete.CommandText = mania_delete_sql;
                maniaDelete.Parameters.Add("$id", SqliteType.Text);
            }

            public void Write(PendingWrite pending)
            {
                var commonSummary = pending.Analysis.CommonSummary;
                var maniaSummary = pending.Analysis.ManiaSummary;

                if (commonSummary == null)
                    return;

                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                entryId.Value = pending.Id.ToString();
                entryHash.Value = pending.Hash;
                entryMd5.Value = pending.Md5;
                entryRuleset.Value = pending.RulesetOnlineId;
                entryUpdatedAt.Value = now;
                entryAverage.Value = pending.Analysis.AverageKps;
                entryMax.Value = pending.Analysis.MaxKps;
                entryKps.Value = JsonSerializer.Serialize(commonSummary.Value.KpsList);
                entryUpsert.ExecuteNonQuery();

                if (maniaSummary == null)
                {
                    maniaDelete.Parameters["$id"].Value = pending.Id.ToString();
                    maniaDelete.ExecuteNonQuery();
                    return;
                }

                maniaId.Value = pending.Id.ToString();
                maniaUpdatedAt.Value = now;
                maniaColumnCounts.Value = JsonSerializer.Serialize(maniaSummary.Value.ColumnCounts);
                maniaHoldCounts.Value = maniaSummary.Value.HasHoldNoteCounts
                    ? JsonSerializer.Serialize(maniaSummary.Value.HoldNoteCounts)
                    : "{}";
                maniaUpsert.ExecuteNonQuery();
            }

            public void Dispose()
            {
                entryUpsert.Dispose();
                maniaUpsert.Dispose();
                maniaDelete.Dispose();
            }
        }

        private static EzAnalysisResult mergeAnalysisResult(EzAnalysisResult storedAnalysis, EzAnalysisResult computedAnalysis) => computedAnalysis;

        internal static MissingDataKind GetMissingData(EzAnalysisResult? storedAnalysis, int rulesetOnlineId)
        {
            if (!storedAnalysis.HasValue)
                return getRequiredDataMask(rulesetOnlineId);

            MissingDataKind missingData = MissingDataKind.None;
            EzAnalysisResult analysis = storedAnalysis.Value;

            if (analysis.CommonSummary == null)
                missingData |= MissingDataKind.Common;

            if (rulesetOnlineId == 3 && analysis.ManiaSummary == null)
                missingData |= MissingDataKind.Mania;

            return missingData;
        }

        internal static bool RequiresAnalysisComputation(MissingDataKind missingData) => (missingData & (MissingDataKind.Common | MissingDataKind.Mania)) != MissingDataKind.None;

        private static MissingDataKind getMissingData(long commonUpdatedAt, bool hasManiaData, int rulesetOnlineId)
        {
            MissingDataKind missingData = MissingDataKind.None;

            if (commonUpdatedAt <= 0)
                missingData |= MissingDataKind.Common;

            if (rulesetOnlineId == 3 && !hasManiaData)
                missingData |= MissingDataKind.Mania;

            return missingData;
        }

        private static MissingDataKind getRequiredDataMask(int rulesetOnlineId)
        {
            MissingDataKind requiredData = MissingDataKind.Common;

            if (rulesetOnlineId == 3)
                requiredData |= MissingDataKind.Mania;

            return requiredData;
        }

        /// <summary>
        /// Validates that the analysis result contains reasonable values.
        /// </summary>
        private static bool isValidAnalysisResult(EzAnalysisResult result)
        {
            if (result.ManiaSummary?.SunnySR is double sunnySR && (double.IsNaN(sunnySR) || double.IsInfinity(sunnySR)))
                return false;

            return true;
        }
    }
}
