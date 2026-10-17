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
        public static IManiaJudgeModeJudgement? GetJudgeModeJudgement(EzEnumJudgeMode judgeMode)
        {
            if (StableClassicJudgeModeJudgement.IsSelected(judgeMode))
                return StableClassicJudgeModeJudgement.Instance;

            return judgeMode switch
            {
                EzEnumJudgeMode.IIDX_HD or EzEnumJudgeMode.LR2_HD or EzEnumJudgeMode.Raja_NM => BmsJudgeModeJudgement.Instance,
                EzEnumJudgeMode.O2Jam => O2JudgeModeJudgement.Instance,
                EzEnumJudgeMode.EZ2AC => Ez2AcJudgeModeJudgement.Instance,
                EzEnumJudgeMode.Malody_E or EzEnumJudgeMode.Malody_B => MalodyJudgeModeJudgement.Instance,
                _ => null,
            };
        }

        public static IManiaNoteJudgementStrategy GetNoteStrategy(IGameplayEnvironment environment)
        {
            var judgeMode = GetJudgeModeJudgement(environment.JudgeMode);
            if (judgeMode != null)
                return judgeMode;

            return LazerNoteJudgementReplica.Instance;
        }

        public static IManiaHoldJudgementStrategy GetHoldStrategy(IGameplayEnvironment environment)
        {
            var judgeMode = GetJudgeModeJudgement(environment.JudgeMode);
            if (judgeMode != null)
                return judgeMode;

            return CommonHoldJudgementStrategy.Instance;
        }

        /// <summary>
        /// 是否走 Ez 原生判定派发（BMS / O2 / EZ2AC / Malody）。
        /// StableClassic 与 Classic 只借 Ez 的窗口与计分挂钩，单键语义仍是 lazer，故不在此列。
        /// </summary>
        public static bool IsEzJudgeMode(EzEnumJudgeMode judgeMode)
            => judgeMode is not (EzEnumJudgeMode.Lazer or EzEnumJudgeMode.StableClassic);
    }
}
