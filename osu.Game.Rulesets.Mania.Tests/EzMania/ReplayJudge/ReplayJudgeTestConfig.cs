// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    internal static class ReplayJudgeTestConfig
    {
        public static void ApplyToGlobalConfig(GameplayEnvironment environment)
        {
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaHitMode, environment.ManiaHitMode);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaHealthMode, environment.ManiaHealthMode);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.JudgePrecedence, environment.JudgePrecedence);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.OffsetPlusMania, environment.OffsetPlusMania);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.BmsPoorHitResultEnable, environment.BmsPoorHitResultEnable);
        }

        public static GameplayEnvironment ApplyAndSnapshot(GameplayEnvironment environment)
        {
            ApplyToGlobalConfig(environment);
            return ManiaRuleset.ResolveEnvironment(null, ReplayRunPurpose.ForLive);
        }

        public static void ApplyEmbeddedModes(Score score, GameplayEnvironment environment)
        {
            score.ScoreInfo.ManiaHitMode = (int)environment.ManiaHitMode;
            score.ScoreInfo.ManiaHealthMode = (int)environment.ManiaHealthMode;
        }

        public static GameplayEnvironment Create(
            EzEnumHitMode hitMode,
            EzEnumHealthMode healthMode,
            EzEnumJudgePrecedence judgePrecedence = EzEnumJudgePrecedence.Earliest,
            double offsetPlusMania = 0,
            bool bmsPoorHitResultEnable = false)
            => new GameplayEnvironment
            {
                ManiaHitMode = hitMode,
                ManiaHealthMode = healthMode,
                JudgePrecedence = judgePrecedence,
                OffsetPlusMania = offsetPlusMania,
                BmsPoorHitResultEnable = bmsPoorHitResultEnable,
            };
    }
}
