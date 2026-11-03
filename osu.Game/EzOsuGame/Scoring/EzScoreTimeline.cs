// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Scoring
{
    public sealed class EzScoreTimeline
    {
        /// <summary>
        /// 空 timeline 哨兵。任何 <see cref="EzScoreTimelineSnapshot.Empty"/> 查询的稳定占位，
        /// 避免上游缓存 "已尝试构建但失败" 的 ghost 时反复重试。
        /// </summary>
        public static readonly EzScoreTimeline EMPTY = new EzScoreTimeline(Array.Empty<EzScoreTimelineSnapshot>());

        private readonly EzScoreTimelineSnapshot[] snapshots;

        public long FinalTotalScore { get; }

        public EzScoreTimeline(IReadOnlyList<EzScoreTimelineSnapshot> snapshots)
        {
            if (snapshots.Count == 0)
            {
                this.snapshots = Array.Empty<EzScoreTimelineSnapshot>();
                FinalTotalScore = 0;
                return;
            }

            // 直接引用传入数组，避免完整拷贝。调用方须保证传入后不再修改该数组。
            // 所有生产路径（ManiaReplayTimelineRecorder.Build / buildFromHitEvents）构建后均不再修改。
            if (snapshots is EzScoreTimelineSnapshot[] arr)
                this.snapshots = arr;
            else
            {
                this.snapshots = new EzScoreTimelineSnapshot[snapshots.Count];
                for (int i = 0; i < snapshots.Count; i++)
                    this.snapshots[i] = snapshots[i];
            }

            FinalTotalScore = this.snapshots[^1].TotalScore;
        }

        // TODO(EZ-SR-TL-021): 动态变速 Mod（GetTrueGameplayRate 随时间变化）下 ghost 可能与玩家时钟不同步。
        // 统一倍速 Mod（DT/HT 等）在 Mod 过滤一致时无需 rate 换算；若将来修复，推荐增量 Session 而非 rate 列表。
        public EzScoreTimelineSnapshot QueryAtTime(double clockTime)
        {
            int index = -1;
            TryQueryAtTime(clockTime, ref index, out var snapshot);
            return snapshot;
        }

        /// <summary>
        /// 时钟单调前进时 O(1) 摊还；回退/seek 时回退到二分查找。
        /// <paramref name="cachedIndex"/> 由调用方（processor）持有，避免多 ghost 共享 timeline 时互相污染。
        /// </summary>
        public bool TryQueryAtTime(double clockTime, ref int cachedIndex, out EzScoreTimelineSnapshot snapshot)
        {
            if (snapshots.Length == 0)
            {
                cachedIndex = -1;
                snapshot = EzScoreTimelineSnapshot.Empty;
                return false;
            }

            if (cachedIndex >= 0 && cachedIndex < snapshots.Length)
            {
                if (cachedIndex + 1 < snapshots.Length && clockTime >= snapshots[cachedIndex].ClockTime && clockTime < snapshots[cachedIndex + 1].ClockTime)
                {
                    snapshot = snapshots[cachedIndex];
                    return true;
                }

                if (cachedIndex == snapshots.Length - 1 && clockTime >= snapshots[cachedIndex].ClockTime)
                {
                    snapshot = snapshots[cachedIndex];
                    return true;
                }
            }

            if (clockTime <= snapshots[0].ClockTime)
            {
                cachedIndex = 0;
                snapshot = snapshots[0];
                return true;
            }

            int left = 0;
            int right = snapshots.Length - 1;

            while (left < right)
            {
                int mid = left + (right - left + 1) / 2;

                if (snapshots[mid].ClockTime <= clockTime)
                    left = mid;
                else
                    right = mid - 1;
            }

            cachedIndex = left;
            snapshot = snapshots[left];
            return true;
        }
    }
}
