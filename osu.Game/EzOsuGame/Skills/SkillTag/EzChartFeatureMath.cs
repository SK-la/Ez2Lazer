// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Extensions;

namespace osu.Game.EzOsuGame.Skills.SkillTag
{
    /// <summary>Pure helpers ported from mania-hub <c>dan-estimator/math.ts</c>.</summary>
    public static class EzChartFeatureMath
    {
        public static double Clamp(double value, double min, double max)
            => Math.Max(min, Math.Min(max, value));

        public static double Clamp01(double value) => Clamp(value, 0, 1);

        public static double MinGate(params ReadOnlySpan<double> values)
        {
            if (values.Length == 0)
                return 0;

            double min = values[0];
            for (int i = 1; i < values.Length; i++)
                min = Math.Min(min, values[i]);

            return Clamp01(min);
        }

        public static double Quantile(IReadOnlyList<double> values, double q)
        {
            if (values.Count == 0)
                return 0;

            double[] copy = copyValues(values);
            int index = quantileIndex(copy.Length, q);
            return quickselect(copy, index);
        }

        public static double[] Quantiles(IReadOnlyList<double> values, params ReadOnlySpan<double> qs)
        {
            if (values.Count == 0)
                return new double[qs.Length];

            double[] sorted = copyValues(values);
            Array.Sort(sorted);

            double[] result = new double[qs.Length];

            for (int i = 0; i < qs.Length; i++)
                result[i] = sorted[quantileIndex(sorted.Length, qs[i])];

            return result;
        }

        public static int CountInWindow(IReadOnlyList<double> times, double windowMs)
        {
            int best = 0;
            int start = 0;

            for (int end = 0; end < times.Count; end++)
            {
                while (times[end] - times[start] > windowMs)
                    start++;

                best = Math.Max(best, end - start + 1);
            }

            return best;
        }

        public static double Average(IReadOnlyList<double> values)
        {
            if (values.Count == 0)
                return 0;

            double sum = 0;
            for (int i = 0; i < values.Count; i++)
                sum += values[i];

            return sum / values.Count;
        }

        public static double Average(IReadOnlyList<int> values)
        {
            if (values.Count == 0)
                return 0;

            double sum = 0;
            for (int i = 0; i < values.Count; i++)
                sum += values[i];

            return sum / values.Count;
        }

        public static double BucketEntropy(IReadOnlyList<double> values, double bucketSize)
        {
            if (values.Count == 0 || bucketSize <= 0)
                return 0;

            var buckets = new Dictionary<double, int>();

            for (int i = 0; i < values.Count; i++)
            {
                double bucket = Math.Round(values[i] / bucketSize) * bucketSize;
                buckets.Increment(bucket);
            }

            double entropy = 0;

            foreach (int count in buckets.Values)
            {
                double probability = (double)count / values.Count;
                entropy -= probability * Math.Log2(probability);
            }

            return entropy;
        }

        public static double[] BucketValues(IReadOnlyList<double> values, double bucketSize)
        {
            if (bucketSize <= 0)
                return copyValues(values);

            double[] result = new double[values.Count];

            for (int i = 0; i < values.Count; i++)
                result[i] = Math.Round(values[i] / bucketSize) * bucketSize;

            return result;
        }

        public static double RaoQuadraticEntropyLog(IReadOnlyList<double> values, int logIterations)
        {
            if (values.Count < 2)
                return 0;

            var counts = new Dictionary<double, int>();

            for (int i = 0; i < values.Count; i++)
                counts.Increment(values[i]);

            double total = values.Count;
            double entropy = 0;

            foreach (var left in counts)
            {
                foreach (var right in counts)
                {
                    double distance = Math.Abs(left.Key - right.Key);

                    for (int j = 0; j < logIterations; j++)
                        distance = Math.Log(1 + distance);

                    entropy += (left.Value / total) * (right.Value / total) * distance;
                }
            }

            return entropy;
        }

        public static double StrainSpikiness(IReadOnlyList<double> values, IReadOnlyList<double> weights)
        {
            if (values.Count < 3 || values.Count != weights.Count)
                return 0;

            double mean = powerMean(values, weights, 5);
            if (mean <= 0)
                return 0;

            double weightSum = 0;
            for (int i = 0; i < weights.Count; i++)
                weightSum += weights[i];

            if (weightSum <= 0)
                return 0;

            double variance = 0;

            for (int i = 0; i < values.Count; i++)
            {
                double diff = Math.Pow(values[i], 8) - Math.Pow(mean, 8);
                variance += diff * diff * weights[i];
            }

            variance /= weightSum;
            return Math.Sqrt(Math.Pow(variance, 1.0 / 8.0)) / mean;
        }

        private static double powerMean(IReadOnlyList<double> values, IReadOnlyList<double> weights, double exponent)
        {
            if (values.Count == 0)
                return 0;

            double weightSum = 0;
            double sum = 0;

            for (int i = 0; i < values.Count; i++)
            {
                weightSum += weights[i];
                sum += Math.Pow(values[i], exponent) * weights[i];
            }

            if (weightSum <= 0)
                return 0;

            return Math.Pow(sum / weightSum, 1.0 / exponent);
        }

        private static double[] copyValues(IReadOnlyList<double> values)
        {
            double[] copy = new double[values.Count];

            switch (values)
            {
                case double[] array:
                    array.AsSpan().CopyTo(copy);
                    break;

                case List<double> list:
                    list.AsSpan().CopyTo(copy);
                    break;

                default:
                    for (int i = 0; i < values.Count; i++)
                        copy[i] = values[i];
                    break;
            }

            return copy;
        }

        private static int quantileIndex(int length, double q)
            => Math.Min(length - 1, Math.Max(0, (int)Math.Floor((length - 1) * q)));

        private static double quickselect(double[] values, int target)
        {
            int left = 0;
            int right = values.Length - 1;

            while (left < right)
            {
                int pivotIndex = partition(values, left, right, (left + right) / 2);
                if (target == pivotIndex)
                    return values[target];

                if (target < pivotIndex)
                    right = pivotIndex - 1;
                else
                    left = pivotIndex + 1;
            }

            return values[left];
        }

        private static int partition(double[] values, int left, int right, int pivotIndex)
        {
            double pivotValue = values[pivotIndex];
            (values[pivotIndex], values[right]) = (values[right], values[pivotIndex]);
            int storeIndex = left;

            for (int index = left; index < right; index++)
            {
                if (values[index] < pivotValue)
                {
                    (values[storeIndex], values[index]) = (values[index], values[storeIndex]);
                    storeIndex++;
                }
            }

            (values[right], values[storeIndex]) = (values[storeIndex], values[right]);
            return storeIndex;
        }
    }
}
