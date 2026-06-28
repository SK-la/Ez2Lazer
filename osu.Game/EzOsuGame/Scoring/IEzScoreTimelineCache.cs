// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Timeline 缓存接口。把 cache 收敛到 Session / 调用方生命周期，避免进程级缓存把
    /// 不同上下文（HitMode / HealthMode / JudgePrecedence / Beatmap）的 timeline 互相覆盖，
    /// 是修复"幽灵成绩停滞 0"问题的核心。
    /// </summary>
    public interface IEzScoreTimelineCache
    {
        /// <summary>
        /// 命中且非空（null 视为 miss）。注意：null timeline 应使用 <see cref="EzScoreTimeline.EMPTY"/>
        /// 作为 sentinel 写入缓存，保证二次查询走"已缓存空"分支而非重建。
        /// </summary>
        bool TryGet(string key, out EzScoreTimeline? timeline);

        /// <summary>
        /// 写入一个 cache 条目；允许 <c>null</c>（哨兵用）。
        /// </summary>
        void Store(string key, EzScoreTimeline? timeline);

        /// <summary>
        /// 清空缓存。
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// 进程内 in-memory 缓存实现。由各调用方持有。
    /// </summary>
    public sealed class EzScoreTimelineCache : IEzScoreTimelineCache
    {
        private readonly ConcurrentDictionary<string, EzScoreTimeline> entries = new ConcurrentDictionary<string, EzScoreTimeline>();

        public bool TryGet(string key, out EzScoreTimeline? timeline)
        {
            if (string.IsNullOrEmpty(key))
            {
                timeline = null;
                return false;
            }

            return entries.TryGetValue(key, out timeline!);
        }

        public void Store(string key, EzScoreTimeline? timeline)
        {
            if (string.IsNullOrEmpty(key))
                return;

            // null 也作为 sentinel 写入，避免反复重建失败的成本（empty timeline 在 QueryAtTime 直接返回 Empty snapshot）。
            entries[key] = timeline ?? EzScoreTimeline.EMPTY;
        }

        public void Clear() => entries.Clear();
    }

    /// <summary>
    /// 永不缓存任何结果。用于测试和"我不希望被外部 cache 影响"的调用方（如 StatisticsPanel 重算）。
    /// </summary>
    public sealed class NullEzScoreTimelineCache : IEzScoreTimelineCache
    {
        public static readonly NullEzScoreTimelineCache INSTANCE = new NullEzScoreTimelineCache();

        public bool TryGet(string key, out EzScoreTimeline? timeline)
        {
            timeline = null;
            return false;
        }

        public void Store(string key, EzScoreTimeline? timeline)
        {
        }

        public void Clear()
        {
        }
    }
}
