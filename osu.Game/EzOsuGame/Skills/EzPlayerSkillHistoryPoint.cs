// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Realms;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// One point on a player's per-skillset history curve (Skills history UI).
    /// </summary>
    [MapTo("EzPlayerSkillHistoryPoint")]
    public class EzPlayerSkillHistoryPoint : RealmObject
    {
        [PrimaryKey]
        public Guid ID { get; set; } = Guid.NewGuid();

        [Indexed]
        public string Username { get; set; } = string.Empty;

        [Indexed]
        public int KeyCount { get; set; }

        [Indexed]
        public string SkillId { get; set; } = string.Empty;

        public double Value { get; set; }

        [Indexed]
        public DateTimeOffset RecordedAt { get; set; }

        public int AlgorithmVersion { get; set; }
    }
}
