// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mods
{
    public interface IHitWindows : IApplicableMod
    {
        bool IsHitResultAllowed(HitResult result);
        double WindowFor(HitResult result);

        // internal DifficultyRange[] GetRanges();
    }
}
