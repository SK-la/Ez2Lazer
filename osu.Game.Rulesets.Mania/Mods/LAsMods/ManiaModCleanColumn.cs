// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Background;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModCleanColumn : Mod, IApplicableAfterBeatmapConversion, IHasApplyOrder
    {
        public override string Name => "Clean Column";

        public override string Acronym => "CC";

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => EzManiaModStrings.CleanColumn_Description;

        public override IconUsage? Icon => FontAwesome.Solid.Backspace;

        public override ModType Type => ModType.LA_Mod;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;

        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.DeleteSColumn_Label), nameof(EzManiaModStrings.DeleteSColumn_Description))]
        public BindableBool DeleteSColumn { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.DeletePColumn_Label), nameof(EzManiaModStrings.DeletePColumn_Description))]
        public BindableBool DeletePColumn { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.DeleteEColumn_Label), nameof(EzManiaModStrings.DeleteEColumn_Description))]
        public BindableBool DeleteEColumn { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.EnableCustomDelete_Label), nameof(EzManiaModStrings.EnableCustomDelete_Description))]
        public BindableBool EnableCustomDelete { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.CustomDeleteColumn_Label), nameof(EzManiaModStrings.CustomDeleteColumn_Description), SettingControlType = typeof(SettingsTextBox))]
        public Bindable<string> CustomDeleteColumn { get; } = new Bindable<string>(string.Empty);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.EnableCustomReorder_Label), nameof(EzManiaModStrings.EnableCustomReorder_Description))]
        public BindableBool EnableCustomReorder { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.UseHealthCapReduction_Label), nameof(EzManiaModStrings.UseHealthCapReduction_Description))]
        public BindableBool UseHealthCapReduction { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.CustomReorderColumn_Label), nameof(EzManiaModStrings.CustomReorderColumn_Description), SettingControlType = typeof(SettingsTextBox))]
        public Bindable<string> CustomReorderColumn { get; } = new Bindable<string>(string.Empty);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.ApplyOrder_Label), nameof(EzManiaModStrings.ApplyOrder_Description))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        private List<int>? columnsToDelete;
        private int keys1;
        private int keys2;
        private bool customReorderApplied;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            try
            {
                var maniaBeatmap = (ManiaBeatmap)beatmap;
                keys1 = (int)maniaBeatmap.Difficulty.CircleSize;

                // 处理自定义列重排
                if (EnableCustomReorder.Value && !string.IsNullOrWhiteSpace(CustomReorderColumn.Value) && !customReorderApplied)
                {
                    Logger.Log($"[ManiaModCleanColumn] Calling applyCustomReorder with reorderRule: {CustomReorderColumn.Value}");
                    applyCustomReorder(maniaBeatmap, CustomReorderColumn.Value, keys1);
                    keys2 = (int)maniaBeatmap.Difficulty.CircleSize;
                    customReorderApplied = true;
                }

                columnsToDelete = new List<int>();

                // 获取列类型（仅用于 S/P/E 列删除）
                if (GlobalConfigStore.EzConfig != null)
                {
                    EzColumnType[] columnTypes = GlobalConfigStore.EzConfig.GetColumnTypes(keys2);

                    if (DeleteSColumn.Value)
                    {
                        for (int i = 0; i < keys2; i++)
                        {
                            if (columnTypes[i] == EzColumnType.S)
                                columnsToDelete.Add(i);
                        }
                    }

                    if (DeletePColumn.Value)
                    {
                        for (int i = 0; i < keys2; i++)
                        {
                            if (columnTypes[i] == EzColumnType.P)
                                columnsToDelete.Add(i);
                        }
                    }

                    if (DeleteEColumn.Value)
                    {
                        for (int i = 0; i < keys2; i++)
                        {
                            if (columnTypes[i] == EzColumnType.E)
                                columnsToDelete.Add(i);
                        }
                    }
                }

                // 处理自定义删除列
                if (EnableCustomDelete.Value && !string.IsNullOrWhiteSpace(CustomDeleteColumn.Value))
                {
                    foreach (char c in CustomDeleteColumn.Value)
                    {
                        if (char.IsDigit(c))
                        {
                            int colIndex = c == '0' ? 9 : (c - '1'); // 0表示第10列，1-9表示第1-9列
                            if (colIndex >= 0 && colIndex < keys1)
                                columnsToDelete.Add(colIndex);
                        }
                    }
                }

                // 执行删除操作（后执行删除）
                if (columnsToDelete.Any())
                {
                    var notesToRemove = maniaBeatmap.HitObjects
                                                    .Where(h => h is ManiaHitObject maniaHitObject && columnsToDelete.Contains(maniaHitObject.Column))
                                                    .OrderBy(h => h.StartTime)
                                                    .ToList();

                    foreach (var note in notesToRemove)
                    {
                        maniaBeatmap.HitObjects.Remove(note);
                    }
                }
            }
            catch
            {
                // 失败时返回原始谱面，不修改
            }
        }

        /// <summary>
        /// 处理自定义列重排和清空note的逻辑
        /// 新格式：字符串长度决定新列数，每位数字代表该新列使用原谱面的哪一列内容
        /// - 数字：该新列位置使用对应原列的note
        /// - '-'：该新列位置删除（清空）
        /// - '|'：该新列位置放置贯穿整个谱面的长按note
        /// - '?'：该新列位置使用原谱中的随机一列填充
        /// 例如对于5k谱面：
        ///   "213" → 转换为3k谱面：新列1使用原列2，新列2使用原列1，新列3使用原列3
        ///   "213|5" → 转换为5k谱面：新列1使用原列2，新列2使用原列1，新列3使用原列3，新列4贯穿长按，新列5使用原列4
        ///   "21??" → 转换为4k谱面：新列1使用原列2，新列2使用原列1，新列3和4使用随机列
        /// </summary>
        /// <param name="beatmap">要处理的Mania谱面</param>
        /// <param name="reorderRule">重排规则字符串</param>
        /// <param name="totalColumns">原始谱面的总列数</param>
        private void applyCustomReorder(ManiaBeatmap beatmap, string reorderRule, int totalColumns)
        {
            try
            {
                // 输入不能为空
                if (string.IsNullOrWhiteSpace(reorderRule))
                    return;

                // 使用当前（转换后）hit objects作为输入，并按时间重建新谱面（类似 ManiaModNtoM 的做法）
                var sourceHitObjects = beatmap.HitObjects.ToList();
                Logger.Log($"[ManiaModCleanColumn] Using {sourceHitObjects.Count} source hit objects for reordering");

                // 构建列映射、清空列表和长按列表
                Dictionary<int, int> columnMapping = new Dictionary<int, int>();
                HashSet<int> clearColumns = new HashSet<int>();
                HashSet<int> holdColumns = new HashSet<int>();

                // 计算实际的源列数（以源 HitObjects 中出现的最大列号为准），避免传入的 totalColumns 失准导致空列
                int sourceColumns = sourceHitObjects.Count > 0 ? sourceHitObjects.Max(h => h.Column) + 1 : totalColumns;

                // 创建随机数生成器，使用当前时间作为种子确保每次运行都不同
                var random = new Random((int)DateTime.Now.Ticks);

                for (int targetPos = 0; targetPos < reorderRule.Length; targetPos++)
                {
                    char c = reorderRule[targetPos];

                    if (c == '-')
                    {
                        clearColumns.Add(targetPos);
                    }
                    else if (c == '|')
                    {
                        holdColumns.Add(targetPos);
                    }
                    else if (c == '?')
                    {
                        int randomSourceCol = sourceColumns > 0 ? random.Next(0, sourceColumns) : 0;
                        columnMapping[targetPos] = randomSourceCol;
                        Logger.Log($"[ManiaModCleanColumn] Column {targetPos} randomly mapped to source column {randomSourceCol}");
                    }
                    else if (char.IsDigit(c))
                    {
                        int digitValue = c - '0';
                        int desired = digitValue == 0 ? 10 : digitValue; // '0' means 10th column
                        int sourceCol = desired - 1;

                        if (sourceColumns <= 0)
                        {
                            sourceCol = 0;
                        }
                        else if (sourceCol < 0)
                        {
                            sourceCol = 0;
                        }
                        else if (sourceCol >= sourceColumns)
                        {
                            Logger.Log($"[ManiaModCleanColumn] Digit mapping at pos {targetPos} ({desired}) out of range, clamping to last source column {sourceColumns - 1}");
                            sourceCol = sourceColumns - 1;
                        }

                        columnMapping[targetPos] = sourceCol;
                    }
                }

                // 如果没有有效映射，则不进行任何操作
                if (columnMapping.Count == 0 && clearColumns.Count == 0 && holdColumns.Count == 0)
                    return;

                // 计算谱面的时间范围，用于创建贯穿的长按note
                double minTime = sourceHitObjects.Min(h => h.StartTime);
                double maxTime = sourceHitObjects.Max(h => h.GetEndTime());

                // 应用列映射、清空和长按操作
                var newObjects = new List<ManiaHitObject>();

                // 显示实际源列的 note 分布
                for (int i = 0; i < sourceColumns; i++)
                {
                    int noteCount = sourceHitObjects.Count(h => h.Column == i);
                    Logger.Log($"[ManiaModCleanColumn] Original column {i} has {noteCount} notes");
                }

                Logger.Log($"[ManiaModCleanColumn] Column mapping: {string.Join(", ", columnMapping.Select(kv => $"{kv.Key}->{kv.Value}"))}");

                // 按时间重建：先把所有源 note 转为时间序列 locations，然后根据反向映射把 note 放到目标列
                var locations = sourceHitObjects.OfType<Note>().Select(n => (
                                                    startTime: n.StartTime,
                                                    endTime: n.StartTime,
                                                    samples: n.Samples,
                                                    column: n.Column,
                                                    duration: 0.0,
                                                    isHold: false
                                                ))
                                                .Concat(sourceHitObjects.OfType<HoldNote>().Select(h => (
                                                    startTime: h.StartTime,
                                                    endTime: h.EndTime,
                                                    samples: h.Samples,
                                                    column: h.Column,
                                                    duration: h.Duration,
                                                    isHold: true
                                                ))).OrderBy(x => x.startTime).ThenBy(x => x.column).ToList();

                // 反向映射：源列 -> 目标列列表
                var reverseMap = new Dictionary<int, List<int>>();

                foreach (var kv in columnMapping)
                {
                    if (!reverseMap.ContainsKey(kv.Value))
                        reverseMap[kv.Value] = new List<int>();
                    reverseMap[kv.Value].Add(kv.Key);
                }

                // 为避免冲突，按时间顺序把每个 location 放到对应的目标列（如果对应多个目标列，则都会放）
                foreach (var loc in locations)
                {
                    if (reverseMap.TryGetValue(loc.column, out var targetCols))
                    {
                        foreach (int target in targetCols)
                        {
                            if (loc.isHold)
                            {
                                {
                                    var hn = new HoldNote
                                    {
                                        Column = target,
                                        StartTime = loc.startTime,
                                        Duration = loc.endTime - loc.startTime,
                                        NodeSamples = new List<IList<HitSampleInfo>> { loc.samples, new List<HitSampleInfo>() }
                                    };
                                    newObjects.Add(hn);
                                }
                            }
                            else
                            {
                                newObjects.Add(new Note
                                {
                                    Column = target,
                                    StartTime = loc.startTime,
                                    Samples = loc.samples
                                });
                            }
                        }
                    }
                }

                // 添加贯穿长按列
                foreach (int hc in holdColumns)
                {
                    Logger.Log($"[ManiaModCleanColumn] Column {hc} gets punishment hold note");
                    newObjects.Add(new PunishmentHoldNote
                    {
                        Column = hc,
                        StartTime = minTime,
                        Duration = maxTime - minTime,
                        UseHealthCapReduction = UseHealthCapReduction.Value
                    });
                }

                Logger.Log($"[ManiaModCleanColumn] Final result: {newObjects.Count} notes in {reorderRule.Length} columns");

                // 记录每个目标列的来源与最终 note 数量，便于调试空列问题
                for (int t = 0; t < reorderRule.Length; t++)
                {
                    string info;
                    if (clearColumns.Contains(t))
                        info = "cleared (-)";
                    else if (holdColumns.Contains(t))
                        info = "hold (|)";
                    else if (columnMapping.TryGetValue(t, out int value))
                        info = $"mapped from source {value}";
                    else
                        info = "no mapping";

                    int count = newObjects.Count(h => h.Column == t);
                    if (count == 0)
                        Logger.Log($"[ManiaModCleanColumn] WARNING: target column {t} ({info}) has 0 notes after rebuild");
                    else
                        Logger.Log($"[ManiaModCleanColumn] target column {t} ({info}) has {count} notes");
                }

                // 更新谱面总列数以匹配重排后的列数，避免难度计算中基于 TotalColumns 分配数组时发生越界
                try
                {
                    beatmap.Stages.Clear();
                    beatmap.Stages.Add(new StageDefinition(reorderRule.Length));
                    beatmap.Difficulty.CircleSize = reorderRule.Length;
                    Logger.Log($"[ManiaModCleanColumn] Updated beatmap stages and Difficulty.CircleSize to {reorderRule.Length}");
                }
                catch (Exception ex)
                {
                    Logger.Log($"[ManiaModCleanColumn] Failed to update stages: {ex.Message}");
                }

                beatmap.HitObjects = newObjects.OrderBy(h => h.StartTime).ToList();
            }
            catch
            {
                // 如果处理失败，保持原谱面不变
            }
        }
    }
}
