// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>One named dimension of a skill vector.</summary>
    public sealed record EzMetricAxis(string Id, LocalisableString DisplayName, string AccentHex, bool InRadar, int Order);

    /// <summary>
    /// A set of producers that must speak the same axis vocabulary. Membership carries no version edge:
    /// it only asserts that their axes are comparable, which is what makes a two-colour radar possible.
    /// </summary>
    public sealed class EzMetricAxisFamily
    {
        public EzMetricAxisFamily(string id, IReadOnlyList<EzMetricAxis> axes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentNullException.ThrowIfNull(axes);

            Id = id;
            Axes = axes;
        }

        public string Id { get; }

        public IReadOnlyList<EzMetricAxis> Axes { get; }
    }

    /// <summary>
    /// One cohesive subset of a producer's axes. This is the unit a write replaces and the unit
    /// completeness is judged by, so two writers can contribute different subsets without clearing
    /// each other's rows.
    /// </summary>
    public sealed class EzMetricAxisGroup
    {
        public EzMetricAxisGroup(
            string id,
            IReadOnlyList<EzMetricAxis> axes,
            Func<int, bool>? appliesToSlice = null,
            Func<int, bool>? appliesToRuleset = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentNullException.ThrowIfNull(axes);

            Id = id;
            Axes = axes;
            AppliesToSlice = appliesToSlice ?? (static _ => true);
            AppliesToRuleset = appliesToRuleset ?? (static _ => true);
        }

        public string Id { get; }

        /// <summary>Axes this group declares. Empty when the producer stores a scalar under this group.</summary>
        public IReadOnlyList<EzMetricAxis> Axes { get; }

        /// <summary>Which slices the group applies to. A group that does not apply is not required to have rows.</summary>
        public Func<int, bool> AppliesToSlice { get; }

        /// <summary>
        /// Which rulesets the group applies to. This is what lets one producer declare different axis
        /// sets per ruleset without splitting into several producers.
        /// </summary>
        public Func<int, bool> AppliesToRuleset { get; }

        public bool Applies(int sliceKey, int rulesetOnlineId)
            => AppliesToSlice(sliceKey) && AppliesToRuleset(rulesetOnlineId);

        /// <summary>
        /// Whether <paramref name="stored"/> holds every axis this group declares for the given producer.
        /// </summary>
        public bool IsSatisfiedBy(EzMetricProducer producer, IReadOnlyDictionary<string, double> stored)
        {
            ArgumentNullException.ThrowIfNull(producer);
            ArgumentNullException.ThrowIfNull(stored);

            foreach (var axis in Axes)
            {
                if (!stored.ContainsKey(EzMetricSkillId.For(producer, axis.Id)))
                    return false;
            }

            return true;
        }
    }

    /// <summary>Builds the stored skill id of one axis of one producer.</summary>
    public static class EzMetricSkillId
    {
        public static string For(EzMetricProducer producer, string axisId)
        {
            ArgumentNullException.ThrowIfNull(producer);
            ArgumentException.ThrowIfNullOrWhiteSpace(axisId);
            return $"{producer.EffectiveSystemId}.{axisId}";
        }

        public static string For(EzMetricProducerId producer, string axisId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(axisId);
            return $"{producer.Id}.{axisId}";
        }

        /// <summary>
        /// Splits a skill id into its producer name and axis. The scope is not recoverable from the id;
        /// the caller knows which table it read.
        /// </summary>
        public static bool TrySplit(string? skillId, out string producerId, out string axisId)
        {
            producerId = string.Empty;
            axisId = string.Empty;

            if (string.IsNullOrEmpty(skillId))
                return false;

            int dot = skillId.LastIndexOf('.');

            if (dot <= 0 || dot >= skillId.Length - 1)
                return false;

            producerId = skillId[..dot];
            axisId = skillId[(dot + 1)..];
            return true;
        }
    }
}
