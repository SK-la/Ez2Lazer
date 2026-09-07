// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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

        /// <summary>Optional <see cref="Beatmaps.BeatmapInfo.ID"/> for maintenance when hash changes.</summary>
        [Indexed]
        public Guid BeatmapId { get; set; } = Guid.Empty;

        [Indexed]
        public string SystemId { get; set; } = string.Empty;

        [Indexed]
        public string SkillId { get; set; } = string.Empty;

        public double Value { get; set; }

        public int AlgorithmVersion { get; set; }

        public DateTimeOffset ComputedAt { get; set; }
    }
}
