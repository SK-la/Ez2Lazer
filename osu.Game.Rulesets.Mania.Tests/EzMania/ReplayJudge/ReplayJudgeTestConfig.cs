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
            var config = GlobalConfigStore.EzConfig;
            GlobalConfigStore.EzConfig = config;

            config.SetValue(Ez2Setting.ManiaHitMode, environment.HitMode);
            config.SetValue(Ez2Setting.ManiaHealthMode, environment.ManiaHealthMode);
            config.SetValue(Ez2Setting.JudgePrecedence, environment.JudgePrecedence);
            config.SetValue(Ez2Setting.OffsetPlusMania, environment.OffsetPlusMania);
            config.SetValue(Ez2Setting.BmsPoorHitResultEnable, environment.BmsPoorHitResultEnable);
        }

        public static GameplayEnvironment ApplyAndSnapshot(GameplayEnvironment environment)
        {
            ApplyToGlobalConfig(environment);
            return GlobalConfigStore.EzConfig.GetGameplayEnvironment(new ManiaRuleset().RulesetInfo);
        }

        public static void ResetGlobalConfig() => ApplyToGlobalConfig(Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer));

        public static void ApplyEmbeddedModes(Score score, GameplayEnvironment environment)
        {
            score.ScoreInfo.ManiaHitMode = (int)environment.HitMode;
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
                HitMode = hitMode,
                ManiaHealthMode = healthMode,
                JudgePrecedence = judgePrecedence,
                OffsetPlusMania = offsetPlusMania,
                BmsPoorHitResultEnable = bmsPoorHitResultEnable,
            };

        /// <summary>
        /// BMS HitMode + Lazer HealthMode：无 KPoor 机制（§6.4 标定环境）。
        /// </summary>
        public static GameplayEnvironment CreateBmsLazerHealth(
            EzEnumHitMode hitMode,
            EzEnumJudgePrecedence judgePrecedence = EzEnumJudgePrecedence.Earliest,
            bool bmsPoorHitResultEnable = false)
            => Create(hitMode, EzEnumHealthMode.Lazer, judgePrecedence, bmsPoorHitResultEnable: bmsPoorHitResultEnable);
    }
}
