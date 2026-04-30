// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods
{
    public class ManiaModCleanColumn : Mod, IApplicableAfterBeatmapConversion, IEzApplyOrder, IHasSeed
    {
        public override string Name => "Clean Column";

        public override string Acronym => "CC";

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => CleanColumnStrings.CLEAN_COLUMN_DESCRIPTION;

        public override IconUsage? Icon => FontAwesome.Solid.Backspace;

        public override ModType Type => ModType.LA_Mod;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;

        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(CleanColumnStrings), nameof(CleanColumnStrings.DELETE_S_COLUMN_LABEL), nameof(CleanColumnStrings.DELETE_S_COLUMN_DESCRIPTION))]
        public BindableBool DeleteSColumn { get; } = new BindableBool();

        [SettingSource(typeof(CleanColumnStrings), nameof(CleanColumnStrings.DELETE_P_COLUMN_LABEL), nameof(CleanColumnStrings.DELETE_P_COLUMN_DESCRIPTION))]
        public BindableBool DeletePColumn { get; } = new BindableBool();

        [SettingSource(typeof(CleanColumnStrings), nameof(CleanColumnStrings.DELETE_E_COLUMN_LABEL), nameof(CleanColumnStrings.DELETE_E_COLUMN_DESCRIPTION))]
        public BindableBool DeleteEColumn { get; } = new BindableBool();

        [SettingSource(typeof(CleanColumnStrings), nameof(CleanColumnStrings.ENABLE_CUSTOM_DELETE_LABEL), nameof(CleanColumnStrings.ENABLE_CUSTOM_DELETE_DESCRIPTION))]
        public BindableBool EnableCustomDelete { get; } = new BindableBool();

        [SettingSource(typeof(CleanColumnStrings), nameof(CleanColumnStrings.CUSTOM_DELETE_COLUMN_LABEL), nameof(CleanColumnStrings.CUSTOM_DELETE_COLUMN_DESCRIPTION),
            SettingControlType = typeof(SettingsTextBox))]
        public Bindable<string> CustomDeleteColumn { get; } = new Bindable<string>(string.Empty);

        [SettingSource(typeof(CleanColumnStrings), nameof(CleanColumnStrings.ENABLE_CUSTOM_REORDER_LABEL), nameof(CleanColumnStrings.ENABLE_CUSTOM_REORDER_DESCRIPTION))]
        public BindableBool EnableCustomReorder { get; } = new BindableBool();

        [SettingSource(typeof(CleanColumnStrings), nameof(CleanColumnStrings.CUSTOM_REORDER_COLUMN_LABEL), nameof(CleanColumnStrings.CUSTOM_REORDER_COLUMN_DESCRIPTION),
            SettingControlType = typeof(SettingsTextBox))]
        public Bindable<string> CustomReorderColumn { get; } = new Bindable<string>(string.Empty);

        [SettingSource(typeof(CleanColumnStrings), nameof(CleanColumnStrings.USE_HEALTH_CAP_REDUCTION_LABEL), nameof(CleanColumnStrings.USE_HEALTH_CAP_REDUCTION_DESCRIPTION))]
        public BindableBool UseHealthCapReduction { get; } = new BindableBool();

        [SettingSource(typeof(CleanColumnStrings), nameof(CleanColumnStrings.USE_ROW_RANDOM_LABEL), nameof(CleanColumnStrings.USE_ROW_RANDOM_DESCRIPTION))]
        public BindableBool UseRowRandom { get; } = new BindableBool();

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SEED_LABEL), nameof(EzCommonModStrings.SEED_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        private HashSet<int>? columnsToDelete;
        private int keys1;
        private int keys2;
        private bool customReorderApplied;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (DeleteSColumn.Value) yield return (CleanColumnStrings.DELETE_S_COLUMN_LABEL, "On");
                if (DeletePColumn.Value) yield return (CleanColumnStrings.DELETE_P_COLUMN_LABEL, "On");
                if (DeleteEColumn.Value) yield return (CleanColumnStrings.DELETE_E_COLUMN_LABEL, "On");
                if (EnableCustomDelete.Value) yield return (CleanColumnStrings.ENABLE_CUSTOM_DELETE_LABEL, string.IsNullOrWhiteSpace(CustomDeleteColumn.Value) ? "Enabled" : CustomDeleteColumn.Value);
                if (EnableCustomReorder.Value) yield return (CleanColumnStrings.ENABLE_CUSTOM_REORDER_LABEL, string.IsNullOrWhiteSpace(CustomReorderColumn.Value) ? "Enabled" : CustomReorderColumn.Value);
                if (UseHealthCapReduction.Value) yield return (CleanColumnStrings.USE_HEALTH_CAP_REDUCTION_LABEL, "On");

                yield return (EzCommonModStrings.SEED_LABEL, Seed.Value?.ToString() ?? "Random");
                yield return (EzCommonModStrings.APPLY_ORDER_LABEL, $"{ApplyOrderIndex.Value}");
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            // TODO: 需要考虑是否把类型删除提前，实现先删盘子，后转谱，再自定义删除
            try
            {
                var maniaBeatmap = (ManiaBeatmap)beatmap;
                keys1 = (int)maniaBeatmap.Difficulty.CircleSize;

                // 处理自定义列重排
                if (EnableCustomReorder.Value && !string.IsNullOrWhiteSpace(CustomReorderColumn.Value) && !customReorderApplied)
                {
                    Logger.Log($"[ManiaModCleanColumn] Calling applyCustomReorder with reorderRule: {CustomReorderColumn.Value}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                    applyCustomReorder(maniaBeatmap, CustomReorderColumn.Value, keys1);
                    keys2 = (int)maniaBeatmap.Difficulty.CircleSize;
                    customReorderApplied = true;
                }

                columnsToDelete = new HashSet<int>();

                // 获取列类型（仅用于 S/P/E 列删除）
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

                // 处理自定义删除列
                if (EnableCustomDelete.Value && !string.IsNullOrWhiteSpace(CustomDeleteColumn.Value))
                {
                    foreach (char c in CustomDeleteColumn.Value)
                    {
                        if (tryParseColumnToken(c, keys1, out int colIndex))
                            columnsToDelete.Add(colIndex);
                    }
                }

                // 执行删除操作（后执行删除） —— 一次性过滤以避免大量 Remove 调用导致的 O(n*m) 行为
                if (columnsToDelete.Any())
                {
                    maniaBeatmap.HitObjects = maniaBeatmap.HitObjects
                                                          .Where(h => !(h is ManiaHitObject maniaHitObject && columnsToDelete.Contains(maniaHitObject.Column)))
                                                          .ToList();
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
                Logger.Log($"[ManiaModCleanColumn] Using {sourceHitObjects.Count} source hit objects for reordering", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

                // 构建列映射、清空列表和长按列表
                Dictionary<int, int> columnMapping = new Dictionary<int, int>();
                HashSet<int> clearColumns = new HashSet<int>();
                HashSet<int> holdColumns = new HashSet<int>();
                HashSet<int> rowRandomColumns = new HashSet<int>();

                // 计算实际的源列数（以源 HitObjects 中出现的最大列号为准），避免传入的 totalColumns 失准导致空列
                int sourceColumns = sourceHitObjects.Count > 0 ? sourceHitObjects.Max(h => h.Column) + 1 : totalColumns;

                int oscSeed = Seed.Value ?? RNG.Next();
                var random = new Random(oscSeed);

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
                        if (UseRowRandom.Value)
                        {
                            rowRandomColumns.Add(targetPos);
                        }
                        else
                        {
                            int randomSourceCol = sourceColumns > 0 ? random.Next(0, sourceColumns) : 0;
                            columnMapping[targetPos] = randomSourceCol;
                            Logger.Log($"[ManiaModCleanColumn] Column {targetPos} randomly mapped to source column {randomSourceCol}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                        }
                    }
                    else if (tryParseColumnToken(c, sourceColumns, out int sourceCol))
                    {
                        columnMapping[targetPos] = sourceCol;
                    }
                }

                // 如果没有有效映射，则不进行任何操作
                if (columnMapping.Count == 0 && clearColumns.Count == 0 && holdColumns.Count == 0 && rowRandomColumns.Count == 0)
                    return;

                // 计算谱面的时间范围，用于创建贯穿的长按note
                double minTime = sourceHitObjects.Min(h => h.StartTime);
                double maxTime = sourceHitObjects.Max(h => h.GetEndTime());

                // 应用列映射、清空和长按操作
                // 预分配容量以减少重复分配
                var newObjects = new List<ManiaHitObject>(sourceHitObjects.Count);

                // 显示实际源列的 note 分布（一次遍历统计以避免每列重复遍历）
                int[] counts = new int[sourceColumns];

                foreach (var h in sourceHitObjects)
                {
                    if (h.Column >= 0 && h.Column < sourceColumns)
                        counts[h.Column]++;
                }

                for (int i = 0; i < sourceColumns; i++)
                    Logger.Log($"[ManiaModCleanColumn] Original column {i} has {counts[i]} notes", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

                Logger.Log($"[ManiaModCleanColumn] Column mapping: {string.Join(", ", columnMapping.Select(kv => $"{kv.Key}->{kv.Value}"))}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

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

                if (rowRandomColumns.Count > 0)
                {
                    Logger.Log($"[ManiaModCleanColumn] Row-random enabled for target columns: {string.Join(", ", rowRandomColumns.OrderBy(c => c))}",
                        Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

                    // 按每个起始时间作为一行：对每个 ? 目标列，从该行原始 note 随机抽取一个（也可能抽空）。
                    var rows = locations.GroupBy(x => x.startTime)
                                        .OrderBy(g => g.Key);

                    foreach (var row in rows)
                    {
                        var rowObjects = row.ToList();
                        int rowCount = rowObjects.Count;

                        if (rowCount == 0)
                            continue;

                        foreach (int target in rowRandomColumns)
                        {
                            // 额外一个“空白”槽位，表示本行该 ? 目标列不放 note（可有可无）。
                            int pick = random.Next(0, rowCount + 1);

                            if (pick == rowCount)
                                continue;

                            var loc = rowObjects[pick];

                            if (loc.isHold)
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
                    Logger.Log($"[ManiaModCleanColumn] Column {hc} gets punishment hold note", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                    newObjects.Add(new PunishmentHoldNote
                    {
                        Column = hc,
                        StartTime = minTime,
                        Duration = maxTime - minTime,
                        UseHealthCapReduction = UseHealthCapReduction.Value
                    });
                }

                Logger.Log($"[ManiaModCleanColumn] Final result: {newObjects.Count} notes in {reorderRule.Length} columns", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

                // 记录每个目标列的来源与最终 note 数量，便于调试空列问题
                for (int t = 0; t < reorderRule.Length; t++)
                {
                    string info;
                    if (clearColumns.Contains(t))
                        info = "cleared (-)";
                    else if (holdColumns.Contains(t))
                        info = "hold (|)";
                    else if (rowRandomColumns.Contains(t))
                        info = "row-random (?)";
                    else if (columnMapping.TryGetValue(t, out int value))
                        info = $"mapped from source {value}";
                    else
                        info = "no mapping";

                    int count = newObjects.Count(h => h.Column == t);
                    if (count == 0)
                        Logger.Log($"[ManiaModCleanColumn] WARNING: target column {t} ({info}) has 0 notes after rebuild", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                    else
                        Logger.Log($"[ManiaModCleanColumn] target column {t} ({info}) has {count} notes", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                }

                // 更新谱面总列数以匹配重排后的列数，避免难度计算中基于 TotalColumns 分配数组时发生越界
                try
                {
                    beatmap.Stages.Clear();
                    beatmap.Stages.Add(new StageDefinition(reorderRule.Length));
                    beatmap.Difficulty.CircleSize = reorderRule.Length;
                    Logger.Log($"[ManiaModCleanColumn] Updated beatmap stages and Difficulty.CircleSize to {reorderRule.Length}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[ManiaModCleanColumn] Failed to update stages: {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                }

                beatmap.HitObjects = newObjects.OrderBy(h => h.StartTime).ToList();
            }
            catch
            {
                // 如果处理失败，保持原谱面不变
            }
        }

        private static bool tryParseColumnToken(char token, int totalColumns, out int column)
        {
            column = -1;

            if (totalColumns <= 0)
                return false;

            if (totalColumns <= 10)
            {
                if (token is >= '1' and <= '9')
                {
                    int index = token - '1';

                    if (index < totalColumns)
                    {
                        column = index;
                        return true;
                    }
                }
                else if (token == '0' && totalColumns >= 10)
                {
                    column = 9;
                    return true;
                }

                return false;
            }

            if (totalColumns >= 12)
            {
                int sideCount = Math.Min(9, totalColumns / 2);
                char rightToken = char.ToLowerInvariant(token);

                if (token is >= '1' and <= '9')
                {
                    int sideIndex = token - '1';

                    if (sideIndex < sideCount)
                    {
                        column = sideIndex;
                        return true;
                    }
                }
                else if (rightToken is >= 'a' and <= 'i')
                {
                    int sideIndex = rightToken - 'a';

                    if (sideIndex < sideCount)
                    {
                        column = totalColumns - 1 - sideIndex;
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public static class CleanColumnStrings
    {
        public static readonly LocalisableString CLEAN_COLUMN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("整理Column, 排序、删除轨道中的note", "Clean Column, Sort, Delete notes in the column.");
        public static readonly LocalisableString DELETE_S_COLUMN_LABEL = new EzLocalizationManager.EzLocalisableString("删除S列", "Delete S Column Type");

        public static readonly LocalisableString DELETE_S_COLUMN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("开启时删除标记了S Column Type的列",
            "Delete columns marked with S column type when enabled");

        public static readonly LocalisableString DELETE_P_COLUMN_LABEL = new EzLocalizationManager.EzLocalisableString("删除P列", "Delete P Column Type");

        public static readonly LocalisableString DELETE_P_COLUMN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("开启时删除标记了P Column Type的列",
            "Delete columns marked with P column type when enabled");

        public static readonly LocalisableString DELETE_E_COLUMN_LABEL = new EzLocalizationManager.EzLocalisableString("删除E列", "Delete E Column Type");

        public static readonly LocalisableString DELETE_E_COLUMN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("开启时删除标记了E Column Type的列",
            "Delete columns marked with E column type when enabled");

        public static readonly LocalisableString ENABLE_CUSTOM_DELETE_LABEL = new EzLocalizationManager.EzLocalisableString("自定义删除列", "Enable Custom Delete");

        public static readonly LocalisableString ENABLE_CUSTOM_DELETE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "开启后启用自定义删除列功能，支持与其他功能同时使用。输入多个数字如'2468'删除第2、4、6、8列",
            "Enable custom column deletion when enabled, can be used with other features. Input multiple digits like '2468' to delete columns 2, 4, 6, 8");

        public static readonly LocalisableString CUSTOM_DELETE_COLUMN_LABEL = new EzLocalizationManager.EzLocalisableString("删除列序号", "Delete Column Indexes");

        public static readonly LocalisableString CUSTOM_DELETE_COLUMN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "输入要删除的列序号，支持多个字符。"
            + "\n10k及以下的索引：1~0（0表示第10列）；"
            + "\n12k及以上的索引：左侧1~9，右侧a~i(对称指令)"
            + "\n超过谱面列数的索引将被忽略。",
            "Input the column indexes to delete, supports multiple tokens."
            + "\nFor 10k or below: 1~0 (0 means column 10)."
            + "\nFor 12k or above: left 1~9, right a~i (symmetric instruction)."
            + "\nIndexes exceeding the beatmap's column count are ignored.");

        public static readonly LocalisableString ENABLE_CUSTOM_REORDER_LABEL = new EzLocalizationManager.EzLocalisableString("自定义列重排", "Enable Custom Reorder");

        public static readonly LocalisableString ENABLE_CUSTOM_REORDER_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "开启后启用自定义列清洗Column功能，如果更改了列数，需要重开此功能才能正常生效",
            "Enable custom column reorder when enabled, if the column count is changed, you need to toggle this feature again to take effect");

        public static readonly LocalisableString CUSTOM_REORDER_COLUMN_LABEL = new EzLocalizationManager.EzLocalisableString("列重排规则", "Column Reorder Rule");

        public static readonly LocalisableString CUSTOM_REORDER_COLUMN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "按列批处理，自定义排序、复制、删除或修改。先重排，后删除。"
            + "\n10k及以下的索引：1~0（0表示第10列）；"
            + "\n12k及以上的索引：左侧1~9，右侧a~i(对称指令)"
            + "\n'-': 此列清空；"
            + "\n'|': 此列放一个全谱长度的LN；"
            + "\n'?': 此列随机（默认按列）。"
            + "\n输入字符的位置代表新谱的列号，数字对应原谱匹配列号的内容，总长度决定转谱的列数（须重启开关）。"
            + "\n示例：原谱面5k(原始1-5列)，输入 2|30-? 转换为6k："
            + "\n- 新列1=原列2；新列2=锁手长按；新列3=原列3；新列4=原列10（超出，忽略，空列）；新列5='-'清空；新列6=随机的原列。",
            "Batch process columns, custom reorder, copy, delete or modify. First reorder, then delete."
            + "\n10k or less index: 1~0 (0 means column 10);"
            + "\n12k or more index: left 1~9, right a~i(symmetric instruction)"
            + "\n'-': This column is cleared;"
            + "\n'|': This column puts a full-length LN;"
            + "\n'?': This column is random (default by column)."
            + "\nThe position of the input character represents the new column number, the number corresponds to the original column number. The total length determines the number of columns in the new beatmap (must restart the switch)."
            + "\nExample: original 5k, input " + '"' + "2|30-1" + '"' + " converts to 6k:"
            + "\n- New col1 = orig col2;"
            + "\n- New col2 = full-length hold;"
            + "\n- New col3 = orig col3;"
            + "\n- New col4 = orig col10 (ignored, empty);"
            + "\n- New col5 = '-' cleared;"
            + "\n- New col6 = orig col1.");

        public static readonly LocalisableString USE_HEALTH_CAP_REDUCTION_LABEL = new EzLocalizationManager.EzLocalisableString(
            "|操作符: 血量上限降低",
            "Health Cap Reduction Mode");

        public static readonly LocalisableString USE_HEALTH_CAP_REDUCTION_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "开启后, |操作符添加的锁手LN，在combo break时，降低血量上限15%而不是直接扣血，最低40%上限",
            "Enable this to reduce health cap when | operation is added. When combo break, health cap will be reduced by 15% instead of being directly deducted, minimum 40% cap.");

        public static readonly LocalisableString USE_ROW_RANDOM_LABEL = new EzLocalizationManager.EzLocalisableString(
            "?操作符: 按行随机", "? operation: Row Random");

        public static readonly LocalisableString USE_ROW_RANDOM_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "开启后，?操作符处理时，按节拍从原谱整行中随机抽取，类似官方Random的单列做法。",
            "When enabled, ? operation will randomly select from the original beatmap's whole row, similar to the single-column Random behavior in official Random.");
    }
}
