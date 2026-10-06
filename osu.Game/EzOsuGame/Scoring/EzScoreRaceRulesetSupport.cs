// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Scoring
{
    public enum EzScoreRaceGhostTimelineMode
    {
        None,
        OsuSession,
        ManiaSession,
        TaikoSession,
        CatchSession,
    }

    /// <summary>
    /// 角逐 HUD 规则集能力：Mania / Osu / Taiko / Catch 支持 ghost 时间线。
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
                    return EzScoreRaceGhostTimelineMode.OsuSession;

                case 1:
                    return EzScoreRaceGhostTimelineMode.TaikoSession;

                case 2:
                    return EzScoreRaceGhostTimelineMode.CatchSession;

                default:
                    return EzScoreRaceGhostTimelineMode.None;
            }
        }
    }
}
