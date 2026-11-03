// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Localisation;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Beatmaps
{
    public class ManiaBeatmap : Beatmap<ManiaHitObject>
    {
        /// <summary>
        /// The definitions for each stage in a <see cref="ManiaPlayfield"/>.
        /// </summary>
        public List<StageDefinition> Stages = new List<StageDefinition>();

        /// <summary>
        /// Total number of columns represented by all stages in this <see cref="ManiaBeatmap"/>.
        /// </summary>
        public int TotalColumns => Stages.Sum(g => g.Columns);

        /// <summary>
        /// 当前实际列数：以 <see cref="Stages"/> 为准，stages 缺失时回落到 <see cref="Beatmap{T}.Difficulty"/> 的 CircleSize。
        /// </summary>
        /// <remarks>
        /// 后转换 mod 只就地改写 <see cref="Beatmap{T}.Difficulty"/> 与 <see cref="Stages"/>，
        /// 不会写回 <c>BeatmapInfo.Difficulty</c>（那是转换开始时克隆的源谱快照）。
        /// 判断「排在前面按 ApplyOrder 跑过的改键 mod（如 PS）之后还剩几列」必须走这里，读元数据会拿到源谱键数。
        /// </remarks>
        public int ResolveColumnCount() => TotalColumns > 0 ? TotalColumns : (int)Math.Round(Difficulty.CircleSize);

        /// <summary>
        /// 把谱面改成单 stage 的 <paramref name="columns"/> 列，并同步 <see cref="Beatmap{T}.Difficulty"/> 的 CircleSize。
        /// </summary>
        /// <remarks>
        /// 两个字段必须一起写：只写 stages 会让读 CircleSize 的代码拿到旧键数，只写 CircleSize 会让读 TotalColumns 的代码拿到旧列数。
        /// </remarks>
        public void SetColumnCount(int columns)
        {
            Stages.Clear();
            Stages.Add(new StageDefinition(columns));
            Difficulty.CircleSize = columns;
        }

        /// <summary>
        /// Creates a new <see cref="ManiaBeatmap"/>.
        /// </summary>
        /// <param name="defaultStage">The initial stages.</param>
        /// <param name="originalTotalColumns">The total number of columns present before any user adjustments. Defaults to the total columns in <paramref name="defaultStage"/>.</param>
        public ManiaBeatmap(StageDefinition defaultStage, int? originalTotalColumns = null)
        {
            Stages.Add(defaultStage);

            // Mods that remove notes leave their audio behind as background sound; keep it panned to the column the
            // note occupied instead of collapsing to the centre.
            SamplePositionResolver = hitObject =>
            {
                if (hitObject is not ManiaHitObject maniaObject)
                    return DetachedNoteSample.CENTRE;

                return Math.Clamp((float)maniaObject.Column / Math.Max(1, TotalColumns), 0, 1);
            };

            // A hold note's head and tail samples live in NodeSamples, not in Samples, and must be reported at the
            // time each sounds at. Reading them here rather than from nested objects is what allows the ledger to see
            // them before ApplyDefaults has run, and on a hold note a mod removed.
            SampleTimelineResolver = hitObject =>
            {
                if (hitObject is not HoldNote hold || hold.NodeSamples is not { Count: > 1 })
                    return DetachedSampleLedger.GetDefaultTimeline(hitObject);

                return enumerateHoldTimeline(hold);
            };
        }

        private static IEnumerable<(double Time, HitSampleInfo Sample)> enumerateHoldTimeline(HoldNote hold)
        {
            var nodeSamples = hold.NodeSamples!;
            double startTime = hold.StartTime;
            double endTime = hold.EndTime;
            int lastIndex = nodeSamples.Count - 1;

            for (int i = 0; i <= lastIndex; i++)
            {
                // 本规则集的节点只在头尾实际发声；多出来的中间节点（osu 折返转来的 hold）按线性位置取时间。
                double time = i == 0 ? startTime
                    : i == lastIndex ? endTime
                    : startTime + (endTime - startTime) * i / lastIndex;

                foreach (var sample in nodeSamples[i])
                    yield return (time, sample);
            }

            var auxiliary = hold.AuxiliarySamples;

            for (int i = 0; i < auxiliary.Count; i++)
            {
                var sample = auxiliary[i];

                if (!hold.Samples.Contains(sample))
                    yield return (startTime, sample);
            }
        }

        public override IEnumerable<BeatmapStatistic> GetStatistics()
        {
            int notes = HitObjects.Count(s => s is Note);
            int holdNotes = HitObjects.Count(s => s is HoldNote);
            int sum = Math.Max(1, notes + holdNotes);

            return new[]
            {
                new BeatmapStatistic
                {
                    Name = BeatmapStatisticStrings.Notes,
                    CreateIcon = () => new BeatmapStatisticIcon(BeatmapStatisticsIconType.Circles),
                    Content = notes.ToString(),
                    BarDisplayLength = notes / (float)sum,
                },
                new BeatmapStatistic
                {
                    Name = BeatmapStatisticStrings.HoldNotes,
                    CreateIcon = () => new BeatmapStatisticIcon(BeatmapStatisticsIconType.Sliders),
                    Content = holdNotes.ToString(),
                    BarDisplayLength = holdNotes / (float)sum,
                },
            };
        }

        public StageDefinition GetStageForColumnIndex(int column)
        {
            foreach (var stage in Stages)
            {
                if (column < stage.Columns)
                    return stage;

                column -= stage.Columns;
            }

            throw new ArgumentOutOfRangeException(nameof(column), "Provided index exceeds all available stages");
        }
    }
}
