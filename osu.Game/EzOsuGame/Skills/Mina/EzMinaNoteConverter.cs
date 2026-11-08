// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.EzOsuGame.Skills.Mina
{
    /// <summary>
    /// Converts a playable mania <see cref="IBeatmap"/> into MinaCalc rows
    /// (column bitmask + row time in seconds). Hold notes contribute their head only
    /// (lnTailTaps = false), matching mania-hub's default.
    /// </summary>
    public static class EzMinaNoteConverter
    {
        /// <summary>
        /// Builds MinaCalc rows, or empty when there are no column objects.
        /// </summary>
        /// <remarks>
        /// Rows are keyed by whole milliseconds (same as mania-hub) so a chord shares one bitmask.
        /// osu! charts may start before the audio lead-in; a negative row time walks MinaCalc's
        /// interval index out of bounds and traps the wasm, so such charts are shifted to start at 0
        /// (only inter-row gaps carry difficulty, so the offset is value-preserving).
        /// </remarks>
        public static EzCalcNote[] Convert(IBeatmap beatmap)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            if (isStartTimeSorted(beatmap.HitObjects))
                return toRowsFromBeatmap(beatmap.HitObjects);

            var byTimeMs = new SortedDictionary<long, uint>();

            foreach (HitObject obj in beatmap.HitObjects)
            {
                if (obj is not IHasColumn columnObj)
                    continue;

                int column = columnObj.Column;
                if (column < 0 || column >= 32)
                    continue;

                long timeMs = (long)Math.Round(obj.StartTime);

                byTimeMs.TryGetValue(timeMs, out uint mask);
                byTimeMs[timeMs] = mask | (1u << column);
            }

            return toRows(byTimeMs);
        }

        /// <summary>
        /// Builds rows from an already-extracted chart input, so a pass that measured the structure does not walk
        /// the playable's hit objects a second time.
        /// </summary>
        public static EzCalcNote[] Convert(IReadOnlyList<EzManiaNote> notes)
        {
            ArgumentNullException.ThrowIfNull(notes);

            if (isTimeSorted(notes))
                return toRowsFromNotes(notes);

            var byTimeMs = new SortedDictionary<long, uint>();

            foreach (var note in notes)
            {
                if (note.Column < 0 || note.Column >= 32)
                    continue;

                long timeMs = (long)Math.Round(note.TimeMs);

                byTimeMs.TryGetValue(timeMs, out uint mask);
                byTimeMs[timeMs] = mask | (1u << note.Column);
            }

            return toRows(byTimeMs);
        }

        private static bool isStartTimeSorted(IReadOnlyList<HitObject> hitObjects)
        {
            for (int i = 1; i < hitObjects.Count; i++)
            {
                if (hitObjects[i].StartTime < hitObjects[i - 1].StartTime)
                    return false;
            }

            return true;
        }

        private static bool isTimeSorted(IReadOnlyList<EzManiaNote> notes)
        {
            for (int i = 1; i < notes.Count; i++)
            {
                if (notes[i].TimeMs < notes[i - 1].TimeMs)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Linear build for the common case, where the source is already in time order. Aggregating equal whole
        /// milliseconds into one mask is what makes a chord a single row, so this produces the same rows as the
        /// sorted-dictionary path without its tree and enumerator allocations.
        /// </summary>
        private static EzCalcNote[] toRowsFromBeatmap(IReadOnlyList<HitObject> hitObjects)
        {
            long firstTimeMs = long.MaxValue;

            for (int i = 0; i < hitObjects.Count; i++)
            {
                if (hitObjects[i] is not IHasColumn columnObj || columnObj.Column < 0 || columnObj.Column >= 32)
                    continue;

                long timeMs = (long)Math.Round(hitObjects[i].StartTime);
                if (timeMs < firstTimeMs)
                    firstTimeMs = timeMs;
            }

            if (firstTimeMs == long.MaxValue)
                return Array.Empty<EzCalcNote>();

            long offsetMs = firstTimeMs < 0 ? -firstTimeMs : 0;
            var rows = new EzCalcNote[hitObjects.Count];
            int count = 0;
            long currentTime = 0;
            uint mask = 0;
            bool open = false;

            for (int i = 0; i < hitObjects.Count; i++)
            {
                if (hitObjects[i] is not IHasColumn columnObj || columnObj.Column < 0 || columnObj.Column >= 32)
                    continue;

                long timeMs = (long)Math.Round(hitObjects[i].StartTime);

                if (open && timeMs == currentTime)
                {
                    mask |= 1u << columnObj.Column;
                    continue;
                }

                if (open)
                    rows[count++] = new EzCalcNote(mask, (currentTime + offsetMs) / 1000f);

                currentTime = timeMs;
                mask = 1u << columnObj.Column;
                open = true;
            }

            if (open)
                rows[count++] = new EzCalcNote(mask, (currentTime + offsetMs) / 1000f);

            if (count != rows.Length)
                Array.Resize(ref rows, count);

            return rows;
        }

        /// <inheritdoc cref="toRowsFromBeatmap"/>
        private static EzCalcNote[] toRowsFromNotes(IReadOnlyList<EzManiaNote> notes)
        {
            long firstTimeMs = long.MaxValue;

            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];

                if (note.Column < 0 || note.Column >= 32)
                    continue;

                long timeMs = (long)Math.Round(note.TimeMs);
                if (timeMs < firstTimeMs)
                    firstTimeMs = timeMs;
            }

            if (firstTimeMs == long.MaxValue)
                return Array.Empty<EzCalcNote>();

            long offsetMs = firstTimeMs < 0 ? -firstTimeMs : 0;
            var rows = new EzCalcNote[notes.Count];
            int count = 0;
            long currentTime = 0;
            uint mask = 0;
            bool open = false;

            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];

                if (note.Column < 0 || note.Column >= 32)
                    continue;

                long timeMs = (long)Math.Round(note.TimeMs);

                if (open && timeMs == currentTime)
                {
                    mask |= 1u << note.Column;
                    continue;
                }

                if (open)
                    rows[count++] = new EzCalcNote(mask, (currentTime + offsetMs) / 1000f);

                currentTime = timeMs;
                mask = 1u << note.Column;
                open = true;
            }

            if (open)
                rows[count++] = new EzCalcNote(mask, (currentTime + offsetMs) / 1000f);

            if (count != rows.Length)
                Array.Resize(ref rows, count);

            return rows;
        }

        private static EzCalcNote[] toRows(SortedDictionary<long, uint> byTimeMs)
        {
            if (byTimeMs.Count == 0)
                return Array.Empty<EzCalcNote>();

            // Rows are sorted, so the first entry decides the shift.
            long firstTimeMs = byTimeMs.Keys.First();
            long offsetMs = firstTimeMs < 0 ? -firstTimeMs : 0;

            var rows = new EzCalcNote[byTimeMs.Count];
            int index = 0;

            foreach (var (timeMs, mask) in byTimeMs)
                rows[index++] = new EzCalcNote(mask, (timeMs + offsetMs) / 1000f);

            return rows;
        }
    }
}
