// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Rulesets.Mania.Beatmaps;

namespace osu.Game.Rulesets.Mania.EzMania.Analysis.KeyPattern
{
    internal static class EzManiaKeyPatternJack
    {
        public static double Compute(ManiaBeatmap beatmap, IReadOnlyList<EzManiaKeyPatternColumnHitObject> columnObjects, CancellationToken cancellationToken)
        {
            var groupedByColumn = columnObjects.GroupBy(obj => obj.Column)
                                               .Select(group => group.OrderBy(obj => obj.StartTime).ToList())
                                               .ToList();

            int totalPairs = 0;
            double weightedPairs = 0;

            for (int groupIndex = 0; groupIndex < groupedByColumn.Count; groupIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<EzManiaKeyPatternColumnHitObject> columnNotes = groupedByColumn[groupIndex];

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
