// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Song-select display snapshot for chart MSD / ChartDan.
    /// Temporary mod-aware compute for the selected chart (never written to Realm).
    /// </summary>
    public sealed class EzLiveChartSkillSnapshot
    {
        public required IReadOnlyDictionary<string, double> Msd { get; init; }

        public EzPersistedChartDan? ChartDan { get; init; }

        public int KeyCount { get; init; }

        public double HoldRatio { get; init; }

        /// <summary>Playable hold object count used for the LN ChartDan gate.</summary>
        public int HoldCount { get; init; } = -1;

        /// <summary>Live xxySR from playable + mods (same path as analysis panel), when available.</summary>
        public double? XxySr { get; init; }

        /// <summary>True when built from playable (selected-chart live overlay; not Realm).</summary>
        public bool IsLiveFromMods { get; init; }
    }
}
