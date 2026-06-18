// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Scoring
{
    public enum EzScoreRaceGhostTimelineMode
    {
        None,
        HitEvents,
        ManiaSession,
    }

    /// <summary>
    /// 角逐 HUD 规则集能力：Mania + Osu 支持 ghost 时间线；其余规则集仅当前局/理论柱，不加载 ghost。
    /// </summary>
    public static class EzScoreRaceRulesetSupport
    {
        public static bool SupportsGhostRace(RulesetInfo? ruleset)
            => GetGhostTimelineMode(ruleset) != EzScoreRaceGhostTimelineMode.None;

        public static EzScoreRaceGhostTimelineMode GetGhostTimelineMode(RulesetInfo? ruleset)
        {
            if (ruleset == null)
                return EzScoreRaceGhostTimelineMode.None;

            switch (ruleset.OnlineID)
            {
                case 3:
                    return EzScoreRaceGhostTimelineMode.ManiaSession;

                case 0:
                    return EzScoreRaceGhostTimelineMode.HitEvents;

                default:
                    return EzScoreRaceGhostTimelineMode.None;
            }
        }
    }
}
