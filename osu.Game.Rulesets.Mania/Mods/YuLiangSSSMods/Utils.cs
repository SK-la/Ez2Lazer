// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using static osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods.StarRatingRebirth;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class Utils
    {
        public static int SearchSortedLeft(int[] array, int value)
        {
            int index = Array.BinarySearch(array, value);
            return index >= 0 ? index : ~index;
        }

        public static int SearchSortedRight(int[] array, int value)
        {
            int index = Array.BinarySearch(array, value);

            if (index >= 0)
            {
                while (index < array.Length - 1 && array[index + 1] == value) index++;
                return index + 1;
            }

            return ~index;
        }

        public static double[] CumulativeSum(int[] x, double[] f)
        {
            double[] F = new double[x.Length];
            for (int i = 1; i < x.Length; i++) F[i] = F[i - 1] + f[i - 1] * (x[i] - x[i - 1]);
            return F;
        }

        public static double QueryCumsum(int q, int[] x, double[] F, double[] f)
        {
            if (q <= x[0]) return 0.0;
            if (q >= x[^1]) return F[^1];

            int i = SearchSortedLeft(x, q) - 1;
            return F[i] + f[i] * (q - x[i]);
        }

        public static double[] SmoothOnCorners(int[] x, double[] f, int window, double scale = 1.0, string mode = "sum")
        {
            double[] F = CumulativeSum(x, f);
            double[] g = new double[f.Length];

            for (int i = 0; i < x.Length; i++)
            {
                int s = x[i];
                int a = Math.Max(s - window, x[0]);
                int b = Math.Min(s + window, x[^1]);
                double val = QueryCumsum(b, x, F, f) - QueryCumsum(a, x, F, f);

                if (mode == "avg")
                    g[i] = b - a > 0 ? val / (b - a) : 0.0;
                else
                    g[i] = scale * val;
            }

            return g;
        }

        public static double[] InterpValues(int[] newX, int[] oldX, double[] oldVals)
        {
            double[] newVals = new double[newX.Length];

            for (int i = 0; i < newX.Length; i++)
            {
                int idx = SearchSortedLeft(oldX, newX[i]);

                if (idx == 0)
                    newVals[i] = oldVals[0];
                else if (idx >= oldX.Length)
                    newVals[i] = oldVals[^1];
                else
                {
                    double t = (double)(newX[i] - oldX[idx - 1]) / (oldX[idx] - oldX[idx - 1]);
                    newVals[i] = oldVals[idx - 1] + t * (oldVals[idx] - oldVals[idx - 1]);
                }
            }

            return newVals;
        }

        public static double[] StepInterp(int[] newX, int[] oldX, double[] oldVals)
        {
            double[] newVals = new double[newX.Length];

            for (int i = 0; i < newX.Length; i++)
            {
                int idx = SearchSortedRight(oldX, newX[i]) - 1;
                idx = Math.Clamp(idx, 0, oldVals.Length - 1);
                newVals[i] = oldVals[idx];
            }

            return newVals;
        }

        public static double RescaleHigh(double sr)
        {
            if (sr <= 9) return sr;

            return 9 + (sr - 9) * (1 / 1.2);
        }

        public static EasyObject FindNextNoteInColumn(EasyObject note, int[] times, EasyObject[][] noteSeqByColumn)
        {
            int idx = Array.BinarySearch(times, note.Head);
            if (idx < 0) idx = ~idx;

            return idx + 1 < noteSeqByColumn[note.Key].Length ? noteSeqByColumn[note.Key][idx + 1] : new EasyObject(0, int.MaxValue, int.MaxValue);
        }
    }
}
