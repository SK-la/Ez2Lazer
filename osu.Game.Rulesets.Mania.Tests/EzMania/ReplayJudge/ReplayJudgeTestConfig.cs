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
        }

        public static GameplayEnvironment ApplyAndSnapshot(GameplayEnvironment environment)
        {
            ApplyToGlobalConfig(environment);
            return GameplayEnvironment.FromLive(GlobalConfigStore.EzConfig);
        }

        public static void ApplyEmbeddedModes(Score score, GameplayEnvironment environment)
        {
            score.ScoreInfo.ManiaHitMode = (int)environment.ManiaHitMode;
            score.ScoreInfo.ManiaHealthMode = (int)environment.ManiaHealthMode;
        }
    }
}
