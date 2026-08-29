// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Shared PP resolution for career aggregation and drill score snapshots.
    /// </summary>
    public sealed class EzLocalProfilePpResolver
    {
        private readonly BeatmapManager beatmapManager;

        public EzLocalProfilePpResolver(BeatmapManager beatmapManager)
        {
            this.beatmapManager = beatmapManager;
        }

        public double[] ResolveAll(
            IReadOnlyList<ScoreInfo> scores,
            IProgress<EzLocalProfileComputeProgress>? progress = null,
            int progressTotal = 0,
            System.Threading.CancellationToken cancellationToken = default)
        {
            double[] values = new double[scores.Count];
            if (scores.Count == 0)
                return values;

            var attributeCache = new Dictionary<string, DifficultyAttributes?>(StringComparer.Ordinal);
            int failures = 0;
            int loggedFailures = 0;
            const int max_logged_failures = 8;
            int reportEvery = Math.Max(10, Math.Max(1, progressTotal) / 100);

            for (int i = 0; i < scores.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                values[i] = resolvePp(scores[i], attributeCache, ref failures, ref loggedFailures, max_logged_failures);

                int current = i + 1;
                if (progress != null && (current == scores.Count || current % reportEvery == 0))
                    progress.Report(new EzLocalProfileComputeProgress(current, progressTotal, Saving: false));
            }

            if (failures > 0)
            {
                Logger.Log(
                    $"[EzLocalProfile] PP calc finished with {failures} failure(s) out of {scores.Count} score(s).",
                    Ez2ConfigManager.LOGGER_NAME);
            }

            return values;
        }

        public double ResolvePp(ScoreInfo score)
        {
            int failures = 0;
            int loggedFailures = 0;
            return resolvePp(score, new Dictionary<string, DifficultyAttributes?>(StringComparer.Ordinal), ref failures, ref loggedFailures, maxLoggedFailures: 0);
        }

        private double resolvePp(
            ScoreInfo score,
            Dictionary<string, DifficultyAttributes?> attributeCache,
            ref int failures,
            ref int loggedFailures,
            int maxLoggedFailures)
        {
            if (score.PP is > 0)
                return score.PP.Value;

            if (score.Rank == ScoreRank.F)
                return 0;

            if (score.BeatmapInfo == null)
                return 0;

            try
            {
                var ruleset = score.Ruleset.CreateInstance();
                var calculator = ruleset.CreatePerformanceCalculator();
                if (calculator == null)
                    return 0;

                string cacheKey = buildAttributeCacheKey(score);

                if (!attributeCache.TryGetValue(cacheKey, out var attributes))
                {
                    attributes = tryCalculateAttributes(score, ruleset);
                    attributeCache[cacheKey] = attributes;
                }

                if (attributes == null)
                    return 0;

                return Math.Max(0, calculator.Calculate(score, attributes).Total);
            }
            catch (Exception ex)
            {
                failures++;

                if (maxLoggedFailures > 0 && ++loggedFailures <= maxLoggedFailures)
                {
                    Logger.Log($"[EzLocalProfile] PP calc failed for score {score.ID}: {ex.Message}", Ez2ConfigManager.LOGGER_NAME);
                }

                return 0;
            }
        }

        private DifficultyAttributes? tryCalculateAttributes(ScoreInfo score, Rulesets.Ruleset ruleset)
        {
            try
            {
                var working = beatmapManager.GetWorkingBeatmap(score.BeatmapInfo);
                return ruleset.CreateDifficultyCalculator(working).Calculate(score.Mods);
            }
            catch (Exception ex)
            {
                Logger.Log(
                    $"[EzLocalProfile] Difficulty attrs failed ({score.BeatmapInfo?.ID}): {ex.Message}",
                    Ez2ConfigManager.LOGGER_NAME,
                    LogLevel.Verbose);
                return null;
            }
        }

        private static string buildAttributeCacheKey(ScoreInfo score) => $"{score.BeatmapInfo?.ID:N}|{score.Ruleset.ShortName}|{score.ModsJson}";
    }
}
