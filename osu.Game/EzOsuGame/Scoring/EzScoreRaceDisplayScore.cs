// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Leaderboards;

namespace osu.Game.EzOsuGame.Scoring
{
    internal static class EzScoreRaceDisplayScore
    {
        public static long ForLeaderboardScore(GameplayLeaderboardScore leaderboardScore, ScoreInfo scoreInfo, ScoringMode mode)
            => forLiveTotal(scoreInfo, leaderboardScore.TotalScore.Value, mode);

        private static long forLiveTotal(ScoreInfo scoreInfo, long liveTotalScore, ScoringMode mode)
        {
            if (mode == ScoringMode.Standardised)
                return liveTotalScore;

            int maxBasicJudgements = scoreInfo.MaximumStatistics
                                     .Where(k => k.Key.IsBasic())
                                     .Select(k => k.Value)
                                     .DefaultIfEmpty(0)
                                     .Sum();

            return convertStandardisedToClassic(scoreInfo.Ruleset.OnlineID, liveTotalScore, maxBasicJudgements);
        }

        // Mirrors ScoreInfoExtensions private conversion; kept here to avoid touching non-Ez files.
        private static long convertStandardisedToClassic(int rulesetId, long standardisedTotalScore, int objectCount)
        {
            switch (rulesetId)
            {
                case 0:
                    return (long)Math.Round((Math.Pow(objectCount, 2) * 32.57 + 100000) * standardisedTotalScore / ScoreProcessor.MAX_SCORE);

                case 1:
                    return (long)Math.Round((objectCount * 1109 + 100000) * standardisedTotalScore / ScoreProcessor.MAX_SCORE);

                case 2:
                    return (long)Math.Round(Math.Pow(standardisedTotalScore / ScoreProcessor.MAX_SCORE * objectCount, 2) * 21.62 + standardisedTotalScore / 10d);

                case 3:
                default:
                    return standardisedTotalScore;
            }
        }
    }
}
