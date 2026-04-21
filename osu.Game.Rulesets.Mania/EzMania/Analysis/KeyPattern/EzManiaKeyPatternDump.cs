// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;

namespace osu.Game.Rulesets.Mania.EzMania.Analysis.KeyPattern
{
    internal static class EzManiaKeyPatternDump
    {
        public static double Compute(IReadOnlyList<EzManiaKeyPatternRow> rows, int totalColumns, CancellationToken cancellationToken)
        {
            if (rows.Count < 3)
                return 0;

            double dumpScore = 0;
            int analysedTriplets = 0;

            for (int i = 1; i < rows.Count - 1; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!EzManiaKeyPatternHelper.TryGetAdjacentPatternMetrics(rows[i - 1], rows[i], totalColumns, out EzManiaAdjacentRowPatternMetrics previousMetrics)
                    || !EzManiaKeyPatternHelper.TryGetAdjacentPatternMetrics(rows[i], rows[i + 1], totalColumns, out EzManiaAdjacentRowPatternMetrics nextMetrics))
                    continue;

                if (previousMetrics.Direction == 0 || nextMetrics.Direction == 0 || previousMetrics.Direction != nextMetrics.Direction)
                    continue;

                double intervalUniformity = 1 - EzManiaKeyPatternHelper.GetRelativeDifference(previousMetrics.Delta, nextMetrics.Delta);
                double movementUniformity = 1 - EzManiaKeyPatternHelper.GetRelativeDifference(previousMetrics.CentreStep, nextMetrics.CentreStep);
                double structureUniformity = (previousMetrics.StructureSimilarity + nextMetrics.StructureSimilarity) / 2.0;
                double density = (previousMetrics.DumpDensity + nextMetrics.DumpDensity) / 2.0;
                double spread = Math.Max(previousMetrics.NormalizedCentreStep, nextMetrics.NormalizedCentreStep);

                double tripletScore = density * Math.Clamp(intervalUniformity * 0.4 + movementUniformity * 0.35 + structureUniformity * 0.2 + spread * 0.05, 0, 1);

                if (tripletScore <= 0)
                    continue;

                dumpScore += tripletScore;
                analysedTriplets++;
            }

            if (analysedTriplets == 0)
                return 0;

            return EzManiaKeyPatternHelper.ScaleToTen(dumpScore / analysedTriplets);
        }
    }
}
