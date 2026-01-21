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
    public class ModStarRatingRebirth : Mod, IApplicableAfterBeatmapConversion
    {
        public override string Name => "Star Rating Rebirth";

        public override string Acronym => "SR";

        public override LocalisableString Description => EzManiaModStrings.StarRatingRebirth_Description;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (Original.Value) yield return ("Original OD", "On");

                if (!Original.Value && Custom.Value)
                {
                    yield return ("Custom OD", "On");
                    yield return ("OD", $"{OD.Value}");
                }
            }
        }

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.UseOriginalOD_Label), nameof(EzManiaModStrings.UseOriginalOD_Description))]
        public BindableBool Original { get; set; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.UseCustomOD_Label), nameof(EzManiaModStrings.UseCustomOD_Description))]
        public BindableBool Custom { get; set; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.OD_Label), nameof(EzManiaModStrings.OD_Description))]
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
            public int Head;
            public int Tail;
            public int Key;

            public bool IsLong => Head != Tail;

            public EasyObject(double startTime, double endTime, int column)
            {
                Head = (int)startTime;
                Tail = (int)endTime;
                Key = column;
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
                    objects[i].Head = (int)(objects[i].Head / rate);
                    objects[i].Tail = (int)(objects[i].Tail / rate);
                }

                return objects;
            }
        }

        public static double CalculateStarRating(List<ManiaHitObject> objects, double od, int keys, double rate)
        {
            // return StarRatingRebirthCalculateForLazer.CalculateStarRatingForLazer(objects, od, keys, rate);

            var hit = EasyObject.FromManiaObjects(objects);
            if (rate != 1) hit = EasyObject.FromRate(hit, rate);

            PreProcess(hit, keys, od, out double x, out int key, out int time,
                out var noteSeq, out var noteSeqByCol,
                out var lnSeq, out var tailSeq, out var lnSeqByColumn);

            GetCorners(noteSeq, time, out int[] baseCorners, out int[] aCorners, out int[] allCorners);

            // 对于每一列，存储其使用情况（在150ms内是否非空）。例如：keyUsage[k, i]
            bool[,] keyUsage = GetKeyUsage(noteSeq, key, time, baseCorners);

            // 在base_corners的每个时间点，构建活跃列的列表
            int[][] activeColumns = Enumerable.Range(0, baseCorners.Length)
                                              .Select(i => Enumerable.Range(0, key)
                                                                     .Where(k => keyUsage[k, i])
                                                                     .ToArray())
                                              .ToArray();

            double[,] keyUsage400 = GetKeyUsage400(noteSeq, key, time, baseCorners);

            double[] anchor = ComputeAnchor(key, keyUsage400, baseCorners);

            ComputeJbar(key, time, x, noteSeqByCol, baseCorners, out double[,] deltaKs, out double[] jBar);
            double[] jBarInterp = Utils.InterpValues(allCorners, baseCorners, jBar);

            double[] xBar = ComputeXbar(key, time, x, noteSeqByCol, activeColumns, baseCorners);
            double[] xBarInterp = Utils.InterpValues(allCorners, baseCorners, xBar);

            // 构建LN主体的稀疏表示
            LNBodiesCountSparseRepresentation(lnSeq, time, out int[] points, out double[] cumsum, out double[] values);

            double[] pBar = ComputePbar(key, time, x, noteSeq, points, cumsum, values, anchor, baseCorners);
            double[] pBarInterp = Utils.InterpValues(allCorners, baseCorners, pBar);

            double[] aBar = ComputeAbar(key, time, x, noteSeqByCol, activeColumns, deltaKs, aCorners, baseCorners);
            double[] aBarInterp = Utils.InterpValues(allCorners, aCorners, aBar);

            double[] rBar = ComputeRbar(key, time, x, noteSeqByCol, tailSeq, baseCorners);
            double[] rBarInterp = Utils.InterpValues(allCorners, baseCorners, rBar);

            ComputeCAndKs(key, time, noteSeq, keyUsage, baseCorners, out double[] cStep, out double[] ksStep);
            double[] cArr = Utils.StepInterp(allCorners, baseCorners, cStep);
            double[] ksArr = Utils.StepInterp(allCorners, baseCorners, ksStep);

            // === 最终计算 ===
            // 在all_corners上计算难度D
            double[] sAll = new double[allCorners.Length];
            double[] tAll = new double[allCorners.Length];
            double[] dAll = new double[allCorners.Length];

            for (int i = 0; i < allCorners.Length; i++)
            {
                double term1 = Math.Pow(aBarInterp[i], 3 / ksArr[i]) * Math.Min(jBarInterp[i], 8 + 0.85 * jBarInterp[i]);
                double term2 = Math.Pow(aBarInterp[i], 2.0 / 3.0) * (0.8 * pBarInterp[i] + rBarInterp[i] * 35 / (cArr[i] + 8));

                sAll[i] = Math.Pow(0.4 * Math.Pow(term1, 1.5) + (1 - 0.4) * Math.Pow(term2, 1.5), 2.0 / 3.0);
                tAll[i] = Math.Pow(aBarInterp[i], 3 / ksArr[i]) * xBarInterp[i] / (xBarInterp[i] + sAll[i] + 1);
                dAll[i] = 2.7 * Math.Pow(sAll[i], 0.5) * Math.Pow(tAll[i], 1.5) + sAll[i] * 0.27;
            }

            // 计算连续时间之间的间隔
            double[] gaps = new double[allCorners.Length];
            gaps[0] = (allCorners[1] - allCorners[0]) / 2.0;
            gaps[^1] = (allCorners[^1] - allCorners[^2]) / 2.0;
            for (int i = 1; i < allCorners.Length - 1; i++) gaps[i] = (allCorners[i + 1] - allCorners[i - 1]) / 2.0;

            // 每个拐角的有效权重是其密度和间隔的乘积
            double[] effectiveWeights = new double[allCorners.Length];
            for (int i = 0; i < allCorners.Length; i++) effectiveWeights[i] = cArr[i] * gaps[i];

            // 按难度D排序
            int[] sortedIndices = Enumerable.Range(0, allCorners.Length)
                                            .OrderBy(i => dAll[i])
                                            .ToArray();

            double[] dSorted = sortedIndices.Select(i => dAll[i]).ToArray();
            double[] wSorted = sortedIndices.Select(i => effectiveWeights[i]).ToArray();

            // 计算有效权重的累积和
            double[] cumWeights = new double[wSorted.Length];
            cumWeights[0] = wSorted[0];
            for (int i = 1; i < wSorted.Length; i++) cumWeights[i] = cumWeights[i - 1] + wSorted[i];
            double totalWeight = cumWeights[^1];

            double[] normCumWeights = cumWeights.Select(w => w / totalWeight).ToArray();

            // 计算目标百分位
            double[] targetPercentiles = { 0.945, 0.935, 0.925, 0.915, 0.845, 0.835, 0.825, 0.815 };
            int[] indices = new int[targetPercentiles.Length];

            for (int i = 0; i < targetPercentiles.Length; i++)
            {
                indices[i] = Array.FindIndex(normCumWeights, w => w >= targetPercentiles[i]);
                if (indices[i] < 0) indices[i] = normCumWeights.Length - 1;
            }

            double percentile93 = (dSorted[indices[0]] + dSorted[indices[1]] + dSorted[indices[2]] + dSorted[indices[3]]) / 4.0;
            double percentile83 = (dSorted[indices[4]] + dSorted[indices[5]] + dSorted[indices[6]] + dSorted[indices[7]]) / 4.0;

            // 计算加权平均值
            double weightedSum = 0;
            double weightSum = 0;

            for (int i = 0; i < dSorted.Length; i++)
            {
                weightedSum += Math.Pow(dSorted[i], 5) * wSorted[i];
                weightSum += wSorted[i];
            }

            double weightedMean = Math.Pow(weightedSum / weightSum, 1.0 / 5.0);

            // 最终SR计算
            double sr = 0.88 * percentile93 * 0.25 + 0.94 * percentile83 * 0.2 + weightedMean * 0.55;
            sr = Math.Pow(sr, 1.0) / Math.Pow(8, 1.0) * 8;

            double totalNotes = noteSeq.Length + 0.5 * lnSeq.Sum(ln => Math.Min(ln.Tail - ln.Head, 1000) / 200.0);
            sr *= totalNotes / (totalNotes + 60);

            sr = Utils.RescaleHigh(sr);
            sr *= 0.975;

            return sr;
        }

        internal static void ComputeCAndKs(int K,
                                           int T,
                                           EasyObject[] noteSeq,
                                           bool[,] keyUsage,
                                           int[] baseCorners,
                                           out double[] cStep,
                                           out double[] ksStep)
        {
            int cornersLength = baseCorners.Length;

            // 提取所有音符的命中时间并排序
            int[] noteHitTimes = noteSeq.Select(n => n.Head).OrderBy(t => t).ToArray();

            // 初始化 C_step 数组
            cStep = new double[cornersLength];

            // 计算每个基准点的 C(s)：500ms 内的音符数量
            for (int i = 0; i < cornersLength; i++)
            {
                int s = baseCorners[i];
                int low = s - 500;
                int high = s + 500;

                // 使用二分查找计算区间内的音符数量
                int highIdx = Utils.SearchSortedLeft(noteHitTimes, high);
                int lowIdx = Utils.SearchSortedLeft(noteHitTimes, low);

                cStep[i] = highIdx - lowIdx;
            }

            // 计算 Ks：本地按键使用计数（最小为1）
            ksStep = new double[cornersLength];

            for (int i = 0; i < cornersLength; i++)
            {
                int count = 0;

                for (int k = 0; k < K; k++)
                {
                    if (keyUsage[k, i])
                        count++;
                }

                ksStep[i] = Math.Max(count, 1);
            }
        }

        internal static double[] ComputePbar(int K, int T, double x, EasyObject[] noteSeq, int[] points, double[] cumsum, double[] values, double[] anchor, int[] baseCorners)
        {
            int cornersLength = baseCorners.Length;
            double[] pStep = new double[cornersLength];

            // stream_booster 函数的C#实现
            static double streamBooster(double delta)
            {
                double ratio = 7.5 / delta;
                return 160 < ratio && ratio < 360 ? 1 + 1.7e-7 * (ratio - 160) * Math.Pow(ratio - 360, 2) : 1;
            }

            for (int i = 0; i < noteSeq.Length - 1; i++)
            {
                int hL = noteSeq[i].Head;
                int hR = noteSeq[i + 1].Head;
                int deltaTime = hR - hL;

                if (deltaTime < 1e-9)
                {
                    // 狄拉克增量情况：当音符同时出现时
                    // 在基准网格中的音符头部精确位置添加尖峰
                    double spike = 1000 * Math.Pow(0.02 * (4 / x - 24), 1.0 / 4);
                    int leftIdx = Utils.SearchSortedLeft(baseCorners, hL);
                    int rightIdx = Utils.SearchSortedRight(baseCorners, hL);

                    for (int idx = leftIdx; idx < rightIdx; idx++) pStep[idx] += spike;
                }
                else
                {
                    // 对于delta_time > 0的常规情况，找出[h_l, h_r)范围内的基准网格索引
                    int leftIdx = Utils.SearchSortedLeft(baseCorners, hL);
                    int rightIdx = Utils.SearchSortedLeft(baseCorners, hR);

                    if (leftIdx >= rightIdx) continue;

                    double delta = 0.001 * deltaTime;
                    double v = 1 + 6 * 0.001 * LNSum(hL, hR, points, cumsum, values);
                    double bVal = streamBooster(delta);

                    double inc;
                    if (delta < 2 * x / 3)
                        inc = 1 / delta * Math.Pow(0.08 / x * (1 - 24 / x * Math.Pow(delta - x / 2, 2)), 1.0 / 4) * Math.Max(bVal, v);
                    else
                        inc = 1 / delta * Math.Pow(0.08 / x * (1 - 24 / x * Math.Pow(x / 6, 2)), 1.0 / 4) * Math.Max(bVal, v);

                    for (int idx = leftIdx; idx < rightIdx; idx++) pStep[idx] += Math.Min(inc * anchor[idx], Math.Max(inc, inc * 2 - 10));
                }
            }

            double[] pBar = Utils.SmoothOnCorners(baseCorners, pStep, 500, 0.001, "sum");
            return pBar;
        }

        internal static double[] ComputeAbar(int K, int T, double x, EasyObject[][] noteSeqByColumn, int[][] activeColumns, double[,] deltaKs, int[] aCorners, int[] baseCorners)
        {
            int cornersLength = baseCorners.Length;

            // 初始化dks数据结构，对应Python中的字典
            double[,] dks = new double[K - 1, cornersLength];

            // 填充dks数组
            for (int i = 0; i < cornersLength; i++)
            {
                int[] cols = activeColumns[i];

                for (int j = 0; j < cols.Length - 1; j++)
                {
                    int k0 = cols[j];
                    int k1 = cols[j + 1];

                    // 使用之前在base_corners上计算的delta_ks
                    dks[k0, i] = Math.Abs(deltaKs[k0, i] - deltaKs[k1, i]) + 0.4 * Math.Max(0, Math.Max(deltaKs[k0, i], deltaKs[k1, i]) - 0.11);
                }
            }

            // 初始化A_step数组，全部填充为1
            double[] aStep = new double[aCorners.Length];
            for (int i = 0; i < aCorners.Length; i++) aStep[i] = 1.0;

            // 修改A_step
            for (int i = 0; i < aCorners.Length; i++)
            {
                int s = aCorners[i];
                int idx = Utils.SearchSortedLeft(baseCorners, s);

                // 确保idx在有效范围内
                if (idx >= baseCorners.Length) idx = baseCorners.Length - 1;

                int[] cols = activeColumns[idx];

                for (int j = 0; j < cols.Length - 1; j++)
                {
                    int k0 = cols[j];
                    int k1 = cols[j + 1];
                    double dVal = dks[k0, idx];

                    if (dVal < 0.02)
                        aStep[i] *= Math.Min(0.75 + 0.5 * Math.Max(deltaKs[k0, idx], deltaKs[k1, idx]), 1);
                    else if (dVal < 0.07) aStep[i] *= Math.Min(0.65 + 5 * dVal + 0.5 * Math.Max(deltaKs[k0, idx], deltaKs[k1, idx]), 1);
                    // 否则保持A_step[i]不变
                }
            }

            // 对A_step应用平滑操作得到Abar
            double[] aBar = Utils.SmoothOnCorners(aCorners, aStep, 250, mode: "avg");
            return aBar;
        }

        internal static double[] ComputeRbar(int K, int T, double x, EasyObject[][] noteSeqByColumn, EasyObject[] tailSeq, int[] baseCorners)
        {
            int cornersLength = baseCorners.Length;
            double[] iArr = new double[cornersLength];
            double[] rStep = new double[cornersLength];

            int[][] timesByColumn = new int[noteSeqByColumn.Length][];
            for (int i = 0; i < noteSeqByColumn.Length; i++) timesByColumn[i] = noteSeqByColumn[i].Select(note => note.Head).ToArray();

            // 计算释放指数(Release Index)
            var iList = new List<double>();

            for (int i = 0; i < tailSeq.Length; i++)
            {
                int k = tailSeq[i].Key;
                int hI = tailSeq[i].Head;
                int tI = tailSeq[i].Tail;

                // 找到同一列中的下一个音符
                var nextNote = Utils.FindNextNoteInColumn(tailSeq[i], timesByColumn[k], noteSeqByColumn);
                int hJ = nextNote.Head;

                double iH = 0.001 * Math.Abs(tI - hI - 80) / x;
                double iT = 0.001 * Math.Abs(hJ - tI - 80) / x;

                iList.Add(2 / (2 + Math.Exp(-5 * (iH - 0.75)) + Math.Exp(-5 * (iT - 0.75))));
            }

            // 对相邻尾音时间之间的每个区间，分配 I 和 R
            for (int i = 0; i < tailSeq.Length - 1; i++)
            {
                int tStart = tailSeq[i].Tail;
                int tEnd = tailSeq[i + 1].Tail;

                int leftIdx = Utils.SearchSortedLeft(baseCorners, tStart);
                int rightIdx = Utils.SearchSortedLeft(baseCorners, tEnd);

                if (leftIdx >= rightIdx) continue;

                // 设置这个区间内所有索引的值
                for (int idx = leftIdx; idx < rightIdx; idx++)
                {
                    iArr[idx] = 1 + iList[i];
                    double deltaR = 0.001 * (tailSeq[i + 1].Tail - tailSeq[i].Tail);
                    rStep[idx] = 0.08 * Math.Pow(deltaR, -0.5) * (1 / x) * (1 + 0.8 * (iList[i] + iList[i + 1]));
                }
            }

            // 应用平滑操作得到Rbar
            double[] rBar = Utils.SmoothOnCorners(baseCorners, rStep, 500, 0.001, "sum");
            return rBar;
        }

        internal static void LNBodiesCountSparseRepresentation(EasyObject[] lnSeq,
                                                               int T,
                                                               out int[] points,
                                                               out double[] cumsum,
                                                               out double[] values)
        {
            // 字典：索引 -> 长音符体变化量（转换前）
            var diff = new Dictionary<int, double>();

            foreach (var ln in lnSeq)
            {
                int t0 = Math.Min(ln.Head + 60, ln.Tail);
                int t1 = Math.Min(ln.Head + 120, ln.Tail);

                diff[t0] = diff.GetValueOrDefault(t0, 0) + 1.3;
                diff[t1] = diff.GetValueOrDefault(t1, 0) + (-1.3 + 1); // t1处的净变化：从第一部分的-1.3，然后+1
                diff[ln.Tail] = diff.GetValueOrDefault(ln.Tail, 0) - 1;
            }

            // 断点是变化发生的时间点
            points = new[] { 0, T }.Concat(diff.Keys).Distinct().OrderBy(p => p).ToArray();

            // 构建分段常量值（转换后）和累积和
            values = new double[points.Length - 1];
            cumsum = new double[points.Length];
            cumsum[0] = 0; // 断点处的累积和
            double curr = 0.0;

            for (int i = 0; i < points.Length - 1; i++)
            {
                int t = points[i];
                // 如果在t处有变化，更新运行值
                if (diff.TryGetValue(t, out double value)) curr += value;

                double v = Math.Min(curr, 2.5 + 0.5 * curr);
                values[i] = v;
                // 计算区间[points[i], points[i+1])上的累积和
                int segLength = points[i + 1] - points[i];
                cumsum[i + 1] = cumsum[i] + segLength * v;
            }
        }

        private static bool contains(int[] array, int value)
        {
            if (array == null) return false;

            foreach (int item in array)
            {
                if (item == value)
                    return true;
            }

            return false;
        }

        internal static double[] ComputeXbar(int K, int T, double x, EasyObject[][] noteSeqByColumn, int[][] activeColumns, int[] baseCorners)
        {
            // 创建交叉矩阵
            double[][] crossMatrix =
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

            int cornersLength = baseCorners.Length;

            // 初始化 X_ks 和 fast_cross
            double[,] xKs = new double[K + 1, cornersLength];
            double[,] fastCross = new double[K + 1, cornersLength];

            // 获取交叉系数
            double[] crossCoeff = crossMatrix[K];

            // 计算每列的 X_ks 和 fast_cross 值
            for (int k = 0; k <= K; k++)
            {
                // 根据不同情况选择要处理的音符
                EasyObject[] notesInPair;

                if (k == 0)
                    notesInPair = noteSeqByColumn[0];
                else if (k == K)
                    notesInPair = noteSeqByColumn[K - 1];
                else
                {
                    // 合并两列的音符并按时间排序
                    var mergedNotes = new List<EasyObject>();
                    mergedNotes.AddRange(noteSeqByColumn[k - 1]);
                    mergedNotes.AddRange(noteSeqByColumn[k]);
                    notesInPair = mergedNotes.OrderBy(n => n.Head).ToArray();
                }

                // 处理相邻音符对
                for (int i = 1; i < notesInPair.Length; i++)
                {
                    int start = notesInPair[i - 1].Head;
                    int end = notesInPair[i].Head;

                    int idxStart = Utils.SearchSortedLeft(baseCorners, start);
                    int idxEnd = Utils.SearchSortedLeft(baseCorners, end);

                    if (idxStart >= idxEnd) continue;

                    double delta = 0.001 * (end - start);
                    double val = 0.16 * Math.Pow(Math.Max(x, delta), -2);

                    // 检查活跃列条件
                    bool condition1 = !contains(activeColumns[idxStart], k - 1) && !contains(activeColumns[idxEnd], k - 1);
                    bool condition2 = !contains(activeColumns[idxStart], k) && !contains(activeColumns[idxEnd], k);

                    if (condition1 || condition2) val *= 1 - crossCoeff[k];

                    // 设置值
                    for (int idx = idxStart; idx < idxEnd; idx++)
                    {
                        xKs[k, idx] = val;
                        fastCross[k, idx] = Math.Max(0, 0.4 * Math.Pow(Math.Max(delta, Math.Max(0.06, 0.75 * x)), -2) - 80);
                    }
                }
            }

            // 计算 X_base
            double[] xBase = new double[cornersLength];

            for (int i = 0; i < cornersLength; i++)
            {
                // 第一部分：xKs 的加权和
                double sum1 = 0;
                for (int k = 0; k <= K; k++) sum1 += xKs[k, i] * crossCoeff[k];

                // 第二部分：fastCross 的平方根乘积和
                double sum2 = 0;
                for (int k = 0; k < K; k++) sum2 += Math.Sqrt(fastCross[k, i] * crossCoeff[k] * fastCross[k + 1, i] * crossCoeff[k + 1]);

                xBase[i] = sum1 + sum2;
            }

            // 应用平滑操作得到 Xbar
            double[] xBar = Utils.SmoothOnCorners(baseCorners, xBase, 500, 0.001, "sum");
            return xBar;
        }

        internal static void ComputeJbar(int K,
                                         int T,
                                         double x,
                                         EasyObject[][] noteSeqByColumn,
                                         int[] baseCorners,
                                         out double[,] deltaKs,
                                         out double[] jBar)
        {
            int cornersLength = baseCorners.Length;

            double[,] jKs = new double[K, cornersLength];
            deltaKs = new double[K, cornersLength];

            for (int k = 0; k < K; k++)
            {
                for (int i = 0; i < cornersLength; i++) deltaKs[k, i] = 1e9;
            }

            static double jackNerfer(double delta) => 1 - 7e-5 * Math.Pow(0.15 + Math.Abs(delta - 0.08), -4);

            for (int k = 0; k < K; k++)
            {
                var notes = noteSeqByColumn[k];

                for (int i = 0; i < notes.Length - 1; i++)
                {
                    int start = notes[i].Head;
                    int end = notes[i + 1].Head;

                    int leftIdx = Utils.SearchSortedLeft(baseCorners, start);
                    int rightIdx = Utils.SearchSortedLeft(baseCorners, end);

                    if (leftIdx >= rightIdx) continue;

                    double delta = 0.001 * (end - start);
                    double val = 1 / delta * (1 / (delta + 0.11 * Math.Pow(x, 1.0 / 4)));
                    double jVal = val * jackNerfer(delta);

                    for (int idx = leftIdx; idx < rightIdx; idx++)
                    {
                        jKs[k, idx] = jVal;
                        deltaKs[k, idx] = delta;
                    }
                }
            }

            // 对每列的J_ks进行平滑处理
            double[,] jBarKs = new double[K, cornersLength];

            for (int k = 0; k < K; k++)
            {
                double[] rowData = new double[cornersLength];
                for (int i = 0; i < cornersLength; i++) rowData[i] = jKs[k, i];

                double[] smoothed = Utils.SmoothOnCorners(baseCorners, rowData, 500, 0.001, "sum");

                for (int i = 0; i < cornersLength; i++) jBarKs[k, i] = smoothed[i];
            }

            // 使用加权平均聚合各列
            jBar = new double[cornersLength];

            for (int i = 0; i < cornersLength; i++)
            {
                double num = 0.0;
                double den = 0.0;

                for (int k = 0; k < K; k++)
                {
                    double v = jBarKs[k, i];
                    double w = 1 / deltaKs[k, i];

                    num += Math.Pow(Math.Max(v, 0), 5) * w;
                    den += w;
                }

                jBar[i] = num / Math.Max(1e-9, den);
                jBar[i] = Math.Pow(jBar[i], 1.0 / 5);
            }
        }

        internal static double[] ComputeAnchor(int K, double[,] keyUsage400, int[] baseCorners)
        {
            double[] anchor = new double[baseCorners.Length];

            for (int idx = 0; idx < baseCorners.Length; idx++)
            {
                var nonzeroCounts = Enumerable.Range(0, K)
                                              .Select(k => keyUsage400[k, idx])
                                              .OrderByDescending(c => c)
                                              .Where(c => c != 0)
                                              .ToList();

                if (nonzeroCounts.Count > 1)
                {
                    double walk = 0;
                    double maxWalk = 0;

                    for (int i = 0; i < nonzeroCounts.Count - 1; i++)
                    {
                        double ratio = nonzeroCounts[i + 1] / nonzeroCounts[i];
                        walk += nonzeroCounts[i] * (1 - 4 * Math.Pow(0.5 - ratio, 2));
                        maxWalk += nonzeroCounts[i];
                    }

                    anchor[idx] = walk / maxWalk;
                }
                else
                    anchor[idx] = 0;
            }

            for (int i = 0; i < anchor.Length; i++) anchor[i] = 1 + Math.Min(anchor[i] - 0.18, 5 * Math.Pow(anchor[i] - 0.22, 3));
            return anchor;
        }

        internal static double[,] GetKeyUsage400(EasyObject[] noteSeq, int K, int T, int[] baseCorners)
        {
            double[,] keyUsage400 = new double[K, baseCorners.Length];

            foreach (var note in noteSeq)
            {
                int startTime = Math.Max(note.Head, 0);
                int endTime = !note.IsLong ? note.Head : Math.Min(note.Tail, T - 1);
                int left400Idx = Utils.SearchSortedLeft(baseCorners, startTime - 400);
                int leftIdx = Utils.SearchSortedLeft(baseCorners, startTime);
                int rightIdx = Utils.SearchSortedLeft(baseCorners, endTime);
                int right400Idx = Utils.SearchSortedLeft(baseCorners, endTime + 400);
                for (int i = leftIdx; i < rightIdx; i++) keyUsage400[note.Key, i] += 3.75 + Math.Min(endTime - startTime, 1500) / 150.0;

                for (int i = left400Idx; i < leftIdx; i++)
                {
                    double diff = baseCorners[i] - startTime;
                    keyUsage400[note.Key, i] += 3.75 - 3.75 / (400.0 * 400.0) * diff * diff;
                }

                for (int i = rightIdx; i < right400Idx; i++)
                {
                    double diff = baseCorners[i] - endTime;
                    keyUsage400[note.Key, i] += 3.75 - 3.75 / (400.0 * 400.0) * diff * diff;
                }
            }

            return keyUsage400;
        }

        internal static double LNSum(int a, int b, int[] points, double[] cumsum, double[] values)
        {
            // 定位包含 a 和 b 的分段
            int i = Utils.SearchSortedRight(points, a) - 1;
            int j = Utils.SearchSortedRight(points, b) - 1;

            double total = 0.0;

            if (i == j)
            {
                // a 和 b 在同一分段内
                total = (b - a) * values[i];
            }
            else
            {
                // 第一个分段：从 a 到第 i 个分段的末尾
                total += (points[i + 1] - a) * values[i];
                // i+1 到 j-1 之间的完整分段
                total += cumsum[j] - cumsum[i + 1];
                // 最后一个分段：从第 j 个分段的开始到 b
                total += (b - points[j]) * values[j];
            }

            return total;
        }

        internal static bool[,] GetKeyUsage(EasyObject[] noteSeq, int K, int T, int[] baseCorners)
        {
            bool[,] keyUsage = new bool[K, baseCorners.Length];

            foreach (var note in noteSeq)
            {
                int startTime = Math.Max(note.Head - 150, 0);
                int endTime = !note.IsLong ? note.Head + 150 : Math.Min(note.Tail + 150, T - 1);
                int leftIdx = Utils.SearchSortedLeft(baseCorners, startTime);
                int rightIdx = Utils.SearchSortedLeft(baseCorners, endTime);
                for (int i = leftIdx; i < rightIdx; i++) keyUsage[note.Key, i] = true;
            }

            return keyUsage;
        }

        internal static void GetCorners(EasyObject[] noteSeq, int T, out int[] baseCorners, out int[] aCorners, out int[] allCorners)
        {
            var baseSet = new HashSet<int>();

            foreach (var note in noteSeq)
            {
                baseSet.Add(note.Head);
                if (note.IsLong) baseSet.Add(note.Tail);
            }

            foreach (int s in baseSet.ToArray())
            {
                baseSet.Add(s + 501);
                baseSet.Add(s - 499);
                baseSet.Add(s + 1); // 在音符确切位置解决狄拉克增量问题
            }

            baseSet.Add(0);
            baseSet.Add(T);
            baseCorners = baseSet
                          .Where(s => 0 <= s && s <= T)
                          .OrderBy(s => s)
                          .ToArray();

            var aSet = new HashSet<int>();

            foreach (var note in noteSeq)
            {
                aSet.Add(note.Head);
                if (note.IsLong) aSet.Add(note.Tail);
            }

            foreach (int s in aSet.ToArray())
            {
                aSet.Add(s + 1000);
                aSet.Add(s - 1000);
            }

            aSet.Add(0);
            aSet.Add(T);
            aCorners = aSet
                       .Where(s => 0 <= s && s <= T)
                       .OrderBy(s => s)
                       .ToArray();

            allCorners = baseCorners.Union(aCorners)
                                    .OrderBy(s => s)
                                    .ToArray();
        }

        internal static void PreProcess(EasyObject[] objects,
                                        int keys,
                                        double od,
                                        out double x,
                                        out int K,
                                        out int T,
                                        out EasyObject[] noteSeq,
                                        out EasyObject[][] noteSeqByCol,
                                        out EasyObject[] lnSeq,
                                        out EasyObject[] tailSeq,
                                        out EasyObject[][] lnSeqByCol)
        {
            K = keys;
            x = 0.3 * Math.Pow((64.5 - Math.Ceiling(od * 3)) / 500, 0.5);
            x = Math.Min(x, 0.6 * (x - 0.09) + 0.09);

            // 排序一次，后续复用这个排序结果
            noteSeq = objects.OrderBy(n => n.Head).ThenBy(n => n.Key).ToArray();

            // 创建包含 K 个空列表的数组
            var noteColumns = new List<EasyObject>[K];
            var lnColumns = new List<EasyObject>[K];
            var lnNotes = new List<EasyObject>();

            // 初始化所有列表
            for (int k = 0; k < K; k++)
            {
                noteColumns[k] = new List<EasyObject>();
                lnColumns[k] = new List<EasyObject>();
            }

            // 单次遍历，同时填充 noteColumns 和 lnColumns
            foreach (var note in noteSeq)
            {
                int key = note.Key;

                if (key >= 0 && key < K) // 确保 key 在有效范围内
                {
                    noteColumns[key].Add(note);

                    if (note.IsLong)
                    {
                        lnNotes.Add(note);
                        lnColumns[key].Add(note);
                    }
                }
            }

            // 将列表转换为数组
            noteSeqByCol = new EasyObject[K][];
            lnSeqByCol = new EasyObject[K][];

            for (int k = 0; k < K; k++)
            {
                noteSeqByCol[k] = noteColumns[k].ToArray();
                lnSeqByCol[k] = lnColumns[k].ToArray();
            }

            // 获取长音符数组和按尾部时间排序的长音符数组
            lnSeq = lnNotes.ToArray();
            tailSeq = lnSeq.OrderBy(n => n.Tail).ToArray();

            // 计算总时长 T
            if (tailSeq.Length > 0)
                T = Math.Max(noteSeq[^1].Head, tailSeq[^1].Tail) + 1;
            else
                T = noteSeq[^1].Head + 1;
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

            var locations = objects.OfType<Note>().Select(n => (
                                       startTime: n.StartTime,
                                       samples: n.Samples,
                                       column: n.Column,
                                       endTime: n.StartTime,
                                       duration: n.StartTime - n.StartTime
                                   ))
                                   .Concat(objects.OfType<HoldNote>().Select(h => (
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

                    locations = fixedColumnObjects.OfType<Note>().Select(n => (
                                                      startTime: n.StartTime,
                                                      samples: n.Samples,
                                                      column: n.Column,
                                                      endTime: n.StartTime,
                                                      duration: n.StartTime - n.StartTime
                                                  ))
                                                  .Concat(fixedColumnObjects.OfType<HoldNote>().Select(h => (
                                                      startTime: h.StartTime,
                                                      samples: h.Samples,
                                                      column: h.Column,
                                                      endTime: h.EndTime,
                                                      duration: h.EndTime - h.StartTime
                                                  ))).OrderBy(h => h.startTime).ThenBy(n => n.column).ToList();

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

            var locations = objects.OfType<Note>().Select(n => (
                                       startTime: n.StartTime,
                                       samples: n.Samples,
                                       column: n.Column,
                                       endTime: n.StartTime
                                   ))
                                   .Concat(objects.OfType<HoldNote>().Select(h => (
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

            var locations = maniaBeatmap.HitObjects.OfType<Note>().Select(n => (
                                            column: n.Column,
                                            startTime: n.StartTime,
                                            endTime: n.StartTime,
                                            samples: n.Samples
                                        ))
                                        .Concat(maniaBeatmap.HitObjects.OfType<HoldNote>().Select(h => (
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

        public static (List<ManiaHitObject> result, List<ManiaHitObject> checkList) ProcessArea(ManiaBeatmap beatmap,
                                                                                                Random Rng,
                                                                                                List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)> hitObjects,
                                                                                                int fromKeys,
                                                                                                int toKeys,
                                                                                                int blankNum = 0,
                                                                                                int clean = 0,
                                                                                                double error = 0,
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
