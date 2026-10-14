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
        public static IManiaHitModeJudgement? GetHitModeJudgement(EzEnumJudgeMode judgeMode)
            => judgeMode switch
            {
                EzEnumJudgeMode.IIDX_HD or EzEnumJudgeMode.LR2_HD or EzEnumJudgeMode.Raja_NM => BmsHitModeJudgement.Instance,
                EzEnumJudgeMode.O2Jam => O2HitModeJudgement.Instance,
                EzEnumJudgeMode.EZ2AC => Ez2AcHitModeJudgement.Instance,
                EzEnumJudgeMode.Malody_E or EzEnumJudgeMode.Malody_B => MalodyHitModeJudgement.Instance,
                _ => null,
            };

        public static IManiaNoteJudgementStrategy GetNoteStrategy(IGameplayEnvironment environment)
        {
            var hitMode = GetHitModeJudgement(environment.JudgeMode);
            if (hitMode != null)
                return hitMode;

            return LazerNoteJudgementReplica.Instance;
        }

        public static IManiaHoldJudgementStrategy GetHoldStrategy(IGameplayEnvironment environment)
        {
            var hitMode = GetHitModeJudgement(environment.JudgeMode);
            if (hitMode != null)
                return hitMode;

            return CommonHoldJudgementStrategy.Instance;
        }

        public static bool IsEzHitMode(EzEnumJudgeMode judgeMode)
            => judgeMode is not (EzEnumJudgeMode.Lazer or EzEnumJudgeMode.Classic);
    }
}
