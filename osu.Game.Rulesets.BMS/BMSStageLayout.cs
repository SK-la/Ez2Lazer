// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Mania;

namespace osu.Game.Rulesets.BMS
{
    /// <summary>
    /// Derived lane metadata for one playable BMS beatmap (column count, scratch indices, input mapping).
    /// </summary>
    public sealed class BMSStageLayout
    {
        /// <summary>
        /// Total columns (mania lanes), derived from highest used column index + 1.
        /// </summary>
        public int TotalColumns { get; }

        /// <summary>
        /// Scratch lane column indices, sorted ascending (left → right on screen).
        /// </summary>
        public IReadOnlyList<int> ScratchColumnIndices { get; }

        public BMSStageLayout(int totalColumns, IReadOnlyList<int> scratchColumnIndices)
        {
            TotalColumns = Math.Max(0, totalColumns);
            ScratchColumnIndices = scratchColumnIndices;
        }

        public static BMSStageLayout FromBeatmap(IBeatmap beatmap)
        {
            var bmsObjects = beatmap.HitObjects.OfType<BMSHitObject>().ToList();
            int totalColumns = bmsObjects.Count == 0
                ? 1
                : bmsObjects.Max(h => h.Column) + 1;

            var scratches = bmsObjects
                            .Where(h => h.IsScratch)
                            .Select(h => h.Column)
                            .Distinct()
                            .OrderBy(c => c)
                            .ToList();

            return new BMSStageLayout(totalColumns, scratches);
        }

        /// <summary>
        /// Map column + scratch flag to key / scratch actions (supports scratch-left or scratch-right via column indices).
        /// </summary>
        public BMSAction ActionFor(BMSHitObject hitObject)
        {
            if (hitObject.IsScratch)
            {
                int idx = indexOfScratchColumn(hitObject.Column);
                return idx <= 0 ? BMSAction.Scratch1 : BMSAction.Scratch2;
            }

            var keyColumns = Enumerable.Range(0, TotalColumns)
                                       .Where(c => !ScratchColumnIndices.Contains(c))
                                       .OrderBy(c => c)
                                       .ToList();

            int keyIndex = keyColumns.IndexOf(hitObject.Column);
            if (keyIndex < 0 || keyIndex > 13)
                return BMSAction.Key1;

            return (BMSAction)((int)BMSAction.Key1 + keyIndex);
        }

        /// <summary>
        /// Value for hidden <see cref="Mania.UI.Column.Action"/> when reusing mania skins on BMS.
        /// Matches <see cref="Mania.UI.ManiaPlayfield"/>: left-to-right lanes use consecutive <see cref="ManiaAction"/> starting at <see cref="ManiaAction.Key1"/> (clamp at <see cref="ManiaAction.Key20"/>).
        /// Scratch side does not remap the enum — lane <paramref name="columnIndex"/> maps to the same ordinal <see cref="ManiaAction"/> as in mania; scratch lanes set <see cref="Mania.UI.Column.IsSpecial"/> on the hidden skin <see cref="Mania.UI.Column"/> (see playfield column ctor) for width / tint behaviour.
        /// </summary>
        public static ManiaAction ManiaSkinActionForColumn(int columnIndex)
        {
            int max = (int)ManiaAction.Key20;
            int clamped = Math.Clamp(columnIndex, 0, max);
            return (ManiaAction)clamped;
        }

        private int indexOfScratchColumn(int column)
        {
            for (int i = 0; i < ScratchColumnIndices.Count; i++)
            {
                if (ScratchColumnIndices[i] == column)
                    return i;
            }

            return -1;
        }
    }
}
