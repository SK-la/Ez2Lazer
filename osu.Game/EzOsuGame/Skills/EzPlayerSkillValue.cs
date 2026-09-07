// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Realms;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Persisted independent player skill value for one keymode (e.g. ssr.stream @ 7K).
    /// </summary>
    [MapTo("EzPlayerSkillValue")]
    public class EzPlayerSkillValue : RealmObject
    {
        [PrimaryKey]
        public Guid ID { get; set; } = Guid.NewGuid();

        [Indexed]
        public string Username { get; set; } = string.Empty;

        [Indexed]
        public int KeyCount { get; set; }

        [Indexed]
        public string SystemId { get; set; } = string.Empty;

        [Indexed]
        public string SkillId { get; set; } = string.Empty;

        public double Value { get; set; }

        public int AnalyzedPlays { get; set; }

        public int AlgorithmVersion { get; set; }

        public DateTimeOffset ComputedAt { get; set; }
    }
}
