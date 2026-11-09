// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Why a stored skill row carries a value or not.
    /// </summary>
    /// <remarks>
    /// Stored in the row itself, so a read never has to infer state from a sentinel value.
    /// </remarks>
    public enum EzSkillRowState
    {
        /// <summary>A value at the current revision is available.</summary>
        Current,

        /// <summary>No value has been produced yet.</summary>
        Missing,

        /// <summary>The ruleset or chart shape cannot produce this value at all.</summary>
        Unsupported,

        /// <summary>The producer ran and settled that no value exists for this subject. Not retried.</summary>
        Unrateable,

        /// <summary>A value exists but at an older revision, so it no longer describes the current algorithm.</summary>
        Stale,
    }
}
