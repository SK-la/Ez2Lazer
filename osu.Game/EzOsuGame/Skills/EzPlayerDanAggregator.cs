// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// MVP player dan: MSD Overall/family → chart rawDan heuristic, then credit clears and average.
    /// Not comparable to mania-hub LeoBlack verdicts; estimates are provisional.
    /// </summary>
    public sealed class EzPlayerDanAggregator
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzSkillStore skillStore;
        private readonly EzBeatmapMsdComputer msdComputer;

        public EzPlayerDanAggregator(BeatmapManager beatmapManager, EzSkillStore skillStore, EzBeatmapMsdComputer msdComputer)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
            this.msdComputer = msdComputer;
        }

        public void ComputeAndStore(string username, IEnumerable<ScoreInfo> scores)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            var clearsByBucket = new Dictionary<(int KeyCount, string Side), List<double>>();

            foreach (var score in scores)
            {
                if (score.Ruleset.OnlineID != 3)
                    continue;

                if (score.Accuracy <= 0 || !double.IsFinite(score.Accuracy))
                    continue;

                var beatmapInfo = score.BeatmapInfo ?? beatmapManager.QueryBeatmap(b => b.Hash == score.BeatmapHash);
                if (beatmapInfo == null)
                    continue;

                // Converts excluded from dan (same spirit as keymode PP).
                if (beatmapInfo.Ruleset.OnlineID != 3)
                    continue;

                var msd = msdComputer.TryGetOrCompute(beatmapInfo);
                if (msd == null || msd.Count == 0)
                    continue;

                if (!msd.TryGetValue(EzSkillIds.Msd(EzSkillIds.OVERALL), out double overall) || overall <= 0)
                    continue;

                string family = EzDanLabels.DominantFamily(msd);
                double chartDan = EzDanLabels.SrToRawDan(overall, family);

                var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                var playable = working.GetPlayableBeatmap(score.Ruleset, score.Mods);
                int keyCount = EzMinaNoteConverter.ResolveKeyCount(playable);
                if (keyCount <= 0)
                    continue;

                double holdRatio = computeLnRatio(playable);
                string side = holdRatio >= EzDanAlgorithm.LnPrimaryMinRatioFor(keyCount)
                    ? DanSkillSystem.SIDE_LN
                    : DanSkillSystem.SIDE_RC;

                double? credited = EzDanCredit.CreditedDanFor(chartDan, score.Accuracy, side, keyCount);
                if (credited is not double value)
                    continue;

                var key = (keyCount, side);
                if (!clearsByBucket.TryGetValue(key, out var list))
                    clearsByBucket[key] = list = new List<double>();

                list.Add(value);
            }

            DateTimeOffset at = DateTimeOffset.UtcNow;

            foreach (((int keyCount, string side), List<double> clears) in clearsByBucket)
            {
                if (clears.Count < EzDanAlgorithm.CLEAR_QUORUM)
                    continue;

                var window = clears
                             .OrderByDescending(v => v)
                             .Take(EzDanAlgorithm.CLEAR_WINDOW)
                             .ToList();

                double rawDan = window.Average();
                string label = EzDanLabels.LabelFor(rawDan, side, keyCount);
                double? ceiling = EzDanLabels.CeilingFor(side, keyCount);

                skillStore.WriteDanEstimate(new EzDanEstimate
                {
                    Username = username,
                    KeyCount = keyCount,
                    Side = side,
                    RawDan = rawDan,
                    Label = label,
                    Clears = clears.Count,
                    BeyondTable = ceiling is double c && rawDan >= c,
                    ClearWindowHave = window.Count,
                    ClearWindowNeed = EzDanAlgorithm.CLEAR_WINDOW,
                    AlgorithmVersion = EzDanAlgorithm.VERSION,
                    ComputedAt = at,
                });
            }
        }

        private static double computeLnRatio(IBeatmap playable)
        {
            int total = 0;
            int holds = 0;

            foreach (HitObject obj in playable.HitObjects)
            {
                if (obj is not IHasColumn)
                    continue;

                total++;

                if (obj is IHasDuration duration && duration.Duration > 0)
                    holds++;
            }

            return total <= 0 ? 0 : (double)holds / total;
        }
    }
}
