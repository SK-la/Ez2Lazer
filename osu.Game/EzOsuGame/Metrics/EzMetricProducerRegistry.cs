// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using osu.Game.EzOsuGame.Skills;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// The single place that knows which producers exist and what revision each of them is at.
    /// </summary>
    /// <remarks>
    /// A producer's revision folds its own probe together with every producer it declares a dependency
    /// on, so bumping an input retires exactly its consumers. Declaring an extra dependency needs no
    /// slot arithmetic and cannot silently collide with an existing one.
    /// </remarks>
    public sealed class EzMetricProducerRegistry
    {
        private readonly Dictionary<EzMetricProducerId, EzMetricProducer> producers = new Dictionary<EzMetricProducerId, EzMetricProducer>();

        public EzMetricProducerRegistry(IEnumerable<EzMetricProducer>? producers = null)
        {
            if (producers == null)
                return;

            foreach (var producer in producers)
                Register(producer);
        }

        public void Register(EzMetricProducer producer)
        {
            ArgumentNullException.ThrowIfNull(producer);
            producers[producer.Id] = producer;
        }

        public EzMetricProducer? Get(EzMetricProducerId id)
            => producers.GetValueOrDefault(id);

        public IReadOnlyList<EzMetricProducer> All => producers.Values.ToList();

        /// <summary>
        /// System ids that carry a skill vector on <paramref name="rulesetOnlineId"/> and <paramref name="scope"/>,
        /// with the default radar source for that scope.
        /// </summary>
        /// <remarks>
        /// Scalar tiers are excluded: they are alternate layers of a system that already appears through its
        /// skill vector, and listing them would duplicate the same system under several ids.
        /// </remarks>
        public EzSkillProfile? GetProfile(int rulesetOnlineId)
            => EzSkillProfile.TryBuild(rulesetOnlineId, this);

        /// <summary>Producers that declare skill axes for <paramref name="rulesetOnlineId"/> and <paramref name="scope"/>.</summary>
        public IEnumerable<EzMetricProducer> VectorProducers(int rulesetOnlineId, EzSkillScope scope)
            => producers.Values.Where(p => p.Id.Scope == scope
                                           && p.Tier is EzMetricTier.Skill or EzMetricTier.Dan
                                           && p.AppliesToRuleset(rulesetOnlineId));

        /// <summary>
        /// Every declared axis applicable to <paramref name="rulesetOnlineId"/> and <paramref name="scope"/>,
        /// flattened out of the producer axis groups.
        /// </summary>
        /// <remarks>
        /// The order is fixed by the declaration: tier, then group, then the axis' own order. Consumers that want
        /// a different arrangement sort this list themselves; they must not keep a parallel axis table.
        /// </remarks>
        public IReadOnlyList<EzMetricSkillItem> Items(int rulesetOnlineId, EzSkillScope scope)
        {
            var result = new List<EzMetricSkillItem>();

            foreach (var producer in producers.Values)
            {
                if (producer.Id.Scope != scope || !producer.AppliesToRuleset(rulesetOnlineId))
                    continue;

                foreach (var group in producer.AxisGroups)
                {
                    if (!group.AppliesToRuleset(rulesetOnlineId))
                        continue;

                    foreach (var axis in group.Axes)
                        result.Add(new EzMetricSkillItem(producer.Id, axis, group.Id, producer.Tier, producer.Unit, producer.EffectiveSystemId));
                }
            }

            result.Sort(static (a, b) =>
            {
                int tier = a.Tier.CompareTo(b.Tier);

                if (tier != 0)
                    return tier;

                int group = string.CompareOrdinal(a.GroupId, b.GroupId);

                if (group != 0)
                    return group;

                int order = a.Axis.Order.CompareTo(b.Axis.Order);

                if (order != 0)
                    return order;

                return string.CompareOrdinal(a.Axis.Id, b.Axis.Id);
            });

            return result;
        }

        public IEnumerable<EzMetricProducer> For(int rulesetOnlineId, EzMetricTier tier, EzSkillScope scope)
            => producers.Values.Where(p => p.Tier == tier && p.Id.Scope == scope && p.AppliesToRuleset(rulesetOnlineId));

        /// <summary>
        /// Current revision of <paramref name="id"/> on <paramref name="rulesetOnlineId"/>, folding every
        /// dependency it declares. Returns 0 when the producer is unknown or does not apply.
        /// </summary>
        public int RevisionOf(EzMetricProducerId id, int rulesetOnlineId)
        {
            var visiting = new HashSet<EzMetricProducerId>();
            return revisionOf(id, rulesetOnlineId, visiting);
        }

        /// <summary>Human-readable breakdown of a composed revision, for logs and diagnostics.</summary>
        public string DescribeRevision(EzMetricProducerId id, int rulesetOnlineId)
        {
            var builder = new StringBuilder();
            describe(id, rulesetOnlineId, builder);
            return builder.ToString();
        }

        private int revisionOf(EzMetricProducerId id, int rulesetOnlineId, HashSet<EzMetricProducerId> visiting)
        {
            if (!producers.TryGetValue(id, out var producer))
                return 0;

            if (!producer.AppliesToRuleset(rulesetOnlineId))
                return 0;

            // A declared cycle would otherwise recurse forever. It is a wiring bug, so fail loudly
            // rather than return a plausible-looking revision.
            if (!visiting.Add(id))
                throw new InvalidOperationException($"Metric producer dependency cycle at {id}.");

            try
            {
                int probe = producer.RevisionProbe(rulesetOnlineId);

                // A producer that reads no other producer's rows keeps its own probe as the revision, so
                // declaring it here does not change the number already stamped on its stored rows.
                if (producer.DependsOn.Count == 0)
                    return probe;

                int value = fold((uint)probe);

                // Dependencies are folded in a fixed order so the same graph always yields the same number.
                foreach (var dependency in producer.DependsOn.OrderBy(static d => d))
                    value = fold((uint)value, (uint)revisionOf(dependency, rulesetOnlineId, visiting));

                return value;
            }
            finally
            {
                visiting.Remove(id);
            }
        }

        private void describe(EzMetricProducerId id, int rulesetOnlineId, StringBuilder builder)
        {
            if (!producers.TryGetValue(id, out var producer) || !producer.AppliesToRuleset(rulesetOnlineId))
            {
                builder.Append(id).Append("=<unknown>");
                return;
            }

            builder.Append(id.Id).Append('/').Append(id.Scope).Append("=v").Append(producer.RevisionProbe(rulesetOnlineId));

            foreach (var dependency in producer.DependsOn.OrderBy(static d => d))
            {
                builder.Append(" + ");
                describe(dependency, rulesetOnlineId, builder);
            }
        }

        private const uint fnv_offset_basis = 2166136261;
        private const uint fnv_prime = 16777619;

        private static int fold(uint value) => fold(fnv_offset_basis, value);

        /// <summary>32-bit FNV-1a fold of one slot into an accumulator, masked to a positive int.</summary>
        private static int fold(uint hash, uint slot)
        {
            hash = (hash ^ (slot & 0xFF)) * fnv_prime;
            hash = (hash ^ ((slot >> 8) & 0xFF)) * fnv_prime;
            hash = (hash ^ ((slot >> 16) & 0xFF)) * fnv_prime;
            hash = (hash ^ ((slot >> 24) & 0xFF)) * fnv_prime;
            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
