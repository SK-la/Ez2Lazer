// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills.SSR
{
    /// <summary>
    /// Outcome of Sunny SR4 / KeyPattern axis backfill before Realm is updated (no star recompute).
    /// </summary>
    public sealed record EzSunnyAxesMaintenanceResult(
        bool RequiresStore,
        Guid BeatmapId,
        string? BeatmapHash,
        IReadOnlyDictionary<string, double>? SsrSkillAxes,
        IReadOnlyDictionary<string, double>? KeyPatternSkillAxes)
    {
        public static EzSunnyAxesMaintenanceResult NoStoreRequired { get; } = new(
            RequiresStore: false,
            BeatmapId: Guid.Empty,
            BeatmapHash: null,
            SsrSkillAxes: null,
            KeyPatternSkillAxes: null);
    }
}