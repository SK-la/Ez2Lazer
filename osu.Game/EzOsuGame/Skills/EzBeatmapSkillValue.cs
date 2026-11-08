// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using Realms;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Persisted independent skill value for a beatmap (e.g. the <c>Mina</c> system's <c>stream</c> axis).
    /// </summary>
    /// <remarks>
    /// The system is stored as its declared id (see <see cref="EzAnalysisSystemStorage"/>), which is stable across
    /// member renames. Realm cannot persist a custom enum, so the row state stays its underlying value.
    /// </remarks>
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

        /// <summary>Declared id of the <see cref="EzAnalysisSystem"/> this row belongs to.</summary>
        [Indexed]
        public string SystemId { get; set; } = string.Empty;

        /// <summary>
        /// Ruleset the value belongs to. A chart can be rated by several rulesets, and rows for different
        /// rulesets must not be read as one another.
        /// </summary>
        [Indexed]
        public int RulesetOnlineId { get; set; } = -1;

        /// <summary>Bare axis id within <see cref="SystemId"/>.</summary>
        [Indexed]
        public string SkillId { get; set; } = string.Empty;

        public double Value { get; set; }

        /// <summary>Underlying value of <see cref="EzSkillRowState"/>.</summary>
        public int State { get; set; }

        public int AlgorithmVersion { get; set; }

        public DateTimeOffset ComputedAt { get; set; }
    }
}
