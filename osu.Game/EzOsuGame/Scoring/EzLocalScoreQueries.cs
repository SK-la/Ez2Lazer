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
        /// 不影响游玩/判定的 Mod 类型白名单：角逐/HUD 取 ghost 时忽略这些 Mod，
        /// 以免 PureScore / Classic / TouchDevice 等造成「相似 Mod」过滤过严，
        /// 导致 ghost 候选池缩小到只剩与当前完全一致的成绩。
        /// </summary>
        public static readonly HashSet<Type> COSMETIC_GHOST_MOD_TYPES = new HashSet<Type>
        {
            typeof(ModClassic),
            typeof(ModScoreV2),
            typeof(ModAccuracyChallenge),
            typeof(ModDifficultyAdjust),
            typeof(ModFailCondition),
            typeof(ModMirror),
            typeof(ModNoFail),
            typeof(ModPerfect),
        };

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

            // SameAsCurrent: 忽略白名单 Mod，只比较影响游玩的 Mod 是否一致。
            return scores.Where(s => GameplayModsMatch(s.Mods, currentMods));
        }

        public static bool ModsMatch(Mod[] left, Mod[] right) => modsMatch(left, right);

        /// <summary>
        /// 比较两侧 Mod 时忽略 <see cref="COSMETIC_GHOST_MOD_TYPES"/> 白名单项。
        /// 使非游玩 Mod 不影响 ghost 候选过滤与 beatmap 复用判定。
        /// </summary>
        public static bool GameplayModsMatch(Mod[] left, Mod[] right)
        {
            static bool isCosmetic(Mod m) => COSMETIC_GHOST_MOD_TYPES.Any(t => t.IsInstanceOfType(m));

            static IEnumerable<string> gameplayAcronyms(Mod[] mods) =>
                mods.Where(m => !isCosmetic(m))
                    .OrderBy(m => m.Acronym)
                    .Select(m => m.Acronym);

            return gameplayAcronyms(left).SequenceEqual(gameplayAcronyms(right));
        }

        public static ScoreInfo? PickBest(IEnumerable<ScoreInfo> scores, EzScoreRaceMetric metric)
        {
            if (metric == EzScoreRaceMetric.TheoreticalMaxScore)
                return null;

            var ordered = EzScoreRaceMetricOrdering.ApplyMetricOrdering(
                scores,
                metric,
                s => s.TotalScore,
                s => s.Accuracy,
                s => s.MaxCombo,
                GetMissCount);

            if (metric == EzScoreRaceMetric.TotalScore)
                return ordered.ThenByDescending(s => s.Date).FirstOrDefault();

            return ordered.FirstOrDefault();
        }

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
