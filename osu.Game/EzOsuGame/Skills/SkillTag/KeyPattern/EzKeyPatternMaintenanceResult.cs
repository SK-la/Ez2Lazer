// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills.SkillTag.KeyPattern
{
    /// <summary>Outcome of KeyPattern axis backfill before Realm is updated.</summary>
    public sealed record EzKeyPatternMaintenanceResult(
        bool RequiresStore,
        string? BeatmapHash,
        IReadOnlyDictionary<string, double>? Skills)
    {
        public static EzKeyPatternMaintenanceResult NoStoreRequired { get; } = new(
            RequiresStore: false,
            BeatmapHash: null,
            Skills: null);
    }
}