// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    internal static class ManiaReplayParityHelper
    {
        private const double time_offset_tolerance = 0.5;

        /// <summary>
        /// 按判定对象时间比较两条 HitEvent 序列（Result + TimeOffset ±容差 + GameplayRate）。
        /// </summary>
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

        /// <summary>
        /// 与 <see cref="AreHitEventsEquivalent"/> 相同；保留旧名供 Session smoke 测试使用。
        /// </summary>
        public static bool AreJudgementsEquivalent(IReadOnlyList<HitEvent> expected, IReadOnlyList<HitEvent> actual)
            => AreHitEventsEquivalent(expected, actual);

        public static string DescribeHitEvents(IReadOnlyList<HitEvent> hitEvents)
            => string.Join(", ", orderedJudgementEvents(hitEvents).Select(describeHitEvent));

        public static string DescribeJudgements(IReadOnlyList<HitEvent> hitEvents)
            => DescribeHitEvents(hitEvents);

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
                return expected.GameplayRate == actual.GameplayRate;

            return Precision.AlmostEquals(expected.GameplayRate.Value, actual.GameplayRate.Value);
        }

        private static string describeHitEvent(HitEvent e)
        {
            string rate = e.GameplayRate?.ToString("F4") ?? "null";
            return $"{describeHitObject(e.HitObject)}:{e.Result}@{e.TimeOffset:F2}r{rate}";
        }

        private static string describeHitObject(HitObject hitObject)
            => $"{hitObject.GetType().Name}@{hitObject.StartTime}";

        public static bool AreScoresEquivalent(double expectedAccuracy, double actualAccuracy, long expectedTotalScore, long actualTotalScore)
            => Precision.AlmostEquals(expectedAccuracy, actualAccuracy)
               && expectedTotalScore == actualTotalScore;
    }
}
