// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Song-select display snapshot for chart MSD / ChartDan / LN skill radar.
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

        /// <summary>LN structure floats for Skill radar (from <see cref="EzDanFeatureExtractor"/>).</summary>
        public EzDanFeatureMetrics? LnMetrics { get; init; }

        /// <summary>
        /// Pattern subtype scores (lngeneral / lntech / lninverse / lnrelease) for 7K LN skill axis replacement.
        /// </summary>
        public IReadOnlyDictionary<string, double>? LnSubtypeScores { get; init; }

        /// <summary>
        /// RC Meta pattern-analyzer scores (chordstream / bracket / … / ln) for Skill radar yellow layer.
        /// </summary>
        public IReadOnlyDictionary<string, double>? RcPatternScores { get; init; }
    }
}
