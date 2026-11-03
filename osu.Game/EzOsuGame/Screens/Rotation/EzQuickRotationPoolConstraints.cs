// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public sealed record EzQuickRotationPoolConstraints(
        double? StarRatingMin,
        HashSet<string>? CollectionMd5Hashes,
        RulesetInfo Ruleset,
        int? LockedKeyCount,
        bool CrossKeyMode)
    {
        public const int CrossKeyMin = 4;

        public const int CrossKeyMax = 10;
    }
}
