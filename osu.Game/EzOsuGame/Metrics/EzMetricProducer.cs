// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// Declares what one producer emits: which layer, which axes, which rulesets, and which other
    /// producers it reads.
    /// </summary>
    /// <remarks>
    /// Only <see cref="DependsOn"/> affects staleness. Reusing another producer's axis vocabulary or
    /// engine is not a dependency: it must not retire a value that was computed from its own inputs.
    /// </remarks>
    public sealed class EzMetricProducer
    {
        public EzMetricProducer(
            EzMetricProducerId id,
            EzMetricTier tier,
            EzMetricUnit unit,
            Func<int, bool> appliesToRuleset)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id.Id);
            ArgumentNullException.ThrowIfNull(appliesToRuleset);

            Id = id;
            Tier = tier;
            Unit = unit;
            AppliesToRuleset = appliesToRuleset;
        }

        public EzMetricProducerId Id { get; }

        /// <summary>
        /// Storage namespace this producer writes under. Kept separate from <see cref="Id"/> so identity and
        /// storage key can be renamed independently: identity is what code compares, this is what existing
        /// rows were written with.
        /// </summary>
        public string SystemId { get; init; } = string.Empty;

        public EzMetricTier Tier { get; }

        public EzMetricUnit Unit { get; }

        public string EffectiveSystemId => string.IsNullOrEmpty(SystemId) ? Id.Id : SystemId;

        /// <summary>Which slices the producer emits. False means the producer is unsliced.</summary>
        public bool Sliced { get; init; }

        public EzMetricModPolicy ModPolicy { get; init; } = EzMetricModPolicy.NoModOnly;

        public Func<int, bool> AppliesToRuleset { get; }

        /// <summary>
        /// Axis family this producer belongs to, when its axes are meant to be compared against another
        /// producer's. <see cref="EzMetricFamily.None"/> for scalar-only producers.
        /// </summary>
        public EzMetricFamily Family { get; init; } = EzMetricFamily.None;

        /// <summary>Axis subsets this producer emits. Empty for a scalar-only producer.</summary>
        public IReadOnlyList<EzMetricAxisGroup> AxisGroups { get; init; } = Array.Empty<EzMetricAxisGroup>();

        /// <summary>Producers whose stored rows this one reads. Only these produce a version edge.</summary>
        public IReadOnlyList<EzMetricProducerId> DependsOn { get; init; } = Array.Empty<EzMetricProducerId>();

        /// <summary>
        /// This producer's own algorithm revision, independent of its dependencies. A producer that
        /// delegates staleness to an upstream scalar returns that scalar here.
        /// </summary>
        public Func<int, int> RevisionProbe { get; init; } = static _ => 1;

        /// <summary>
        /// Whether this producer is the default radar source for its side on rulesetOnlineId.
        /// At most one producer per catalog and ruleset may answer true.
        /// </summary>
        public Func<int, bool> IsDefaultRadarFor { get; init; } = static _ => false;

        /// <summary>Analysis system whose flattened rating this producer writes, or <see cref="EzRatingSystem.None"/>.</summary>
        public EzRatingSystem RatingSystem { get; init; } = EzRatingSystem.None;

        /// <summary>Which half of <see cref="RatingSystem"/> this producer writes.</summary>
        public EzRatingField RatingField { get; init; } = EzRatingField.None;

        /// <summary>Whether this producer contributes a scalar tier (star, performance, tags or dan) rather than a vector.</summary>
        public bool IsScalar => Tier is EzMetricTier.Star or EzMetricTier.Performance or EzMetricTier.Tags or EzMetricTier.Dan;

        /// <summary>Every axis across the groups applicable to a ruleset, de-duplicated and ordered.</summary>
        public IReadOnlyList<EzMetricAxis> AxesFor(int rulesetOnlineId)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<EzMetricAxis>();

            foreach (var group in AxisGroups)
            {
                if (!group.AppliesToRuleset(rulesetOnlineId))
                    continue;

                foreach (var axis in group.Axes)
                {
                    if (seen.Add(axis.Id))
                        result.Add(axis);
                }
            }

            result.Sort(static (a, b) => a.Order.CompareTo(b.Order));
            return result;
        }

        /// <summary>Groups that apply to a slice and ruleset.</summary>
        public IEnumerable<EzMetricAxisGroup> GroupsFor(int sliceKey, int rulesetOnlineId)
        {
            foreach (var group in AxisGroups)
            {
                if (group.Applies(sliceKey, rulesetOnlineId))
                    yield return group;
            }
        }

        /// <summary>
        /// Whether every group applicable to this slice and ruleset is satisfied by <paramref name="stored"/>.
        /// A producer that declares no groups at all stores a scalar, so it is satisfied by holding any row;
        /// a producer whose groups simply do not cover this slice/ruleset owes nothing here.
        /// </summary>
        public bool IsCompleteFor(int sliceKey, int rulesetOnlineId, IReadOnlyDictionary<string, double> stored)
        {
            ArgumentNullException.ThrowIfNull(stored);

            if (AxisGroups.Count == 0)
                return stored.Count > 0;

            foreach (var group in GroupsFor(sliceKey, rulesetOnlineId))
            {
                if (!group.IsSatisfiedBy(this, stored))
                    return false;
            }

            return true;
        }
    }
}
