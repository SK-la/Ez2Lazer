// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.ReplayJudge
{
    internal static class OsuReplayParityHelper
    {
        /// <summary>内核在事件时刻落判，Drawable 与 Session 必须逐字段一致；只留浮点比较余量。</summary>
        private const double input_time_offset_tolerance = 0.01;

        /// <summary>非输入判定（过期 Miss、滑条 tick/尾、转盘 tick）同样由内核按 deadline 落判，容差与输入一致。</summary>
        private const double frame_time_offset_tolerance = 0.01;

        public static bool AreHitEventsEquivalent(IReadOnlyList<HitEvent> expected, IReadOnlyList<HitEvent> actual)
        {
            var expectedEvents = orderedJudgementEvents(expected).ToList();
            var actualEvents = orderedJudgementEvents(actual).ToList();

            if (expectedEvents.Count != actualEvents.Count)
                return false;

            for (int i = 0; i < expectedEvents.Count; i++)
            {
                if (!hitEventsMatch(expectedEvents[i], actualEvents[i]))
                    return false;
            }

            return true;
        }

        public static string DescribeHitEvents(IReadOnlyList<HitEvent> hitEvents)
            => string.Join(", ", orderedJudgementEvents(hitEvents).Select(describeHitEvent));

        /// <summary>
        /// 按固定间隔抽帧模拟掉帧，保留按键状态发生变化的首帧，避免按键边沿被抽掉。
        /// </summary>
        public static Score WithDroppedFrames(Score score, double interval)
        {
            var frames = score.Replay!.Frames;
            var kept = new List<ReplayFrame>(frames.Count);

            ReplayFrame? last = null;

            foreach (var frame in frames)
            {
                if (last != null && actionsEqual(last, frame) && frame.Time - last.Time < interval)
                    continue;

                kept.Add(frame);
                last = frame;
            }

            if (frames.Count > 0 && kept.Count > 0 && !ReferenceEquals(kept[kept.Count - 1], frames[frames.Count - 1]))
                kept.Add(frames[frames.Count - 1]);

            score.Replay = new Replay { Frames = kept };
            return score;
        }

        private static bool actionsEqual(ReplayFrame a, ReplayFrame b)
        {
            // 只有 osu 帧带按键集合；其他帧类型一律视为状态变化，宁可少抽帧。
            if (a is not OsuReplayFrame first || b is not OsuReplayFrame second)
                return false;

            return first.Actions.SequenceEqual(second.Actions);
        }

        /// <summary>判定结果序列（过滤 <c>Ignore*</c>，按容器序）。</summary>
        public static IEnumerable<HitResult> OrderedJudgementResults(IReadOnlyList<HitEvent> hitEvents)
            => orderedJudgementEvents(hitEvents).Select(e => e.Result);

        private static IEnumerable<HitEvent> orderedJudgementEvents(IReadOnlyList<HitEvent> hitEvents)
            => judgementEvents(hitEvents)
               .OrderBy(e => e.HitObject.StartTime)
               .ThenBy(e => e.HitObject.GetEndTime());

        private static IEnumerable<HitEvent> judgementEvents(IReadOnlyList<HitEvent> hitEvents)
            => hitEvents.Where(e => e.Result != HitResult.IgnoreHit && e.Result != HitResult.IgnoreMiss);

        private static bool hitEventsMatch(HitEvent expected, HitEvent actual)
        {
            if (expected.Result != actual.Result)
                return false;

            // 只有玩家按键直接触发的判定（圆圈、滑条头）用输入容差；过期 Miss、滑条 tick/尾、转盘 tick 由帧回调结算，用量级容差。
            bool isFrameTimed = !expected.Result.IsHit() || expected.HitObject is SliderEndCircle or SliderTick or SpinnerTick;
            double tolerance = isFrameTimed ? frame_time_offset_tolerance : input_time_offset_tolerance;

            if (!Precision.AlmostEquals(expected.TimeOffset, actual.TimeOffset, tolerance))
                return false;

            if (expected.GameplayRate == null || actual.GameplayRate == null)
            {
                if (expected.GameplayRate != actual.GameplayRate)
                    return false;
            }
            else if (!Precision.AlmostEquals(expected.GameplayRate.Value, actual.GameplayRate.Value))
            {
                return false;
            }

            // 非命中判定的 cursor 元数据是 Drawable receptor 记下的「物件范围内最后一次按键位置」，Session 不产出。
            return !expected.Result.IsHit() || cursorPositionsMatch(expected, actual);
        }

        private static bool cursorPositionsMatch(HitEvent expected, HitEvent actual)
        {
            var expectedPos = getCursorPosition(expected);
            var actualPos = getCursorPosition(actual);

            if (expectedPos == null || actualPos == null)
                return expectedPos == actualPos;

            return Precision.AlmostEquals(expectedPos.Value.X, actualPos.Value.X)
                   && Precision.AlmostEquals(expectedPos.Value.Y, actualPos.Value.Y);
        }

        private static Vector2? getCursorPosition(HitEvent hitEvent) => hitEvent.Position;

        private static string describeHitEvent(HitEvent e)
        {
            string rate = e.GameplayRate?.ToString("F4") ?? "null";
            return $"{describeHitObject(e.HitObject)}:{e.Result}@{e.TimeOffset:F2}r{rate}";
        }

        private static string describeHitObject(HitObject hitObject)
            => $"{hitObject.GetType().Name}@{hitObject.StartTime}";
    }
}
