// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Song-select display snapshot spanning several systems: rating axes, chart dan and the LN radar
    /// metrics. Mod-aware and session-only; never written to storage.
    /// </summary>
    public sealed class EzLiveChartSkillSnapshot
    {
        public required IReadOnlyDictionary<string, double> Msd { get; init; }

        public EzPersistedChartDan? ChartDan { get; init; }

        public int KeyCount { get; init; }

        public double HoldRatio { get; init; }

        /// <summary>Playable hold object count used for the LN chart-dan gate.</summary>
        public int HoldCount { get; init; } = -1;

        /// <summary>Live Sunny star rating from the playable and current mods, when available.</summary>
        public double? XxySr { get; init; }

        /// <summary>True when built from the playable rather than read from storage.</summary>
        public bool IsLiveFromMods { get; init; }

        /// <summary>LN structure floats for the skill radar.</summary>
        public EzChartFeatureMetrics? LnMetrics { get; init; }

        /// <summary>Pattern subtype scores used for 7K LN axis replacement.</summary>
        public IReadOnlyDictionary<string, double>? LnSubtypeScores { get; init; }

        /// <summary>RC pattern-analyzer scores for the yellow radar layer.</summary>
        public IReadOnlyDictionary<string, double>? RcPatternScores { get; init; }
    }
}
