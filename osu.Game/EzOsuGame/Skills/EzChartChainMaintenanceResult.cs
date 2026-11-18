// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Outcome of one chart-chain maintenance iteration before Realm is updated.
    /// </summary>
    public sealed record EzChartChainMaintenanceResult(
        Guid BeatmapId,
        string? BeatmapHash,
        EzMinaMaintenanceSection? Mina,
        EzCsiMaintenanceSection? Csi,
        EzDanMaintenanceSection? Dan,
        EzPppMaintenanceSection? Ppp,
        bool RequiresBeatmapCacheInvalidate)
    {
        public static EzChartChainMaintenanceResult Empty(Guid beatmapId, string? beatmapHash)
            => new(beatmapId, beatmapHash, null, null, null, null, false);

        public static bool ComputeRequiresBeatmapCacheInvalidate(
            EzMinaMaintenanceSection? mina,
            EzPppMaintenanceSection? ppp)
            => mina is EzMinaMaintenanceSection.Vector or EzMinaMaintenanceSection.Unrateable
               || ppp is EzPppMaintenanceSection.Skills;

        public bool AnyCsiWrite()
            => Csi is EzCsiMaintenanceSection.Unavailable or EzCsiMaintenanceSection.Computed;
    }
}
