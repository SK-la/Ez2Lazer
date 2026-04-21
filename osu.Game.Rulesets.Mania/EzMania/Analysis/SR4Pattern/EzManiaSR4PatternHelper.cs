// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.EzMania.Analysis.SR4Pattern
{
    internal static class EzManiaSR4PatternHelper
    {
        public static double ComputeMetric(EzManiaSR4PatternSnapshot snapshot, double[] curve, double calibration, double densityInfluence, double columnInfluence)
        {
            if (snapshot.Length == 0 || curve.Length == 0)
                return 0;

            int count = Math.Min(snapshot.Length, curve.Length);
            double weightedSum = 0;
            double totalWeight = 0;

            for (int i = 0; i < count; i++)
            {
                double weight = snapshot.EffectiveWeights[i];

                if (weight <= 0)
                    continue;

                double value = Math.Max(0, curve[i]);

                if (value <= 0)
                    continue;

                double terminalFactor = computeTerminalFactor(snapshot, i, densityInfluence, columnInfluence);
                weightedSum += value * terminalFactor * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0)
                return 0;

            double average = weightedSum / totalWeight;
            return toStarLikeScore(average, calibration);
        }

        private static double computeTerminalFactor(EzManiaSR4PatternSnapshot snapshot, int index, double densityInfluence, double columnInfluence)
        {
            double densityNormalised = Math.Clamp(Math.Log(snapshot.DensityValues[index] + 1) / Math.Log(13), 0, 1);
            double columnNormalised = snapshot.KeyCount <= 1
                ? 0
                : Math.Clamp((snapshot.ActiveColumnValues[index] - 1) / Math.Max(snapshot.KeyCount - 1, 1), 0, 1);

            double densityFactor = 1 + Math.Clamp(densityInfluence, 0, 1) * densityNormalised * 0.45;
            double columnFactor = 1 + Math.Clamp(columnInfluence, 0, 1) * columnNormalised * 0.35;

            return densityFactor * columnFactor;
        }

        private static double toStarLikeScore(double rawValue, double calibration)
        {
            if (rawValue <= 0)
                return 0;

            double reference = Math.Max(calibration, 0.0001);
            double score = rawValue / reference * 10.0;
            return Math.Round(Math.Max(score, 0), 2, MidpointRounding.AwayFromZero);
        }
    }
}
