using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Logging;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Mods.LAsMods;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Mods.KrrConversion
{
    public static class KrrN2NcConverter
    {
        public static void Transform(ManiaBeatmap beatmap, KrrOptions? options)
        {
            int targetKeys = options?.TargetKeys ?? beatmap.TotalColumns;
            int maxKeys    = options?.MaxKeys    ?? targetKeys;
            int minKeys    = options?.MinKeys    ?? 1;
            int speedIndex = options?.BeatSpeed ?? 4;
            int seedValue  = options?.Seed       ?? KrrConversionHelper.ComputeSeedFromBeatmap(beatmap);

            int originalKeys = KrrConversionHelper.InferOriginalKeys(beatmap, targetKeys);
            if (originalKeys == targetKeys) return;

            var rng = new Random(seedValue);
            var osc = new Oscillator(seedValue);

            // 计算转换时间窗口
            double convertTime = KrrConversionHelper.ComputeConvertTime(speedIndex, beatmap.BeatmapInfo.BPM);

            // 1. 按时间区间切分
            var segmentedObjects = ApplyTimeSegmentation(beatmap.HitObjects, targetKeys, convertTime, rng);

            // 2. 冲突检测与删除
            var conflictResolved = ResolveConflicts(segmentedObjects, convertTime);

            // 3. 空行补充
            var filledObjects = FillEmptyRows(conflictResolved, targetKeys, rng);

            // 4. 密度控制
            var finalObjects = AdjustDensity(filledObjects, targetKeys, minKeys, maxKeys, rng);

            // 更新谱面对象
            beatmap.HitObjects.Clear();
            beatmap.HitObjects.AddRange(finalObjects.OrderBy(h => h.StartTime).ThenBy(h => h.Column));

            // 更新谱面总列数
            beatmap.Stages.Clear();
            beatmap.Stages.Add(new StageDefinition(targetKeys));
            beatmap.Difficulty.CircleSize = targetKeys;
        }

        public static List<ManiaHitObject> ApplyTimeSegmentation(List<ManiaHitObject> objects, int targetKeys, double convertTime, Random rng)
        {
            var result = new List<ManiaHitObject>();
            var grouped = objects.GroupBy(o => (int)(o.StartTime / convertTime));

            foreach (var segment in grouped)
            {
                foreach (var obj in segment)
                {
                    int newCol = rng.Next(0, targetKeys);
                    var clone = KrrConversionHelper.CloneWithColumn(obj, newCol);
                    result.Add(clone);
                }
            }

            return result.OrderBy(o => o.StartTime).ToList();
        }

        public static List<ManiaHitObject> ResolveConflicts(List<ManiaHitObject> objects, double convertTime)
        {
            var result = new List<ManiaHitObject>();
            var grouped = objects.GroupBy(o => (int)(o.StartTime / convertTime));

            foreach (var segment in grouped)
            {
                var byColumn = segment.GroupBy(o => o.Column);

                foreach (var colGroup in byColumn)
                {
                    // 如果同一时间段同一列有多个音符，保留一个
                    result.Add(colGroup.First());
                }
            }

            return result.OrderBy(o => o.StartTime).ToList();
        }

        public static List<ManiaHitObject> FillEmptyRows(List<ManiaHitObject> objects, int targetKeys, Random rng)
        {
            var result = new List<ManiaHitObject>(objects);
            var grouped = objects.GroupBy(o => o.StartTime);

            foreach (var group in grouped)
            {
                if (!group.Any())
                {
                    // 插入一个随机音符
                    var note = new Note
                    {
                        Column = rng.Next(0, targetKeys),
                        StartTime = group.Key
                    };
                    result.Add(note);
                }
            }

            return result.OrderBy(o => o.StartTime).ToList();
        }

        public static List<ManiaHitObject> AdjustDensity(List<ManiaHitObject> objects, int targetKeys, int minKeys, int maxKeys, Random rng)
        {
            var result = new List<ManiaHitObject>();
            var grouped = objects.GroupBy(o => o.StartTime);

            foreach (var group in grouped)
            {
                var list = group.ToList();

                if (list.Count > maxKeys)
                    list = list.OrderBy(x => rng.Next()).Take(maxKeys).ToList();
                else if (list.Count < minKeys && list.Count > 0)
                {
                    int idx = 0;

                    while (list.Count < minKeys)
                    {
                        list.Add(KrrConversionHelper.CloneWithColumn(list[idx % list.Count], rng.Next(0, targetKeys)));
                        idx++;
                    }
                }

                result.AddRange(list);
            }

            return result.OrderBy(o => o.StartTime).ToList();
        }
    }
}
