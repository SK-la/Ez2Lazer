// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// One selectable readout: the pair of star / performance producers shown together, plus the skill
    /// producers that feed the radar for the same system.
    /// </summary>
    /// <remarks>
    /// A variant exists so the same consumer can render any of the systems without knowing which ones
    /// exist. Adding a system means adding a row to <see cref="EzMetricVariantCatalog"/>; no consumer
    /// changes. <see cref="AxisFamilyId"/> is what makes the chart and player spokes comparable, so a
    /// variant that pairs two different families must not be declared.
    /// </remarks>
    public sealed class EzMetricVariant
    {
        public EzMetricVariant(
            string id,
            LocalisableString name,
            string axisFamilyId,
            EzMetricProducerId starProducer,
            EzMetricProducerId? performanceProducer,
            EzMetricProducerId? chartSkillProducer = null,
            EzMetricProducerId? playerSkillProducer = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentException.ThrowIfNullOrWhiteSpace(axisFamilyId);

            Id = id;
            Name = name;
            AxisFamilyId = axisFamilyId;
            StarProducer = starProducer;
            PerformanceProducer = performanceProducer;
            ChartSkillProducer = chartSkillProducer;
            PlayerSkillProducer = playerSkillProducer;
        }

        public string Id { get; }

        public LocalisableString Name { get; }

        public string AxisFamilyId { get; }

        public EzMetricProducerId StarProducer { get; }

        public EzMetricProducerId? PerformanceProducer { get; }

        public EzMetricProducerId? ChartSkillProducer { get; }

        public EzMetricProducerId? PlayerSkillProducer { get; }

        public override string ToString() => Id;
    }
}
