// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills.SSR
{
    /// <summary>
    /// Outcome of one Sunny rating computation before Realm is updated.
    /// </summary>
    public sealed record EzSunnyMaintenanceResult(
        bool RequiresStore,
        double Star,
        double? SunnyPp,
        double? OfficialStar,
        double? OfficialPp,
        string? BeatmapHash,
        IReadOnlyDictionary<string, double>? SsrSkillAxes,
        IReadOnlyDictionary<string, double>? KeyPatternSkillAxes)
    {
        public static EzSunnyMaintenanceResult NoStoreRequired { get; } = new EzSunnyMaintenanceResult(
            RequiresStore: false, Star: 0, SunnyPp: null, OfficialStar: null, OfficialPp: null, BeatmapHash: null, SsrSkillAxes: null, KeyPatternSkillAxes: null);
    }
}
