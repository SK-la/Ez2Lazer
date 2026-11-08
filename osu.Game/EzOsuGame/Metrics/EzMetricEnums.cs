// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// Which of the four metric layers a value belongs to. Layers have different units and different
    /// consumers, so a value never moves between them.
    /// </summary>
    public enum EzMetricTier
    {
        /// <summary>Chart difficulty rating (a star rating).</summary>
        Star,

        /// <summary>Chart performance rating (a pp number).</summary>
        Performance,

        /// <summary>Per-axis skill vector, or a player aggregate of one.</summary>
        Skill,

        /// <summary>
        /// Structural chart tags (pattern labels, shares, motion). Not a numeric scale and not a skill
        /// vector — it only decides which plays feed which axis, so it has no unit of its own.
        /// </summary>
        Tags,

        /// <summary>Dan rank classification (key count × side), not a numeric scale.</summary>
        Dan,
    }

    /// <summary>Unit a value is expressed in, so two values can be compared or formatted consistently.</summary>
    public enum EzMetricUnit
    {
        Star,
        PerformancePoints,
        SkillPoint,
        DanRank,
        Ratio,
        Count,
    }

    /// <summary>Which mods a stored value was produced under.</summary>
    public enum EzMetricModPolicy
    {
        /// <summary>NoMod baseline. The only policy that may be persisted.</summary>
        NoModOnly,

        /// <summary>NoMod baseline, but rate-changing mods are applied before measuring.</summary>
        RateOnly,

        /// <summary>Fully mod-aware. Live only; a snapshot of it belongs to a read-only branch store.</summary>
        FullMods,
    }

    /// <summary>
    /// Why a read produced a value or not. Replaces the encoding-specific sentinels (a negative number,
    /// a magic axis id, an empty dictionary) that each caller had to interpret itself.
    /// </summary>
    public enum EzMetricState
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
