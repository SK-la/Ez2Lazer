// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using Realms;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Persisted independent skill value for a beatmap (e.g. beatmap_msd.stream).
    /// </summary>
    [MapTo("EzBeatmapSkillValue")]
    public class EzBeatmapSkillValue : RealmObject
    {
        [PrimaryKey]
        public Guid ID { get; set; } = Guid.NewGuid();

        [Indexed]
        public string BeatmapHash { get; set; } = string.Empty;

        /// <summary>Optional <see cref="BeatmapInfo.ID"/> for maintenance when hash changes.</summary>
        [Indexed]
        public Guid BeatmapId { get; set; } = Guid.Empty;

        [Indexed]
        public string SystemId { get; set; } = string.Empty;

        /// <summary>
        /// Ruleset the value belongs to. A chart can be rated by several rulesets, and rows for different
        /// rulesets must not be read as one another.
        /// </summary>
        [Indexed]
        public int RulesetOnlineId { get; set; } = -1;

        [Indexed]
        public string SkillId { get; set; } = string.Empty;

        public double Value { get; set; }

        /// <summary>Whether this row represents a computed value or a settled miss.</summary>
        public int State { get; set; }

        public int AlgorithmVersion { get; set; }

        public DateTimeOffset ComputedAt { get; set; }
    }
}
