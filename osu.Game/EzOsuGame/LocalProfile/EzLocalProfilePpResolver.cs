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
    /// Shared PP / star-rating resolution for career aggregation and drill score snapshots.
    /// </summary>
    public sealed class EzLocalProfilePpResolver
    {
        private readonly BeatmapManager beatmapManager;

        public EzLocalProfilePpResolver(BeatmapManager beatmapManager)
        {
            this.beatmapManager = beatmapManager;
        }

        /// <summary>
        /// Shared difficulty-attribute cache for a single analysis pass (keyed by beatmap+ruleset+mods).
        /// </summary>
        public Dictionary<string, DifficultyAttributes?> CreateAttributeCache()
            => new Dictionary<string, DifficultyAttributes?>(StringComparer.Ordinal);

        public readonly record struct ResolvedDifficulty(double Pp, double StarRating);

        /// <summary>
        /// Resolve PP and star rating for one score, reusing <paramref name="attributeCache"/>.
        /// </summary>
        public ResolvedDifficulty Resolve(
            ScoreInfo score,
            Dictionary<string, DifficultyAttributes?> attributeCache,
            ref int failures,
            ref int loggedFailures,
            int maxLoggedFailures = 8)
        {
            double starRating = resolveStarRating(score, attributeCache);
            double pp = resolvePp(score, attributeCache, ref failures, ref loggedFailures, maxLoggedFailures);
            return new ResolvedDifficulty(pp, starRating);
        }

        public double ResolvePp(ScoreInfo score)
        {
            int failures = 0;
            int loggedFailures = 0;
            return resolvePp(score, CreateAttributeCache(), ref failures, ref loggedFailures, maxLoggedFailures: 0);
        }

        private double resolveStarRating(ScoreInfo score, Dictionary<string, DifficultyAttributes?> attributeCache)
        {
            if (score.BeatmapInfo == null)
                return -1;

            try
            {
                string cacheKey = buildAttributeCacheKey(score);

                if (!attributeCache.TryGetValue(cacheKey, out var attributes))
                {
                    var ruleset = score.Ruleset.CreateInstance();
                    attributes = tryCalculateAttributes(score, ruleset);
                    attributeCache[cacheKey] = attributes;
                }

                if (attributes != null)
                    return attributes.StarRating;

                return score.BeatmapInfo.StarRating;
            }
            catch
            {
                return score.BeatmapInfo.StarRating;
            }
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
