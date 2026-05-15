// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Scoring.Lamp.Persistence;

namespace osu.Game.Rulesets.BMS.Scoring.Lamp
{
    /// <summary>
    /// Process-wide lookup of "best lamp seen so far" per BMS beatmap.
    /// Acts as a thin facade for the rest of the BMS UI: nothing else needs to know
    /// where the lamp data came from (SQLite store, beatoraja import, network, …).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The in-memory dictionary is the hot path: <c>PanelBeatmap.Update</c> reads it
    /// every frame for every visible row, so look-ups must stay O(1) and lock-free.
    /// <see cref="ConcurrentDictionary{TKey,TValue}"/> gives us that.
    /// </para>
    /// <para>
    /// Persistence is opt-in via <see cref="AttachRepository"/>: tests and headless flows
    /// keep the store fully in-memory; song-select wires up <see cref="BmsLampSqliteRepository"/>
    /// at startup. Writes are fire-and-forget on the thread pool so the gameplay/results
    /// path never blocks on disk I/O — best-lamp arbitration happens synchronously in
    /// <see cref="ReportPlay"/> first, then the resulting row is shipped to the repository.
    /// </para>
    /// </remarks>
    public class BmsLampStore
    {
        private readonly ConcurrentDictionary<Guid, BmsLampRecord> bestByBeatmap = new ConcurrentDictionary<Guid, BmsLampRecord>();

        private IBmsLampRepository? repository;

        public BmsLampStore(IBmsLampScheme scheme)
        {
            Scheme = scheme;
        }

        /// <summary>
        /// The lamp scheme currently in use. UI components that want lamp colours
        /// should route through this property rather than holding their own reference.
        /// </summary>
        public IBmsLampScheme Scheme { get; }

        /// <summary>
        /// Wire up a persistence backend. Loads every persisted record into the
        /// in-memory map synchronously so the carousel sees correct lamps on first paint.
        /// </summary>
        /// <remarks>
        /// Safe to call multiple times — subsequent calls replace the backend and re-seed
        /// the dictionary from the new source (which is what you want when the storage
        /// directory is rebound, e.g. profile switching). Existing in-memory entries that
        /// the new repository doesn't know about are dropped — the persistent store is
        /// authoritative.
        /// </remarks>
        public void AttachRepository(IBmsLampRepository repository)
        {
            this.repository = repository;

            try
            {
                var rows = repository.LoadAll();

                bestByBeatmap.Clear();

                foreach (var record in rows)
                    bestByBeatmap[record.BeatmapId] = record;

                Logger.Log($"[BMS] Lamp store attached: loaded {rows.Count} record(s).", LoggingTarget.Database, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                // LoadAll is contracted not to throw, but defend in depth: a broken store
                // should leave the carousel running on a clean in-memory slate, not crash.
                Logger.Log($"[BMS] Lamp store attach failed: {ex.Message}", LoggingTarget.Database, LogLevel.Important);
            }
        }

        /// <summary>
        /// Look up the best lamp recorded for the given beatmap, or <see cref="BmsClearLamp.NoPlay"/>
        /// if nothing has been recorded yet.
        /// </summary>
        public BmsClearLamp GetLamp(BeatmapInfo beatmap)
        {
            if (beatmap == null)
                return BmsClearLamp.NoPlay;

            return bestByBeatmap.TryGetValue(beatmap.ID, out var record) ? record.Lamp : BmsClearLamp.NoPlay;
        }

        /// <summary>
        /// Record a finished play for <paramref name="beatmap"/>. The store keeps the
        /// best (highest-ranked) lamp seen for the beatmap so far. When the new play sets
        /// a new personal best the row is shipped to <see cref="AttachRepository"/>'s
        /// backend asynchronously.
        /// </summary>
        public BmsClearLamp ReportPlay(BeatmapInfo beatmap, BmsLampContext context)
        {
            var lamp = Scheme.ResolveLamp(context);

            if (beatmap == null)
                return lamp;

            var candidate = new BmsLampRecord(
                BeatmapId: beatmap.ID,
                Lamp: lamp,
                MissCount: context.MissCount,
                GreatCount: context.GreatCount,
                GoodCount: context.GoodCount,
                BadCount: context.BadCount,
                PerfectGreatCount: context.PerfectGreatCount,
                TotalNotes: context.TotalNotes,
                UpdatedAtUnixMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            // AddOrUpdate keeps the existing row when the new lamp is strictly worse.
            // `wonBest` flips iff the candidate became the winning entry; the closure may run
            // more than once under bucket contention but the flag is still safe because every
            // execution agrees on the same outcome — the worst case is a duplicate persist,
            // which the repo handles idempotently via UPSERT.
            bool wonBest = false;

            bestByBeatmap.AddOrUpdate(
                beatmap.ID,
                _ =>
                {
                    wonBest = true;
                    return candidate;
                },
                (_, existing) =>
                {
                    if ((int)candidate.Lamp > (int)existing.Lamp)
                    {
                        wonBest = true;
                        return candidate;
                    }

                    return existing;
                });

            // Only persist when the candidate actually won — avoids re-writing every play and
            // trims SQLite WAL pressure during practice grinding.
            if (wonBest)
                persist(candidate);

            return lamp;
        }

        /// <summary>
        /// Direct seed — bypasses scheme resolution and forces the best lamp for a beatmap.
        /// Intended for the beatoraja importer and tests that need to pin the carousel to a
        /// specific lamp colour.
        /// </summary>
        public void SetLamp(BeatmapInfo beatmap, BmsClearLamp lamp)
        {
            if (beatmap == null)
                return;

            var record = new BmsLampRecord(
                BeatmapId: beatmap.ID,
                Lamp: lamp,
                MissCount: 0,
                GreatCount: 0,
                GoodCount: 0,
                BadCount: 0,
                PerfectGreatCount: 0,
                TotalNotes: 0,
                UpdatedAtUnixMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            bestByBeatmap[beatmap.ID] = record;
            persist(record);
        }

        /// <summary>
        /// Clear in-memory records. The persistent store is left intact so a refresh of the
        /// carousel doesn't wipe history; pass <c>true</c> for <paramref name="alsoPersistent"/>
        /// to also delete from disk (used by the future "reset lamps" admin action).
        /// </summary>
        public void Clear(bool alsoPersistent = false)
        {
            if (alsoPersistent && repository != null)
            {
                foreach (var id in bestByBeatmap.Keys)
                {
                    try
                    {
                        repository.Delete(id);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[BMS] Lamp store delete failed for {id}: {ex.Message}", LoggingTarget.Database, LogLevel.Important);
                    }
                }
            }

            bestByBeatmap.Clear();
        }

        private void persist(BmsLampRecord record)
        {
            var repo = repository;
            if (repo == null)
                return;

            // Fire-and-forget on the thread pool. The repository is internally synchronous +
            // self-locked; running on the pool keeps gameplay's PrepareScoreForResultsAsync
            // free of any disk-bound wait. Exceptions inside the repo are already logged.
            Task.Run(() =>
            {
                try
                {
                    repo.Upsert(record);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[BMS] Lamp store persist failed: {ex.Message}", LoggingTarget.Database, LogLevel.Important);
                }
            });
        }
    }
}
