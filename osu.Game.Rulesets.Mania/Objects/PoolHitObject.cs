// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.Objects
{
    public class PoolHitObject : ManiaHitObject
    {
        public PoolHitObject(double startTime, int column)
        {
            StartTime = startTime;
            Column = column;
        }
    }
}
