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
    /// Aggregates per-play PP+-shaped vectors into player skills (Etterna AggregateSSRs per axis).
    /// </summary>
    public sealed class EzPlayerPpPlusAggregator
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzSkillStore skillStore;
        private readonly IEzPpPlusEngine engine;

        public EzPlayerPpPlusAggregator(BeatmapManager beatmapManager, EzSkillStore skillStore, IEzPpPlusEngine? engine = null)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
            this.engine = engine ?? new EzPpPlusStubEngine();
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
                    var working = beatmapManager.GetWorkingBeatmap(score.BeatmapInfo);
                    var diffAttrs = ruleset.CreateDifficultyCalculator(working).Calculate(score.Mods);
                    double lengthSeconds = Math.Max(0, (score.BeatmapInfo?.Length ?? 0) / 1000.0);
                    var difficulty = score.BeatmapInfo?.Difficulty ?? working.BeatmapInfo.Difficulty;
                    var chart = engine.CalculateChart(diffAttrs, difficulty, lengthSeconds);
                    var portions = engine.CalculatePlay(chart, score).ToPlayerSkills();

                    foreach (var axis in EzOsuSkillAxisExtensions.PlayerAxes)
                    {
                        double value = portions.GetValueOrDefault(axis.ToPlayerSkillId(), 0);
                        if (double.IsFinite(value) && value > 0)
                            byAxis[axis].Add(value);
                    }

                    analyzed++;
                }
                catch (Exception e)
                {
                    Logger.Log($"[EzSkills] osu PP+ play failed for score {play.ScoreId}: {e.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Verbose);
                }
            }

            var aggregated = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var axis in EzOsuSkillAxisExtensions.PlayerAxes)
                aggregated[axis.ToPlayerSkillId()] = EzSsrAggregator.Aggregate(byAxis[axis]);

            skillStore.WritePlayerSystemSkills(
                username,
                EzSkillSystems.OSU_SLICE_KEY,
                EzSkillSystems.PLAYER_PPPLUS,
                aggregated,
                analyzed);
        }
    }
}
