// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.LAsEzExtensions.Analysis;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Rulesets.Mania.LAsEZMania.Analysis
{
    /// <summary>
    ///     Star Rating calculator – C# port of the official Python implementation.
    /// </summary>
    public class SRCalculator
    {
        [Resolved]
        protected RulesetInfo RulesetInfo { get; set; } = null!;

        /// <summary>
        ///     Singleton entry point for SR calculations.
        /// </summary>
        public static SRCalculator Instance { get; } = new SRCalculator();

        #region 工具方法

        /// <summary>
        ///     Computes the star rating for the supplied beatmap synchronously.
        /// </summary>
        /// <param name="beatmap">Beatmap instance.</param>
        /// <param name="times">Timing breakdown produced by the calculation.</param>
        /// <returns>Calculated SR value.</returns>
        public static double CalculateSRWithTime(IBeatmap beatmap, out Dictionary<string, long>? times)
        {
            (double sr, var collectedTimes) = computeInternalXxySR(beatmap, 1.0);
            times = collectedTimes;
            return sr;
        }

        public static double CalculateSR(IBeatmap beatmap)
        {
            (double sr, _) = computeInternalXxySR(beatmap, 1.0);
            return sr;
        }

        /// <summary>
        ///     Computes the star rating with clock rate adjustment (for rate-changing mods like DT/HT).
        /// </summary>
        /// <param name="beatmap">Beatmap instance.</param>
        /// <param name="clockRate">Clock rate multiplier (1.5 for DT, 0.75 for HT, etc.).</param>
        /// <returns>Calculated SR value adjusted for clock rate.</returns>
        public static double CalculateSR(IBeatmap beatmap, double clockRate)
        {
            (double sr, _) = computeInternalXxySR(beatmap, clockRate);
            return sr;
        }

        /// <summary>
        ///     Computes the star rating for the supplied beatmap asynchronously.
        /// </summary>
        /// <param name="beatmap">Beatmap instance.</param>
        /// <returns>Tuple containing the SR value and timing breakdown.</returns>
        public Task<(double sr, Dictionary<string, long> times)> CalculateSRAsync(IBeatmap beatmap)
        {
            return Task.FromResult(computeInternalXxySR(beatmap, 1.0));
        }

        private static (double sr, Dictionary<string, long> times) computeInternalXxySR(IBeatmap beatmap, double clockRate = 1.0)
        {
            var stopwatch = Stopwatch.StartNew();

            ManiaBeatmap maniaBeatmap = (ManiaBeatmap)beatmap;
            // Prefer TotalColumns (reflects keymods / conversion output) over CS.
            int keyCount = Math.Max(1, maniaBeatmap.TotalColumns > 0 ? maniaBeatmap.TotalColumns : (int)Math.Round(maniaBeatmap.BeatmapInfo.Difficulty.CircleSize));

            double sr = XxySRCalculateCore(maniaBeatmap, keyCount, clockRate);
            stopwatch.Stop();

            var timings = new Dictionary<string, long>
            {
                ["Total"] = stopwatch.ElapsedMilliseconds
            };

            return (sr, timings);
        }

        #endregion

        #region 结构体

        private readonly struct NoteStruct : IEquatable<NoteStruct>
        {
            public NoteStruct(int column, int headTime, int tailTime)
            {
                Column = column;
                HeadTime = headTime;
                TailTime = tailTime;
            }

            public int Column { get; }
            public int HeadTime { get; }
            public int TailTime { get; }

            public bool IsLongNote => TailTime >= 0 && TailTime > HeadTime;

            public bool Equals(NoteStruct other)
            {
                return Column == other.Column && HeadTime == other.HeadTime && TailTime == other.TailTime;
            }

            public override bool Equals(object? obj)
            {
                return obj is NoteStruct other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Column, HeadTime, TailTime);
            }
        }

        private readonly struct LNRepStruct
        {
            public LNRepStruct(int[] points, double[] cumulative, double[] values)
            {
                Points = points;
                Cumulative = cumulative;
                Values = values;
            }

            public int[] Points { get; }
            public double[] Cumulative { get; }
            public double[] Values { get; }
        }

        #endregion

        private static readonly Comparison<NoteStruct> note_comparer = compareNotes;

        public static double XxySRCalculateCore(ManiaBeatmap maniaBeatmap, int keyCount, double clockRate = 1.0)
        {
            double[]? cross = CrossMatrixProvider.GetMatrix(keyCount);

            if (cross == null || cross[0] == -1)
            {
                Console.WriteLine($"[SR][ERROR] Key mode {keyCount}k is not supported by the SR algorithm.");
                throw new NotSupportedException($"Key mode {keyCount}k is not supported by the SR algorithm.");
            }

            int estimatedNotes = maniaBeatmap.HitObjects.Count;
            if (estimatedNotes == 0) return 0.0;

            var notes = new List<NoteStruct>(estimatedNotes);
            var notesByColumn = new List<NoteStruct>[keyCount];

            for (int i = 0; i < keyCount; i++)
                notesByColumn[i] = new List<NoteStruct>((estimatedNotes / keyCount) + 1);

            foreach (var hitObject in maniaBeatmap.HitObjects)
            {
                int column = Math.Clamp(hitObject.Column, 0, keyCount - 1);
                // Apply clockRate to timing: faster rate = shorter intervals in calculation
                int head = (int)Math.Round(hitObject.StartTime / clockRate);
                int tail = (int)Math.Round(hitObject.GetEndTime() / clockRate);
                if ((hitObject as IHasDuration)?.EndTime == null)
                    tail = -1;

                // Align with the original library behaviour: treat non-positive durations as non-LN.
                if (tail <= head)
                    tail = -1;

                var note = new NoteStruct(column, head, tail);
                notes.Add(note);
                notesByColumn[column].Add(note);
            }

            notes.Sort(note_comparer);
            foreach (var columnNotes in notesByColumn)
                columnNotes.Sort(note_comparer);

            var longNotes = notes.Where(n => n.IsLongNote).ToList();
            var longNotesByTails = longNotes.OrderBy(n => n.TailTime).ToList();

            double od = maniaBeatmap.BeatmapInfo.Difficulty.OverallDifficulty;
            double x = computeHitLeniency(od);

            int maxHead = notes.Max(n => n.HeadTime);
            int maxTail = longNotes.Count > 0 ? longNotes.Max(n => n.TailTime) : maxHead;
            int totalTime = Math.Max(maxHead, maxTail) + 1;

            //Logger.Log($"C# maxHead: {maxHead}, maxTail: {maxTail}, totalTime: {totalTime}");

            (double[] allCorners, double[] baseCorners, double[] aCorners) = buildCorners(totalTime, notes);

            //Logger.Log($"C# allCorners length: {allCorners.Length}, baseCorners length: {baseCorners.Length}");

            bool[][] keyUsage = buildKeyUsage(keyCount, totalTime, notes, baseCorners);
            int[][] activeColumns = deriveActiveColumns(keyUsage);

            double[][] keyUsage400 = buildKeyUsage400(keyCount, totalTime, notes, baseCorners);
            double[] anchorBase = computeAnchor(keyCount, keyUsage400, baseCorners);

            LNRepStruct? lnRep = longNotes.Count > 0 ? buildLNRepresentation(longNotes, totalTime) : null;

            (double[][] deltaKs, double[] jBarBase) = computeJBar(keyCount, totalTime, x, notesByColumn, baseCorners);
            double[] jBar = interpValues(allCorners, baseCorners, jBarBase);

            double[] xBarBase = computeXBar(keyCount, totalTime, x, notesByColumn, activeColumns, baseCorners, cross);
            double[] xBar = interpValues(allCorners, baseCorners, xBarBase);

            //Logger.Log($"C# xBarBase sample: {string.Join(", ", xBarBase.Take(10))}");

            double[] pBarBase = computePBar(keyCount, totalTime, x, notes, lnRep, anchorBase, baseCorners);
            double[] pBar = interpValues(allCorners, baseCorners, pBarBase);

            //Logger.Log($"C# pBarBase sample: {string.Join(", ", pBarBase.Take(10))}");

            double[] aBarBase = computeABar(keyCount, totalTime, deltaKs, activeColumns, aCorners, baseCorners);
            double[] aBar = interpValues(allCorners, aCorners, aBarBase);

            double[] rBarBase = computeRBar(keyCount, totalTime, x, notesByColumn, longNotesByTails, baseCorners);
            double[] rBar = interpValues(allCorners, baseCorners, rBarBase);

            (double[] cStep, double[] ksStep) = computeCAndKs(keyCount, notes, keyUsage, baseCorners);
            double[] cArr = stepInterp(allCorners, baseCorners, cStep);
            double[] ksArr = stepInterp(allCorners, baseCorners, ksStep);

            double[] gaps = computeGaps(allCorners);
            double[] effectiveWeights = new double[allCorners.Length];
            for (int i = 0; i < allCorners.Length; i++)
                effectiveWeights[i] = cArr[i] * gaps[i];

            double[] dAll = new double[allCorners.Length];

            // Original sequential loop
            // for (int i = 0; i < allCorners.Length; i++)
            // {
            //     ...
            // }

            // Parallel version for better performance
            Parallel.For(0, allCorners.Length, i =>
            {
                double abarExponent = 3.0 / Math.Max(ksArr[i], 1e-6);
                double abarPow = aBar[i] <= 0 ? 0 : Math.Pow(aBar[i], abarExponent);
                double minCandidateContribution = 0.85 * jBar[i];
                double minCandidate = 8 + minCandidateContribution;
                double minJ = Math.Min(jBar[i], minCandidate);
                double jackComponent = abarPow * minJ;
                double term1 = 0.4 * (jackComponent <= 0 ? 0 : Math.Pow(jackComponent, 1.5));

                double scaledP = 0.8 * pBar[i];
                double jackPenalty = rBar[i] * 35.0;
                double ratio = jackPenalty / (cArr[i] + 8);
                double pComponent = scaledP + ratio;
                double powerBase = (aBar[i] <= 0 ? 0 : Math.Pow(aBar[i], 2.0 / 3.0)) * pComponent;
                double term2 = 0.6 * (powerBase <= 0 ? 0 : Math.Pow(powerBase, 1.5));

                double sumTerms = term1 + term2;
                double s = sumTerms <= 0 ? 0 : Math.Pow(sumTerms, 2.0 / 3.0);
                double numerator = abarPow * xBar[i];
                double denominator = xBar[i] + s + 1;
                double tValue = denominator <= 0 ? 0 : numerator / denominator;
                double sqrtComponent = Math.Sqrt(Math.Max(s, 0));
                double primaryImpact = 2.7 * sqrtComponent * (tValue <= 0 ? 0 : Math.Pow(tValue, 1.5));
                double secondaryImpact = s * 0.27;

                dAll[i] = primaryImpact + secondaryImpact;
            });

            double sr = finaliseDifficulty(dAll, effectiveWeights, notes, longNotes);

            //Logger.Log($"C# final SR: {sr}");

            return sr;
        }

        private static double computeHitLeniency(double overallDifficulty)
        {
            double leniency = 0.3 * Math.Sqrt((64.5 - Math.Ceiling(overallDifficulty * 3.0)) / 500.0);
            double offset = leniency - 0.09;

            double scaledOffset = 0.6 * offset;
            double adjustedWindow = scaledOffset + 0.09;
            return Math.Min(leniency, adjustedWindow);
        }

        private static (double[] allCorners, double[] baseCorners, double[] aCorners) buildCorners(int totalTime, List<NoteStruct> notes)
        {
            var baseSet = new HashSet<int>();

            foreach (var note in notes)
            {
                baseSet.Add(note.HeadTime);
                if (note.IsLongNote)
                    baseSet.Add(note.TailTime);
            }

            foreach (int value in baseSet.ToArray())
            {
                baseSet.Add(value + 501);
                baseSet.Add(value - 499);
                baseSet.Add(value + 1);
            }

            baseSet.Add(0);
            baseSet.Add(totalTime);

            double[] baseCorners = baseSet.Where(v => v >= 0 && v <= totalTime).Select(v => (double)v).Distinct().OrderBy(v => v).ToArray();

            var aSet = new HashSet<int>();

            foreach (var note in notes)
            {
                aSet.Add(note.HeadTime);
                if (note.IsLongNote)
                    aSet.Add(note.TailTime);
            }

            foreach (int value in aSet.ToArray())
            {
                aSet.Add(value + 1000);
                aSet.Add(value - 1000);
            }

            aSet.Add(0);
            aSet.Add(totalTime);

            double[] aCorners = aSet.Where(v => v >= 0 && v <= totalTime).Select(v => (double)v).Distinct().OrderBy(v => v).ToArray();

            double[] allCorners = baseCorners.Concat(aCorners).Distinct().OrderBy(v => v).ToArray();
            return (allCorners, baseCorners, aCorners);
        }

        private static bool[][] buildKeyUsage(int keyCount, int totalTime, List<NoteStruct> notes, double[] baseCorners)
        {
            bool[][] keyUsage = new bool[keyCount][];
            for (int i = 0; i < keyCount; i++)
                keyUsage[i] = new bool[baseCorners.Length];

            foreach (var note in notes)
            {
                int start = Math.Max(note.HeadTime - 150, 0);
                int end = note.IsLongNote ? Math.Min(note.TailTime + 150, totalTime - 1) : Math.Min(note.HeadTime + 150, totalTime - 1);

                int left = lowerBound(baseCorners, start);
                int right = lowerBound(baseCorners, end);
                for (int idx = left; idx < right; idx++)
                    keyUsage[note.Column][idx] = true;
            }

            return keyUsage;
        }

        private static int[][] deriveActiveColumns(bool[][] keyUsage)
        {
            int length = keyUsage[0].Length;
            int[][] active = new int[length][];

            for (int i = 0; i < length; i++)
            {
                var list = new List<int>();

                for (int col = 0; col < keyUsage.Length; col++)
                {
                    if (keyUsage[col][i])
                        list.Add(col);
                }

                active[i] = list.ToArray();
            }

            return active;
        }

        private static double[][] buildKeyUsage400(int keyCount, int totalTime, List<NoteStruct> notes, double[] baseCorners)
        {
            double[][] usage = new double[keyCount][];
            for (int k = 0; k < keyCount; k++)
                usage[k] = new double[baseCorners.Length];

            const double base_contribution = 3.75;
            const double falloff = 3.75 / (400.0 * 400.0);

            foreach (var note in notes)
            {
                int startTime = Math.Max(note.HeadTime, 0);
                int endTime = note.IsLongNote ? Math.Min(note.TailTime, totalTime - 1) : note.HeadTime;

                int left400 = lowerBound(baseCorners, startTime - 400);
                int left = lowerBound(baseCorners, startTime);
                int right = lowerBound(baseCorners, endTime);
                int right400 = lowerBound(baseCorners, endTime + 400);

                int duration = endTime - startTime;
                double clampedDuration = Math.Min(duration, 1500);
                double extension = clampedDuration / 150.0;
                double contribution = base_contribution + extension;

                for (int idx = left; idx < right; idx++) usage[note.Column][idx] += contribution;

                for (int idx = left400; idx < left; idx++)
                {
                    double offset = baseCorners[idx] - startTime;
                    double falloffContribution = falloff * Math.Pow(offset, 2);
                    double value = base_contribution - falloffContribution;
                    double clamped = Math.Max(value, 0);
                    usage[note.Column][idx] += clamped;
                }

                for (int idx = right; idx < right400; idx++)
                {
                    double offset = baseCorners[idx] - endTime;
                    double falloffContribution = falloff * Math.Pow(offset, 2);
                    double value = base_contribution - falloffContribution;
                    double clamped = Math.Max(value, 0);
                    usage[note.Column][idx] += clamped;
                }
            }

            return usage;
        }

                #region LN计算

        private static LNRepStruct buildLNRepresentation(List<NoteStruct> longNotes, int totalTime)
        {
            var diff = new Dictionary<int, double>();

            foreach (var note in longNotes)
            {
                int t0 = Math.Min(note.HeadTime + 60, note.TailTime);
                int t1 = Math.Min(note.HeadTime + 120, note.TailTime);

                addToMap(diff, t0, 1.3);
                addToMap(diff, t1, -0.3);
                addToMap(diff, note.TailTime, -1);
            }

            var pointsSet = new SortedSet<int> { 0, totalTime };
            foreach (int key in diff.Keys)
                pointsSet.Add(key);

            int[] points = pointsSet.ToArray();
            double[] cumulative = new double[points.Length];
            double[] values = new double[points.Length - 1];

            double current = 0;

            for (int i = 0; i < points.Length - 1; i++)
            {
                if (diff.TryGetValue(points[i], out double delta))
                    current += delta;

                double fallbackOffset = 0.5 * current;
                double fallback = 2.5 + fallbackOffset;
                double transformed = Math.Min(current, fallback);
                values[i] = transformed;

                int length = points[i + 1] - points[i];
                double segment = length * transformed;
                cumulative[i + 1] = cumulative[i] + segment;
            }

            return new LNRepStruct(points, cumulative, values);
        }

        private static double lnIntegral(LNRepStruct repStruct, int a, int b)
        {
            int[] points = repStruct.Points;
            double[] cumulative = repStruct.Cumulative;
            double[] values = repStruct.Values;

            int startIndex = upperBound(points, a) - 1;
            int endIndex = upperBound(points, b) - 1;

            if (startIndex < 0) startIndex = 0;
            if (endIndex < startIndex) endIndex = startIndex;

            double total = 0;

            if (startIndex == endIndex)
                total = (b - a) * values[startIndex];
            else
            {
                total += (points[startIndex + 1] - a) * values[startIndex];
                total += cumulative[endIndex] - cumulative[startIndex + 1];
                total += (b - points[endIndex]) * values[endIndex];
            }

            return total;
        }

        #endregion

        #region 计算核心

        private static double[] computeAnchor(int keyCount, double[][] keyUsage400, double[] baseCorners)
        {
            double[] anchor = new double[baseCorners.Length];

            for (int i = 0; i < baseCorners.Length; i++)
            {
                double[] counts = new double[keyCount];
                for (int k = 0; k < keyCount; k++)
                    counts[k] = keyUsage400[k][i];

                Array.Sort(counts);
                Array.Reverse(counts);

                double[] nonZero = counts.Where(c => c > 0).ToArray();

                if (nonZero.Length <= 1)
                {
                    anchor[i] = 0;
                    continue;
                }

                double walk = 0;
                double maxWalk = 0;

                for (int idx = 0; idx < nonZero.Length - 1; idx++)
                {
                    double current = nonZero[idx];
                    double next = nonZero[idx + 1];
                    double ratio = next / current;
                    double offset = 0.5 - ratio;
                    double offsetPenalty = 4 * Math.Pow(offset, 2);
                    double damping = 1 - offsetPenalty;
                    walk += current * damping;
                    maxWalk += current;
                }

                double value = maxWalk <= 0 ? 0 : walk / maxWalk;
                anchor[i] = 1 + Math.Min(value - 0.18, 5 * Math.Pow(value - 0.22, 3));
            }

            return anchor;
        }

        private static (double[][] deltaKs, double[] jBar) computeJBar(int keyCount, int totalTime, double x, List<NoteStruct>[] notesByColumn, double[] baseCorners)
        {
            const double default_delta = 1e9;

            double[][] deltaKs = new double[keyCount][];
            double[][] jKs = new double[keyCount][];

            Parallel.For(0, keyCount, k =>
            {
                deltaKs[k] = Enumerable.Repeat(default_delta, baseCorners.Length).ToArray();
                jKs[k] = new double[baseCorners.Length];

                var columnNotes = notesByColumn[k];

                for (int i = 0; i < columnNotes.Count - 1; i++)
                {
                    var current = columnNotes[i];
                    var next = columnNotes[i + 1];

                    int left = lowerBound(baseCorners, current.HeadTime);
                    int right = lowerBound(baseCorners, next.HeadTime);

                    if (right <= left)
                        continue;

                    double headGap = Math.Max(next.HeadTime - current.HeadTime, 1e-6);
                    double delta = 0.001 * headGap;
                    double deltaShift = Math.Abs(delta - 0.08);
                    double penalty = 0.15 + deltaShift;
                    double attenuation = Math.Pow(penalty, -4);
                    double nerfFactor = 7e-5 * attenuation;
                    double jackNerfer = 1 - nerfFactor;

                    double xRoot = Math.Pow(x, 0.25);
                    double rootScale = 0.11 * xRoot;
                    double jackBase = delta + rootScale;
                    double inverseJack = Math.Pow(jackBase, -1);
                    double inverseDelta = 1.0 / delta;
                    double value = inverseDelta * inverseJack * jackNerfer;

                    for (int idx = left; idx < right; idx++)
                    {
                        deltaKs[k][idx] = Math.Min(deltaKs[k][idx], delta);
                        jKs[k][idx] = value;
                    }
                }

                jKs[k] = SmoothOnCorners(baseCorners, jKs[k], 500, 0.001, SmoothMode.Sum);
            });
            // for (int k = 0; k < keyCount; k++)

            double[] jBar = new double[baseCorners.Length];

            for (int idx = 0; idx < baseCorners.Length; idx++)
            {
                double numerator = 0;
                double denominator = 0;

                for (int k = 0; k < keyCount; k++)
                {
                    double v = Math.Max(jKs[k][idx], 0);
                    double weight = 1.0 / Math.Max(deltaKs[k][idx], 1e-9);
                    numerator += Math.Pow(v, 5) * weight;
                    denominator += weight;
                }

                double combined = denominator <= 0 ? 0 : numerator / denominator;
                jBar[idx] = Math.Pow(Math.Max(combined, 0), 0.2);
            }

            return (deltaKs, jBar);
        }

        private static double[] computeXBar(int keyCount, int totalTime, double x, List<NoteStruct>[] notesByColumn, int[][] activeColumns, double[] baseCorners, double[] cross)
        {
            double[][] xKs = new double[keyCount + 1][];
            double[][] fastCross = new double[keyCount + 1][];

            for (int i = 0; i < xKs.Length; i++)
            {
                xKs[i] = new double[baseCorners.Length];
                fastCross[i] = new double[baseCorners.Length];
            }

            // Parallel.For(0, keyCount + 1, k =>
            Parallel.For(0, keyCount + 1, k =>
            {
                var pair = new List<NoteStruct>();

                if (k == 0)
                    pair.AddRange(notesByColumn[0]);
                else if (k == keyCount)
                    pair.AddRange(notesByColumn[keyCount - 1]);
                else
                {
                    pair.AddRange(notesByColumn[k - 1]);
                    pair.AddRange(notesByColumn[k]);
                }

                pair.Sort(note_comparer);
                if (pair.Count < 2) return;

                for (int i = 1; i < pair.Count; i++)
                {
                    var prev = pair[i - 1];
                    var current = pair[i];
                    int left = lowerBound(baseCorners, prev.HeadTime);
                    int right = lowerBound(baseCorners, current.HeadTime);
                    if (right <= left) continue;

                    double delta = 0.001 * Math.Max(current.HeadTime - prev.HeadTime, 1e-6);
                    double val = 0.16 * Math.Pow(Math.Max(x, delta), -2);

                    int idxStart = Math.Min(left, baseCorners.Length - 1);
                    int idxEnd = Math.Min(Math.Max(right, 0), baseCorners.Length - 1);

                    bool condition1 = !contains(activeColumns[idxStart], k - 1) && !contains(activeColumns[idxEnd], k - 1);
                    bool condition2 = !contains(activeColumns[idxStart], k) && !contains(activeColumns[idxEnd], k);
                    if (condition1 || condition2)
                        val *= 1 - cross[Math.Min(k, cross.Length - 1)];

                    for (int idx = left; idx < right; idx++)
                    {
                        xKs[k][idx] = val;
                        fastCross[k][idx] = Math.Max(0, (0.4 * Math.Pow(Math.Max(Math.Max(delta, 0.06), 0.75 * x), -2)) - 80);
                    }
                }
            });
            // for (int k = 0; k <= keyCount; k++)

            double[] xBase = new double[baseCorners.Length];

            for (int idx = 0; idx < baseCorners.Length; idx++)
            {
                double sum = 0;
                for (int k = 0; k <= keyCount; k++)
                    sum += cross[Math.Min(k, cross.Length - 1)] * xKs[k][idx];

                for (int k = 0; k < keyCount; k++)
                {
                    double leftVal = fastCross[k][idx] * cross[Math.Min(k, cross.Length - 1)];
                    double rightVal = fastCross[k + 1][idx] * cross[Math.Min(k + 1, cross.Length - 1)];
                    sum += Math.Sqrt(Math.Max(leftVal * rightVal, 0));
                }

                xBase[idx] = sum;
            }

            return SmoothOnCorners(baseCorners, xBase, 500, 0.001, SmoothMode.Sum);
        }

        private static double[] computePBar(int keyCount, int totalTime, double x, List<NoteStruct> notes, LNRepStruct? lnRep, double[] anchor, double[] baseCorners)
        {
            double[] pStep = new double[baseCorners.Length];

            for (int i = 0; i < notes.Count - 1; i++)
            {
                var leftNote = notes[i];
                var rightNote = notes[i + 1];

                int deltaTime = rightNote.HeadTime - leftNote.HeadTime;

                if (deltaTime <= 0)
                {
                    double invX = 1.0 / Math.Max(x, 1e-6);
                    double spikeInnerBase = 4 * invX;
                    double spikeInner = spikeInnerBase - 24;
                    double spikeBase = 0.02 * spikeInner;
                    if (spikeBase <= 0)
                        continue;

                    double spikeMagnitude = Math.Pow(spikeBase, 0.25);
                    double spike = 1000 * spikeMagnitude;
                    int leftIdx = lowerBound(baseCorners, leftNote.HeadTime);
                    int rightIdx = upperBound(baseCorners, leftNote.HeadTime);
                    for (int idx = leftIdx; idx < rightIdx; idx++)
                        pStep[idx] += spike;

                    continue;
                }

                int left = lowerBound(baseCorners, leftNote.HeadTime);
                int right = lowerBound(baseCorners, rightNote.HeadTime);
                if (right <= left) continue;

                double delta = 0.001 * deltaTime;
                double v = 1;
                if (lnRep.HasValue)
                    v += 6 * 0.001 * lnIntegral(lnRep.Value, leftNote.HeadTime, rightNote.HeadTime);

                double booster = streamBooster(delta);
                double effective = Math.Max(booster, v);

                double inc;

                if (delta < 2 * x / 3)
                {
                    double invX = 1.0 / Math.Max(x, 1e-6);
                    double halfX = x / 2.0;
                    double deltaCentre = delta - halfX;
                    double deltaTerm = 24 * invX * Math.Pow(deltaCentre, 2);
                    double inner = 0.08 * invX * (1 - deltaTerm);
                    double innerClamp = Math.Max(inner, 0);
                    double magnitude = Math.Pow(innerClamp, 0.25);
                    inc = magnitude / Math.Max(delta, 1e-6) * effective;
                }
                else
                {
                    double invX = 1.0 / Math.Max(x, 1e-6);
                    double centreTerm = Math.Pow(x / 6.0, 2);
                    double deltaTerm = 24 * invX * centreTerm;
                    double inner = 0.08 * invX * (1 - deltaTerm);
                    double innerClamp = Math.Max(inner, 0);
                    double magnitude = Math.Pow(innerClamp, 0.25);
                    inc = magnitude / Math.Max(delta, 1e-6) * effective;
                }

                for (int idx = left; idx < right; idx++)
                {
                    double doubled = inc * 2;
                    double limit = Math.Max(inc, doubled - 10);
                    double anchored = inc * anchor[idx];
                    double contribution = Math.Min(anchored, limit);

                    pStep[idx] += contribution;
                }
            }

            return SmoothOnCorners(baseCorners, pStep, 500, 0.001, SmoothMode.Sum);
        }

        private static double[] computeABar(int keyCount, int totalTime, double[][] deltaKs, int[][] activeColumns, double[] aCorners, double[] baseCorners)
        {
            double[] aStep = Enumerable.Repeat(1.0, aCorners.Length).ToArray();

            for (int i = 0; i < aCorners.Length; i++)
            {
                int idx = lowerBound(baseCorners, aCorners[i]);
                idx = Math.Min(idx, baseCorners.Length - 1);
                int[] cols = activeColumns[idx];
                if (cols.Length < 2) continue;

                for (int j = 0; j < cols.Length - 1; j++)
                {
                    int c0 = cols[j];
                    int c1 = cols[j + 1];

                    double deltaGap = Math.Abs(deltaKs[c0][idx] - deltaKs[c1][idx]);
                    double maxDelta = Math.Max(deltaKs[c0][idx], deltaKs[c1][idx]);
                    double offset = Math.Max(maxDelta - 0.11, 0);
                    double offsetContribution = 0.4 * offset;
                    double diff = deltaGap + offsetContribution;

                    if (diff < 0.02)
                    {
                        double factorBase = Math.Max(deltaKs[c0][idx], deltaKs[c1][idx]);
                        double factorContribution = 0.5 * factorBase;
                        double factor = 0.75 + factorContribution;
                        aStep[i] *= Math.Min(factor, 1);
                    }
                    else if (diff < 0.07)
                    {
                        double factorBase = Math.Max(deltaKs[c0][idx], deltaKs[c1][idx]);
                        double growth = 5 * diff;
                        double factorContribution = 0.5 * factorBase;
                        double factor = 0.65 + growth + factorContribution;
                        aStep[i] *= Math.Min(factor, 1);
                    }
                }
            }

            return SmoothOnCorners(aCorners, aStep, 250, 0, SmoothMode.Average);
        }

        private static double[] computeRBar(int keyCount, int totalTime, double x, List<NoteStruct>[] notesByColumn, List<NoteStruct> tailNotes, double[] baseCorners)
        {
            if (tailNotes.Count < 2) return new double[baseCorners.Length];

            double[] iList = new double[tailNotes.Count];

            for (int idx = 0; idx < tailNotes.Count; idx++)
            {
                var note = tailNotes[idx];
                var next = findNextColumnNote(note, notesByColumn);
                double nextHead = next?.HeadTime ?? 1_000_000_000;

                double ih = 0.001 * Math.Abs(note.TailTime - note.HeadTime - 80) / Math.Max(x, 1e-6);
                double it = 0.001 * Math.Abs(nextHead - note.TailTime - 80) / Math.Max(x, 1e-6);

                iList[idx] = 2 / (2 + Math.Exp(-5 * (ih - 0.75)) + Math.Exp(-5 * (it - 0.75)));
            }

            double[] rStep = new double[baseCorners.Length];

            for (int idx = 0; idx < tailNotes.Count - 1; idx++)
            {
                var current = tailNotes[idx];
                var next = tailNotes[idx + 1];

                int left = lowerBound(baseCorners, current.TailTime);
                int right = lowerBound(baseCorners, next.TailTime);
                if (right <= left) continue;

                double delta = 0.001 * Math.Max(next.TailTime - current.TailTime, 1e-6);
                double invSqrtDelta = Math.Pow(delta, -0.5);
                double invX = 1.0 / Math.Max(x, 1e-6);
                double blend = iList[idx] + iList[idx + 1];
                double blendContribution = 0.8 * blend;
                double modulation = 1 + blendContribution;
                double strength = 0.08 * invSqrtDelta * invX * modulation;

                for (int baseIdx = left; baseIdx < right; baseIdx++)
                    rStep[baseIdx] = Math.Max(rStep[baseIdx], strength);
            }

            return SmoothOnCorners(baseCorners, rStep, 500, 0.001, SmoothMode.Sum);
        }

        #endregion

        private static (double[] cStep, double[] ksStep) computeCAndKs(int keyCount, List<NoteStruct> notes, bool[][] keyUsage, double[] baseCorners)
        {
            double[] cStep = new double[baseCorners.Length];
            double[] ksStep = new double[baseCorners.Length];

            var noteTimesList = new List<double>(notes.Count);
            foreach (var note in notes)
                noteTimesList.Add(note.HeadTime);
            noteTimesList.Sort();
            double[] noteTimes = noteTimesList.ToArray();

            for (int idx = 0; idx < baseCorners.Length; idx++)
            {
                double left = baseCorners[idx] - 500;
                double right = baseCorners[idx] + 500;

                int leftIndex = lowerBound(noteTimes, left);
                int rightIndex = lowerBound(noteTimes, right);
                cStep[idx] = Math.Max(rightIndex - leftIndex, 0);

                int activeCount = 0;

                for (int col = 0; col < keyCount; col++)
                {
                    if (keyUsage[col][idx])
                        activeCount++;
                }

                ksStep[idx] = Math.Max(activeCount, 1);
            }

            return (cStep, ksStep);
        }

        private static double[] computeGaps(double[] corners)
        {
            if (corners.Length == 0)
                return Array.Empty<double>();

            double[] gaps = new double[corners.Length];

            if (corners.Length == 1)
            {
                gaps[0] = 0;
                return gaps;
            }

            gaps[0] = (corners[1] - corners[0]) / 2.0;
            gaps[^1] = (corners[^1] - corners[^2]) / 2.0;

            for (int i = 1; i < corners.Length - 1; i++) gaps[i] = (corners[i + 1] - corners[i - 1]) / 2.0;

            return gaps;
        }

        private static double finaliseDifficulty(List<double> difficulties, List<double> weights, List<NoteStruct> notes, List<NoteStruct> longNotes)
        {
            var combined = difficulties.Zip(weights, (d, w) => (d, w)).OrderBy(pair => pair.d).ToList();
            if (combined.Count == 0)
                return 0;

            double[] sortedD = combined.Select(p => p.d).ToArray();
            double[] sortedWeights = combined.Select(p => Math.Max(p.w, 0)).ToArray();

            double[] cumulative = new double[sortedWeights.Length];
            cumulative[0] = sortedWeights[0];
            for (int i = 1; i < sortedWeights.Length; i++)
                cumulative[i] = cumulative[i - 1] + sortedWeights[i];

            double totalWeight = Math.Max(cumulative[^1], 1e-9);
            double[] norm = cumulative.Select(v => v / totalWeight).ToArray();

            double[] targets = { 0.945, 0.935, 0.925, 0.915, 0.845, 0.835, 0.825, 0.815 };
            double percentile93 = 0;
            double percentile83 = 0;

            for (int i = 0; i < 4; i++)
            {
                int index = Math.Min(bisectLeft(norm, targets[i]), sortedD.Length - 1);
                percentile93 += sortedD[index];
            }

            percentile93 /= 4.0;

            for (int i = 4; i < 8; i++)
            {
                int index = Math.Min(bisectLeft(norm, targets[i]), sortedD.Length - 1);
                percentile83 += sortedD[index];
            }

            percentile83 /= 4.0;

            //Logger.Log($"C# percentile93: {percentile93}, percentile83: {percentile83}");

            double weightedMeanNumerator = 0;
            for (int i = 0; i < sortedD.Length; i++)
                weightedMeanNumerator += Math.Pow(sortedD[i], 5) * sortedWeights[i];

            double weightedMean = Math.Pow(Math.Max(weightedMeanNumerator / totalWeight, 0), 0.2);

            //Logger.Log($"C# weightedMean: {weightedMean}");

            double topComponent = 0.25 * 0.88 * percentile93;
            double middleComponent = 0.2 * 0.94 * percentile83;
            double meanComponent = 0.55 * weightedMean;
            double sr = topComponent + middleComponent + meanComponent;
            sr = Math.Pow(sr, 1.0) / Math.Pow(8, 1.0) * 8;

            //Logger.Log($"C# sr before notes adjustment: {sr}");

            double totalNotes = notes.Count;

            foreach (var ln in longNotes)
            {
                double len = Math.Min(ln.TailTime - ln.HeadTime, 1000);
                totalNotes += 0.5 * (len / 200.0);
            }

            //Logger.Log($"C# totalNotes: {totalNotes}");

            sr *= totalNotes / (totalNotes + 60);
            sr = rescaleHigh(sr);
            sr *= 0.975;

            //Logger.Log($"C# final SR: {sr}");

            return sr;
        }

        private static double finaliseDifficulty(double[] difficulties, double[] weights, List<NoteStruct> notes, List<NoteStruct> longNotes)
        {
            return finaliseDifficulty(difficulties.ToList(), weights.ToList(), notes, longNotes);
        }

        private static NoteStruct? findNextColumnNote(NoteStruct note, List<NoteStruct>[] notesByColumn)
        {
            var columnNotes = notesByColumn[note.Column];
            int index = columnNotes.IndexOf(note);
            if (index >= 0 && index + 1 < columnNotes.Count)
                return columnNotes[index + 1];

            return null;
        }

        private static double[] interpValues(double[] newX, double[] oldX, double[] oldVals)
        {
            double[] result = new double[newX.Length];

            for (int i = 0; i < newX.Length; i++)
            {
                double x = newX[i];

                if (x <= oldX[0])
                {
                    result[i] = oldVals[0];
                    continue;
                }

                if (x >= oldX[^1])
                {
                    result[i] = oldVals[^1];
                    continue;
                }

                int idx = lowerBound(oldX, x);

                if (idx < oldX.Length && nearlyEquals(oldX[idx], x))
                {
                    result[i] = oldVals[idx];
                    continue;
                }

                int prev = Math.Max(idx - 1, 0);
                double x0 = oldX[prev];
                double x1 = oldX[idx];
                double y0 = oldVals[prev];
                double y1 = oldVals[idx];
                double deltaY = y1 - y0;
                double deltaX = x - x0;
                double numerator = deltaY * deltaX;
                double fraction = numerator / (x1 - x0);
                result[i] = y0 + fraction;
            }

            return result;
        }

        private static double[] stepInterp(double[] newX, double[] oldX, double[] oldVals)
        {
            double[] result = new double[newX.Length];

            for (int i = 0; i < newX.Length; i++)
            {
                int idx = upperBound(oldX, newX[i]) - 1;
                if (idx < 0)
                    idx = 0;
                result[i] = oldVals[Math.Min(idx, oldVals.Length - 1)];
            }

            return result;
        }

        private static double[] SmoothOnCorners(double[] positions, double[] values, double window, double scale, SmoothMode mode)
        {
            if (positions.Length == 0)
                return Array.Empty<double>();

            double[] cumulative = buildCumulative(positions, values);
            double[] output = new double[positions.Length];

            for (int i = 0; i < positions.Length; i++)
            {
                double s = positions[i];
                double a = Math.Max(s - window, positions[0]);
                double b = Math.Min(s + window, positions[^1]);

                if (b <= a)
                {
                    output[i] = 0;
                    continue;
                }

                double integral = queryIntegral(positions, cumulative, values, b) - queryIntegral(positions, cumulative, values, a);

                if (mode == SmoothMode.Average)
                    output[i] = integral / Math.Max(b - a, 1e-9);
                else
                    output[i] = integral * scale;
            }

            return output;
        }

        private static double[] buildCumulative(double[] positions, double[] values)
        {
            double[] cumulative = new double[positions.Length];

            for (int i = 1; i < positions.Length; i++)
            {
                double width = positions[i] - positions[i - 1];
                double increment = values[i - 1] * width;
                cumulative[i] = cumulative[i - 1] + increment;
            }

            return cumulative;
        }

        private static double queryIntegral(double[] positions, double[] cumulative, double[] values, double point)
        {
            if (point <= positions[0])
                return 0;
            if (point >= positions[^1])
                return cumulative[^1];

            int idx = lowerBound(positions, point);
            if (idx < positions.Length && nearlyEquals(positions[idx], point))
                return cumulative[idx];

            int prev = Math.Max(idx - 1, 0);
            double delta = point - positions[prev];
            double contribution = values[prev] * delta;

            return cumulative[prev] + contribution;
        }

        private static double streamBooster(double delta)
        {
            double inv = 7.5 / Math.Max(delta, 1e-6);
            if (inv <= 160 || inv >= 360)
                return 1;

            double shifted = inv - 160;
            double distance = inv - 360;
            double adjustment = 1.7e-7 * shifted * Math.Pow(distance, 2);

            return 1 + adjustment;
        }

        private static bool contains(int[] array, int target)
        {
            if (target < 0)
                return false;

            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == target)
                    return true;
            }

            return false;
        }

        private static int lowerBound(double[] array, double value)
        {
            int left = 0;
            int right = array.Length;

            while (left < right)
            {
                int span = right - left;
                int mid = left + (span >> 1);
                if (array[mid] < value)
                    left = mid + 1;
                else
                    right = mid;
            }

            return left;
        }

        private static int lowerBound(double[] array, int value)
        {
            return lowerBound(array, (double)value);
        }

        private static int lowerBound(int[] array, double value)
        {
            int left = 0;
            int right = array.Length;

            while (left < right)
            {
                int span = right - left;
                int mid = left + (span >> 1);
                if (array[mid] < value)
                    left = mid + 1;
                else
                    right = mid;
            }

            return left;
        }

        private static int upperBound(int[] array, int value)
        {
            int left = 0;
            int right = array.Length;

            while (left < right)
            {
                int span = right - left;
                int mid = left + (span >> 1);
                if (array[mid] <= value)
                    left = mid + 1;
                else
                    right = mid;
            }

            return left;
        }

        private static int upperBound(double[] array, double value)
        {
            int left = 0;
            int right = array.Length;

            while (left < right)
            {
                int span = right - left;
                int mid = left + (span >> 1);
                if (array[mid] <= value)
                    left = mid + 1;
                else
                    right = mid;
            }

            return left;
        }

        private static int bisectLeft(double[] array, double value)
        {
            int left = 0;
            int right = array.Length;

            while (left < right)
            {
                int span = right - left;
                int mid = left + (span >> 1);
                if (array[mid] < value)
                    left = mid + 1;
                else
                    right = mid;
            }

            return left;
        }

        private static double safePow(double value, double exponent)
        {
            if (value <= 0)
                return 0;

            double result = Math.Pow(value, exponent);

            return result;
        }

        private static double rescaleHigh(double sr)
        {
            double excess = sr - 9;
            double normalized = excess / 1.2;
            double softened = 9 + normalized;

            return sr <= 9 ? sr : softened;
        }

        private static int clamp(int value, int min, int max)
        {
            return Math.Min(Math.Max(value, min), max);
        }

        private static bool nearlyEquals(double a, double b, double epsilon = 1e-9)
        {
            return Math.Abs(a - b) <= epsilon;
        }

        private static int compareNotes(NoteStruct a, NoteStruct b)
        {
            int headCompare = a.HeadTime.CompareTo(b.HeadTime);
            return headCompare != 0 ? headCompare : a.Column.CompareTo(b.Column);
        }

        private static void addToMap(Dictionary<int, double> map, int key, double value)
        {
            if (!map.TryAdd(key, value))
                map[key] += value;
        }

        private enum SmoothMode
        {
            Sum,
            Average
        }
    }
}
