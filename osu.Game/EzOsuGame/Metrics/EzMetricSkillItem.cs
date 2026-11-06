// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// One addressable axis of one producer: its identity, its metadata, and the group it belongs to.
    /// </summary>
    /// <remarks>
    /// <see cref="SkillId"/> is the storage key, not the producer identity: the two are deliberately separable
    /// so a storage namespace can be renamed without touching the producer id. Ordering and grouping come from
    /// the declaration, so a surface that wants a different arrangement sorts or filters this list itself.
    /// </remarks>
    public readonly record struct EzMetricSkillItem(
        EzMetricProducerId Producer,
        EzMetricAxis Axis,
        string GroupId,
        EzMetricTier Tier,
        EzMetricUnit Unit,
        string SystemId)
    {
        /// <summary>Storage key of this axis.</summary>
        public string SkillId => $"{SystemId}.{Axis.Id}";
    }
}
