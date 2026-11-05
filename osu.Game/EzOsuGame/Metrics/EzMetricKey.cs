// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// Locates exactly one stored value: which producer made it, for which subject, under which
    /// ruleset and slice, on which axis.
    /// </summary>
    /// <remarks>
    /// The ruleset is part of the key rather than derived from the subject. A subject can be rated by
    /// several rulesets (a converted chart), and player rows for different rulesets must not share a slice.
    /// </remarks>
    public readonly record struct EzMetricKey(
        EzMetricProducerId Producer,
        string Subject,
        int RulesetOnlineId,
        int SliceKey,
        string? AxisId)
    {
        /// <summary>True when this key addresses a single value rather than one axis of a vector.</summary>
        public bool IsScalar => string.IsNullOrEmpty(AxisId);

        public override string ToString()
            => $"{Producer}|{Subject}|r{RulesetOnlineId}|s{SliceKey}|{AxisId ?? "<scalar>"}";
    }

    /// <summary>
    /// One stored value together with what it took to produce it and whether it still applies.
    /// </summary>
    public readonly record struct EzMetricValue(double Value, int Revision, EzMetricState State, DateTimeOffset ComputedAt)
    {
        public static EzMetricValue Missing(int revision)
            => new(0, revision, EzMetricState.Missing, default);

        public static EzMetricValue Unsupported(int revision)
            => new(0, revision, EzMetricState.Unsupported, default);

        public static EzMetricValue Unrateable(int revision, DateTimeOffset computedAt)
            => new(0, revision, EzMetricState.Unrateable, computedAt);

        /// <summary>Whether a caller may use <see cref="Value"/> as the current answer.</summary>
        public bool HasValue => State == EzMetricState.Current;
    }
}
