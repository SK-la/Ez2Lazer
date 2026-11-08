// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading;
using osu.Game.Rulesets.Mania.Beatmaps;

namespace osu.Game.Rulesets.Mania.EzMania.Analysis.KeyPattern
{
    internal static class EzManiaKeyPatternJack
    {
        public static double Compute(ManiaBeatmap beatmap, IReadOnlyList<EzManiaKeyPatternColumnHitObject> columnObjects, CancellationToken cancellationToken)
        {
            if (columnObjects.Count == 0)
                return 0;

            // Column objects already arrive sorted by time, so appending to per-column lists keeps each column in
            // order without the grouping and re-sorting the previous shape did. Groups stay in first-appearance order
            // so the running sums accumulate in the same sequence as before.
            var orderedGroups = new List<List<EzManiaKeyPatternColumnHitObject>>();
            var groupByColumn = new Dictionary<int, List<EzManiaKeyPatternColumnHitObject>>();

            for (int i = 0; i < columnObjects.Count; i++)
            {
                var obj = columnObjects[i];

                if (!groupByColumn.TryGetValue(obj.Column, out var group))
                {
                    group = new List<EzManiaKeyPatternColumnHitObject>();
                    groupByColumn[obj.Column] = group;
                    orderedGroups.Add(group);
                }

                group.Add(obj);
            }

            int totalPairs = 0;
            double weightedPairs = 0;

            for (int groupIndex = 0; groupIndex < orderedGroups.Count; groupIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<EzManiaKeyPatternColumnHitObject> columnNotes = orderedGroups[groupIndex];

                for (int i = 1; i < columnNotes.Count; i++)
                {
                    EzManiaKeyPatternColumnHitObject previous = columnNotes[i - 1];
                    EzManiaKeyPatternColumnHitObject current = columnNotes[i];
                    double beatLength = beatmap.ControlPointInfo.TimingPointAt(previous.StartTime).BeatLength;

                    if (beatLength <= 0)
                        continue;

                    totalPairs++;

                    double delta = current.StartTime - previous.StartTime;

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
