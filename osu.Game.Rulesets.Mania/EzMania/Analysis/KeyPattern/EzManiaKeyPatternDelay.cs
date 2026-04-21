// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;

namespace osu.Game.Rulesets.Mania.EzMania.Analysis.KeyPattern
{
    internal static class EzManiaKeyPatternDelay
    {
        public static double Compute(IReadOnlyList<EzManiaKeyPatternRow> rows, int totalColumns, CancellationToken cancellationToken)
        {
            if (rows.Count < 3)
                return 0;

            double delayScore = 0;
            int analysedTriplets = 0;

            for (int i = 1; i < rows.Count - 1; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!EzManiaKeyPatternHelper.TryGetAdjacentPatternMetrics(rows[i - 1], rows[i], totalColumns, out EzManiaAdjacentRowPatternMetrics previousMetrics)
                    || !EzManiaKeyPatternHelper.TryGetAdjacentPatternMetrics(rows[i], rows[i + 1], totalColumns, out EzManiaAdjacentRowPatternMetrics nextMetrics))
                    continue;

                double fineTiming = Math.Max(
                    EzManiaKeyPatternHelper.GetFineTimingSeverity(rows[i - 1].Time, rows[i - 1].BeatLength),
                    Math.Max(
                        EzManiaKeyPatternHelper.GetFineTimingSeverity(rows[i].Time, rows[i].BeatLength),
                        EzManiaKeyPatternHelper.GetFineTimingSeverity(rows[i + 1].Time, rows[i + 1].BeatLength)));

                double fineDensity = Math.Max((previousMetrics.DelayDensity + nextMetrics.DelayDensity) / 2.0, fineTiming);

                if (fineDensity <= 0)
                    continue;

                double directionChaos = previousMetrics.Direction != 0 && nextMetrics.Direction != 0 && previousMetrics.Direction != nextMetrics.Direction ? 1.0 : 0.0;
                double intervalChaos = EzManiaKeyPatternHelper.GetRelativeDifference(previousMetrics.Delta, nextMetrics.Delta);
                double movementChaos = EzManiaKeyPatternHelper.GetRelativeDifference(previousMetrics.CentreStep, nextMetrics.CentreStep);
                double structureChaos = 1 - (previousMetrics.StructureSimilarity + nextMetrics.StructureSimilarity) / 2.0;
                double spread = Math.Max(previousMetrics.NormalizedCentreStep, nextMetrics.NormalizedCentreStep);

                double dumpLike = 0;

                if (previousMetrics.Direction != 0 && previousMetrics.Direction == nextMetrics.Direction)
                {
                    double intervalUniformity = 1 - intervalChaos;
                    double movementUniformity = 1 - movementChaos;
                    double structureUniformity = (previousMetrics.StructureSimilarity + nextMetrics.StructureSimilarity) / 2.0;

                    dumpLike = Math.Clamp(intervalUniformity * 0.45 + movementUniformity * 0.35 + structureUniformity * 0.2, 0, 1);
                }

                double chaos = Math.Clamp(directionChaos * 0.35 + intervalChaos * 0.25 + movementChaos * 0.15 + structureChaos * 0.1 + spread * 0.15, 0, 1);

                if (chaos <= 0.15)
                    continue;

                delayScore += fineDensity * chaos * (1 - dumpLike * 0.85);
                analysedTriplets++;
            }

            if (analysedTriplets == 0)
                return 0;

            return EzManiaKeyPatternHelper.ScaleToTen(delayScore / analysedTriplets);
        }
    }
}
