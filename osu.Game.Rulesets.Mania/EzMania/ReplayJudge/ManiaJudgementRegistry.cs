// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Replicas;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    public static class ManiaJudgementRegistry
    {
        public static IManiaHitModeJudgement? GetHitModeJudgement(EzEnumHitMode hitMode)
            => hitMode switch
            {
                EzEnumHitMode.IIDX_HD or EzEnumHitMode.LR2_HD or EzEnumHitMode.Raja_NM => BmsHitModeJudgement.Instance,
                EzEnumHitMode.O2Jam => O2HitModeJudgement.Instance,
                EzEnumHitMode.EZ2AC => Ez2AcHitModeJudgement.Instance,
                EzEnumHitMode.Malody_E or EzEnumHitMode.Malody_B => MalodyHitModeJudgement.Instance,
                _ => null,
            };

        public static IManiaNoteJudgementStrategy GetNoteStrategy(IGameplayEnvironment environment)
        {
            var hitMode = GetHitModeJudgement(environment.ManiaHitMode);
            if (hitMode != null)
                return hitMode;

            return LazerNoteJudgementReplica.Instance;
        }

        public static IManiaHoldJudgementStrategy GetHoldStrategy(IGameplayEnvironment environment)
        {
            var hitMode = GetHitModeJudgement(environment.ManiaHitMode);
            if (hitMode != null)
                return hitMode;

            return CommonHoldJudgementStrategy.Instance;
        }

        public static bool IsEzHitMode(EzEnumHitMode hitMode)
            => hitMode is not (EzEnumHitMode.Lazer or EzEnumHitMode.Classic);
    }
}
