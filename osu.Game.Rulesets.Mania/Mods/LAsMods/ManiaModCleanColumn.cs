// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Background;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.LAsEzExtensions.Mods;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModCleanColumn : Mod, IApplicableToBeatmapConverter, IApplicableAfterBeatmapConversion, IHasApplyOrder
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

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.CustomReorderColumn_Label), nameof(EzManiaModStrings.CustomReorderColumn_Description), SettingControlType = typeof(SettingsTextBox))]
        public Bindable<string> CustomReorderColumn { get; } = new Bindable<string>(string.Empty);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.ApplyOrder_Label), nameof(EzManiaModStrings.ApplyOrder_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> ApplyOrderSetting { get; } = new Bindable<int?>(1000);

        public void ApplyToBeatmapConverter(IBeatmapConverter converter)
        {
            var mbc = (ManiaBeatmapConverter)converter;

            if (!EnableCustomReorder.Value || string.IsNullOrWhiteSpace(CustomReorderColumn.Value))
                return;

            int targetColumns = CustomReorderColumn.Value.Length;

            if ((targetColumns < 1 || targetColumns > 10))
                return;

            mbc.TargetColumns = targetColumns;
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            try
            {
                var maniaBeatmap = (ManiaBeatmap)beatmap;

                int keys = maniaBeatmap.TotalColumns;

                // 确定要删除的列
                HashSet<int> columnsToDelete = new HashSet<int>();

                // 获取列类型（仅用于 S/P/E 列删除）
                if (GlobalConfigStore.EzConfig != null)
                {
                    EzColumnType[] columnTypes = GlobalConfigStore.EzConfig.GetColumnTypes(keys);

                    if (DeleteSColumn.Value)
                    {
                        for (int i = 0; i < keys; i++)
                        {
                            if (columnTypes[i] == EzColumnType.S)
                                columnsToDelete.Add(i);
                        }
                    }

                    if (DeletePColumn.Value)
                    {
                        for (int i = 0; i < keys; i++)
                        {
                            if (columnTypes[i] == EzColumnType.P)
                                columnsToDelete.Add(i);
                        }
                    }

                    if (DeleteEColumn.Value)
                    {
                        for (int i = 0; i < keys; i++)
                        {
                            if (columnTypes[i] == EzColumnType.E)
                                columnsToDelete.Add(i);
                        }
                    }
                }

                // 处理自定义删除列（独立于 EzConfig）
                if (EnableCustomDelete.Value && !string.IsNullOrWhiteSpace(CustomDeleteColumn.Value))
                {
                    foreach (char c in CustomDeleteColumn.Value)
                    {
                        if (char.IsDigit(c))
                        {
                            int colIndex = c == '0' ? 9 : (c - '1'); // 0表示第10列，1-9表示第1-9列
                            if (colIndex >= 0 && colIndex < keys)
                                columnsToDelete.Add(colIndex);
                        }
                    }
                }

                // 处理自定义列重排和清空note（先执行重排）
                if (EnableCustomReorder.Value && !string.IsNullOrWhiteSpace(CustomReorderColumn.Value))
                {
                    applyCustomReorder(maniaBeatmap, CustomReorderColumn.Value, keys);
                }

                // 执行删除操作（后执行删除）
                if (columnsToDelete.Any())
                {
                    var newObjects = new List<ManiaHitObject>();

                    var locations = maniaBeatmap.HitObjects.OfType<Note>().Select(n => (
                                                    startTime: n.StartTime,
                                                    samples: n.Samples,
                                                    column: n.Column,
                                                    endTime: n.StartTime,
                                                    duration: n.StartTime - n.StartTime
                                                ))
                                                .Concat(maniaBeatmap.HitObjects.OfType<HoldNote>().Select(h => (
                                                    startTime: h.StartTime,
                                                    samples: h.Samples,
                                                    column: h.Column,
                                                    endTime: h.EndTime,
                                                    duration: h.EndTime - h.StartTime
                                                ))).OrderBy(h => h.startTime).ThenBy(n => n.column).ToList();

                    foreach (var note in locations)
                    {
                        int column = note.column;

                        if (columnsToDelete.Contains(column))
                            continue;

                        if (note.startTime != note.endTime)
                        {
                            newObjects.Add(new HoldNote
                            {
                                Column = column,
                                StartTime = note.startTime,
                                Duration = note.endTime - note.startTime,
                                NodeSamples = [note.samples, Array.Empty<HitSampleInfo>()]
                            });
                        }
                        else
                        {
                            newObjects.Add(new Note
                            {
                                Column = column,
                                StartTime = note.startTime,
                                Samples = note.samples
                            });
                        }
                    }

                    maniaBeatmap.HitObjects = newObjects.OrderBy(h => h.StartTime).ToList();
                }
            }
            catch
            {
                // 失败时返回原始谱面，不修改
            }
        }

        /// <summary>
        /// 处理自定义列重排和清空note的逻辑
        /// 新格式：字符串长度决定新的列数，每位数字代表该新列使用原谱面的哪一列内容
        /// - 数字：该新列位置使用对应原列的note
        /// - '-'：该新列位置删除（清空）
        /// 例如对于5k谱面：
        ///   "213" → 转换为3k谱面：新列1使用原列2，新列2使用原列1，新列3使用原列3
        ///   "1234555" → 转换为7k谱面：新列1-5使用原列1-5，新列6-7使用原列5
        /// </summary>
        private void applyCustomReorder(ManiaBeatmap beatmap, string reorderRule, int totalColumns)
        {
            try
            {
                // 输入不能为空
                if (string.IsNullOrWhiteSpace(reorderRule))
                    return;

                // 构建列映射和清空列表
                Dictionary<int, int> columnMapping = new Dictionary<int, int>();
                HashSet<int> clearColumns = new HashSet<int>();

                for (int targetPos = 0; targetPos < reorderRule.Length; targetPos++)
                {
                    char c = reorderRule[targetPos];

                    if (c == '-')
                    {
                        // 该新列位置删除
                        clearColumns.Add(targetPos);
                    }
                    else if (char.IsDigit(c))
                    {
                        // 该新列位置使用指定原列的note
                        int sourceCol = c == '0' ? 9 : (c - '1');

                        if (sourceCol >= 0 && sourceCol < totalColumns)
                        {
                            columnMapping[targetPos] = sourceCol;
                        }
                        // 如果源列超出范围，忽略该映射
                    }
                }

                // 如果没有有效的映射，则不进行任何操作
                if (columnMapping.Count == 0 && clearColumns.Count == 0)
                    return;

                // 应用列映射和清空操作
                var newObjects = new List<ManiaHitObject>();

                // 遍历每个新列位置
                for (int newColumn = 0; newColumn < reorderRule.Length; newColumn++)
                {
                    // 如果该新列要被清空，跳过
                    if (clearColumns.Contains(newColumn))
                        continue;

                    // 找到该新列应该使用哪个原列的note
                    if (columnMapping.TryGetValue(newColumn, out int sourceColumn))
                    {
                        // 复制该原列的所有note到新列
                        foreach (var hitObject in beatmap.HitObjects.Where(h => h.Column == sourceColumn))
                        {
                            if (hitObject is Note note)
                            {
                                newObjects.Add(new Note
                                {
                                    Column = newColumn,
                                    StartTime = note.StartTime,
                                    Samples = note.Samples
                                });
                            }
                            else if (hitObject is HoldNote holdNote)
                            {
                                newObjects.Add(new HoldNote
                                {
                                    Column = newColumn,
                                    StartTime = holdNote.StartTime,
                                    Duration = holdNote.Duration,
                                    NodeSamples = holdNote.NodeSamples
                                });
                            }
                        }
                    }
                }

                beatmap.HitObjects = newObjects.OrderBy(h => h.StartTime).ToList();
            }
            catch
            {
                // 如果处理失败，保持原谱面不变
            }
        }

        // 确认此 Mod 在其他转换后 Mod 之后应用，返回更高的应用顺序。
        // 没有此接口的 Mod 被视为顺序 0。
        public int ApplyOrder => ApplyOrderSetting.Value ?? 1000;
    }
}
