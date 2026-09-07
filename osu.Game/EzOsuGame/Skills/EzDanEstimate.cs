// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Realms;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Independent dan estimate row (not an MSD/SSR skillset). Stub until LeoBlack port.
    /// </summary>
    [MapTo("EzDanEstimate")]
    public class EzDanEstimate : RealmObject
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

        public double RawDan { get; set; } = -1;

        public string Label { get; set; } = string.Empty;

        public int Clears { get; set; }

        public int AlgorithmVersion { get; set; }

        public DateTimeOffset ComputedAt { get; set; }
    }
}
