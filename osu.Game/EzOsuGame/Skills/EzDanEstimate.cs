// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Realms;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Independent dan estimate row (not an MSD/SSR skillset). Extended columns reserved for LeoBlack; evidence lists stay SQLite.
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

        /// <summary>Estimate sits past the published ladder ceiling.</summary>
        public bool BeyondTable { get; set; }

        public string CourseName { get; set; } = string.Empty;

        /// <summary>Course clear accuracy; -1 when unset.</summary>
        public double CourseAccuracy { get; set; } = -1;

        /// <summary>Weighted clears counted toward the estimate window; -1 unset.</summary>
        public int ClearWindowHave { get; set; } = -1;

        /// <summary>Clears needed to fill the averaging window; -1 unset.</summary>
        public int ClearWindowNeed { get; set; } = -1;

        public int AlgorithmVersion { get; set; }

        public DateTimeOffset ComputedAt { get; set; }
    }
}
