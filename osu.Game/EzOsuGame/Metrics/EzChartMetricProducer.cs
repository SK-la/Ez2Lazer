// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Skills;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// Chart-side metric producers: everything measured from the chart itself.
    /// </summary>
    /// <remarks>
    /// Player producers name these members to declare the stored rows they read; the reverse edge does not exist.
    /// Ids are derived from the member names, so renaming a member is the only way to rename a producer.
    /// </remarks>
    public enum EzChartMetricProducer
    {
        [EzMetricProducerMeta(EzMetricTier.Star, EzMetricUnit.Star,
            Family = EzMetricFamily.Official, Revision = EzMetricRevisionSource.UpstreamOfficialDifficulty)]
        Official,

        [EzMetricProducerMeta(EzMetricTier.Performance, EzMetricUnit.PerformancePoints,
            Family = EzMetricFamily.Official, ChartDependsOn = new[] { Official })]
        OfficialPerformance,

        [EzMetricProducerMeta(EzMetricTier.Star, EzMetricUnit.Star,
            Family = EzMetricFamily.Sunny, Rulesets = EzMetricRulesetScope.ManiaOnly,
            Revision = EzMetricRevisionSource.UpstreamSsr)]
        Ssr,

        [EzMetricProducerMeta(EzMetricTier.Performance, EzMetricUnit.PerformancePoints,
            Family = EzMetricFamily.Sunny, Rulesets = EzMetricRulesetScope.ManiaOnly,
            ChartDependsOn = new[] { Ssr })]
        SsrPerformance,

        [EzMetricProducerMeta(EzMetricTier.Skill, EzMetricUnit.SkillPoint,
            SystemId = EzSkillSystems.BEATMAP_PPP, Family = EzMetricFamily.Ppp,
            Rulesets = EzMetricRulesetScope.PppSupported, Axes = EzMetricAxisSource.PppCatalog,
            Revision = EzMetricRevisionSource.PppBundle, DefaultRadar = true, RadarPriority = 0)]
        Ppp,

        [EzMetricProducerMeta(EzMetricTier.Performance, EzMetricUnit.PerformancePoints,
            Family = EzMetricFamily.Ppp, Rulesets = EzMetricRulesetScope.PppSupported,
            ChartDependsOn = new[] { Ppp })]
        PppPerformance,

        [EzMetricProducerMeta(EzMetricTier.Skill, EzMetricUnit.SkillPoint,
            SystemId = EzSkillSystems.BEATMAP_MINA, Family = EzMetricFamily.Mina, Sliced = true,
            Rulesets = EzMetricRulesetScope.ManiaOnly, Axes = EzMetricAxisSource.MinaEngine,
            Revision = EzMetricRevisionSource.MinaSkill, DefaultRadar = true, RadarPriority = 10)]
        Mina,

        [EzMetricProducerMeta(EzMetricTier.Tags, EzMetricUnit.Count,
            SystemId = EzSkillSystems.BEATMAP_CSI, Rulesets = EzMetricRulesetScope.ManiaOnly,
            Axes = EzMetricAxisSource.ChartStructure, Revision = EzMetricRevisionSource.ChartSkillInfo)]
        Csi,

        [EzMetricProducerMeta(EzMetricTier.Dan, EzMetricUnit.DanRank,
            Rulesets = EzMetricRulesetScope.ManiaOnly, Revision = EzMetricRevisionSource.DanAlgorithm,
            ChartDependsOn = new[] { Mina, Csi, Ssr })]
        Dan,
    }
}
