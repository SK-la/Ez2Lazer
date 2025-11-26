// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Scoring
{
    /// <summary>
    /// A dummy hit object for pool judgements.
    /// </summary>
    public class PoolHitObject : HitObject
    {
        public readonly int Column;

        public PoolHitObject(double time, int column)
        {
            StartTime = time;
            Column = column;
            HitWindows = HitWindows.Empty; // Pool doesn't have timing windows
        }
    }
}
