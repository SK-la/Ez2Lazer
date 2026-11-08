// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;

namespace osu.Game.EzOsuGame.Skills.SkillTag.KeyPattern
{
    internal static class EzManiaKeyPatternJack
    {
        public static double Compute(IReadOnlyList<EzManiaNote> orderedNotes, Func<double, double> beatLengthAt, CancellationToken cancellationToken)
        {
            if (orderedNotes.Count == 0)
                return 0;

            // The notes arrive sorted by time, so appending to per-column lists keeps each column in order without
            // the grouping and re-sorting the previous shape did. Groups stay in first-appearance order so the running
            // sums accumulate in the same sequence as before.
            var orderedGroups = new List<List<EzManiaNote>>();
            var groupByColumn = new Dictionary<int, List<EzManiaNote>>();

            for (int i = 0; i < orderedNotes.Count; i++)
            {
                var note = orderedNotes[i];

                if (!groupByColumn.TryGetValue(note.Column, out var group))
                {
                    group = new List<EzManiaNote>();
                    groupByColumn[note.Column] = group;
                    orderedGroups.Add(group);
                }

                group.Add(note);
            }

            int totalPairs = 0;
            double weightedPairs = 0;

            for (int groupIndex = 0; groupIndex < orderedGroups.Count; groupIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<EzManiaNote> columnNotes = orderedGroups[groupIndex];

                for (int i = 1; i < columnNotes.Count; i++)
                {
                    EzManiaNote previous = columnNotes[i - 1];
                    EzManiaNote current = columnNotes[i];
                    double beatLength = beatLengthAt(previous.TimeMs);

                    if (beatLength <= 0)
                        continue;

                    totalPairs++;

                    double delta = current.TimeMs - previous.TimeMs;

                    if (delta <= beatLength / 4.0 + EzManiaKeyPatternHelper.TIME_TOLERANCE)
                        weightedPairs += 1.0;
                    else if (delta <= beatLength / 2.0 + EzManiaKeyPatternHelper.TIME_TOLERANCE)
                        weightedPairs += 0.6;
                }
            }

            if (totalPairs == 0)
                return 0;

            return EzManiaKeyPatternHelper.ScaleToTen(weightedPairs / totalPairs);
        }
    }
}
