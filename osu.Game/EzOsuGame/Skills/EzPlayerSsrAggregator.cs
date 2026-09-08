// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Aggregates per-play SSR vectors into independent player skills (Etterna AggregateSSRs).
    /// Accuracy→goal is a simplified mapping for foundation; Wife estimate can be refined later.
    /// </summary>
    public sealed class EzPlayerSsrAggregator
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzSkillStore skillStore;

        public EzPlayerSsrAggregator(BeatmapManager beatmapManager, EzSkillStore skillStore)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
        }

        public void ComputeAndStore(string username, IEnumerable<ScoreInfo> scores)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            var byKey = new Dictionary<int, List<EzSkillsetVector>>();

            using var calc = new EzMinaCalcFacade();

            foreach (var score in scores)
            {
                if (score.Ruleset.OnlineID != 3)
                    continue;

                if (string.IsNullOrWhiteSpace(score.BeatmapHash))
                    continue;

                var beatmapInfo = score.BeatmapInfo ?? beatmapManager.QueryBeatmap(b => b.Hash == score.BeatmapHash);
                if (beatmapInfo == null)
                    continue;

                var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                var playable = working.GetPlayableBeatmap(score.Ruleset, score.Mods);
                int keyCount = EzMinaNoteConverter.ResolveKeyCount(playable);
                float rate = resolveRate(score.Mods);
                float goal = accuracyToGoal(score.Accuracy);

                if (goal <= 0.8f)
                    continue;

                var notes = EzMinaNoteConverter.Convert(playable);
                if (notes.Length == 0)
                    continue;

                var vector = calc.CalculateSsr(notes, rate, goal);
                if (vector.Overall <= 0)
                    continue;

                if (!byKey.TryGetValue(keyCount, out var list))
                    byKey[keyCount] = list = new List<EzSkillsetVector>();

                list.Add(vector);
            }

            foreach ((int keyCount, List<EzSkillsetVector> plays) in byKey)
            {
                var aggregated = EzSsrAggregator.AggregateVectors(plays);
                bool provisional = plays.Count < EzPlayerSsrSnapshot.QUALIFYING_PLAYS;
                skillStore.WritePlayerSsr(username, keyCount, aggregated, plays.Count, provisional);
            }
        }

        private static float resolveRate(IEnumerable<Mod> mods)
        {
            double rate = 1;

            foreach (var mod in mods)
            {
                if (mod is ModRateAdjust rateAdjust)
                    rate *= rateAdjust.SpeedChange.Value;
            }

            return (float)Math.Clamp(rate, 0.5, 2.0);
        }

        /// <summary>
        /// Maps display accuracy to MinaCalc SSR goal. Foundation: clamp accuracy into [0.8, 0.9975].
        /// </summary>
        public static float AccuracyToGoal(double accuracy) => accuracyToGoal(accuracy);

        private static float accuracyToGoal(double accuracy)
        {
            if (!double.IsFinite(accuracy) || accuracy <= 0)
                return 0.8f;

            return (float)Math.Clamp(accuracy, 0.8, 0.9975);
        }
    }
}
