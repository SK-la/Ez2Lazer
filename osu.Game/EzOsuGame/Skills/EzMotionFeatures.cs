// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    ///     4K wrist-versus-roll note shares (hub <c>MotionFeatures</c> subset used by the speed/tech model).
    /// </summary>
    public sealed class EzMotionFeatures
    {
        public double RhythmBreak { get; init; }

        public double CrossHandTrill { get; init; }

        public double MiniJack { get; init; }

        public double SameHand { get; init; }
    }
}
