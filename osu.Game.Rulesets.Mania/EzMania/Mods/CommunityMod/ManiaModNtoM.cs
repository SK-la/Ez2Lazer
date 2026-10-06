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
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.EzOsuGame.Mods;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod
{
    public class ManiaModNtoM : Mod, IApplicableAfterBeatmapConversion, IHasSeed, IEzApplyOrder, IApplicableToBeatmapConverter
    {
        public override string Name => "Nk to Mk Converter";

        public override string Acronym => "NTM"; //Nk to Mk Letter

        public override LocalisableString Description => NtoMStrings.NTOM_DESCRIPTION;

        public override IconUsage? Icon => FontAwesome.Solid.Moon;

        public override ModType Type => ModType.CommunityMod;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return (EzCommonModStrings.PROBABILITY_LABEL, $"{Probability.Value}%");
                yield return (EzCommonModStrings.KEY_LABEL, $"{Key.Value}");
                yield return (EzCommonModStrings.SEED_LABEL, $"{(Seed.Value == null ? "Null" : Seed.Value)}");
                yield return (EzCommonModStrings.APPLY_ORDER_LABEL, $"{ApplyOrderIndex.Value}");
            }
        }

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.PROBABILITY_LABEL), nameof(EzCommonModStrings.PROBABILITY_DESCRIPTION))]
        public BindableNumber<int> Probability { get; set; } = new BindableInt(70)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 5
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.KEY_LABEL), nameof(EzCommonModStrings.KEY_DESCRIPTION))]
        public BindableNumber<int> Key { get; set; } = new BindableInt(8)
        {
            MinValue = 2,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SEED_LABEL), nameof(EzCommonModStrings.SEED_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>(114514);

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 100
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        public void ApplyToBeatmapConverter(IBeatmapConverter converter)
        {
            var mbc = (ManiaBeatmapConverter)converter;

            float keys = mbc.TotalColumns;

            if (keys > 9 || Key.Value <= keys) return;

            mbc.TargetColumns = Key.Value;
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            int seed = EzModSeed.Resolve(Seed);
            var rng = new Random(seed);
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var maniaBeatmap = (ManiaBeatmap)beatmap;

            int keys = (int)maniaBeatmap.Difficulty.CircleSize;

            if (keys > 9 || Key.Value <= keys) return;

            var newObjects = new List<ManiaHitObject>();

            var newColumnObjects = new List<ManiaHitObject>();

            // 与 newColumnObjects 同步维护的按列索引：第一轮的重叠判定原本每列都要扫全表。
            var columnIndex = new ManiaObjectColumnIndex(Key.Value);

            var fixedColumnObjects = new List<ManiaHitObject>();

            // 每个键数轮次都要重建这份快照（原来是 OfType/Select/Concat/OrderBy/ThenBy 的迭代器 + 元组列表），
            // 改成结构体列表 + 带原始序号的稳定键排序：同一 (开始时间, 列) 的先后与 OrderBy().ThenBy() 一致。
            var locations = new List<Location>();
            buildLocations(maniaBeatmap.HitObjects, locations);

            #region Null column

            int keyValue = keys + 1;
            bool firstKeyFlag = true;

            int emptyColumn = rng.Next(-1, 1 + keyValue - 2);

            while (keyValue <= Key.Value)
            {
                var confirmNull = new List<bool>();
                for (int i = 0; i <= Key.Value; i++) confirmNull.Add(false);
                var nullColumnList = new List<int>();

                if (firstKeyFlag)
                {
                    foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
                    {
                        int count = column.Count();
                        if (!confirmNull[column.Key] && count != 0) confirmNull[column.Key] = true;
                    }

                    for (int i = 0; i < Key.Value; i++)
                    {
                        if (!confirmNull[i])
                            nullColumnList.Add(i);
                    }

                    firstKeyFlag = false;
                }

                int atLeast = 5;
                double changeTime = 0;

                bool plus = true;
                bool minus = false;
                bool next = false;

                for (int i = 0; i < locations.Count; i++)
                {
                    // 原来这里无条件 new 了一个 Note 和一个 HoldNote（随后又各自被全新的对象取代），
                    // 两个分配与它们的 Samples setter 拷贝都是纯浪费，只留判定。
                    bool isLN = locations[i].StartTime != locations[i].EndTime;
                    int columnNum = locations[i].Column;
                    int minusColumn = 0;

                    foreach (int nul in nullColumnList)
                    {
                        if (columnNum > nul)
                            minusColumn++;
                    }

                    columnNum -= minusColumn;

                    #endregion

                    atLeast--;

                    bool error = changeTime != locations[i].StartTime;

                    if (keys < 4) // why you are converting 1k 2k 3k into upper keys?
                        columnNum = rng.Next(keyValue);
                    else
                    {
                        if (error && rng.Next(100) < Probability.Value && atLeast < 0)
                        {
                            changeTime = locations[i].StartTime;
                            atLeast = keys - 2;
                            next = true;
                        }

                        if (next && plus)
                        {
                            next = false;
                            emptyColumn++;

                            if (emptyColumn > keyValue - 2)
                            {
                                plus = !plus;
                                minus = !minus;
                                emptyColumn = keyValue - 2;
                            }
                        }
                        else if (next && minus)
                        {
                            next = false;
                            emptyColumn--;

                            if (emptyColumn < -1)
                            {
                                plus = !plus;
                                minus = !minus;
                                emptyColumn = -1;
                            }
                        }

                        if (columnNum > emptyColumn) columnNum++;
                    }

                    bool overlap = columnIndex.Overlaps(columnNum, locations[i].StartTime);

                    if (overlap)
                    {
                        for (int k = 0; k < keyValue; k++)
                        {
                            if (!columnIndex.Overlaps(columnNum - k, locations[i].StartTime) && columnNum - k >= 0)
                                columnNum -= k;
                            else if (!columnIndex.Overlaps(columnNum + k, locations[i].StartTime)
                                     && columnNum + k <= keyValue - 1) columnNum += k;
                        }
                    }

                    if (isLN)
                    {
                        newColumnObjects.Add(new HoldNote
                        {
                            Column = Math.Clamp(columnNum, 0, Key.Value - 1),
                            StartTime = locations[i].StartTime,
                            Duration = locations[i].EndTime - locations[i].StartTime,
                            NodeSamples = [locations[i].Samples, Array.Empty<HitSampleInfo>()]
                        });
                    }
                    else
                    {
                        newColumnObjects.Add(new Note
                        {
                            Column = Math.Clamp(columnNum, 0, Key.Value - 1),
                            StartTime = locations[i].StartTime,
                            Samples = locations[i].Samples
                        });
                    }

                    columnIndex.Add(newColumnObjects[^1]);
                }

                for (int i = 0; i < newColumnObjects.Count; i++)
                {
                    bool overlap = false, outIndex = false;

                    if (newColumnObjects[i].Column < 0 || newColumnObjects[i].Column > Key.Value - 1)
                    {
                        outIndex = true;
                        newColumnObjects[i].Column = rng.Next(Key.Value - 1);
                    }

                    for (int j = i + 1; j < newColumnObjects.Count; j++)
                    {
                        // 两个分支都要求 newColumnObjects[i].StartTime >= newColumnObjects[j].StartTime - 2，
                        // 而本列表按开始时间非降序（由 locations 的顺序追加而来），所以 j 的开始时间一旦超出 i+2，
                        // 后面的 j 只会更晚，直接收尾。原来这里每轮都要把整表扫完。
                        if (newColumnObjects[j].StartTime > newColumnObjects[i].StartTime + 2)
                            break;

                        if (newColumnObjects[i].Column == newColumnObjects[j].Column && newColumnObjects[i].StartTime >= newColumnObjects[j].StartTime - 2
                                                                                     && newColumnObjects[i].StartTime <= newColumnObjects[j].StartTime + 2) overlap = true;

                        if (newColumnObjects[j].StartTime != newColumnObjects[j].GetEndTime())
                        {
                            if (newColumnObjects[i].Column == newColumnObjects[j].Column && newColumnObjects[i].StartTime >= newColumnObjects[j].StartTime - 2
                                                                                         && newColumnObjects[i].StartTime <= newColumnObjects[j].GetEndTime() + 2)
                                overlap = true;
                        }
                    }

                    if (outIndex) overlap = true;

                    if (!overlap)
                        fixedColumnObjects.Add(newColumnObjects[i]);
                    else
                    {
                        // 原来的两次 FindOverlapInList(obj, 过滤到某列的列表) 恒为 true（判定内部按 obj.Column 比较，
                        // 而列表已按别的列过滤），于是这个循环实际只是在边界内按 k 挪列。删掉每轮 keyValue 次
                        // 「过滤 + 全表扫描 + 临时列表」，语义不变。
                        for (int k = 1; k < keyValue; k++)
                        {
                            if (newColumnObjects[i].Column - k >= 0)
                                newColumnObjects[i].Column -= k;
                            else if (newColumnObjects[i].Column + k <= keyValue - 1) newColumnObjects[i].Column += k;
                        }

                        fixedColumnObjects.Add(newColumnObjects[i]);
                    }
                }

                if (keyValue < Key.Value)
                {
                    keys++;
                    keyValue = keys + 1;

                    // 复用同一个 locations 列表：原来是每轮重建一份匿名元组列表。
                    buildLocations(fixedColumnObjects, locations);

                    emptyColumn = -1;
                    fixedColumnObjects.Clear();
                    newColumnObjects.Clear();
                    columnIndex.Clear();
                }
                else
                    break;
            }

            newObjects.AddRange(fixedColumnObjects);

            maniaBeatmap.HitObjects = newObjects;
        }

        /// <summary>
        /// 把一份 <see cref="ManiaHitObject"/> 列表压成按 (开始时间, 列) 升序的快照。
        /// </summary>
        /// <remarks>
        /// 顺序必须与原来的 <c>OfType&lt;Note&gt;().Select(…).Concat(OfType&lt;HoldNote&gt;().Select(…)).OrderBy(startTime).ThenBy(column)</c>
        /// 完全一致：先非长条、后长条，再按 (开始时间, 列) 稳定排序。<see cref="Location.Order"/>
        /// 记录进入排序前的序号并作为最终比较键，从而在没有稳定排序原语时复现 <c>OrderBy</c> 的稳定性。
        /// </remarks>
        private static void buildLocations(IReadOnlyList<ManiaHitObject> source, List<Location> destination)
        {
            destination.Clear();

            addLocations(source, destination, holds: false);
            addLocations(source, destination, holds: true);

            destination.Sort(static (a, b) => compare(a, b));
        }

        private static void addLocations(IReadOnlyList<ManiaHitObject> source, List<Location> destination, bool holds)
        {
            foreach (var hitObject in source)
            {
                if (holds ? hitObject is not HoldNote : hitObject is not Note)
                    continue;

                destination.Add(new Location
                {
                    StartTime = hitObject.StartTime,
                    Column = hitObject.Column,
                    EndTime = hitObject.GetEndTime(),
                    Samples = hitObject.Samples,
                    Order = destination.Count
                });
            }
        }

        private static int compare(Location a, Location b)
        {
            int byStartTime = a.StartTime.CompareTo(b.StartTime);

            if (byStartTime != 0)
                return byStartTime;

            int byColumn = a.Column.CompareTo(b.Column);

            return byColumn != 0 ? byColumn : a.Order.CompareTo(b.Order);
        }

        private struct Location
        {
            public double StartTime;
            public int Column;
            public double EndTime;
            public IList<HitSampleInfo> Samples;

            /// <summary>进入排序前的序号，用作最终比较键以复现稳定排序。</summary>
            public int Order;
        }
    }

    public static class NtoMStrings
    {
        public static readonly LocalisableString NTOM_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("转换为更高的按键数模式", "Convert to upper Keys mode.");
    }
}
