// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Realms;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Cached player skillset-dan tile (EZ≥9). Schema reserved so Cache PR needs no EZ bump.
    /// Writers / Provider cache-hit wiring land in DATA-Skillset-Cache — do not delete this type.
    /// </summary>
    [MapTo("EzPlayerDanSkillsetValue")]
    public class EzPlayerDanSkillsetValue : RealmObject
    {
        [PrimaryKey]
        public Guid ID { get; set; } = Guid.NewGuid();

        [Indexed]
        public string Username { get; set; } = string.Empty;

        [Indexed]
        public int KeyCount { get; set; }

        /// <summary><see cref="DanSkillSystem.SIDE_RC"/> or <see cref="DanSkillSystem.SIDE_LN"/>.</summary>
        [Indexed]
        public string Side { get; set; } = DanSkillSystem.SIDE_RC;

        [Indexed]
        public string SkillsetId { get; set; } = string.Empty;

        /// <summary>-1 when unset.</summary>
        public double RawDan { get; set; } = -1;

        public string Label { get; set; } = string.Empty;

        public int Clears { get; set; }

        public int AlgorithmVersion { get; set; }

        public DateTimeOffset ComputedAt { get; set; }
    }
}
