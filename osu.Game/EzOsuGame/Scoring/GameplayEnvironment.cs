// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    public sealed record GameplayEnvironment : IGameplayEnvironment
    {
        public EzEnumHitMode ManiaHitMode { get; init; }

        public EzEnumHealthMode ManiaHealthMode { get; init; }

        public EzEnumJudgePrecedence JudgePrecedence { get; init; }

        public double OffsetPlusMania { get; init; }

        public bool BmsPoorHitResultEnable { get; init; }

        public static GameplayEnvironment FromLive(Ez2ConfigManager config) => new GameplayEnvironment
        {
            ManiaHitMode = config.Get<EzEnumHitMode>(Ez2Setting.ManiaHitMode),
            ManiaHealthMode = config.Get<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode),
            JudgePrecedence = config.Get<EzEnumJudgePrecedence>(Ez2Setting.JudgePrecedence),
            OffsetPlusMania = config.Get<double>(Ez2Setting.OffsetPlusMania),
            BmsPoorHitResultEnable = config.Get<bool>(Ez2Setting.BmsPoorHitResultEnable),
        };

        public static GameplayEnvironment FromScore(ScoreInfo score, Ez2ConfigManager configFallback)
        {
            var live = FromLive(configFallback);

            if (!score.TryGetManiaGameplayModes(out int hitMode, out int healthMode))
                return live;

            return new GameplayEnvironment
            {
                ManiaHitMode = (EzEnumHitMode)hitMode,
                ManiaHealthMode = (EzEnumHealthMode)healthMode,
                JudgePrecedence = live.JudgePrecedence,
                OffsetPlusMania = live.OffsetPlusMania,
                BmsPoorHitResultEnable = live.BmsPoorHitResultEnable,
            };
        }
    }
}
