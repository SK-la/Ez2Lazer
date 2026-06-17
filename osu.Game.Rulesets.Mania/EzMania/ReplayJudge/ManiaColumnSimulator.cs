// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
//
// Replica of UI.OrderedHitPolicy Earliest behaviour — sync when merging ppy/osu master.

using System.Collections.Generic;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    internal static class ManiaColumnSimulator
    {
        internal sealed class LaneTargetState
        {
            public HitObject Target { get; }

            public bool IsTail => Target is TailNote;

            public bool Judged { get; set; }

            public BmsHitModeJudgement.BmsRouteState BmsRoute { get; } = new BmsHitModeJudgement.BmsRouteState();

            public bool HoldBroken { get; set; }

            public HitResult Result { get; set; }

            public LaneTargetState(HitObject target) => Target = target;
        }

        internal static bool IsHittableEarliest(IReadOnlyList<LaneTargetState> column, int index, double time)
        {
            for (int i = index + 1; i < column.Count; i++)
            {
                if (column[i].Judged)
                    continue;

                if (time >= column[i].Target.StartTime)
                    return false;
            }

            return true;
        }

        internal static IEnumerable<LaneTargetState> ForceMissEarlier(IReadOnlyList<LaneTargetState> column, double targetStartTime)
        {
            foreach (var state in column)
            {
                if (state.Judged)
                    continue;

                if (state.Target.StartTime >= targetStartTime)
                    break;

                state.Judged = true;
                state.Result = HitResult.Miss;
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
