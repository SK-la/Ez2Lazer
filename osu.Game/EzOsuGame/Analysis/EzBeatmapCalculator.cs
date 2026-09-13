// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.EzOsuGame.Analysis
{
    public class EzBeatmapCalculator
    {
        public static (double averageKps, double maxKps, List<double> kpsList) GetKps(IBeatmap beatmap)
        {
            var kpsList = new List<double>();
            var hitObjects = beatmap.HitObjects;
            if (hitObjects.Count == 0)
                return (0, 0, kpsList);

            double interval = 4 * 60000 / beatmap.BeatmapInfo.BPM;
            double songEndTime = hitObjects[^1].StartTime;
            const double start_time = 0;

            // 预处理HitObjects按StartTime排序（假设原列表已排序，可省略）
            // 使用二分查找优化区间查询
            for (double currentTime = start_time; currentTime < songEndTime; currentTime += interval)
            {
                double endTime = currentTime + interval;
                int startIdx = findFirstIndexGreaterOrEqual(hitObjects, currentTime);
                int endIdx = findFirstIndexGreaterOrEqual(hitObjects, endTime);
                int hits = endIdx - startIdx;

                kpsList.Add(hits / (interval / 1000));
            }

            if (kpsList.Count == 0)
                return (0, 0, kpsList);

            return (kpsList.Average(), kpsList.Max(), kpsList);
        }

        private static int findFirstIndexGreaterOrEqual(IReadOnlyList<HitObject> hitObjects, double targetTime)
        {
            int low = 0, high = hitObjects.Count;

            while (low < high)
            {
                int mid = (low + high) / 2;
                if (hitObjects[mid].StartTime < targetTime)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low;
        }

        public static Dictionary<int, int> GetColumnNoteCounts(IBeatmap beatmap)
        {
            var counts = new Dictionary<int, int>();

            foreach (var obj in beatmap.HitObjects.OfType<IHasColumn>())
            {
                if (obj is IHasDuration) continue;

                counts[obj.Column] = counts.TryGetValue(obj.Column, out int c) ? c + 1 : 1;
            }

            return counts;
        }

        /// <summary>
        /// Scratch 标签速查表（Markdown），供文档与选歌面板悬浮表格复用。
        /// <para>
        /// N = 真实列数（<c>Ruleset.GetVariantForBeatmap</c>，Mania 下等于 <c>GetKeyCount</c>）。<br/>
        /// 显著高 / 低 = 首列（或末列）Note 数 &gt; 相邻列 × 2 / &lt; 相邻列 ÷ 2。<br/>
        /// 一高一低（首尾各显著且方向相反）：7k / 9k / 16k 按双高那一列，6k / 8k / 10k 走单高列。<br/>
        /// 单边 = 只有一侧显著；双边同时显著时 6k / 8k / 10k 的 mixed 走单高列。<br/>
        /// 标签里 s = scratch、p = pedal，e 见 9k 行；数字相加等于 N。
        /// </para>
        /// <para>
        /// 10k 及以下在上述结果之后还会叠加空列规则：存在 e 个空列时输出 <c>[N-e k _e[]]</c>
        /// （例 7k 末列为空 → <c>[6k_1[]]</c>）。<br/>
        /// 11k 不判别首尾列，恒为 <c>[11k]</c>；12k 及以上不套空列规则。
        /// </para>
        /// </summary>
        public const string SCRATCH_LABEL_TABLE = """
            | 列数 N | 双高 | 单高 | 单低 | 双低 | 无显著 |
            | --- | --- | --- | --- | --- | --- |
            | 6k | 6k | 5k1s | 5+1k | 4+2k | [6k] |
            | 7k | 5k1s1p(含一高一低) | 7k | 7k | 5k1s1p(含一高一低) | [7k] |
            | 8k | 6k2s | 7k1s | 7+1k | 6+2k | [8k] |
            | 9k | 5k1s2e1p（10k2s1p 开(含一高一低)）/ 7k2s（关） | 9k | 9k | 5k1s2e1p（10k2s1p 开(含一高一低)）/ 7+2k（关） | [9k] |
            | 10k | 8k2s | 10k | 10k | 8+2k | [10k] |
            | 11k | [11k] | [11k] | [11k] | [11k] | [11k] |
            | 12k | 10k2s | 10k2s | 10k2s | 10k2s | [12k] |
            | 16k | 10k2s4e（10k2s1p 开(含一高一低))/ 14k2s（关） | 14k2s | 14k2s | 10k2s4e（10k2s1p 开(含一高一低))/ 14k2s（关） | [16k] |
            | 18k | 16k2s | 16k2s | 16k2s | 16k2s | [18k] |

            14k 特例（首 / 末列 = 第 0 / 第 13 列，第 14 列为 ez2ac 的 alpha 列）:
            | 使用 ez2ac 10k2s1p | 第 14 列 | 标签 |
            | --- | --- | --- |
            | 开 | 空 | 10k2s1p |
            | 开 | 有音符 | [14k] |
            | 关 | 任意 | [14k] |
            """;

        /// <summary>
        /// 复用外部已经计算好的 列统计与 KPS 数据，生成 Scratch 标签。
        /// 用于选歌面板：避免重复遍历 HitObjects / 重复计算 KPS。
        /// keyCount 从 columnCounts 推断。
        /// </summary>
        public static string? GetScratchFromPrecomputed(Dictionary<int, int>? columnCounts, double maxKps)
        {
            if (columnCounts == null || columnCounts.Count == 0)
                return null;

            // 从 columnCounts 的最大 key + 1 推断列数
            int keyCount = 0;

            foreach (int k in columnCounts.Keys)
            {
                if (k >= keyCount)
                    keyCount = k + 1;
            }

            if (maxKps == 0) return $"[{keyCount}k] ";

            // 将列统计映射为固定长度数组，方便计算空列与首尾列高低。
            int[] countsByColumn = new int[keyCount];

            foreach (var (column, count) in columnCounts)
            {
                if ((uint)column < (uint)keyCount)
                    countsByColumn[column] = count;
            }

            string result = resolveEdgeLabel(keyCount, countsByColumn) ?? $"[{keyCount}k] ";

            // // 10k 及以下：空列规则覆盖其它标签（12k 及以上不再套用，见 SCRATCH_LABEL_TABLE 文档）。
            // if (keyCount <= 10)
            // {
            //     int emptyColumns = countsByColumn.Count(c => c == 0);
            //     if (emptyColumns > 0)
            //         result = $"[{keyCount - emptyColumns}k_{emptyColumns}[]] ";
            // }

            return result;
        }

        /// <summary>
        /// 按 <see cref="SCRATCH_LABEL_TABLE"/> 生成首尾列标签；无特殊标签返回 <see langword="null"/>（走默认 [NK]）。
        /// </summary>
        private static string? resolveEdgeLabel(int keyCount, int[] countsByColumn)
        {
            var (isFirstLow, isFirstHigh, isLastLow, isLastHigh) = checkNotes(countsByColumn, keyCount);

            bool firstSignificant = isFirstHigh || isFirstLow;
            bool lastSignificant = isLastHigh || isLastLow;
            bool bothSignificant = firstSignificant && lastSignificant;
            bool anySignificant = firstSignificant || lastSignificant;

            bool bothHigh = isFirstHigh && isLastHigh;
            bool bothLow = isFirstLow && isLastLow;

            // 单边：仅一侧显著；两边都显著时不算单边。
            bool singleHigh = (isFirstHigh || isLastHigh) && !bothSignificant;
            bool singleLow = (isFirstLow || isLastLow) && !bothSignificant;

            // 一高一低：6k / 8k / 10k 按“单高”处理，7k / 9k / 16k 按“双高”处理。
            bool mixed = bothSignificant && !bothHigh && !bothLow;

            bool skipEmptyEdgeColumns = GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.ManiaSkipEmptyEdgeColumns);

            switch (keyCount)
            {
                case 6:
                    if (bothLow) return "[4+2k] ";
                    if (singleLow) return "[5+1k] ";
                    if (singleHigh || mixed) return "[5k1s] ";

                    return null;

                case 7:
                    return bothSignificant ? "[5k1s1p] " : null;

                case 8:
                    if (bothHigh) return "[6k2s] ";
                    if (bothLow) return "[6+2k] ";
                    if (singleHigh || mixed) return "[7k1s] ";
                    if (singleLow) return "[7+1k] ";

                    return null;

                case 9:
                    if (!bothSignificant) return null;

                    if (skipEmptyEdgeColumns) return "[5k1s2e1p] ";

                    return bothLow ? "[7+2k] " : "[7k2s] ";

                case 10:

                    if (bothHigh) return "[8k2s] ";
                    if (bothLow) return "[8+2k] ";

                    return null;

                case 12:
                    return anySignificant ? "[10k2s] " : null;

                case 13:
                    return skipEmptyEdgeColumns ? "[10k2s1p] " : "[13k] ";

                case 14:
                    return skipEmptyEdgeColumns ? "[10k2s1p] " : "[14k] ";

                case 16:
                    if (bothSignificant && skipEmptyEdgeColumns) return "[10k2s4e] ";

                    return anySignificant ? "[14k2s] " : null;

                case 18:
                    return anySignificant ? "[16k2s] " : null;

                default:
                    return null;
            }
        }

        private static (bool isFirstLow, bool isFirstHigh, bool isLastLow, bool isLastHigh) checkNotes(int[] countsByColumn, int keyCount)
        {
            bool isFirstLow = false;
            bool isFirstHigh = false;
            bool isLastLow = false;
            bool isLastHigh = false;

            if (keyCount >= 2)
            {
                int firstCount = countsByColumn[0];
                int secondCount = countsByColumn[1];
                isFirstLow = (firstCount > 0 && firstCount < secondCount / 2.0);
                isFirstHigh = firstCount > secondCount * 2;

                int lastCount = countsByColumn[^1];
                int secondLastCount = countsByColumn[^2];
                isLastLow = (lastCount > 0 && lastCount < secondLastCount / 2.0);
                isLastHigh = lastCount > secondLastCount * 2;
            }

            return (isFirstLow, isFirstHigh, isLastLow, isLastHigh);
        }

        // private static bool checkHighSpeed(double maxKps, List<double> kpsList)
        // {
        //     double threshold = maxKps / 4;

        //     foreach (double kps in kpsList)
        //     {
        //         if (kps > threshold)
        //             return true;
        //     }

        //     return false;
        // }
    }
}
