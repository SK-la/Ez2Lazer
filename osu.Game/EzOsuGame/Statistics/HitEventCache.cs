// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.EzOsuGame.Statistics
{
    /// <summary>
    /// 游戏现场的精确 <see cref="HitEvent"/> 轻量化缓存。
    /// <see cref="ScoreInfo.HitEvents"/> 被 Realm 标记为 <c>[Ignored]</c> 不会持久化，
    /// 从 Replay 帧重新生成的 HitEvents 受 ~16.67ms 帧量子化影响，TimeOffset 与现场
    /// 结果存在偏差，导致 <c>rejudgeOriginalHitEvents()</c> 无法对齐原始统计。
    /// 此缓存在游戏结束（精确 HitEvents 尚在内存）时保存 HitEvent 核心字段，
    /// 使统计面板在重新进入时能恢复精确数据并正确重判。
    /// </summary>
    public static class HitEventCache
    {
        /// <summary>
        /// HitEvent 的轻量化表示：只保留重判所需的核心字段。
        /// 不含 <see cref="HitObject"/> 引用，支持跨 Session 持久化。
        /// </summary>
        public sealed class HitEventData
        {
            public double TimeOffset { get; init; }
            public HitResult Result { get; init; }
            /// <summary>
            /// HitObject 类型名（如 "Note" / "HeadNote" / "TailNote"）。
            /// 重建 <see cref="HitEvent"/> 时据此创建对应类型的虚拟 HitObject，
            /// 使判定策略的 <c>is HeadNote / is TailNote</c> 类型检查正确工作。
            /// </summary>
            public string? HitObjectType { get; init; }

            public HitEventData()
            {
            }

            public HitEventData(HitEvent hitEvent)
            {
                TimeOffset = hitEvent.TimeOffset;
                Result = hitEvent.Result;
                HitObjectType = hitEvent.HitObject?.GetType().Name;
            }
        }

        private static readonly ConcurrentDictionary<Guid, List<HitEventData>> cache = new ConcurrentDictionary<Guid, List<HitEventData>>();

        /// <summary>
        /// 规则集特定的 HitObject 工厂。由各规则集在静态初始化时注册，
        /// 使 <see cref="Restore"/> 能根据 <see cref="HitEventData.HitObjectType"/>
        /// 重建正确类型的虚拟 HitObject。
        /// 工厂参数为类型名字符串，返回值应满足判定策略的 <c>is</c> 类型检查。
        /// </summary>
        public static Func<string?, HitObject>? HitObjectFactory { get; set; }

        /// <summary>
        /// 将精确 HitEvents 存入缓存（游戏现场唯一入口）。
        /// </summary>
        public static void Store(Guid scoreId, IReadOnlyList<HitEvent> hitEvents)
        {
            if (scoreId == Guid.Empty || hitEvents.Count == 0)
                return;

            cache[scoreId] = hitEvents.Select(e => new HitEventData(e)).ToList();
        }

        /// <summary>
        /// 从缓存恢复精确 HitEvents。返回空列表表示未命中。
        /// 重建的 HitEvent 包含与现场完全相同的 TimeOffset / Result / HitObject 类型检查，
        /// 使 <c>rejudgeOriginalHitEvents()</c> 能产出与原始统计完全相同的结果。
        /// </summary>
        public static List<HitEvent> Restore(Guid scoreId)
        {
            if (!cache.TryGetValue(scoreId, out var data) || data.Count == 0)
                return new List<HitEvent>();

            return data.Select(d =>
            {
                HitObject hitObj = createHitObject(d.HitObjectType);
                return new HitEvent(d.TimeOffset, null, d.Result, hitObj, null, null);
            }).ToList();
        }

        private static HitObject createHitObject(string? typeName)
        {
            if (HitObjectFactory != null)
                return HitObjectFactory(typeName);

            return new HitObject();
        }
    }
}
