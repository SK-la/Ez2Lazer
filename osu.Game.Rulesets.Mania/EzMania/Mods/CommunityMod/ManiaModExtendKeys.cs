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
using osu.Game.EzOsuGame.Mods;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Mods.KrrConversion;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod
{
    /// <summary>
    /// 奇数键谱面中间列的归属侧。统计半区密度时，中间列的按键算到哪一侧。
    /// </summary>
    public enum MiddleColumnSide
    {
        Both,
        Left,
        Right,
    }

    /// <summary>
    /// 在谱面左右两侧各加若干列，按原谱同侧密度把按键镜像到扩展列里。
    /// </summary>
    /// <remarks>
    /// 只加宽不缩窄；外列一律加在整体最外侧，原列整体右移。判定完全按行做：先用
    /// <see cref="NoteMatrix"/> 把谱面摊成「行 × 列」，再对每条外列逐行掷骰。
    /// </remarks>
    public class ManiaModExtendKeys : Mod, IApplicableAfterBeatmapConversion, IHasSeed, IEzApplyOrder, IEzColumnCountMod
    {
        /// <summary>
        /// 目标键数上限。原谱最大 9 键，扩展只往更高键数走。
        /// </summary>
        private const int max_target_keys = 10;

        /// <summary>
        /// 被叠限制挤掉一个键后，同轨下一个可落点的概率加成（相对值）。
        /// </summary>
        private const double suppressed_note_bonus = 0.5;

        public override string Name => "Extend Keys";

        public override string Acronym => "EXT";

        public override LocalisableString Description => ExtendKeysStrings.DESCRIPTION;

        public override IconUsage? Icon => FontAwesome.Solid.ArrowsAltH;

        public override ModType Type => ModType.CommunityMod;

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => true;

        public override bool ValidForFreestyleAsRequiredMod => false;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return (EzCommonModStrings.KEY_LABEL, $"{TargetKeys.Value}");
                yield return (ExtendKeysStrings.MIDDLE_SIDE_LABEL, $"{MiddleSide.Value}");
                yield return (ExtendKeysStrings.MAX_JACK_LABEL, $"{MaxJackInterval.Value}");
                yield return (ExtendKeysStrings.MIN_GAP_LABEL, $"{MinGap.Value:0.##}");
                yield return (EzCommonModStrings.SEED_LABEL, $"{(Seed.Value == null ? "Null" : Seed.Value)}");
                yield return (EzCommonModStrings.APPLY_ORDER_LABEL, $"{ApplyOrderIndex.Value}");
            }
        }

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.KEY_LABEL), nameof(EzCommonModStrings.KEY_DESCRIPTION))]
        public BindableNumber<int> TargetKeys { get; } = new BindableInt(10)
        {
            MinValue = 2,
            MaxValue = max_target_keys,
            Precision = 1,
        };

        [SettingSource(typeof(ExtendKeysStrings), nameof(ExtendKeysStrings.MIDDLE_SIDE_LABEL), nameof(ExtendKeysStrings.MIDDLE_SIDE_DESCRIPTION))]
        public Bindable<MiddleColumnSide> MiddleSide { get; } = new Bindable<MiddleColumnSide>(MiddleColumnSide.Both);

        [SettingSource(typeof(ExtendKeysStrings), nameof(ExtendKeysStrings.MAX_JACK_LABEL), nameof(ExtendKeysStrings.MAX_JACK_DESCRIPTION))]
        public BindableNumber<double> MaxJackInterval { get; } = new BindableDouble(200)
        {
            MinValue = 0,
            MaxValue = 500,
            Precision = 1,
        };

        [SettingSource(typeof(ExtendKeysStrings), nameof(ExtendKeysStrings.MIN_GAP_LABEL), nameof(ExtendKeysStrings.MIN_GAP_DESCRIPTION))]
        public BindableNumber<double> MinGap { get; } = new BindableDouble(200.0 / 3)
        {
            MinValue = 1,
            MaxValue = 500,
            Precision = 1,
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SEED_LABEL), nameof(EzCommonModStrings.SEED_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>(114514);

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        public int ResolveColumnCount(int currentColumns)
            => currentColumns <= 0 || currentColumns >= TargetKeys.Value ? currentColumns : TargetKeys.Value;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            int currentColumns = maniaBeatmap.ResolveColumnCount();
            int targetColumns = TargetKeys.Value;
            int addedColumns = targetColumns - currentColumns;

            if (currentColumns <= 0 || addedColumns <= 0)
                return;

            // 增量是奇数时先给左侧，保证两侧只差一列。
            int leftColumns = (addedColumns + 1) / 2;
            int rightColumns = addedColumns / 2;

            var hitObjects = maniaBeatmap.HitObjects;
            var extension = new List<ManiaHitObject>();

            if (hitObjects.Count > 0)
                buildExtension(maniaBeatmap, hitObjects, currentColumns, leftColumns, rightColumns, extension);

            // 外列只往整体两侧长，原列统一右移。此刻 ApplyDefaults 未跑，HoldNote 的 Head/Tail 还是 null，
            // 不能依赖 Column setter 把列号传导给嵌套对象。
            foreach (var hitObject in hitObjects)
                hitObject.Column += leftColumns;

            var merged = new List<ManiaHitObject>(hitObjects.Count + extension.Count);
            merged.AddRange(hitObjects);
            merged.AddRange(extension);

            maniaBeatmap.HitObjects = merged.OrderBy(h => h.StartTime).ThenBy(h => h.Column).ToList();
            maniaBeatmap.SetColumnCount(targetColumns);
        }

        private void buildExtension(ManiaBeatmap beatmap, List<ManiaHitObject> hitObjects, int currentColumns, int leftColumns, int rightColumns, List<ManiaHitObject> extension)
        {
            var (matrix, timeAxis) = KrrN2NcConverter.BuildMatrix(beatmap, hitObjects, currentColumns, true);

            if (matrix.Rows == 0)
                return;

            var rowByTime = new Dictionary<int, int>(matrix.Rows);

            for (int i = 0; i < timeAxis.Count; i++)
                rowByTime[timeAxis[i]] = i;

            double[] rowTimes = resolveRowTimes(hitObjects, rowByTime, matrix.Rows);

            var left = resolveSide(currentColumns, true);
            var right = resolveSide(currentColumns, false);

            var builder = new ColumnBuilder(matrix, hitObjects, rowTimes, extension, LnGaps.Resolve(hitObjects), MaxJackInterval.Value, MinGap.Value, new Random(EzModSeed.Resolve(Seed)));

            // 同一侧的多条外列共享同一份密度与 jack 间隔，但各自独立掷骰。
            var leftDensity = left.Length > 0 ? buildDensity(matrix, hitObjects, left) : null;
            var rightDensity = right.Length > 0 ? buildDensity(matrix, hitObjects, right) : null;

            var leftJack = left.Length > 0 ? buildJackMap(matrix, rowTimes, left) : null;
            var rightJack = right.Length > 0 ? buildJackMap(matrix, rowTimes, right) : null;

            // 左外列从 0 起编，右外列接在原列与左外列之后。
            for (int i = 0; i < leftColumns && leftDensity != null && leftJack != null; i++)
                builder.AppendColumn(i, left, leftDensity, leftJack);

            for (int i = 0; i < rightColumns && rightDensity != null && rightJack != null; i++)
                builder.AppendColumn(currentColumns + leftColumns + i, right, rightDensity, rightJack);
        }

        private SideRange resolveSide(int columns, bool left)
        {
            int middle = columns / 2;

            if (columns % 2 == 0)
                return left ? new SideRange(0, middle) : new SideRange(middle, columns - middle);

            return (left, MiddleSide.Value) switch
            {
                (true, MiddleColumnSide.Right) => new SideRange(0, middle),
                (false, MiddleColumnSide.Left) => new SideRange(middle + 1, columns - middle - 1),
                _ => left ? new SideRange(0, middle + 1) : new SideRange(middle, columns - middle),
            };
        }

        /// <summary>
        /// 把行时刻还原成精确的 double 时间。行的 key 是取整后的毫秒，直接用它会把这行所有新对象的
        /// 时间按毫秒对齐，和弦相对原谱漂 &lt; 1ms，长条也会跟着差 1ms。
        /// </summary>
        private static double[] resolveRowTimes(List<ManiaHitObject> hitObjects, Dictionary<int, int> rowByTime, int rows)
        {
            var times = new double[rows];
            var assigned = new bool[rows];

            // 起始时刻先占位：同一行被多个时刻取整命中时保留最先到达的那个精确时间。
            foreach (var hitObject in hitObjects)
            {
                if (!rowByTime.TryGetValue(toTimeKey(hitObject.StartTime), out int row) || assigned[row])
                    continue;

                times[row] = hitObject.StartTime;
                assigned[row] = true;
            }

            foreach (var hitObject in hitObjects)
            {
                if (hitObject is not HoldNote hold || hold.EndTime <= hold.StartTime)
                    continue;

                if (!rowByTime.TryGetValue(toTimeKey(hold.EndTime), out int row) || assigned[row])
                    continue;

                times[row] = hold.EndTime;
                assigned[row] = true;
            }

            return times;
        }

        private static RowDensity buildDensity(NoteMatrix matrix, List<ManiaHitObject> hitObjects, SideRange side)
        {
            var density = new RowDensity(matrix.Rows);

            for (int row = 0; row < matrix.Rows; row++)
            {
                for (int column = side.Start; column < side.Start + side.Length; column++)
                {
                    int cell = matrix[row, column];

                    if (cell < 0)
                        continue;

                    if (hitObjects[cell] is HoldNote)
                        density.Head[row]++;
                    else
                        density.Note[row]++;
                }
            }

            for (int row = 0; row < matrix.Rows; row++)
                density.Probability[row] = Math.Min(1, (density.Note[row] + density.Head[row]) / (double)side.Length);

            return density;
        }

        /// <summary>
        /// 每个按下时间点到「同一轨道上一次按下」的最小间隔。扩展列在同一个时间点最多只能按得一样紧。
        /// 该侧这一行没有按键时是正无穷（不受限制）。
        /// </summary>
        private static double[] buildJackMap(NoteMatrix matrix, double[] rowTimes, SideRange side)
        {
            var jackMap = new double[matrix.Rows];
            var lastPress = new double[side.Length];

            for (int row = 0; row < jackMap.Length; row++)
                jackMap[row] = double.PositiveInfinity;

            for (int i = 0; i < lastPress.Length; i++)
                lastPress[i] = double.NegativeInfinity;

            for (int row = 0; row < matrix.Rows; row++)
            {
                for (int i = 0; i < side.Length; i++)
                {
                    if (matrix[row, side.Start + i] < 0)
                        continue;

                    double gap = rowTimes[row] - lastPress[i];

                    if (gap < jackMap[row])
                        jackMap[row] = gap;

                    lastPress[i] = rowTimes[row];
                }
            }

            return jackMap;
        }

        private static int toTimeKey(double time) => (int)Math.Round(time);

        private readonly struct SideRange
        {
            public readonly int Start;
            public readonly int Length;

            public SideRange(int start, int length)
            {
                Start = start;
                Length = length;
            }

            public bool Contains(int column) => column >= Start && column < Start + Length;
        }

        private sealed class RowDensity
        {
            public readonly int[] Note;
            public readonly int[] Head;
            public readonly double[] Probability;

            public RowDensity(int rows)
            {
                Note = new int[rows];
                Head = new int[rows];
                Probability = new double[rows];
            }
        }

        /// <summary>
        /// 源谱面的缝分布：每条 LN 的尾到同列下一个按键的间距。20% 分位当作最小缝，
        /// 80% 分位离它足够近就认为整张谱的缝很匀（反键图），扩展列固定按中位数复刻。
        /// </summary>
        private readonly struct LnGaps
        {
            public readonly double Min;
            public readonly double Median;
            public readonly bool Uniform;

            private LnGaps(double min, double median, bool uniform)
            {
                Min = min;
                Median = median;
                Uniform = uniform;
            }

            public static LnGaps Resolve(List<ManiaHitObject> hitObjects)
            {
                if (hitObjects.Count == 0)
                    return new LnGaps(0, 0, false);

                int columns = hitObjects.Max(h => h.Column) + 1;
                var pressTimes = new List<double>[columns];

                foreach (var hitObject in hitObjects)
                    (pressTimes[hitObject.Column] ??= new List<double>()).Add(hitObject.StartTime);

                foreach (var times in pressTimes)
                    times?.Sort();

                var gaps = new List<double>();

                foreach (var hitObject in hitObjects)
                {
                    if (hitObject is not HoldNote hold || hold.EndTime <= hold.StartTime)
                        continue;

                    var times = pressTimes[hold.Column];

                    if (times == null)
                        continue;

                    int index = times.BinarySearch(hold.EndTime);

                    if (index < 0)
                        index = ~index;

                    if (index < times.Count)
                        gaps.Add(times[index] - hold.EndTime);
                }

                if (gaps.Count == 0)
                    return new LnGaps(0, 0, false);

                gaps.Sort();

                double min = percentile(gaps, 0.2);
                double max = percentile(gaps, 0.8);

                return new LnGaps(min, percentile(gaps, 0.5), max - min <= Math.Max(16, min * 0.25));
            }

            private static double percentile(List<double> sorted, double quantile)
                => sorted[(int)Math.Round((sorted.Count - 1) * quantile)];
        }

        /// <summary>
        /// 单条外列的生成器。逐行判定 → 同侧叠规则筛选 → 类型与长条状态机。
        /// </summary>
        private sealed class ColumnBuilder
        {
            private readonly NoteMatrix matrix;
            private readonly List<ManiaHitObject> hitObjects;
            private readonly double[] rowTimes;
            private readonly List<ManiaHitObject> extension;
            private readonly LnGaps lnGaps;
            private readonly double maxJackInterval;
            private readonly double minGap;
            private readonly Random rng;

            // 每列的源 LN 尾时刻，已排序：挑收尾候选时按窗口二分，避免每次扫全表。
            private readonly List<double>[] tailTimes;

            // 复用的候选缓冲，避免每次挑来源都新建列表。
            private readonly List<int> candidates = new List<int>();
            private readonly List<double> tailCandidates = new List<double>();

            public ColumnBuilder(NoteMatrix matrix, List<ManiaHitObject> hitObjects, double[] rowTimes, List<ManiaHitObject> extension, LnGaps lnGaps,
                                 double maxJackInterval, double minGap, Random rng)
            {
                this.matrix = matrix;
                this.hitObjects = hitObjects;
                this.rowTimes = rowTimes;
                this.extension = extension;
                this.lnGaps = lnGaps;
                this.maxJackInterval = maxJackInterval;
                this.minGap = minGap;
                this.rng = rng;

                tailTimes = new List<double>[matrix.Cols];

                for (int column = 0; column < tailTimes.Length; column++)
                    tailTimes[column] = new List<double>();

                foreach (var hitObject in hitObjects)
                {
                    if (hitObject is HoldNote hold && hold.EndTime > hold.StartTime && hold.Column < tailTimes.Length)
                        tailTimes[hold.Column].Add(hold.EndTime);
                }

                foreach (var times in tailTimes)
                    times.Sort();
            }

            public void AppendColumn(int targetColumn, SideRange side, RowDensity density, double[] jackMap)
            {
                bool[] selected = selectRows(density, jackMap);
                int[] nextSelected = buildNextSelected(selected);

                // 上一条扩展长条的收尾时刻：这之前的行都被它盖住，不再落键。
                double openEnd = double.NegativeInfinity;

                for (int row = 0; row < matrix.Rows; row++)
                {
                    double rowTime = rowTimes[row];

                    if (rowTime < openEnd)
                        continue;

                    if (!selected[row])
                        continue;

                    int noteCount = density.Note[row];
                    int headCount = density.Head[row];

                    if (headCount > 0 && shouldStartHold(noteCount, headCount) && tryCreateHold(targetColumn, side, row, rowTime, nextSelected[row], out HoldNote? hold, out double closeTime))
                    {
                        extension.Add(hold!);
                        openEnd = closeTime;
                        continue;
                    }

                    extension.Add(createNote(targetColumn, side, row, rowTime));
                }
            }

            /// <summary>
            /// 记录每行之后的第一个被选中的行：它就是外列的下一个按键，收尾时刻要以它为界。
            /// </summary>
            private int[] buildNextSelected(bool[] selected)
            {
                var next = new int[matrix.Rows];
                int upcoming = -1;

                for (int row = matrix.Rows - 1; row >= 0; row--)
                {
                    next[row] = upcoming;

                    if (selected[row])
                        upcoming = row;
                }

                return next;
            }

            private bool[] selectRows(RowDensity density, double[] jackMap)
            {
                var selected = new bool[matrix.Rows];
                double lastPress = double.NegativeInfinity;
                int suppressed = 0;

                for (int row = 0; row < matrix.Rows; row++)
                {
                    if (density.Probability[row] <= 0)
                        continue;

                    // 扩展列两次按下的间隔不能比源谱同一轨道在这一行的间隔更小，最多放宽到 200ms。
                    // 被叠限制挤掉的键攒成加成，转给同轨下一个真正可能落键的位置。
                    if (rowTimes[row] - lastPress < Math.Min(jackMap[row], maxJackInterval))
                    {
                        suppressed++;
                        continue;
                    }

                    double probability = Math.Min(1, density.Probability[row] * (1 + suppressed * suppressed_note_bonus));

                    if (rng.NextDouble() >= probability)
                        continue;

                    selected[row] = true;
                    lastPress = rowTimes[row];
                    suppressed = 0;
                }

                return selected;
            }

            private bool shouldStartHold(int noteCount, int headCount)
                => noteCount == 0 || rng.NextDouble() * (noteCount + headCount) >= noteCount;

            private bool tryCreateHold(int targetColumn, SideRange side, int row, double rowTime, int nextRow, out HoldNote? hold, out double closeTime)
            {
                hold = null;
                closeTime = 0;

                candidates.Clear();

                for (int column = side.Start; column < side.Start + side.Length; column++)
                {
                    int cell = matrix[row, column];

                    if (cell >= 0 && hitObjects[cell] is HoldNote source && source.EndTime > source.StartTime)
                        candidates.Add(cell);
                }

                if (candidates.Count == 0)
                    return false;

                var origin = (HoldNote)hitObjects[candidates[rng.Next(candidates.Count)]];

                // 镜像源的尾是默认收尾时刻，实际用哪个由源谱的缝分布决定。
                double close = resolveClose(side, origin.Column, rowTime, rowTime + origin.Duration, nextRow);

                if (double.IsNaN(close) || close - rowTime <= 0)
                    return false;

                closeTime = close;
                hold = new HoldNote
                {
                    Column = targetColumn,
                    StartTime = rowTime,
                    Duration = close - rowTime,
                    NodeSamples = new List<IList<HitSampleInfo>>
                    {
                        origin.GetNodeSamples(0).ToList(),
                        Array.Empty<HitSampleInfo>(),
                    },
                };

                return true;
            }

            /// <summary>
            /// 决定这条扩展长条在哪收尾。缝要跟源谱一致，所以收尾不早于「头 + 最小缝」，
            /// 也不晚于「外列的下一个按键 - 最小缝」；最小缝另有 100ms 下限。
            /// </summary>
            private double resolveClose(SideRange side, int originColumn, double headTime, double sourceTail, int nextRow)
            {
                // 后面没有按键了，没有缝要照顾，直接沿用源的尾。
                if (nextRow < 0)
                    return sourceTail;

                double gap = Math.Max(lnGaps.Min, minGap);
                double nextTime = rowTimes[nextRow];

                double earliest = headTime + gap;
                double latest = nextTime - gap;

                // 这一段放不下两侧都留缝的长条，退化成单点。
                if (latest <= earliest)
                    return double.NaN;

                // 源谱的缝很匀（反键图）：固定用中位数，让扩展列也长成一串等距的缝。
                if (lnGaps.Uniform)
                    return Math.Clamp(nextTime - lnGaps.Median, earliest, latest);

                // 一半沿用镜像源的尾，另一半与「同侧其他列在窗口内的 LN 尾」「下一个按键 - 最小缝」均分。
                if (rng.NextDouble() < 0.5)
                    return Math.Clamp(sourceTail, earliest, latest);

                tailCandidates.Clear();

                for (int column = side.Start; column < side.Start + side.Length; column++)
                {
                    if (column == originColumn)
                        continue;

                    var times = tailTimes[column];

                    for (int i = firstAtLeast(times, earliest); i < times.Count && times[i] <= latest; i++)
                        tailCandidates.Add(times[i]);
                }

                tailCandidates.Add(latest);

                return tailCandidates[rng.Next(tailCandidates.Count)];
            }

            private static int firstAtLeast(List<double> values, double threshold)
            {
                int index = values.BinarySearch(threshold);

                return index < 0 ? ~index : index;
            }

            private Note createNote(int targetColumn, SideRange side, int row, double rowTime)
                => new Note
                {
                    Column = targetColumn,
                    StartTime = rowTime,
                    Samples = pickSamples(side, row),
                };

            private IList<HitSampleInfo> pickSamples(SideRange side, int row)
            {
                candidates.Clear();

                for (int column = side.Start; column < side.Start + side.Length; column++)
                {
                    int cell = matrix[row, column];

                    if (cell >= 0)
                        candidates.Add(cell);
                }

                if (candidates.Count == 0)
                    return Array.Empty<HitSampleInfo>();

                var origin = hitObjects[candidates[rng.Next(candidates.Count)]];

                return origin is HoldNote hold ? hold.GetNodeSamples(0).ToList() : origin.Samples.ToList();
            }
        }
    }

    public static class ExtendKeysStrings
    {
        public static readonly LocalisableString DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "在谱面左右两侧各加入若干列，按原谱同侧的按键密度把音符镜像到新列，把谱面加宽；增量是奇数时多出来的一列加在左侧",
            "Add columns on both sides of the chart and mirror the matching side's density into them. When the added column count is odd, the extra column goes on the left.");

        public static readonly LocalisableString MIDDLE_SIDE_LABEL = new EzLocalizationManager.EzLocalisableString(
            "中间列归属",
            "Middle Column Side");

        public static readonly LocalisableString MAX_JACK_LABEL = new EzLocalizationManager.EzLocalisableString(
            "叠间隔上限",
            "Max Jack Interval");

        public static readonly LocalisableString MAX_JACK_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "扩展列两次按下的最小间隔最多放宽到这个毫秒数；源谱同轨更紧时可以更紧",
            "Upper bound, in milliseconds, for the minimum interval between two presses in an extension column.");

        public static readonly LocalisableString MIN_GAP_LABEL = new EzLocalizationManager.EzLocalisableString(
            "缝下限",
            "Min LN Gap");

        public static readonly LocalisableString MIN_GAP_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "源谱 LN 尾到下个按键的间距比它窄时按它算，避免极短的缝把扩展列挤成一串碎条",
            "Lower bound, in milliseconds, for the gap between an LN tail and the next press.");

        public static readonly LocalisableString MIDDLE_SIDE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "奇数键谱面中间列计入哪一侧的密度统计；两侧都算时左右扩展列都会镜像它",
            "Which side the middle column of an odd-key chart counts towards. Both mirrors it on both sides.");
    }
}
