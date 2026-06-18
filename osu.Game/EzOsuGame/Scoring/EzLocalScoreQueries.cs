// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    public static class EzLocalScoreQueries
    {
        /// <summary>
        /// 返回当前谱面规则集下的本地成绩（含无 <c>.osr</c> 元数据但磁盘有 replay 的项）。
        /// 能否构建时间线由 EzScoreTimelineBuilder.TryBuild（GetScore().Replay 门槛）过滤。
        /// </summary>
        public static List<ScoreInfo> GetLocalScoresWithReplay(RealmAccess realm, BeatmapInfo beatmap, RulesetInfo ruleset)
        {
            ArgumentNullException.ThrowIfNull(beatmap);
            ArgumentNullException.ThrowIfNull(ruleset);

            return realm.Run(r =>
            {
                var liveBeatmap = r.Find<BeatmapInfo>(beatmap.ID);

                if (liveBeatmap == null)
                    return new List<ScoreInfo>();

                // 与选歌界面一致：走 BeatmapInfo.Scores 关联，避免 BeatmapHash 字段不一致时漏成绩。
                return liveBeatmap.Scores
                                  .AsEnumerable()
                                  .Where(s => s.Ruleset.ShortName == ruleset.ShortName && !s.DeletePending)
                                  .Select(s => s.Detach())
                                  .ToList();
            });
        }

        public static IEnumerable<ScoreInfo> FilterByMods(IEnumerable<ScoreInfo> scores, Mod[] currentMods, EzScoreModFilter filter)
        {
            if (filter == EzScoreModFilter.Any)
                return scores;

            return scores.Where(s => ModsMatch(s.Mods, currentMods));
        }

        public static bool ModsMatch(Mod[] left, Mod[] right) => modsMatch(left, right);

        public static ScoreInfo? PickBest(IEnumerable<ScoreInfo> scores, EzScorePickCriterion criterion, ScoreInfo? exclude = null)
        {
            var candidates = scores.Where(s => exclude == null || s.ID != exclude.ID);

            return criterion switch
            {
                EzScorePickCriterion.TotalScore => candidates.OrderByDescending(s => s.TotalScore).ThenByDescending(s => s.Date).FirstOrDefault(),
                EzScorePickCriterion.Accuracy => candidates.OrderByDescending(s => s.Accuracy).ThenByDescending(s => s.TotalScore).FirstOrDefault(),
                EzScorePickCriterion.MaxCombo => candidates.OrderByDescending(s => s.MaxCombo).ThenByDescending(s => s.TotalScore).FirstOrDefault(),
                EzScorePickCriterion.MissCount => candidates.OrderBy(GetMissCount).ThenByDescending(s => s.TotalScore).FirstOrDefault(),
                _ => throw new ArgumentOutOfRangeException(nameof(criterion), criterion, null),
            };
        }

        public static ScoreInfo? PickBest(IEnumerable<ScoreInfo> scores, EzScoreCompareCondition condition, ScoreInfo? exclude = null)
        {
            if (condition == EzScoreCompareCondition.TheoreticalMaxScore)
                return null;

            return PickBest(scores, toPickCriterion(condition), exclude);
        }

        private static EzScorePickCriterion toPickCriterion(EzScoreCompareCondition condition) => condition switch
        {
            EzScoreCompareCondition.BestTotalScore => EzScorePickCriterion.TotalScore,
            EzScoreCompareCondition.BestAccuracy => EzScorePickCriterion.Accuracy,
            EzScoreCompareCondition.BestMaxCombo => EzScorePickCriterion.MaxCombo,
            EzScoreCompareCondition.BestMissCount => EzScorePickCriterion.MissCount,
            _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, null),
        };

        public static List<ScoreInfo> GetTopByTotalScore(IEnumerable<ScoreInfo> scores, int take)
        {
            return scores.OrderByDescending(s => s.TotalScore)
                         .ThenByDescending(s => s.Date)
                         .Take(take)
                         .ToList();
        }

        public static int GetMissCount(ScoreInfo score)
        {
            return score.Statistics.Where(kv => kv.Key.IsMiss()).Sum(kv => kv.Value);
        }

        private static bool modsMatch(Mod[] left, Mod[] right)
        {
            static IEnumerable<string> acronyms(Mod[] mods) => mods.OrderBy(m => m.Acronym).Select(m => m.Acronym);
            return acronyms(left).SequenceEqual(acronyms(right));
        }
    }
}
