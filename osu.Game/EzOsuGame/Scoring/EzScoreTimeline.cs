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

        public EzScoreTimelineSnapshot QueryAtTime(double clockTime)
        {
            if (snapshots.Length == 0)
                return EzScoreTimelineSnapshot.Empty;

            if (clockTime <= snapshots[0].ClockTime)
                return snapshots[0];

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

            return snapshots[left];
        }
    }
}
