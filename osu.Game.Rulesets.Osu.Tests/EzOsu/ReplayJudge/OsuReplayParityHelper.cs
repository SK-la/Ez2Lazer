// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.ReplayJudge
{
    internal static class OsuReplayParityHelper
    {
        private const double time_offset_tolerance = 12;

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

        /// <summary>判定结果序列（过滤 <c>Ignore*</c>，按容器序）。</summary>
        public static IEnumerable<HitResult> OrderedJudgementResults(IReadOnlyList<HitEvent> hitEvents)
            => orderedJudgementEvents(hitEvents).Select(e => e.Result);

        /// <summary>
        /// 只比较判定结果序列（计数 + 结果）。note lock 场景下 Drawable 会把被抢拍的旧物件立刻 Miss，
        /// Session 让旧物件走自己的过期窗，Miss 时刻因此不同，故这类场景不比较 <see cref="HitEvent.TimeOffset"/>。
        /// </summary>
        public static bool AreHitResultSequencesEquivalent(IReadOnlyList<HitEvent> expected, IReadOnlyList<HitEvent> actual)
            => OrderedJudgementResults(expected).SequenceEqual(OrderedJudgementResults(actual));

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

            if (!Precision.AlmostEquals(expected.TimeOffset, actual.TimeOffset, time_offset_tolerance))
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

            if (!cursorPositionsMatch(expected, actual))
                return false;

            return true;
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
