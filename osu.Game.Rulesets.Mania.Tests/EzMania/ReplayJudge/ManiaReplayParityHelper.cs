// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    internal static class ManiaReplayParityHelper
    {
        /// <summary>
        /// 按判定对象时间与 <see cref="HitResult"/> 比较两条 HitEvent 序列（Phase 1 暂不比较 TimeOffset）。
        /// </summary>
        public static bool AreJudgementsEquivalent(IReadOnlyList<HitEvent> expected, IReadOnlyList<HitEvent> actual)
            => format(expected) == format(actual);

        public static string DescribeJudgements(IReadOnlyList<HitEvent> hitEvents)
            => string.Join(", ", judgementEvents(hitEvents).Select(e => $"{describeHitObject(e.HitObject)}:{e.Result}"));

        private static IEnumerable<HitEvent> judgementEvents(IReadOnlyList<HitEvent> hitEvents)
            => hitEvents.Where(e => e.Result != HitResult.IgnoreHit && e.Result != HitResult.IgnoreMiss);

        private static string format(IReadOnlyList<HitEvent> hitEvents)
            => string.Join("|", judgementEvents(hitEvents)
                .OrderBy(e => e.HitObject.StartTime)
                .ThenBy(e => e.HitObject.GetEndTime())
                .Select(e => $"{describeHitObject(e.HitObject)}:{e.Result}"));

        private static string describeHitObject(HitObject hitObject)
            => $"{hitObject.GetType().Name}@{hitObject.StartTime}";
    }
}
