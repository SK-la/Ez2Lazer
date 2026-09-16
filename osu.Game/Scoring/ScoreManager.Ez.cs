// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring.Legacy;

namespace osu.Game.Scoring
{
    public partial class ScoreManager
    {
        /// <summary>
        /// Ez2Lazer: 用户显式「成绩重算」— 将 Session 产出写回 Realm 与调用方快照。
        /// Gameplay 入库不经此方法；StatisticsJson 须同步以便 list / hover 重载时读到重算结果。
        /// </summary>
        public void ApplyEzSessionRecalculation(ScoreInfo scoreInfo, ScoreInfo sessionInfo, ReplayRunPurpose purpose, GameplayEnvironment environment)
        {
            double? accuracy = null;
            ScoreRank? rank = null;
            int? maxCombo = null;
            long? totalScore = null;
            Dictionary<HitResult, int>? statistics = null;
            Dictionary<HitResult, int>? maximumStatistics = null;
            List<HitEvent>? hitEvents = null;
            int? maniaHitMode = null;
            int? maniaHealthMode = null;

            Realm.Write(realm =>
            {
                var managed = realm.Find<ScoreInfo>(scoreInfo.ID);
                if (managed == null)
                    return;

                ApplyEzSessionRecalculationToDetachedScoreInfo(managed, sessionInfo, purpose, environment);

                accuracy = managed.Accuracy;
                rank = managed.Rank;
                maxCombo = managed.MaxCombo;
                totalScore = managed.TotalScore;
                statistics = managed.Statistics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                maximumStatistics = managed.MaximumStatistics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                hitEvents = managed.HitEvents.ToList();
                maniaHitMode = managed.ManiaHitMode;
                maniaHealthMode = managed.ManiaHealthMode;
            });

            if (accuracy == null)
                return;

            scoreInfo.Accuracy = accuracy.Value;
            scoreInfo.Rank = rank!.Value;
            scoreInfo.MaxCombo = maxCombo!.Value;
            scoreInfo.TotalScore = totalScore!.Value;
            scoreInfo.TotalScoreWithoutMods = sessionInfo.TotalScoreWithoutMods;
            scoreInfo.TotalScoreVersion = LegacyScoreEncoder.LATEST_VERSION;
            scoreInfo.HitEvents = hitEvents!;

            scoreInfo.Statistics.Clear();
            foreach (var kvp in statistics!)
                scoreInfo.Statistics[kvp.Key] = kvp.Value;

            scoreInfo.MaximumStatistics.Clear();
            foreach (var kvp in maximumStatistics!)
                scoreInfo.MaximumStatistics[kvp.Key] = kvp.Value;

            scoreInfo.ManiaHitMode = maniaHitMode!.Value;
            scoreInfo.ManiaHealthMode = maniaHealthMode!.Value;

            if (scoreInfo.OnlineID > 0 || scoreInfo.LegacyOnlineID > 0)
            {
                scoreInfo.OnlineID = -1;
                scoreInfo.LegacyOnlineID = -1;
            }
        }

        public static void ApplyEzSessionRecalculationToDetachedScoreInfo(ScoreInfo scoreInfo, ScoreInfo sessionInfo, ReplayRunPurpose purpose, GameplayEnvironment environment)
        {
            var statistics = sessionInfo.Statistics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            scoreInfo.Statistics = statistics;
            scoreInfo.StatisticsJson = JsonConvert.SerializeObject(statistics);

            var maximumStatistics = sessionInfo.MaximumStatistics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            scoreInfo.MaximumStatistics = maximumStatistics;
            scoreInfo.MaximumStatisticsJson = JsonConvert.SerializeObject(maximumStatistics);

            scoreInfo.HitEvents = sessionInfo.HitEvents.ToList();
            scoreInfo.Accuracy = sessionInfo.Accuracy;
            scoreInfo.Rank = sessionInfo.Rank;
            scoreInfo.MaxCombo = sessionInfo.MaxCombo;
            scoreInfo.TotalScore = sessionInfo.TotalScore;
            scoreInfo.TotalScoreWithoutMods = sessionInfo.TotalScoreWithoutMods;
            scoreInfo.TotalScoreVersion = LegacyScoreEncoder.LATEST_VERSION;

            if (purpose == ReplayRunPurpose.ForLive)
            {
                scoreInfo.ManiaHitMode = (int)environment.ManiaHitMode;
                scoreInfo.ManiaHealthMode = (int)environment.ManiaHealthMode;
            }

            if (scoreInfo.OnlineID > 0 || scoreInfo.LegacyOnlineID > 0)
            {
                scoreInfo.OnlineID = -1;
                scoreInfo.LegacyOnlineID = -1;
            }
        }
    }
}
