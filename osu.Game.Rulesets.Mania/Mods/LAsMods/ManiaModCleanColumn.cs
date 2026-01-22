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
    /// <summary>
    /// 基于 YuLiangSSS 的 ManiaModDeleteColumn 修改而来
    /// 增加一些高阶功能
    /// </summary>
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
        public BindableBool DeleteSColumn { get; } = new BindableBool(true);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.DeletePColumn_Label), nameof(EzManiaModStrings.DeletePColumn_Description))]
        public BindableBool DeletePColumn { get; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.DeleteEColumn_Label), nameof(EzManiaModStrings.DeleteEColumn_Description))]
        public BindableBool DeleteEColumn { get; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.EnableCustomDelete_Label), nameof(EzManiaModStrings.EnableCustomDelete_Description))]
        public BindableBool EnableCustomDelete { get; } = new BindableBool(false);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.CustomDeleteColumn_Label), nameof(EzManiaModStrings.CustomDeleteColumn_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> CustomDeleteColumn { get; } = new Bindable<int?>(0);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.ApplyOrder_Label), nameof(EzManiaModStrings.ApplyOrder_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> ApplyOrderSetting { get; } = new Bindable<int?>(1000);

        public static int TargetColumns = 7;

        public void ApplyToBeatmapConverter(IBeatmapConverter converter)
        {
            var mbc = (ManiaBeatmapConverter)converter;

            float keys = mbc.TotalColumns;

            if (keys != 7)
            {
                return;
            }

            mbc.TargetColumns = TargetColumns;
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            try
            {
                var maniaBeatmap = (ManiaBeatmap)beatmap;

                int keys = maniaBeatmap.TotalColumns;

                // 获取列类型
                if (GlobalConfigStore.EzConfig != null)
                {
                    EzColumnType[] columnTypes = GlobalConfigStore.EzConfig.GetColumnTypes(keys);

                    // 确定要删除的列
                    HashSet<int> columnsToDelete = new HashSet<int>();

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

                    if (EnableCustomDelete.Value && CustomDeleteColumn.Value.HasValue && CustomDeleteColumn.Value.Value >= 0 && CustomDeleteColumn.Value.Value < keys)
                        columnsToDelete.Add(CustomDeleteColumn.Value.Value);

                    if (!columnsToDelete.Any())
                        return; // 没有要删除的列

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

        // 确认此 Mod 在其他转换后 Mod 之后应用，返回更高的应用顺序。
        // 没有此接口的 Mod 被视为顺序 0。
        public int ApplyOrder => ApplyOrderSetting.Value ?? 1000;
    }
}
