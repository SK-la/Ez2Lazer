// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Aggregates per-play osu PP portions into player skills (Etterna AggregateSSRs per axis).
    /// </summary>
    public sealed class EzPlayerOsuPerfAggregator
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzSkillStore skillStore;

        public EzPlayerOsuPerfAggregator(BeatmapManager beatmapManager, EzSkillStore skillStore)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
        }

        public void ComputeAndStore(
            string username,
            IEnumerable<EzSkillPlayRow> plays,
            Func<Guid, ScoreInfo?>? resolveScore,
            CancellationToken cancellationToken = default,
            Action? afterEachScore = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);

            var byAxis = new Dictionary<EzOsuSkillAxis, List<double>>();

            foreach (var axis in EzOsuSkillAxisExtensions.PlayerAxes)
                byAxis[axis] = new List<double>();

            int analyzed = 0;

            foreach (var play in plays)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var score = resolveScore?.Invoke(play.ScoreId);
                afterEachScore?.Invoke();

                if (score == null)
                    continue;

                if (score.Ruleset.OnlineID != EzLocalProfileConstants.OSU_RULESET_ID)
                    continue;

                try
                {
                    var ruleset = score.Ruleset.CreateInstance();
                    var calculator = ruleset.CreatePerformanceCalculator();
                    if (calculator == null)
                        continue;

                    var working = beatmapManager.GetWorkingBeatmap(score.BeatmapInfo);
                    var diffAttrs = ruleset.CreateDifficultyCalculator(working).Calculate(score.Mods);
                    var perf = calculator.Calculate(score, diffAttrs);
                    var portions = EzOsuSkillMapping.FromPerformanceAttributes(perf);

                    foreach (var axis in EzOsuSkillAxisExtensions.PlayerAxes)
                    {
                        double value = portions.GetValueOrDefault(axis.ToPerfSkillId(), 0);
                        if (double.IsFinite(value) && value > 0)
                            byAxis[axis].Add(value);
                    }

                    analyzed++;
                }
                catch (Exception e)
                {
                    Logger.Log($"[EzSkills] osu perf failed for score {play.ScoreId}: {e.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Verbose);
                }
            }

            var aggregated = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var axis in EzOsuSkillAxisExtensions.PlayerAxes)
                aggregated[axis.ToPerfSkillId()] = EzSsrAggregator.Aggregate(byAxis[axis]);

            skillStore.WritePlayerSystemSkills(
                username,
                EzSkillSystems.OSU_SLICE_KEY,
                EzSkillSystems.PLAYER_OSU_PERF,
                aggregated,
                analyzed);
        }
    }
}
