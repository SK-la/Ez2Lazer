// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    internal sealed class LaneTargetState
    {
        public HitObject Target { get; }

        public bool IsTail => Target is TailNote;

        public bool Judged { get; set; }

        public BmsHitModeJudgement.BmsRouteState BmsRoute { get; } = new BmsHitModeJudgement.BmsRouteState();

        public bool HoldBroken { get; set; }

        /// <summary>
        /// [仅尾键状态] HoldNoteBody 辅助判定是否已产出。
        /// 对齐局内：断连时刻 Body 立即记 ComboBreak，之后尾判/结算不得重复补判。
        /// </summary>
        public bool BodyJudged { get; set; }

        public HitResult Result { get; set; }

        public LaneTargetState(HitObject target) => Target = target;
    }

    internal static class ManiaColumnSimulator
    {
        internal static bool IsHittableEarliest(IReadOnlyList<LaneTargetState> column, int index, double time)
            => ManiaLanePressSelector.IsHittableEarliest(column, index, time, static s => s.Judged, static s => s.Target.StartTime);

        internal static IEnumerable<LaneTargetState> ForceMissEarlier(IReadOnlyList<LaneTargetState> column, double targetStartTime)
        {
            foreach (var state in column)
            {
                if (state.Judged)
                    continue;

                if (state.Target.StartTime >= targetStartTime)
                    break;

                yield return state;
            }
        }

        internal static bool IsWithinMissWindow(HitObject target, double eventTime, bool useTailReleaseLenience)
        {
            if (target.HitWindows == null || ReferenceEquals(target.HitWindows, HitWindows.Empty))
                return false;

            double lenienceFactor = useTailReleaseLenience ? TailNote.RELEASE_WINDOW_LENIENCE : 1;
            double missWindow = target.HitWindows.WindowFor(HitResult.Miss) * lenienceFactor;
            double minTime = target.StartTime - missWindow;
            double maxTime = target.StartTime + missWindow;

            return eventTime >= minTime && eventTime <= maxTime;
        }
    }
}
