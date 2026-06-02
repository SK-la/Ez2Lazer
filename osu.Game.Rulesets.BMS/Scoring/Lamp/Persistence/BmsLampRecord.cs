// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.Scoring.Lamp.Persistence
{
    /// <summary>
    /// Single row of the BMS lamp persistence table.
    /// Carries the canonical lamp value plus auxiliary stat columns so future UI (history,
    /// inline tooltips, "personal best" surfaces) can light up without another schema migration.
    /// </summary>
    /// <param name="BeatmapId">
    /// <see cref="osu.Game.Beatmaps.BeatmapInfo"/>.<c>ID</c>. This is stable per Realm-managed
    /// beatmap row, so any rescan that keeps the same beatmap entry preserves history.
    /// </param>
    /// <param name="Lamp">Best lamp seen so far on this beatmap.</param>
    /// <param name="MissCount">Miss count of the play that produced <paramref name="Lamp"/>.</param>
    /// <param name="GreatCount">Great count of the play that produced <paramref name="Lamp"/>.</param>
    /// <param name="GoodCount">Good count of the play that produced <paramref name="Lamp"/>.</param>
    /// <param name="BadCount">Bad count of the play that produced <paramref name="Lamp"/>.</param>
    /// <param name="PerfectGreatCount">PGREAT count of the play that produced <paramref name="Lamp"/>.</param>
    /// <param name="TotalNotes">Judged note count of the play that produced <paramref name="Lamp"/>.</param>
    /// <param name="UpdatedAtUnixMs">UTC wall-clock at write, milliseconds since epoch.</param>
    public readonly record struct BmsLampRecord(
        Guid BeatmapId,
        BmsClearLamp Lamp,
        int MissCount,
        int GreatCount,
        int GoodCount,
        int BadCount,
        int PerfectGreatCount,
        int TotalNotes,
        long UpdatedAtUnixMs);
}
