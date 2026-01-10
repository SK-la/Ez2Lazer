// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public partial class StarRatingRebirthNoTask : Mod, IApplicableAfterBeatmapConversion
    {
        public override string Name => "Star Rating Rebirth NoTask";

        public override string Acronym => "SRR";

        public override LocalisableString Description => EzManiaModStrings.StarRatingRebirthNoTask_Description;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        [SettingSource("Use original OD", "High Priority")]
        public BindableBool Original { get; set; } = new BindableBool(false);

        [SettingSource("Use custom OD", "Low Priority")]
        public BindableBool Custom { get; set; } = new BindableBool();

        [SettingSource("OD", "Choose the OD you want to recalculate.")]
        public BindableDouble OD { get; set; } = new BindableDouble(0)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 15
        };

        public void ApplyToBeatmap(IBeatmap beatmap)
        { }

        public class EasyObject
        {
            public double StartTime;
            public double EndTime;
            public int Column;

            public EasyObject(double startTime, double endTime, int column)
            {
                StartTime = startTime;
                EndTime = endTime;
                Column = column;
            }

            public static EasyObject[] FromManiaObjects(List<ManiaHitObject> objects)
            {
                var easyObjects = new EasyObject[objects.Count];
                for (int i = 0; i < objects.Count; i++) easyObjects[i] = new EasyObject(objects[i].StartTime, objects[i].GetEndTime(), objects[i].Column);
                return easyObjects;
            }

            public static EasyObject[] FromRate(EasyObject[] objects, double rate)
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    objects[i].StartTime = objects[i].StartTime * (1 / rate);
                    objects[i].EndTime = objects[i].EndTime * (1 / rate);
                }

                return objects;
            }
        }

        public static double CalculateStarRating(List<ManiaHitObject> objects, double od, int keys, double rate)
        {
            double lambda_n = 5;
            double lambda_1 = 0.11;
            double lambda_2 = 7;
            double lambda_3 = 24;
            double lambda_4 = 0.1;
            double w_0 = 0.4;
            double w_1 = 2.7;
            double w_2 = 0.27;
            double p_0 = 1.0;
            double p_1 = 1.5;

            double x = 0.3 * Math.Pow((64.5 - Math.Ceiling(od * 3)) / 500.0, 0.5);
            //for (int i = 0; i < objects.Count; i++)
            //{
            //    objects[i].StartTime = (int)objects[i].StartTime;
            //}

            var hit = EasyObject.FromManiaObjects(objects);
            if (rate != 1) hit = EasyObject.FromRate(hit, rate);

            var note_seq = hit.OrderBy(t => t.StartTime).ThenBy(t => t.Column).ToArray();

            // Preprocessing (Completed)
            var note_seq_by_column = note_seq.GroupBy(n => n.Column).OrderBy(g => g.Key).Select(g => g.ToArray()).ToArray();
            var LN_seq = note_seq
                         .Where(t => t.EndTime != t.StartTime)
                         .ToArray();
            var tail_seq = LN_seq
                           .OrderBy(t => t.EndTime)
                           .ToArray();

            var LN_list = new List<List<EasyObject>>(keys);
            for (int i = 0; i < keys; i++) LN_list.Add(new List<EasyObject>(1500));

            foreach (var ln in LN_seq) LN_list[ln.Column].Add(ln);

            var LN_seq_by_column = LN_list.ToList();

            LN_seq_by_column = LN_seq_by_column
                               .Where(list => list != null && list.Count > 0)
                               //.OrderBy(t => t.First().Column)
                               .ToList();

            int K = keys;
            int T = (int)(note_seq.Max(t => Math.Max(t.StartTime, t.EndTime)) + 1);

            // Helper Functions (Completed)
            double[] smooth(double[] list)
            {
                double[] lstbar = new double[T];
                double window_sum = list.Take(Math.Min(500, T)).Sum();

                for (int s = 0; s < T; s++)
                {
                    lstbar[s] = 0.001 * window_sum;
                    if (s + 500 < T) window_sum += list[s + 500];

                    if (s - 500 >= 0) window_sum -= list[s - 500];
                }

                return lstbar;
            }

            double[] smooth2(double[] list)
            {
                double[] lstbar = new double[T];
                int window_len = Math.Min(500, T);
                double window_sum = list.Take(window_len).Sum();

                for (int s = 0; s < T; s++)
                {
                    lstbar[s] = window_sum / window_len;

                    if (s + 500 < T)
                    {
                        window_sum += list[s + 500];
                        window_len += 1;
                    }

                    if (s - 500 >= 0)
                    {
                        window_sum -= list[s - 500];
                        window_len -= 1;
                    }
                }

                return lstbar;
            }

            // Section 2.3 (Completed)
            double jackNerfer(double delta) => 1 - 7 * 1e-5 * Math.Pow(0.15 + Math.Abs(delta - 0.08), -4);

            double[][] J_ks = new double[K][];
            double[][] delta_ks = new double[K][];

            double t1 = lambda_1 * Math.Pow(x, 1.0 / 4.0);

            for (int k = 0; k < K; k++)
            {
                J_ks[k] = new double[T];
                delta_ks[k] = new double[T];
                var noteColumn = note_seq_by_column[k];
                Array.Fill(delta_ks[k], 1e9);

                for (int i = 0; i < noteColumn.Length - 1; i++)
                {
                    double startTimeI = noteColumn[i].StartTime;
                    double startTimeIPlus1 = noteColumn[i + 1].StartTime;
                    double delta = 0.001 * (startTimeIPlus1 - startTimeI);
                    double val = Math.Pow(delta, -1.0) * Math.Pow(delta + t1, -1.0) * jackNerfer(delta);
                    double timeLength = startTimeIPlus1 - startTimeI;
                    var deltaSpan = new Span<double>(delta_ks[k], (int)startTimeI, (int)timeLength);
                    var JksSpan = new Span<double>(J_ks[k], (int)startTimeI, (int)timeLength);

                    deltaSpan.Fill(delta);
                    JksSpan.Fill(val);
                }
            }

            double[][] Jbar_ks = new double[K][];
            for (int k = 0; k < K; k++) Jbar_ks[k] = smooth(J_ks[k]);

            double[] Jbar = new double[T];

            for (int s = 0; s < T; s++)
            {
                double sum1 = 0.0;
                double sum2 = 0.0;

                for (int i = 0; i < K; i++)
                {
                    double weight = 1 / delta_ks[i][s];
                    sum2 += weight;
                    sum1 += Math.Pow(Math.Max(Jbar_ks[i][s], 0), lambda_n) * weight;
                }

                double weighted_avg = Math.Pow(sum1 / Math.Max(1e-9, sum2), 1.0 / lambda_n);
                Jbar[s] = weighted_avg;
            }

            // Section 2.4 (Completed)
            double[][] X_ks = new double[K + 1][];

            for (int k = 0; k <= K; k++)
            {
                X_ks[k] = new double[T];
                var notes_in_pair = k == 0 ? note_seq_by_column[0] :
                    k == K ? note_seq_by_column[K - 1] :
                    note_seq_by_column[k - 1].Concat(note_seq_by_column[k])
                                             .OrderBy(n => n.StartTime).ToArray();
                int pairLength = notes_in_pair.Length;
                double previousStartTime = pairLength > 0 ? notes_in_pair[0].StartTime : 0;

                for (int i = 1; i < pairLength; i++)
                {
                    double currentStartTime = notes_in_pair[i].StartTime;
                    double delta = 0.001 * (currentStartTime - previousStartTime);
                    if (delta <= 0) continue;
                    double val = 0.16 * Math.Pow(Math.Max(x, delta), -2.0);
                    int starts = (int)previousStartTime;
                    int ends = (int)currentStartTime;
                    for (int s = starts; s < ends; s++) X_ks[k][s] = val;
                    previousStartTime = currentStartTime;
                }
            }

            double[][] cross_matrix =
            [
                [-1],
                [0.075, 0.075],
                [0.125, 0.05, 0.125],
                [0.125, 0.125, 0.125, 0.125],
                [0.175, 0.25, 0.05, 0.25, 0.175],
                [0.175, 0.25, 0.175, 0.175, 0.25, 0.175],
                [0.225, 0.35, 0.25, 0.05, 0.25, 0.35, 0.225],
                [0.225, 0.35, 0.25, 0.225, 0.225, 0.25, 0.35, 0.225],
                [0.275, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.275],
                [0.275, 0.45, 0.35, 0.25, 0.275, 0.275, 0.25, 0.35, 0.45, 0.275],
                [0.325, 0.55, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.55, 0.325]
            ];

            double[] X = new double[T];
            int[] K_rangePlus1 = Enumerable.Range(0, K + 1).ToArray();
            for (int s = 0; s < T; s++) X[s] = K_rangePlus1.Sum(k => X_ks[k][s] * cross_matrix[K][k]);

            double[] Xbar = smooth(X);

            // Section 2.5 (Completed)
            double[] P = new double[T];
            double[] LN_bodies = new double[T];

            foreach (var tuple in LN_seq)
            {
                int t2 = (int)Math.Min(tuple.StartTime + 80, tuple.EndTime);
                for (int temp = (int)tuple.StartTime; temp < t2; temp++) LN_bodies[temp] += 0.5;

                for (int temp = t2; temp < tuple.EndTime; temp++) LN_bodies[temp] += 1;
            }

            double b(double delta)
            {
                double val = 7.5 / delta;
                if (160 < val && val < 360) return 1 + 1.4 * Math.Pow(10, -7) * (val - 160) * Math.Pow(val - 360, 2);
                return 1;
            }

            for (int i = 0; i < note_seq.Length - 1; i++)
            {
                double delta = 0.001 * (note_seq[i + 1].StartTime - note_seq[i].StartTime);

                if (delta < 1e-9)
                    P[(int)note_seq[i].StartTime] += 1000 * Math.Pow(0.02 * (4.0 / x - lambda_3), 1.0 / 4.0);
                else
                {
                    double h_l = note_seq[i].StartTime;
                    double h_r = note_seq[i + 1].StartTime;
                    double v = 1 + lambda_2 * 0.001 * LN_bodies.Skip((int)h_l).Take((int)(h_r - h_l)).Sum();

                    if (delta < 2 * x / 3)
                    {
                        double baseVal = Math.Pow(0.08 * Math.Pow(x, -1) *
                                                  (1 - lambda_3 * Math.Pow(x, -1) * Math.Pow(delta - x / 2, 2)), 1.0 / 4) *
                            b(delta) * v / delta;

                        for (int s = (int)h_l; s < (int)h_r; s++)
                            P[s] += baseVal;
                    }
                    else
                    {
                        double baseVal = Math.Pow(0.08 * Math.Pow(x, -1) *
                                                  (1 - lambda_3 * Math.Pow(x, -1) * Math.Pow(x / 6, 2)), 1.0 / 4) *
                            b(delta) * v / delta;

                        for (int s = (int)h_l; s < (int)h_r; s++)
                            P[s] += baseVal;
                    }
                }
            }

            double[] Pbar = smooth(P);

            // Section 2.6 (Completed)
            bool[][] KU_ks = new bool[K][];
            for (int k = 0; k < K; k++) KU_ks[k] = new bool[T];

            foreach (var note in note_seq)
            {
                double startTime = Math.Max(0, note.StartTime - 500);
                double endTime = Math.Min(note.EndTime == note.StartTime ? note.StartTime + 500 : note.EndTime + 500, T - 1);

                for (int s = (int)startTime; s < (int)endTime; s++) KU_ks[note.Column][s] = true;
            }

            int[] K_s = new int[T];
            double[][] dks = new double[K - 1][];
            double[] A = new double[T];
            Array.Fill(A, 1);

            for (int k = 0; k < K - 1; k++) dks[k] = new double[T];

            for (int s = 0; s < T; s++)
            {
                var cols = new List<int>(K);

                for (int k = 0; k < K; k++)
                {
                    if (KU_ks[k][s])
                        cols.Add(k);
                }

                K_s[s] = Math.Max(cols.Count, 1);

                for (int i = 0; i < cols.Count - 1; i++)
                {
                    if (cols[i + 1] > K - 1) continue;

                    double currentDks = Math.Abs(delta_ks[cols[i]][s] - delta_ks[cols[i + 1]][s])
                                        + Math.Max(0, Math.Max(delta_ks[cols[i + 1]][s], delta_ks[cols[i]][s]) - 0.3);
                    dks[cols[i]][s] = currentDks;

                    if (currentDks < 0.02)
                        A[s] *= Math.Min(0.75 + 0.5 * Math.Max(delta_ks[cols[i + 1]][s], delta_ks[cols[i]][s]), 1);
                    else if (currentDks < 0.07) A[s] *= Math.Min(0.65 + 5 * currentDks + 0.5 * Math.Max(delta_ks[cols[i + 1]][s], delta_ks[cols[i]][s]), 1);
                }
            }

            double[] Abar = smooth2(A);

            // Section 2.7 (Completed)
            (int, double, double) find_next_note_in_column(EasyObject note, EasyObject[][] note_seq_by_column)
            {
                int k = note.Column;
                double h = note.StartTime;

                double[] second_values = new double[note_seq_by_column[k].Length];
                for (int i = 0; i < second_values.Length; i++) second_values[i] = note_seq_by_column[k][i].StartTime;

                int index = Array.BinarySearch(second_values, h);
                if (index < 0) index = ~index;

                return index + 1 < second_values.Length
                    ? (note_seq_by_column[k][index + 1].Column, note_seq_by_column[k][index + 1].StartTime, note_seq_by_column[k][index + 1].EndTime)
                    : (0, 1e9, 1e9);
            }

            double[] I = new double[LN_seq.Length];

            for (int i = 0; i < tail_seq.Length; i++)
            {
                (int Column, double StartTime, double endTime) next = find_next_note_in_column(tail_seq[i], note_seq_by_column);
                double l_h = 0.001 * Math.Abs(tail_seq[i].EndTime - tail_seq[i].StartTime - 80) / x;
                double l_t = 0.001 * Math.Abs(next.StartTime - tail_seq[i].EndTime - 80) / x;
                I[i] = 2.0 / (2.0 + Math.Exp(-5.0 * (l_h - 0.75)) + Math.Exp(-5.0 * (l_t - 0.75)));
            }

            double[] Is = new double[T];
            double[] R = new double[T];

            if (tail_seq.Length > 0)
            {
                for (int i = 0; i < tail_seq.Length - 1; i++)
                {
                    double delta_r = 0.001 * (tail_seq[i + 1].EndTime - tail_seq[i].EndTime);

                    for (int s = (int)tail_seq[i].EndTime; s < (int)tail_seq[i + 1].EndTime; s++)
                    {
                        Is[s] = 1 + I[i];
                        R[s] = 0.08 * Math.Pow(delta_r, -1.0 / 2.0) * Math.Pow(x, -1) * (1 + lambda_4 * (I[i] + I[i + 1]));
                    }
                }
            }

            double[] Rbar = smooth(R);

            // Section 3 (Completed)
            double[] C = new double[T];
            int start = 0;
            int end = 0;

            for (int t = 0; t < T; t++)
            {
                while (start < note_seq.Length && note_seq[start].StartTime < t - 500) start += 1;

                while (end < note_seq.Length && note_seq[end].StartTime < t + 500) end += 1;
                C[t] = end - start;
            }

            double[] S = new double[T];
            double[] D = new double[T];

            for (int t = 0; t < T; t++)
            {
                Jbar[t] = Math.Max(0, Jbar[t]);
                Xbar[t] = Math.Max(0, Xbar[t]);
                Pbar[t] = Math.Max(0, Pbar[t]);
                Abar[t] = Math.Max(0, Abar[t]);
                Rbar[t] = Math.Max(0, Rbar[t]);
                C[t] = Math.Max(0, C[t]);
                K_s[t] = Math.Max(0, K_s[t]);

                double term1 = w_0 * Math.Pow(Math.Pow(Abar[t], 3.0 / K_s[t]) * Jbar[t], 1.5);
                double term2 = (1 - w_0) * Math.Pow(Math.Pow(Abar[t], 2.0 / 3) *
                                                    (0.8 * Pbar[t] + Rbar[t]), 1.5);
                S[t] = Math.Pow(term1 + term2, 2.0 / 3);

                double T_t = Math.Pow(Abar[t], 3.0 / K_s[t]) * Xbar[t] / (Xbar[t] + S[t] + 1);
                D[t] = w_1 * Math.Pow(S[t], 1.0 / 2) * Math.Pow(T_t, p_1) + S[t] * w_2;
            }

            double weightedSum = 0.0;
            double weightSum = C.Sum();

            for (int t = 0; t < T; t++) weightedSum += Math.Pow(D[t], lambda_n) * C[t];

            double SR = Math.Pow(weightedSum / weightSum, 1.0 / lambda_n);
            SR = Math.Pow(SR, p_0) / Math.Pow(8.0, p_0) * 8;
            SR *= (note_seq.Length + 0.5 * LN_seq.Length) / (note_seq.Length + 0.5 * LN_seq.Length + 60);
            if (SR <= 2.0) SR = Math.Sqrt(SR * 2);
            SR *= 0.96 + 0.01 * K;
            // SR *= 0.88+0.03*K

            return SR;
        }

        /// <summary>
        /// Return null if style is wrong.
        /// </summary>
        /// <param name="objects"></param>
        /// <param name="cs"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public static List<ManiaHitObject>? NTM(List<ManiaHitObject> objects, int keys, int cs)
        {
            var Rng = new Random();

            var newObjects = new List<ManiaHitObject>();

            var newColumnObjects = new List<ManiaHitObject>();

            var fixedColumnObjects = new List<ManiaHitObject>();

            var locations = objects.OfType<Note>().Select(n =>
                                   (
                                       startTime: n.StartTime,
                                       samples: n.Samples,
                                       column: n.Column,
                                       endTime: n.StartTime,
                                       duration: n.StartTime - n.StartTime
                                   ))
                                   .Concat(objects.OfType<HoldNote>().Select(h =>
                                   (
                                       startTime: h.StartTime,
                                       samples: h.Samples,
                                       column: h.Column,
                                       endTime: h.EndTime,
                                       duration: h.EndTime - h.StartTime
                                   ))).OrderBy(h => h.startTime).ThenBy(n => n.column).ToList();

            int keyvalue = keys + 1;
            bool firstKeyFlag = true;

            int nullColumn = Rng.Next(-1, 1 + keyvalue - 2);

            while (keyvalue <= keys)
            {
                var confirmnull = new List<bool>();
                for (int i = 0; i <= keys; i++) confirmnull.Add(false);
                var nullcolumnlist = new List<int>();

                if (firstKeyFlag)
                {
                    foreach (var column in objects.GroupBy(h => h.Column))
                    {
                        int count = column.Count();
                        if (!confirmnull[column.Key] && count != 0) confirmnull[column.Key] = true;
                    }

                    for (int i = 0; i < keys; i++)
                    {
                        if (!confirmnull[i])
                            nullcolumnlist.Add(i);
                    }

                    firstKeyFlag = false;
                }

                int atLeast = 5;

                double changetime = 0;

                bool plus = true;
                bool minus = false;
                bool next = false;

                for (int i = 0; i < locations.Count; i++)
                {
                    bool isLN = false;
                    var note = new Note();
                    var hold = new HoldNote();
                    int columnnum = locations[i].column;
                    int minuscolumn = 0;

                    foreach (int nul in nullcolumnlist)
                    {
                        if (columnnum > nul)
                            minuscolumn++;
                    }

                    columnnum -= minuscolumn;
                    int testcolumn = columnnum;
                    atLeast--;

                    if (locations[i].startTime == locations[i].endTime)
                    {
                        note.StartTime = locations[i].startTime;
                        note.Samples = locations[i].samples;
                    }
                    else
                    {
                        hold.StartTime = locations[i].startTime;
                        hold.Samples = locations[i].samples;
                        hold.EndTime = locations[i].endTime;
                        isLN = true;
                    }

                    bool error = changetime != locations[i].startTime;

                    if (keys < 4)
                        columnnum = Rng.Next(keyvalue);
                    else
                    {
                        if (error && Rng.Next(100) < 70 /*Probability.Value*/ && atLeast < 0)
                        {
                            changetime = locations[i].startTime;
                            atLeast = keys - 2;
                            next = true;
                        }

                        if (next && plus)
                        {
                            next = false;
                            nullColumn++;

                            if (nullColumn > keyvalue - 2)
                            {
                                plus = !plus;
                                minus = !minus;
                                nullColumn = keyvalue - 2;
                            }
                        }
                        else if (next && minus)
                        {
                            next = false;
                            nullColumn--;

                            if (nullColumn < -1)
                            {
                                plus = !plus;
                                minus = !minus;
                                nullColumn = -1;
                            }
                        }

                        if (columnnum > nullColumn) columnnum++;
                    }

                    bool overlap = FindOverlap(newColumnObjects, columnnum, locations[i].startTime, locations[i].endTime);

                    if (overlap)
                    {
                        for (int k = 0; k < keyvalue; k++)
                        {
                            if (!FindOverlap(newColumnObjects, columnnum - k, locations[i].startTime, locations[i].endTime) && columnnum - k >= 0)
                                columnnum -= k;
                            else if (!FindOverlap(newColumnObjects, columnnum + k, locations[i].startTime, locations[i].endTime) && columnnum + k <= keyvalue - 1) columnnum += k;
                        }
                    }

                    if (isLN)
                    {
                        newColumnObjects.Add(new HoldNote
                        {
                            Column = columnnum,
                            StartTime = locations[i].startTime,
                            Duration = locations[i].endTime - locations[i].startTime,
                            NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                        });
                    }
                    else
                    {
                        newColumnObjects.Add(new Note
                        {
                            Column = columnnum,
                            StartTime = locations[i].startTime,
                            Samples = locations[i].samples
                        });
                    }
                }

                for (int i = 0; i < newColumnObjects.Count; i++)
                {
                    bool overlap = false, outindex = false;

                    if (newColumnObjects[i].Column < 0 || newColumnObjects[i].Column > keys - 1)
                    {
                        outindex = true;
                        newColumnObjects[i].Column = Rng.Next(keys - 1);
                    }

                    for (int j = i + 1; j < newColumnObjects.Count; j++)
                    {
                        if (newColumnObjects[i].Column == newColumnObjects[j].Column && newColumnObjects[i].StartTime >= newColumnObjects[j].StartTime - 2
                                                                                     && newColumnObjects[i].StartTime <= newColumnObjects[j].StartTime + 2) overlap = true;

                        if (newColumnObjects[j].StartTime != newColumnObjects[j].GetEndTime())
                        {
                            if (newColumnObjects[i].Column == newColumnObjects[j].Column && newColumnObjects[i].StartTime >= newColumnObjects[j].StartTime - 2
                                                                                         && newColumnObjects[i].StartTime <= newColumnObjects[j].GetEndTime() + 2)
                                overlap = true;
                        }
                    }

                    if (outindex) overlap = true;

                    if (!overlap)
                        fixedColumnObjects.Add(newColumnObjects[i]);
                    else
                    {
                        for (int k = 0; k < keyvalue; k++)
                        {
                            if (!FindOverlap(newColumnObjects[i], newColumnObjects.Where(h => h.Column == newColumnObjects[i].Column - k).ToList()) && newColumnObjects[i].Column - k >= 0)
                                newColumnObjects[i].Column -= k;
                            else if (!FindOverlap(newColumnObjects[i], newColumnObjects.Where(h => h.Column == newColumnObjects[i].Column + k).ToList())
                                     && newColumnObjects[i].Column + k <= keyvalue - 1) newColumnObjects[i].Column += k;
                        }

                        fixedColumnObjects.Add(newColumnObjects[i]);
                    }
                }

                if (keyvalue < keys)
                {
                    keys++;
                    keyvalue = keys + 1;

                    locations = fixedColumnObjects.OfType<Note>().Select(n =>
                                                  (
                                                      startTime: n.StartTime,
                                                      samples: n.Samples,
                                                      column: n.Column,
                                                      endTime: n.StartTime,
                                                      duration: n.StartTime - n.StartTime
                                                  ))
                                                  .Concat(fixedColumnObjects.OfType<HoldNote>().Select(h =>
                                                  (
                                                      startTime: h.StartTime,
                                                      samples: h.Samples,
                                                      column: h.Column,
                                                      endTime: h.EndTime,
                                                      duration: h.EndTime - h.StartTime
                                                  ))).OrderBy(h => h.startTime).ThenBy(n => n.column).ToList();
                    ;
                    nullColumn = -1;
                    fixedColumnObjects.Clear();
                    newColumnObjects.Clear();
                }
                else
                    break;
            }

            newObjects.AddRange(fixedColumnObjects);

            return newObjects;
        }

        public (int Left, int Right) FindConsecutive(List<int> othercolumn, int number, int keys)
        {
            int left = -1, right = keys - 1;

            foreach (int consecutive in othercolumn)
            {
                if (consecutive > number) // right
                    right = Math.Min(right, consecutive);

                if (consecutive < number) // left
                    left = Math.Max(left, consecutive);
            }

            return (left, right);
        }

        public static bool FindOverlap(List<ManiaHitObject> hitobj, int column, double starttime, double endtime)
        {
            foreach (var obj in hitobj)
            {
                if (obj.Column == column && starttime <= obj.StartTime && starttime >= obj.StartTime) return true;

                if (obj.StartTime != obj.GetEndTime())
                {
                    if (obj.Column == column && starttime >= obj.StartTime && starttime <= obj.GetEndTime())
                    {
                        if (endtime != starttime)
                        {
                            if (endtime >= obj.StartTime && endtime <= obj.GetEndTime())
                                return true;
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        public static bool FindOverlap(ManiaHitObject hitobj, List<ManiaHitObject> objs)
        {
            return FindOverlap(objs, hitobj.Column, hitobj.StartTime, hitobj.GetEndTime());
        }

        public static bool FindOverlap(ManiaHitObject hitobj, int column, double starttime, double endtime)
        {
            List<ManiaHitObject> onenote = [hitobj];
            return FindOverlap(onenote, column, starttime, endtime);
        }

        public static bool FindOverlap(List<ManiaHitObject> hitobj)
        {
            for (int i = 0; i < hitobj.Count; i++)
            {
                for (int j = i + 1; j < hitobj.Count; j++)
                {
                    if (hitobj[i].Column == hitobj[j].Column && hitobj[i].StartTime == hitobj[j].StartTime) return true;

                    if (hitobj[j].StartTime != hitobj[j].GetEndTime())
                    {
                        if (hitobj[i].Column == hitobj[j].Column && hitobj[i].StartTime >= hitobj[j].StartTime - 2 && hitobj[i].StartTime <= hitobj[j].GetEndTime() + 2)
                        {
                            if (hitobj[i].GetEndTime() != hitobj[j].StartTime)
                            {
                                if (hitobj[i].GetEndTime() >= hitobj[j].StartTime - 2 && hitobj[i].GetEndTime() <= hitobj[j].GetEndTime() + 2)
                                    return true;
                            }

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static List<ManiaHitObject> DP(List<ManiaHitObject> objects, int style)
        {
            var newObjects = new List<ManiaHitObject>();

            var newColumnObjects = new List<ManiaHitObject>();

            var locations = objects.OfType<Note>().Select(n =>
                                   (
                                       startTime: n.StartTime,
                                       samples: n.Samples,
                                       column: n.Column,
                                       endTime: n.StartTime
                                   ))
                                   .Concat(objects.OfType<HoldNote>().Select(h =>
                                   (
                                       startTime: h.StartTime,
                                       samples: h.Samples,
                                       column: h.Column,
                                       endTime: h.EndTime
                                   ))).OrderBy(h => h.startTime).ToList();

            for (int i = 0; i < locations.Count; i++)
            {
                var note = new Note();
                var hold = new HoldNote();
                int columnnum = locations[i].column;

                switch (columnnum)
                {
                    case 1:
                    {
                        columnnum = 0;
                    }
                        break;

                    case 3:
                    {
                        columnnum = 1;
                    }
                        break;

                    case 5:
                    {
                        columnnum = 2;
                        if (style >= 5 && style <= 8) columnnum = 4;
                    }
                        break;

                    case 7:
                    {
                        columnnum = 3;
                        if (style >= 5 && style <= 8) columnnum = 5;
                    }
                        break;
                }

                if (locations[i].startTime == locations[i].endTime)
                {
                    note.StartTime = locations[i].startTime;
                    note.Samples = locations[i].samples;
                }
                else
                {
                    hold.StartTime = locations[i].startTime;
                    hold.Samples = locations[i].samples;
                    hold.EndTime = locations[i].endTime;
                }

                switch (style)
                {
                    case 1:
                    {
                        newColumnObjects.AddNote(locations[i].samples, columnnum, locations[i].startTime, locations[i].endTime);
                        newColumnObjects.AddNote(locations[i].samples, 4 + columnnum, locations[i].startTime, locations[i].endTime);
                    }
                        break;

                    case 2:
                    {
                        newColumnObjects.AddNote(locations[i].samples, 3 - columnnum, locations[i].startTime, locations[i].endTime);
                        newColumnObjects.AddNote(locations[i].samples, 7 - columnnum, locations[i].startTime, locations[i].endTime);
                    }
                        break;

                    case 3:
                    {
                        newColumnObjects.AddNote(locations[i].samples, columnnum, locations[i].startTime, locations[i].endTime);
                        newColumnObjects.AddNote(locations[i].samples, 7 - columnnum, locations[i].startTime, locations[i].endTime);
                    }
                        break;

                    case 4:
                    {
                        newColumnObjects.AddNote(locations[i].samples, 3 - columnnum, locations[i].startTime, locations[i].endTime);
                        newColumnObjects.AddNote(locations[i].samples, 4 + columnnum, locations[i].startTime, locations[i].endTime);
                    }
                        break;

                    case 5:
                    {
                        newColumnObjects.AddNote(locations[i].samples, columnnum, locations[i].startTime, locations[i].endTime);
                        newColumnObjects.AddNote(locations[i].samples, 2 + columnnum, locations[i].startTime, locations[i].endTime);
                    }
                        break;

                    case 6:
                    {
                        if (columnnum <= 1) columnnum = 3 - columnnum;

                        if (columnnum >= 4) columnnum = 7 - columnnum + 4;
                        newColumnObjects.AddNote(locations[i].samples, columnnum, locations[i].startTime, locations[i].endTime);
                        newColumnObjects.AddNote(locations[i].samples, columnnum - 2, locations[i].startTime, locations[i].endTime);
                    }
                        break;

                    case 7:
                    case 8:
                    {
                        if (style == 8)
                        {
                            if (columnnum == 0 || columnnum == 4)
                                columnnum++;
                            else if (columnnum == 1 || columnnum == 5) columnnum--;
                        }

                        if (columnnum < 4)
                        {
                            newColumnObjects.AddNote(locations[i].samples, columnnum, locations[i].startTime, locations[i].endTime);
                            newColumnObjects.AddNote(locations[i].samples, 3 - columnnum, locations[i].startTime, locations[i].endTime);
                        }

                        if (columnnum > 3)
                        {
                            newColumnObjects.AddNote(locations[i].samples, columnnum, locations[i].startTime, locations[i].endTime);
                            newColumnObjects.AddNote(locations[i].samples, 7 - (columnnum - 4), locations[i].startTime, locations[i].endTime);
                        }
                    }
                        break;
                }
            }

            newObjects.AddRange(newColumnObjects);
            return newObjects;
        }

        public static List<ManiaHitObject> NTMA(IBeatmap beatmap, int blank, int toKey, int gapValue, int cleanDivide)
        {
            var Rng = new Random();
            const double error = 1.5;
            const double interval = 50;
            const double ln_interval = 10;

            var maniaBeatmap = (ManiaBeatmap)beatmap;

            int keys = (int)maniaBeatmap.Difficulty.CircleSize;

            if (blank > toKey - keys) blank = toKey - keys;

            if (keys > 9 || toKey <= keys) return new List<ManiaHitObject>();

            var newObjects = new List<ManiaHitObject>();

            var locations = maniaBeatmap.HitObjects.OfType<Note>().Select(n =>
                                        (
                                            column: n.Column,
                                            startTime: n.StartTime,
                                            endTime: n.StartTime,
                                            samples: n.Samples
                                        ))
                                        .Concat(maniaBeatmap.HitObjects.OfType<HoldNote>().Select(h =>
                                        (
                                            column: h.Column,
                                            startTime: h.StartTime,
                                            endTime: h.EndTime,
                                            samples: h.Samples
                                        ))).OrderBy(h => h.startTime).ThenBy(n => n.column).ToList();

            var confirmNull = new List<bool>();
            var nullColumnList = new List<int>();

            for (int i = 0; i <= toKey; i++) confirmNull.Add(false);

            foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
            {
                int count = column.Count();
                if (!confirmNull[column.Key] && count != 0) confirmNull[column.Key] = true;
            }

            for (int i = 0; i < toKey; i++)
            {
                if (!confirmNull[i])
                    nullColumnList.Add(i);
            }

            for (int i = 0; i < locations.Count; i++)
            {
                int minusColumn = 0;

                foreach (int nul in nullColumnList)
                {
                    if (locations[i].column > nul)
                        minusColumn++;
                }

                var thisLocations = locations[i];
                thisLocations.column -= minusColumn;
                locations[i] = thisLocations;
            }

            var area = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();
            var checkList = new List<ManiaHitObject>();

            var tempObjects = locations.OrderBy(h => h.startTime).ToList();

            double sumTime = 0;
            double lastTime = 0;

            foreach (var timingPoint in tempObjects.GroupBy(h => h.startTime))
            {
                var newLocations = timingPoint.OfType<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>()
                                              .Select(n => (Column: n.column, StartTime: n.startTime, EndTime: n.endTime, Samples: n.samples)).OrderBy(h => h.Column).ToList();

                var line = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();

                foreach (var note in newLocations) line.Add((note.Column, note.StartTime, note.EndTime, note.Samples));

                //manyLine.Add(line);
                int blankColumn = blank;

                sumTime += timingPoint.Key - lastTime;
                lastTime = timingPoint.Key;

                area.AddRange(line);

                double gap = 29998.8584 * Math.Pow(Math.E, -0.3176 * gapValue) + 347.7248;

                if (gapValue == 0) gap = double.MaxValue;

                if (sumTime >= gap)
                {
                    sumTime = 0;
                    // Process area
                    var processed = ProcessArea(maniaBeatmap, Rng, area, keys, toKey, blank, cleanDivide, error, checkList);
                    newObjects.AddRange(processed.result);
                    checkList = processed.checkList.ToList();
                    area.Clear();
                }
            }

            if (area.Count > 0)
            {
                var processed = ProcessArea(maniaBeatmap, Rng, area, keys, toKey, blank, cleanDivide, error, checkList);
                newObjects.AddRange(processed.result);
            }

            newObjects = newObjects.OrderBy(h => h.StartTime).ToList();

            var cleanObjects = new List<ManiaHitObject>();

            foreach (var column in newObjects.GroupBy(h => h.Column))
            {
                var newColumnObjects = new List<ManiaHitObject>();

                var cleanLocations = column.OfType<Note>().Select(n => (startTime: n.StartTime, samples: n.Samples, endTime: n.StartTime))
                                           .Concat(column.OfType<HoldNote>().SelectMany(h => new[]
                                           {
                                               (startTime: h.StartTime, samples: h.GetNodeSamples(0), endTime: h.EndTime)
                                           }))
                                           .OrderBy(h => h.startTime).ToList();

                double lastStartTime = cleanLocations[0].startTime;
                double lastEndTime = cleanLocations[0].endTime;

                for (int i = 0; i < cleanLocations.Count; i++)
                {
                    if (i == 0)
                    {
                        lastStartTime = cleanLocations[0].startTime;
                        lastEndTime = cleanLocations[0].endTime;
                        continue;
                    }

                    if (cleanLocations[i].startTime >= lastStartTime && cleanLocations[i].startTime <= lastEndTime)
                    {
                        cleanLocations.RemoveAt(i);
                        i--;
                        continue;
                    } // if the note in a LN

                    if (Math.Abs(cleanLocations[i].startTime - lastStartTime) <= interval)
                    {
                        lastStartTime = cleanLocations[i].startTime;
                        lastEndTime = cleanLocations[i].endTime;
                        cleanLocations.RemoveAt(i);
                        i--;
                        continue;
                    } // interval judgement

                    if (Math.Abs(cleanLocations[i].startTime - lastEndTime) <= ln_interval)
                    {
                        lastStartTime = cleanLocations[i].startTime;
                        lastEndTime = cleanLocations[i].endTime;
                        cleanLocations.RemoveAt(i);
                        i--;
                        continue;
                    } // LN interval judgement

                    if (lastStartTime != lastEndTime)
                    {
                        newColumnObjects.Add(new HoldNote
                        {
                            Column = column.Key,
                            StartTime = lastStartTime,
                            Duration = lastEndTime - lastStartTime,
                            NodeSamples = [cleanLocations[i].samples, Array.Empty<HitSampleInfo>()]
                        });
                    }
                    else
                    {
                        newColumnObjects.Add(new Note
                        {
                            Column = column.Key,
                            StartTime = lastStartTime,
                            Samples = cleanLocations[i].samples
                        });
                    }

                    lastStartTime = cleanLocations[i].startTime;
                    lastEndTime = cleanLocations[i].endTime;
                }

                cleanObjects.AddRange(newColumnObjects);
            }

            return cleanObjects.OrderBy(h => h.StartTime).ToList();
        }

        public static (List<ManiaHitObject> result, List<ManiaHitObject> checkList) ProcessArea(ManiaBeatmap beatmap, Random Rng,
                                                                                                List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)> hitObjects,
                                                                                                int fromKeys, int toKeys, int blankNum = 0, int clean = 0, double error = 0,
                                                                                                List<ManiaHitObject>? checkList = null)
        {
            var newObjects = new List<ManiaHitObject>();
            List<(int column, bool isBlank)> copyColumn = [];
            List<int> insertColumn = [];
            List<ManiaHitObject> checkColumn = [];
            bool isFirst = true;

            int num = toKeys - fromKeys - blankNum;

            while (num > 0)
            {
                int copy = Rng.Next(fromKeys);

                if (!copyColumn.Contains((copy, false)))
                {
                    copyColumn.Add((copy, false));
                    num--;
                }
            }

            num = blankNum;

            while (num > 0)
            {
                int copy = -1;
                copyColumn.Add((copy, true));
                num--;
            }

            num = toKeys - fromKeys;

            while (num > 0)
            {
                int insert = Rng.Next(toKeys);

                if (!insertColumn.Contains(insert))
                {
                    insertColumn.Add(insert);
                    num--;
                }
            }

            insertColumn = insertColumn.OrderBy(c => c).ToList();

            foreach (var timingPoint in hitObjects.GroupBy(h => h.startTime))
            {
                var locations = timingPoint.OfType<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>().ToList();
                var tempObjects = new List<ManiaHitObject>();
                int length = copyColumn.Count;

                for (int i = 0; i < locations.Count; i++)
                {
                    int column = locations[i].column;

                    for (int j = 0; j < length; j++)
                    {
                        if (column == copyColumn[j].column && !copyColumn[j].isBlank) tempObjects.AddNote(locations[i].samples, insertColumn[j], locations[i].startTime, locations[i].endTime);

                        if (locations[i].column >= insertColumn[j]) locations[i] = (locations[i].column + 1, locations[i].startTime, locations[i].endTime, locations[i].samples);
                    }

                    tempObjects.AddNote(locations[i].samples, locations[i].column, locations[i].startTime, locations[i].endTime);
                }

                if (isFirst && checkList is not null && checkList.Count > 0 && clean > 0)
                {
                    var checkC = checkList.Select(h => h.Column).ToList();
                    var checkS = checkList.Select(h => h.StartTime).ToList();

                    for (int i = 0; i < tempObjects.Count; i++)
                    {
                        if (checkC.Contains(tempObjects[i].Column))
                        {
                            if (clean != 0)
                            {
                                double beatLength = beatmap.ControlPointInfo.TimingPointAt(tempObjects[i].StartTime).BeatLength;
                                double timeDivide = beatLength / clean;
                                int index = checkC.IndexOf(tempObjects[i].Column);

                                if (tempObjects[i].StartTime - checkS[index] < timeDivide + error)
                                {
                                    tempObjects.RemoveAt(i);
                                    i--;
                                }
                            }
                            else
                            {
                                tempObjects.RemoveAt(i);
                                i--;
                            }
                        }
                    }

                    isFirst = false;
                }

                checkColumn.Clear();
                checkColumn.AddRange(tempObjects);
                newObjects.AddRange(tempObjects);
            }

            return (newObjects, checkColumn);
        }
    }
}
