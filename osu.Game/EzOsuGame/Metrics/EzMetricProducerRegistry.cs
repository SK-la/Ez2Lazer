// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Skills;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// One side's producer table: chart or player. It answers what exists and what axes exist; version numbers come
    /// from <see cref="EzMetricRevisionResolver"/>, because a player producer's revision folds chart rows in.
    /// </summary>
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
        /// The skill-vector wiring of this side on <paramref name="rulesetOnlineId"/>, with its default radar source.
        /// </summary>
        /// <remarks>
        /// Scalar tiers are excluded: they are alternate layers of a system that already appears through its
        /// skill vector, and listing them would duplicate the same system under several ids.
        /// </remarks>
        public EzSkillProfile? GetProfile(int rulesetOnlineId)
            => EzSkillProfile.TryBuild(rulesetOnlineId, this);

        /// <summary>Producers that declare skill axes on <paramref name="rulesetOnlineId"/>.</summary>
        public IEnumerable<EzMetricProducer> VectorProducers(int rulesetOnlineId)
            => producers.Values.Where(p => p.Tier is EzMetricTier.Skill or EzMetricTier.Dan && p.AppliesToRuleset(rulesetOnlineId));

        /// <summary>
        /// Every declared axis applicable to <paramref name="rulesetOnlineId"/>, flattened out of the producer axis
        /// groups.
        /// </summary>
        /// <remarks>
        /// The order is fixed by the declaration: tier, then group, then the axis' own order. Consumers that want
        /// a different arrangement sort this list themselves; they must not keep a parallel axis table.
        /// </remarks>
        public IReadOnlyList<EzMetricSkillItem> Items(int rulesetOnlineId)
        {
            var result = new List<EzMetricSkillItem>();

            foreach (var producer in producers.Values)
            {
                if (!producer.AppliesToRuleset(rulesetOnlineId))
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

        public IEnumerable<EzMetricProducer> For(int rulesetOnlineId, EzMetricTier tier)
            => producers.Values.Where(p => p.Tier == tier && p.AppliesToRuleset(rulesetOnlineId));
    }
}
