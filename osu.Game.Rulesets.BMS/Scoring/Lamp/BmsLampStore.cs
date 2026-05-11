// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.BMS.Scoring.Lamp
{
    /// <summary>
    /// Process-wide lookup of "best lamp seen so far" per BMS beatmap.
    /// Acts as a thin facade for the rest of the BMS UI: nothing else needs to know
    /// where the lamp data came from (Realm scores, beatoraja import, network, …).
    /// </summary>
    /// <remarks>
    /// The store currently keeps everything in memory and starts empty. Persistence
    /// hook-up (write on score submission, read on app boot) is intentionally left
    /// for a follow-up so the lamp <em>template</em> can land independently of the
    /// scoring pipeline. Producers (score submission, beatoraja importer) should call
    /// <see cref="ReportPlay"/>; consumers (carousel panel, details wedge) call
    /// <see cref="GetLamp(BeatmapInfo)"/>.
    /// </remarks>
    public class BmsLampStore
    {
        private readonly IBmsLampScheme scheme;
        private readonly ConcurrentDictionary<Guid, BmsClearLamp> bestLampByBeatmap = new ConcurrentDictionary<Guid, BmsClearLamp>();

        public BmsLampStore(IBmsLampScheme scheme)
        {
            this.scheme = scheme;
        }

        /// <summary>
        /// The lamp scheme currently in use. UI components that want lamp colours
        /// should route through this property rather than holding their own reference.
        /// </summary>
        public IBmsLampScheme Scheme => scheme;

        /// <summary>
        /// Look up the best lamp recorded for the given beatmap, or <see cref="BmsClearLamp.NoPlay"/>
        /// if nothing has been recorded yet.
        /// </summary>
        public BmsClearLamp GetLamp(BeatmapInfo beatmap)
        {
            if (beatmap == null)
                return BmsClearLamp.NoPlay;

            return bestLampByBeatmap.TryGetValue(beatmap.ID, out var lamp) ? lamp : BmsClearLamp.NoPlay;
        }

        /// <summary>
        /// Record a finished play for <paramref name="beatmap"/>. The store keeps the
        /// best (highest-ranked) lamp seen for the beatmap so far.
        /// </summary>
        public BmsClearLamp ReportPlay(BeatmapInfo beatmap, BmsLampContext context)
        {
            var lamp = scheme.ResolveLamp(context);

            if (beatmap == null)
                return lamp;

            bestLampByBeatmap.AddOrUpdate(
                beatmap.ID,
                lamp,
                (_, existing) => (int)lamp > (int)existing ? lamp : existing);

            return lamp;
        }

        /// <summary>
        /// Test/import hook: directly seed the best lamp for a beatmap, bypassing
        /// score reconstruction. Used by the beatoraja importer.
        /// </summary>
        public void SetLamp(BeatmapInfo beatmap, BmsClearLamp lamp)
        {
            if (beatmap == null)
                return;

            bestLampByBeatmap[beatmap.ID] = lamp;
        }

        /// <summary>
        /// Clear all recorded lamps. Used by the "refresh library" flow when the
        /// underlying chart catalogue is rebuilt from scratch.
        /// </summary>
        public void Clear() => bestLampByBeatmap.Clear();
    }
}
