// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;

namespace osu.Game.Rulesets.Mania.EzMania.Analysis.KeyPattern
{
    internal static class EzManiaKeyPatternChord
    {
        public static double Compute(IReadOnlyList<EzManiaKeyPatternRow> rows, CancellationToken cancellationToken)
        {
            if (rows.Count == 0)
                return 0;

            double structureScore = 0;
            int multiNoteRows = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                EzManiaKeyPatternRow row = rows[i];

                if (row.NoteCount < 2)
                    continue;

                multiNoteRows++;

                double pairCount = row.NoteCount - 1;
                double adjacentRatio = row.AdjacentLinkCount / pairCount;
                double separatedRatio = row.SeparatedLinkCount / pairCount;
                double compactness = row.ColumnSpan <= 0 ? 1 : Math.Clamp((row.NoteCount - 1) / (double)row.ColumnSpan, 0, 1);
                double intensity = Math.Clamp((row.NoteCount - 1) / 2.0, 0, 1);

                structureScore += intensity * Math.Clamp(adjacentRatio * 0.72 + compactness * 0.68 - separatedRatio * 0.25, 0, 1);
            }

            if (multiNoteRows == 0)
                return 0;

            return EzManiaKeyPatternHelper.ScaleToTen(structureScore / multiNoteRows);
        }
    }
}
