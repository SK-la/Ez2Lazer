// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Skills;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// Player-side metric producers: folds over the score archive, written only by the manual score analysis.
    /// </summary>
    /// <remarks>
    /// A player vector is never measured on demand, because it reads the whole archive. Player producers may read
    /// chart rows; chart producers never read player rows.
    /// </remarks>
    public enum EzPlayerMetricProducer
    {
        [EzMetricProducerMeta(EzMetricTier.Skill, EzMetricUnit.SkillPoint,
            SystemId = EzSkillSystems.PLAYER_PPP, Family = EzMetricFamily.Ppp, Sliced = true,
            Rulesets = EzMetricRulesetScope.PppSupported, Axes = EzMetricAxisSource.PppCatalog,
            Revision = EzMetricRevisionSource.PppFacet, DefaultRadar = true, RadarPriority = 0,
            ChartDependsOn = new[] { EzChartMetricProducer.Ppp })]
        Ppp,

        [EzMetricProducerMeta(EzMetricTier.Skill, EzMetricUnit.SkillPoint,
            SystemId = EzSkillSystems.PLAYER_MINA, Family = EzMetricFamily.Mina, Sliced = true,
            Rulesets = EzMetricRulesetScope.ManiaOnly, Axes = EzMetricAxisSource.MinaEngine,
            Revision = EzMetricRevisionSource.MinaSkill, DefaultRadar = true, RadarPriority = 10)]
        Mina,

        [EzMetricProducerMeta(EzMetricTier.Dan, EzMetricUnit.DanRank, Sliced = true,
            Rulesets = EzMetricRulesetScope.ManiaOnly, Revision = EzMetricRevisionSource.DanAlgorithm,
            ChartDependsOn = new[] { EzChartMetricProducer.Dan })]
        Dan,
    }
}
