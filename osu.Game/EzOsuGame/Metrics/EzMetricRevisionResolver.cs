// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// Composes each producer's revision, folding the producers it declares a dependency on.
    /// </summary>
    /// <remarks>
    /// Two tables are needed because a player producer reads chart rows: resolving one id has to search both sides.
    /// A chart producer can never reach a player producer, which is why the search order is fixed.
    /// </remarks>
    public sealed class EzMetricRevisionResolver
    {
        private readonly EzMetricProducerRegistry chartRegistry;
        private readonly EzMetricProducerRegistry playerRegistry;

        public EzMetricRevisionResolver(EzMetricProducerRegistry chartRegistry, EzMetricProducerRegistry playerRegistry)
        {
            ArgumentNullException.ThrowIfNull(chartRegistry);
            ArgumentNullException.ThrowIfNull(playerRegistry);

            this.chartRegistry = chartRegistry;
            this.playerRegistry = playerRegistry;
        }

        public EzMetricProducerRegistry ChartRegistry => chartRegistry;

        public EzMetricProducerRegistry PlayerRegistry => playerRegistry;

        /// <summary>
        /// Current revision of <paramref name="id"/> on <paramref name="rulesetOnlineId"/>, folding every dependency
        /// it declares. Returns 0 when the producer is unknown or does not apply.
        /// </summary>
        public int RevisionOf(EzMetricProducerId id, int rulesetOnlineId)
            => revisionOf(id, rulesetOnlineId, new HashSet<EzMetricProducerId>());

        /// <summary>Human-readable breakdown of a composed revision, for logs and diagnostics.</summary>
        public string DescribeRevision(EzMetricProducerId id, int rulesetOnlineId)
        {
            var builder = new StringBuilder();
            describe(id, rulesetOnlineId, builder);
            return builder.ToString();
        }

        /// <summary>First table that knows the id. Chart first: a player id never shadows a chart id.</summary>
        private EzMetricProducer? resolve(EzMetricProducerId id)
            => chartRegistry.Get(id) ?? playerRegistry.Get(id);

        private int revisionOf(EzMetricProducerId id, int rulesetOnlineId, HashSet<EzMetricProducerId> visiting)
        {
            var producer = resolve(id);

            if (producer == null)
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
            var producer = resolve(id);

            if (producer == null || !producer.AppliesToRuleset(rulesetOnlineId))
            {
                builder.Append(id).Append("=<unknown>");
                return;
            }

            builder.Append(id).Append("=v").Append(producer.RevisionProbe(rulesetOnlineId));

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

    /// <summary>
    /// The registry pair in force for this process, plus the resolver over it.
    /// </summary>
    /// <remarks>
    /// Lives in <c>Metrics</c> so the vocabulary layer never has to reach into <c>Analysis</c> to find the producers.
    /// </remarks>
    public static class EzMetricRegistries
    {
        private static EzMetricRevisionResolver current = createDefault();

        public static EzMetricRevisionResolver Current => current;

        public static EzMetricProducerRegistry ChartRegistry => current.ChartRegistry;

        public static EzMetricProducerRegistry PlayerRegistry => current.PlayerRegistry;

        /// <summary>Installs the registries that supply the revisions. Call once during startup, before rows are stamped.</summary>
        public static void Configure(EzMetricRevisionResolver resolver)
        {
            ArgumentNullException.ThrowIfNull(resolver);
            current = resolver;
        }

        private static EzMetricRevisionResolver createDefault()
            => new EzMetricRevisionResolver(EzChartMetricProducers.CreateRegistry(), EzPlayerMetricProducers.CreateRegistry());
    }
}
