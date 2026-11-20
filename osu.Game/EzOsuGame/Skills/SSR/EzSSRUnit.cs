// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills.SkillTag.KeyPattern;

namespace osu.Game.EzOsuGame.Skills.SSR
{
    /// <summary>
    /// Runs the SSR system for one chart: gate, one conversion, engine, store.
    /// </summary>
    /// <remarks>
    /// The rating math lives in <see cref="EzSSREngine"/>. This type rates through
    /// <see cref="EzAnalysisComputation.ComputeSsr"/>, the same entry the live pass uses, then writes.
    /// </remarks>
    public sealed class EzSSRUnit
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzChartSkillStore skillStore;

        public EzSSRUnit(BeatmapManager beatmapManager, EzChartSkillStore skillStore)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
        }

        /// <summary>
        /// Revision stamped on the SSR rows. Defined by <see cref="EzSSREngine"/>, which owns the algorithm.
        /// </summary>
        public const int VERSION = EzSSREngine.VERSION;

        /// <summary>
        /// Whether charts of <paramref name="rulesetOnlineId"/> can be rated by this system.
        /// </summary>
        /// <remarks>
        /// The rating reads a mania column layout, so only mania declares it. A ruleset that converts another
        /// format into mania rates the converted chart where the conversion happens, not from its own id.
        /// </remarks>
        public static bool SupportsRuleset(int rulesetOnlineId) => rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID;

        /// <summary>Revision the stored SSR values are stamped with.</summary>
        public int Revision => VERSION;

        /// <summary>
        /// Binding scope of the single conversion a rating pass owns.
        /// </summary>
        /// <remarks>
        /// The baseline pp binds judgements onto the chart, so the shared entry cannot be used: it is read-only by
        /// contract. A scope of its own keeps that one conversion for the star and for the pp, and keeps it away from
        /// the JudgeMode-scoped simulation instances. Distinct from
        /// <see cref="Scoring.EzScoreRacePlayableResolver.SESSION_READY_SCOPE"/> (1000) and the Osu simulation band
        /// (2000+); the Mania simulation band is JudgeMode + 1.
        /// </remarks>
        private const int binding_scope = 3000;

        /// <summary>
        /// Rates one chart and writes its star and pp. Returns false when the chart could not be loaded, so a later
        /// pass retries it.
        /// </summary>
        public bool ComputeAndStore(BeatmapInfo beatmapInfo, CancellationToken cancellationToken = default)
        {
            EzSunnyMaintenanceResult? result = TryCompute(beatmapInfo, cancellationToken);

            if (result == null)
                return false;

            if (!result.RequiresStore)
                return true;

            ApplyStore(beatmapInfo.ID, result);
            return true;
        }

        /// <summary>CPU-only half of Sunny maintenance for parallel passes.</summary>
        public EzSunnyMaintenanceResult? TryCompute(BeatmapInfo beatmapInfo, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (!SupportsRuleset(beatmapInfo.Ruleset.OnlineID))
                return EzSunnyMaintenanceResult.NoStoreRequired;

            IBeatmap playable;

            try
            {
                playable = EzPlayableBeatmapCache.GetBound(beatmapManager.GetWorkingBeatmap(beatmapInfo), beatmapInfo.Ruleset, null,
                    binding_scope, static _ => { }, cancellationToken == CancellationToken.None ? null : cancellationToken);
            }
            catch (Exception e) when (e is not ObjectDisposedException)
            {
                return null;
            }

            int keyCount = EzHoldMetrics.ResolveKeyCount(playable);

            if (!EzSSREngine.IsPatternSupported(keyCount))
                return EzSunnyMaintenanceResult.NoStoreRequired;

            var ssr = EzAnalysisComputation.ComputeSsr(playable, beatmapInfo.Ruleset.OnlineID, 1, cancellationToken);
            double star;
            IReadOnlyDictionary<string, double>? ssrSkillAxes = null;

            if (playable.HitObjects.Count == 0)
            {
                star = 0;
                ssrSkillAxes = buildEmptySsrSkillAxes();
            }
            else
            {
                if (ssr.SunnySR is not double resolvedSunny || !double.IsFinite(resolvedSunny) || resolvedSunny < 0)
                    return null;

                star = resolvedSunny;
                ssrSkillAxes = ssr.PatternSnapshot != null ? EzSunnySrSkillValues.FromSnapshot(ssr.PatternSnapshot) : null;
            }

            if (!double.IsFinite(star) || star < 0)
                return null;

            var official = EzAnalysisComputation.ComputeOfficialBaseline(beatmapManager, beatmapInfo, star, playable, cancellationToken);
            double? sunnyPp = official.SubstitutedPp >= 0 ? official.SubstitutedPp : null;
            double? officialStar = beatmapInfo.StarRating < 0 && official.Star >= 0 ? official.Star : null;
            double? officialPp = beatmapInfo.PerformancePoints < 0 && official.Pp >= 0 ? official.Pp : null;

            IReadOnlyDictionary<string, double>? keyPatternSkills = string.IsNullOrEmpty(beatmapInfo.Hash)
                || ssr.Chart is not { } chart
                || ssr.KeyPatternRadar is not { } relative
                    ? null
                    : EzKeyPatternUnit.BuildSkillsForPersistence(chart, relative, star);

            return new EzSunnyMaintenanceResult(
                RequiresStore: true,
                Star: star,
                SunnyPp: sunnyPp,
                OfficialStar: officialStar,
                OfficialPp: officialPp,
                BeatmapHash: beatmapInfo.Hash,
                SsrSkillAxes: ssrSkillAxes,
                KeyPatternSkillAxes: keyPatternSkills);
        }

        public void ApplyStore(Guid beatmapId, EzSunnyMaintenanceResult result) => skillStore.ApplySunnyMaintenanceResult(beatmapId, result);

        private static IReadOnlyDictionary<string, double> buildEmptySsrSkillAxes()
        {
            var values = new Dictionary<string, double>(EzSunnySrSkillAxisExtensions.ChartAxisIds.Count, StringComparer.Ordinal);

            foreach (string axisId in EzSunnySrSkillAxisExtensions.ChartAxisIds)
                values[axisId] = 0;

            return values;
        }

        /// <summary>
        /// Writes Sunny SR4 chart skill axes for a chart that already has a stored Sunny star (B 欠账路径：不重算星).
        /// </summary>
        public bool BackfillChartSkillAxes(BeatmapInfo beatmapInfo, CancellationToken cancellationToken = default)
        {
            EzSunnyAxesMaintenanceResult? result = TryBackfillSunnyAxes(beatmapInfo, cancellationToken);

            if (result == null)
                return false;

            if (!result.RequiresStore)
                return true;

            skillStore.ApplySunnyAxesMaintenanceResult(result);
            return true;
        }

        /// <summary>CPU-only half of Sunny SR4 / KeyPattern axis backfill.</summary>
        public EzSunnyAxesMaintenanceResult? TryBackfillSunnyAxes(BeatmapInfo beatmapInfo, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (!SupportsRuleset(beatmapInfo.Ruleset.OnlineID))
                return EzSunnyAxesMaintenanceResult.NoStoreRequired;

            if (beatmapInfo.EzRatingData.SunnySR < 0)
                return EzSunnyAxesMaintenanceResult.NoStoreRequired;

            IBeatmap playable;

            try
            {
                playable = EzPlayableBeatmapCache.GetBound(beatmapManager.GetWorkingBeatmap(beatmapInfo), beatmapInfo.Ruleset, null,
                    binding_scope, static _ => { }, cancellationToken == CancellationToken.None ? null : cancellationToken);
            }
            catch (Exception e) when (e is not ObjectDisposedException)
            {
                return null;
            }

            int keyCount = EzHoldMetrics.ResolveKeyCount(playable);

            if (!EzSSREngine.IsPatternSupported(keyCount))
                return EzSunnyAxesMaintenanceResult.NoStoreRequired;

            var ssr = EzAnalysisComputation.ComputeSsr(playable, beatmapInfo.Ruleset.OnlineID, 1, cancellationToken);
            IReadOnlyDictionary<string, double>? ssrAxes;
            IReadOnlyDictionary<string, double>? keyPatternAxes = ssr.Chart is { } chart && ssr.KeyPatternRadar is { } relative
                ? EzKeyPatternUnit.BuildSkillsForPersistence(chart, relative, beatmapInfo.EzRatingData.SunnySR)
                : null;

            if (playable.HitObjects.Count == 0)
            {
                ssrAxes = buildEmptySsrSkillAxes();
            }
            else
            {
                ssrAxes = ssr.PatternSnapshot != null ? EzSunnySrSkillValues.FromSnapshot(ssr.PatternSnapshot) : null;
            }

            if (string.IsNullOrEmpty(beatmapInfo.Hash))
                return EzSunnyAxesMaintenanceResult.NoStoreRequired;

            if (ssrAxes == null && keyPatternAxes == null)
                return EzSunnyAxesMaintenanceResult.NoStoreRequired;

            return new EzSunnyAxesMaintenanceResult(
                RequiresStore: true,
                BeatmapId: beatmapInfo.ID,
                BeatmapHash: beatmapInfo.Hash,
                SsrSkillAxes: ssrAxes,
                KeyPatternSkillAxes: keyPatternAxes);
        }
    }
}
