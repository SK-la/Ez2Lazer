// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods
{
    /// <summary>
    /// Krr随机增强RP Mod
    /// 支持5种随机模式、固定轨道、DP左右分区独立随机、自定义种子、应用顺序设置
    /// 支持1~18键位：轨道 1~9,a~i 对应内部索引0~17
    /// </summary>
    public class ManiaModKrrRandomPlus : Mod, IApplicableAfterBeatmapConversion, IEzApplyOrder, IHasSeed
    {
        /// <summary>
        /// 随机模式枚举
        /// </summary>
        public enum RandomizationMode
        {
            /// <summary>无操作</summary>
            None,

            /// <summary>全随机：打乱剩余轨道顺序</summary>
            Random,

            /// <summary>循环滚动：轨道整体循环偏移</summary>
            R_Random,

            /// <summary>行随机：按时间窗口合并行内单独打乱Note</summary>
            S_Random,

            /// <summary>大窗口随机：SRandom的大窗口版本（200ms窗口，150ms阈值）</summary>
            H_Random,

            /// <summary>螺旋随机：随时间线性变化的轨道旋转</summary>
            Spiral,

            /// <summary>镜像：左右翻转轨道</summary>
            Mirror
        }

        #region Mod基础信息

        public override string Name => "Krr Random Plus";
        public override string Acronym => "RP";
        public override double ScoreMultiplier => 1.0;
        public override LocalisableString Description => "添加不同的随机模式，支持固定轨道，DP左右分区独立随机";
        public override IconUsage? Icon => FontAwesome.Solid.Random;
        public override ModType Type => ModType.LA_Mod;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        #endregion

        #region Mod设置控件

        /// <summary>主随机模式下拉框</summary>
        [SettingSource("模式", "可选：无/Random轨道随机/R-Random循环滚动/S-Random每个note随机/Mirror镜像")]
        public Bindable<RandomizationMode> MainMode { get; } = new Bindable<RandomizationMode>();

        /// <summary>Flip开关：左右交换</summary>
        [SettingSource("Flip", "左右轨道互换。偶数键：1234|5678→5678|1234；奇数键：1234|5|6789→6789|5|1234")]
        public BindableBool FlipEnabled { get; } = new BindableBool();

        /// <summary>带盘Flip开关：边缘包裹交换</summary>
        [SettingSource("带盘Flip", "单独交换最外侧轨道，内部左右轨道互换。偶数键：1234|5678→8567|2341；奇数键：中间不动，两侧包裹交换")]
        public BindableBool WrapFlipEnabled { get; } = new BindableBool();

        /// <summary>固定轨道输入框</summary>
        [SettingSource("固定轨道", "输入轨道号，如57；1-9、a-i对应1~18键，固定轨道不参与随机")]
        public Bindable<string> FixedColumnsInput { get; } = new Bindable<string>(string.Empty);

        /// <summary>DP左右分区开关</summary>
        [SettingSource("DP分区模式", "拆分左右区域各自独立随机，奇数keys时中间轨道固定不参与随机，上边设置变成左侧区域随机模式")]
        public BindableBool DPModeEnabled { get; } = new BindableBool();

        /// <summary>右侧区域随机模式下拉框（仅DP开启生效）</summary>
        [SettingSource("右侧模式", "DP分区开启时生效，右侧区域使用的随机模式")]
        public Bindable<RandomizationMode> RightMode { get; } = new Bindable<RandomizationMode>();

        /// <summary>随机种子输入框</summary>
        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SEED_LABEL), nameof(EzCommonModStrings.SEED_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        /// <summary>Mod应用顺序</summary>
        [SettingSource("应用顺序", "控制该Mod在所有修改类Mod中的执行先后顺序")]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 1
        };

        #endregion

        #region 接口实现 & 种子逻辑

        public ManiaModKrrRandomPlus()
        {
            // 设置互斥逻辑：开启 Flip 时关闭带盘 Flip
            FlipEnabled.ValueChanged += _ =>
            {
                if (FlipEnabled.Value)
                    WrapFlipEnabled.Value = false;
            };

            // 设置互斥逻辑：开启带盘 Flip 时关闭 Flip
            WrapFlipEnabled.ValueChanged += _ =>
            {
                if (WrapFlipEnabled.Value)
                    FlipEnabled.Value = false;
            };
        }

        // Mod应用顺序接口
        public int ApplyOrder => ApplyOrderIndex.Value;

        #endregion

        #region 设置面板详情描述

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("主模式", MainMode.Value.ToString());

                if (FlipEnabled.Value)
                    yield return ("Flip", "左右互换");
                else if (WrapFlipEnabled.Value)
                    yield return ("带盘Flip", "边缘包裹");

                if (DPModeEnabled.Value)
                    yield return ("右侧模式", RightMode.Value.ToString());

                yield return ("固定轨道", string.IsNullOrWhiteSpace(FixedColumnsInput.Value) ? "无" : FixedColumnsInput.Value);
                yield return (EzCommonModStrings.SEED_LABEL, Seed.Value?.ToString() ?? "随机");
                yield return ("应用顺序", ApplyOrderIndex.Value.ToString());
            }
        }

        #endregion

        #region 谱面入口逻辑

        /// <summary>
        /// Mod应用到谱面主入口
        /// </summary>
        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            try
            {
                var maniaBeatmap = beatmap as ManiaBeatmap;
                if (maniaBeatmap == null) return;

                // 获取总键数 1~18
                int totalKeys = (int)maniaBeatmap.Difficulty.CircleSize;
                if (totalKeys < 1 || totalKeys > 18) return;

                // 初始化随机器
                int seed = Seed.Value ?? RNG.Next();
                var rng = new Random(seed);

                // 第一步：结构层 - 最先执行 Flip 逻辑（优先于固定轨道）
                // Flip 和带盘Flip 是互斥的，优先判断
                if (FlipEnabled.Value || WrapFlipEnabled.Value)
                {
                    int[] flipMapping = new int[totalKeys];

                    if (totalKeys % 2 == 0)
                    {
                        // 偶数键：直接左右互换
                        int midSplit = totalKeys / 2;

                        for (int i = 0; i < midSplit; i++)
                        {
                            flipMapping[i] = i + midSplit;
                            flipMapping[i + midSplit] = i;
                        }
                    }
                    else
                    {
                        // 奇数键：中间固定，左右互换
                        int midIndex = totalKeys / 2;
                        flipMapping[midIndex] = midIndex;

                        for (int i = 0; i < midIndex; i++)
                        {
                            flipMapping[i] = i + midIndex + 1;
                            flipMapping[i + midIndex + 1] = i;
                        }
                    }

                    // 应用普通 Flip
                    if (FlipEnabled.Value)
                    {
                        foreach (var note in maniaBeatmap.HitObjects.OfType<ManiaHitObject>())
                            note.Column = flipMapping[note.Column];
                    }
                    // 应用带盘 Flip（边缘包裹）
                    else if (WrapFlipEnabled.Value)
                    {
                        int[] wrapMap = buildWrapFlipMapping(totalKeys);
                        foreach (var note in maniaBeatmap.HitObjects.OfType<ManiaHitObject>())
                            note.Column = wrapMap[note.Column];
                    }
                }

                // 第二步：解析固定轨道（此时 Note 已经根据 Flip 移动到了新位置）
                var fixedCols = parseFixedColumnsInput(FixedColumnsInput.Value, totalKeys);

                // 标记所有位于固定轨道的 Note（全程免疫后续操作）
                var lockedNotes = new HashSet<ManiaHitObject>();

                foreach (var note in maniaBeatmap.HitObjects.OfType<ManiaHitObject>())
                {
                    if (fixedCols.Contains(note.Column))
                    {
                        lockedNotes.Add(note);
                    }
                }

                // 第三步：结构层处理 - DP 分区划分
                var processRegions = new List<ProcessingRegion>();

                if (DPModeEnabled.Value)
                {
                    if (totalKeys % 2 == 0)
                    {
                        int midSplit = totalKeys / 2;
                        processRegions.Add(new ProcessingRegion(0, midSplit - 1, MainMode.Value, fixedCols));
                        processRegions.Add(new ProcessingRegion(midSplit, totalKeys - 1, RightMode.Value, fixedCols));
                    }
                    else
                    {
                        int midIndex = totalKeys / 2;
                        var fixedColsWithMiddle = new HashSet<int>(fixedCols) { midIndex };

                        if (midIndex > 0)
                            processRegions.Add(new ProcessingRegion(0, midIndex - 1, MainMode.Value, fixedColsWithMiddle));
                        if (midIndex < totalKeys - 1)
                            processRegions.Add(new ProcessingRegion(midIndex + 1, totalKeys - 1, RightMode.Value, fixedColsWithMiddle));
                    }
                }
                else
                {
                    processRegions.Add(new ProcessingRegion(0, totalKeys - 1, MainMode.Value, fixedCols));
                }

                // 第四步：规则层处理 - 平级调用各模式策略
                foreach (var region in processRegions)
                {
                    if (region.Mode == RandomizationMode.None) continue;

                    // 为每个区域创建独立的 RNG 实例，避免 DP 分区时左右区域相互影响
                    var regionRng = new Random(rng.Next());

                    switch (region.Mode)
                    {
                        case RandomizationMode.Mirror:
                            applyMirror(maniaBeatmap, region, lockedNotes);
                            break;

                        case RandomizationMode.Random:
                            applyRandom(maniaBeatmap, region, regionRng, lockedNotes);
                            break;

                        case RandomizationMode.R_Random:
                            applyRRandom(maniaBeatmap, region, regionRng, lockedNotes);
                            break;

                        case RandomizationMode.S_Random:
                            applySRandom(maniaBeatmap, region, regionRng, lockedNotes, 80, 60);
                            break;

                        case RandomizationMode.H_Random:
                            applySRandom(maniaBeatmap, region, regionRng, lockedNotes, 200, 150);
                            break;

                        case RandomizationMode.Spiral:
                            applySpiral(maniaBeatmap, region, regionRng, lockedNotes);
                            break;
                    }
                }

                // 第五步：按时间重新排序物件
                maniaBeatmap.HitObjects = maniaBeatmap.HitObjects.OrderBy(h => h.StartTime).ToList();
            }
            catch
            {
                // 异常静默容错
            }
        }

        #endregion

        #region 工具方法：固定轨道解析

        /// <summary>
        /// 解析固定轨道输入字符串
        /// 规则：1-9→0~8  a-i→9~17  超出键数自动忽略
        /// </summary>
        private HashSet<int> parseFixedColumnsInput(string input, int totalKeys)
        {
            var result = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(input))
                return result;

            foreach (char c in input.ToLower(CultureInfo.CurrentCulture))
            {
                int colIndex = c switch
                {
                    >= '1' and <= '9' => c - '1',
                    >= 'a' and <= 'i' => 9 + (c - 'a'),
                    _ => -1
                };

                if (colIndex >= 0 && colIndex < totalKeys)
                    result.Add(colIndex);
            }

            return result;
        }

        #endregion

        #region 工具方法：构建带盘Flip映射

        /// <summary>
        /// 构建带盘Flip（边缘包裹）的映射表
        /// 逻辑：先交换最外两头，内部剩余部分再执行标准 Flip（左右互换）
        /// 偶数键：12345678 → 8(234|567 Flip)1 → 85672341
        /// 奇数键：1234567 → 7(23|4|56 Flip)1 → 7564231
        /// </summary>
        private int[] buildWrapFlipMapping(int totalKeys)
        {
            int[] map = new int[totalKeys];
            for (int i = 0; i < totalKeys; i++) map[i] = i; // 初始化为自身

            if (totalKeys < 2) return map;

            // 第一步：交换最外两头
            map[0] = totalKeys - 1;
            map[totalKeys - 1] = 0;

            // 第二步：对内部剩余部分执行标准 Flip（左右互换）
            const int inner_start = 1;
            int innerEnd = totalKeys - 2;
            int innerCount = innerEnd - inner_start + 1; // 内部轨道数量

            if (innerCount <= 0) return map; // 没有内部轨道

            if (innerCount % 2 == 0)
            {
                // 偶数个内部轨道：直接左右对半互换
                int midSplit = innerCount / 2;

                for (int i = 0; i < midSplit; i++)
                {
                    map[inner_start + i] = inner_start + i + midSplit;
                    map[inner_start + i + midSplit] = inner_start + i;
                }
            }
            else
            {
                // 奇数个内部轨道：中间固定，左右互换
                int midIndex = inner_start + innerCount / 2;
                map[midIndex] = midIndex; // 中间不动

                for (int i = inner_start; i < midIndex; i++)
                {
                    map[i] = midIndex + 1 + (i - inner_start);
                    map[midIndex + 1 + (i - inner_start)] = i;
                }
            }

            return map;
        }

        #endregion

        #region 规则层：平级模式实现

        /// <summary>
        /// Mirror 模式：区域镜像翻转
        /// </summary>
        private void applyMirror(ManiaBeatmap beatmap, ProcessingRegion region, HashSet<ManiaHitObject> lockedNotes)
        {
            var activeCols = region.GetActiveColumnList().ToList();
            if (activeCols.Count <= 1) return;

            var mirrorMap = new Dictionary<int, int>();

            for (int i = 0; i < activeCols.Count; i++)
            {
                mirrorMap[activeCols[i]] = activeCols[activeCols.Count - 1 - i];
            }

            foreach (var note in beatmap.HitObjects.OfType<ManiaHitObject>())
            {
                if (!lockedNotes.Contains(note) && region.StartCol <= note.Column && note.Column <= region.EndCol)
                {
                    if (mirrorMap.TryGetValue(note.Column, out int newCol))
                        note.Column = newCol;
                }
            }
        }

        /// <summary>
        /// Random 模式：区域完全随机打乱
        /// </summary>
        private void applyRandom(ManiaBeatmap beatmap, ProcessingRegion region, Random rng, HashSet<ManiaHitObject> lockedNotes)
        {
            var activeCols = region.GetActiveColumnList().ToList();
            if (activeCols.Count <= 1) return;

            var movableNotes = beatmap.HitObjects
                                      .Where(n => region.StartCol <= n.Column && n.Column <= region.EndCol &&
                                                  activeCols.Contains(n.Column) && !lockedNotes.Contains(n))
                                      .ToList();

            if (movableNotes.Count == 0) return;

            var randomSlots = activeCols.OrderBy(_ => rng.Next()).Take(movableNotes.Count).ToList();

            for (int i = 0; i < movableNotes.Count; i++)
            {
                movableNotes[i].Column = randomSlots[i];
            }
        }

        /// <summary>
        /// R-Random 模式：区域循环滚动偏移
        /// </summary>
        private void applyRRandom(ManiaBeatmap beatmap, ProcessingRegion region, Random rng, HashSet<ManiaHitObject> lockedNotes)
        {
            var activeCols = region.GetActiveColumnList().ToList();
            if (activeCols.Count <= 1) return;

            int rollStep = rng.Next(1, activeCols.Count);
            var rollMap = new Dictionary<int, int>();

            for (int i = 0; i < activeCols.Count; i++)
            {
                rollMap[activeCols[i]] = activeCols[(i + rollStep) % activeCols.Count];
            }

            foreach (var note in beatmap.HitObjects.OfType<ManiaHitObject>())
            {
                if (!lockedNotes.Contains(note) && region.StartCol <= note.Column && note.Column <= region.EndCol)
                {
                    if (rollMap.TryGetValue(note.Column, out int newCol))
                        note.Column = newCol;
                }
            }
        }

        /// <summary>
        /// S-Random 模式：滑动窗口行内随机（带跨窗口避连）
        /// </summary>
        private void applySRandom(ManiaBeatmap beatmap, ProcessingRegion region, Random rng, HashSet<ManiaHitObject> lockedNotes, double window_interval = 80, double threshold = 60)
        {
            var activeCols = region.GetActiveColumnList().ToList();
            if (activeCols.Count <= 1) return;

            // 初始化轨道时间追踪字典
            var lastNoteTime = new Dictionary<int, double>();

            foreach (int col in activeCols)
            {
                lastNoteTime[col] = -999999; // 初始化为极小值
            }

            var allNotes = beatmap.HitObjects
                                  .Where(n => region.StartCol <= n.Column && n.Column <= region.EndCol)
                                  .OrderBy(n => n.StartTime)
                                  .ToList();

            if (allNotes.Count == 0) return;

            var holdNoteWindows = new List<(int startWindow, int endWindow)>();

            foreach (var note in allNotes)
            {
                if (note is HoldNote holdNote)
                {
                    int startWindow = (int)Math.Floor(holdNote.StartTime / window_interval);
                    int endWindow = (int)Math.Floor(holdNote.EndTime / window_interval);

                    if (Math.Abs(holdNote.EndTime - endWindow * window_interval) < 0.001)
                        endWindow += 1;

                    holdNoteWindows.Add((startWindow, endWindow));
                }
            }

            var windowGroups = new Dictionary<int, List<ManiaHitObject>>();

            foreach (var note in allNotes)
            {
                int windowIndex = (int)Math.Floor(note.StartTime / window_interval);
                if (!windowGroups.ContainsKey(windowIndex))
                    windowGroups[windowIndex] = new List<ManiaHitObject>();
                windowGroups[windowIndex].Add(note);
            }

            // 标记被 Hold Note 覆盖的窗口
            var mergedWindows = new HashSet<int>();

            foreach (var (startWin, endWin) in holdNoteWindows)
            {
                for (int w = startWin; w <= endWin; w++)
                    mergedWindows.Add(w);
            }

            // 构建按时间排序的所有窗口组
            var allWindowGroups = new SortedDictionary<int, List<ManiaHitObject>>();

            // 添加合并窗口组
            foreach (var (startWin, endWin) in holdNoteWindows)
            {
                var combinedNotes = new List<ManiaHitObject>();

                for (int w = startWin; w <= endWin; w++)
                {
                    if (windowGroups.TryGetValue(w, out var value))
                    {
                        combinedNotes.AddRange(value);
                    }
                }

                allWindowGroups[startWin] = combinedNotes.OrderBy(n => n.StartTime).ToList();
            }

            // 添加未被合并的普通窗口
            foreach (var kvp in windowGroups)
            {
                if (!mergedWindows.Contains(kvp.Key))
                {
                    allWindowGroups[kvp.Key] = kvp.Value.OrderBy(n => n.StartTime).ToList();
                }
            }

            // 按时间顺序处理所有窗口组
            foreach (var kvp in allWindowGroups)
            {
                shuffleWindowNotes(kvp.Value, activeCols, rng, lockedNotes, lastNoteTime, threshold);
            }
        }

        /// <summary>
        /// 窗口内 Note 智能分配（带跨窗口避连）
        /// </summary>
        private void shuffleWindowNotes(
            List<ManiaHitObject> notes,
            List<int> activeCols,
            Random rng,
            HashSet<ManiaHitObject> lockedNotes,
            Dictionary<int, double> lastNoteTime,
            double threshold)
        {
            if (notes.Count == 0) return;

            var movableNotes = notes
                               .Where(n => activeCols.Contains(n.Column) && !lockedNotes.Contains(n))
                               .OrderBy(n => n.StartTime) // 按时间排序，确保逐个处理
                               .ToList();

            if (movableNotes.Count == 0) return;

            foreach (var note in movableNotes)
            {
                // 第一阶段：筛选满足时间阈值的轨道（Primary Lanes）
                var primaryLanes = activeCols
                                   .Where(col => note.StartTime - lastNoteTime[col] > threshold)
                                   .ToList();

                // 第二阶段：不满足阈值的轨道（Inferior Lanes）
                var inferiorLanes = activeCols
                                    .Where(col => note.StartTime - lastNoteTime[col] <= threshold)
                                    .ToList();

                int targetLane;

                // 分配逻辑：优先选择 Primary Lanes
                if (primaryLanes.Count > 0)
                {
                    targetLane = primaryLanes[rng.Next(primaryLanes.Count)];
                }
                else if (inferiorLanes.Count > 0)
                {
                    // 退而求其次，从 Inferior Lanes 中随机选择
                    targetLane = inferiorLanes[rng.Next(inferiorLanes.Count)];
                }
                else
                {
                    // 极端情况：所有轨道都被占用（理论上不会发生）
                    continue;
                }

                // 应用分配并更新时间戳
                note.Column = targetLane;
                lastNoteTime[targetLane] = note.StartTime;
            }
        }

        /// <summary>
        /// Spiral 模式：螺旋随机（带智能分组与方向系数）
        /// </summary>
        private void applySpiral(ManiaBeatmap beatmap, ProcessingRegion region, Random rng, HashSet<ManiaHitObject> lockedNotes)
        {
            var activeCols = region.GetActiveColumnList().ToList();
            if (activeCols.Count <= 1) return;

            int count = activeCols.Count;
            int R = rng.Next(0, count); // 初始相位偏移
            int k = rng.Next(0, 2) == 0 ? 1 : -1; // 随机方向：1为右旋，-1为左旋
            int i = 0; // 分区计数器

            // 获取区域内所有 Note 并按时间排序
            var allNotes = beatmap.HitObjects
                                  .Where(n => region.StartCol <= n.Column && n.Column <= region.EndCol && !lockedNotes.Contains(n))
                                  .OrderBy(n => n.StartTime)
                                  .ToList();

            if (allNotes.Count == 0) return;

            // 智能分组逻辑
            var groups = new List<List<ManiaHitObject>>();
            var currentGroup = new List<ManiaHitObject>();
            double? lastEndTime = null; // 记录上一组中 LN 的最晚结束时间

            foreach (var note in allNotes)
            {
                bool startNewGroup = false;

                if (currentGroup.Count == 0)
                {
                    // 第一个 Note 直接加入
                }
                else
                {
                    double timeDiff = note.StartTime - currentGroup.Last().StartTime;

                    // 约束1：如果离上一行太近（<60ms），强制合并
                    if (timeDiff < 60)
                    {
                        startNewGroup = false;
                    }
                    // 约束2：如果超过了基础窗口（100ms），且没有 LN 约束，则新开一组
                    else if (timeDiff >= 100)
                    {
                        // 检查是否受 LN 约束（LN 结束后 40ms 内）
                        if (lastEndTime.HasValue && note.StartTime <= lastEndTime.Value + 40)
                        {
                            startNewGroup = false;
                        }
                        else
                        {
                            startNewGroup = true;
                        }
                    }
                }

                if (startNewGroup && currentGroup.Count > 0)
                {
                    groups.Add(currentGroup);
                    currentGroup = new List<ManiaHitObject>();
                    lastEndTime = null;
                }

                currentGroup.Add(note);

                // 更新 LN 结束时间追踪
                if (note is HoldNote holdNote)
                {
                    if (!lastEndTime.HasValue || holdNote.EndTime > lastEndTime.Value)
                    {
                        lastEndTime = holdNote.EndTime;
                    }
                }
            }

            if (currentGroup.Count > 0)
                groups.Add(currentGroup);

            // 应用螺旋偏移
            foreach (var group in groups)
            {
                // 计算当前分区的总偏移量
                int totalShift = (R + i * k) % count;
                if (totalShift < 0) totalShift += count; // 处理负数取模

                foreach (var note in group)
                {
                    // 找到 Note 当前轨道在 activeCols 里的索引
                    int currentIndex = activeCols.IndexOf(note.Column);

                    if (currentIndex != -1)
                    {
                        // 计算新索引并映射回实际轨道
                        int newIndex = (currentIndex + totalShift) % count;
                        note.Column = activeCols[newIndex];
                    }
                }

                i++; // 螺旋递增
            }
        }

        #endregion

        #region 内部辅助类：处理区域

        /// <summary>
        /// 单个处理区域
        /// </summary>
        private class ProcessingRegion
        {
            public int StartCol { get; }
            public int EndCol { get; }
            public RandomizationMode Mode { get; }
            public HashSet<int> FixedColumns { get; }

            public ProcessingRegion(int startCol, int endCol, RandomizationMode mode, HashSet<int> fixedColumns)
            {
                StartCol = startCol;
                EndCol = endCol;
                Mode = mode;
                FixedColumns = fixedColumns;
            }

            public IEnumerable<int> GetActiveColumnList()
            {
                for (int i = StartCol; i <= EndCol; i++)
                {
                    if (!FixedColumns.Contains(i))
                        yield return i;
                }
            }
        }

        #endregion
    }
}
