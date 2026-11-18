// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.EzOsuGame.Skills.SkillTag.KeyPattern;
using osu.Game.EzOsuGame.Skills.SSR.SR4Pattern;

namespace osu.Game.EzOsuGame.Skills.SSR
{
    /// <summary>
    /// Runs the SSR system for one chart: gate, one conversion, engine, store.
    /// </summary>
    /// <remarks>
    /// The rating math lives in <see cref="EzSSREngine"/>, in this assembly alongside the other systems. This type
    /// answers which charts it rates and performs the write, so a ruleset never has to announce its own capability.
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

            var chart = EzCSIEngine.FromPlayable(playable, cancellationToken);
            double star;
            IReadOnlyDictionary<string, double>? ssrSkillAxes = null;

            if (playable.HitObjects.Count == 0)
            {
                star = 0;
                ssrSkillAxes = buildEmptySsrSkillAxes();
            }
            else
            {
                var combined = EzSSREngine.CalculateSRWithPatternSnapshot(chart, keyCount, playable.Difficulty.OverallDifficulty);
                star = combined.Sr;
                ssrSkillAxes = EzSunnySrSkillValues.FromSnapshot(combined.PatternSnapshot);
            }

            if (!double.IsFinite(star) || star < 0)
                return null;

            var official = EzAnalysisComputation.ComputeOfficialBaseline(beatmapManager, beatmapInfo, star, playable, cancellationToken);
            double? sunnyPp = official.SubstitutedPp >= 0 ? official.SubstitutedPp : null;
            double? officialStar = beatmapInfo.StarRating < 0 && official.Star >= 0 ? official.Star : null;
            double? officialPp = beatmapInfo.PerformancePoints < 0 && official.Pp >= 0 ? official.Pp : null;

            IReadOnlyDictionary<string, double>? keyPatternSkills = string.IsNullOrEmpty(beatmapInfo.Hash)
                ? null
                : EzKeyPatternUnit.BuildSkillsForPersistence(chart, star, cancellationToken);

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
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (!SupportsRuleset(beatmapInfo.Ruleset.OnlineID))
                return true;

            if (beatmapInfo.EzRatingData.SunnySR < 0)
                return true;

            IBeatmap playable;

            try
            {
                playable = EzPlayableBeatmapCache.GetBound(beatmapManager.GetWorkingBeatmap(beatmapInfo), beatmapInfo.Ruleset, null,
                    binding_scope, static _ => { }, cancellationToken == CancellationToken.None ? null : cancellationToken);
            }
            catch (Exception e) when (e is not ObjectDisposedException)
            {
                return false;
            }

            int keyCount = EzHoldMetrics.ResolveKeyCount(playable);

            if (!EzSSREngine.IsPatternSupported(keyCount))
                return true;

            var chart = EzCSIEngine.FromPlayable(playable, cancellationToken);

            if (playable.HitObjects.Count == 0)
            {
                storeEmptySunnySrSkillAxes(beatmapInfo);
                EzKeyPatternUnit.Write(skillStore, beatmapInfo, chart, beatmapInfo.EzRatingData.SunnySR, cancellationToken);
                return true;
            }

            var combined = EzSSREngine.CalculateSRWithPatternSnapshot(chart, keyCount, playable.Difficulty.OverallDifficulty);
            storeSunnySrSkillAxes(beatmapInfo, combined.PatternSnapshot);
            EzKeyPatternUnit.Write(skillStore, beatmapInfo, chart, beatmapInfo.EzRatingData.SunnySR, cancellationToken);
            return true;
        }

        private void storeEmptySunnySrSkillAxes(BeatmapInfo beatmapInfo)
        {
            if (string.IsNullOrEmpty(beatmapInfo.Hash))
                return;

            var values = new Dictionary<string, double>(EzSunnySrSkillAxisExtensions.ChartAxisIds.Count, StringComparer.Ordinal);

            foreach (string axisId in EzSunnySrSkillAxisExtensions.ChartAxisIds)
                values[axisId] = 0;

            skillStore.WriteBeatmapSystemSkills(
                beatmapInfo.Hash,
                EzAnalysisSystem.SSR,
                values,
                beatmapInfo.ID,
                algorithmVersion: VERSION,
                rulesetOnlineId: EzLocalProfileConstants.MANIA_RULESET_ID);
        }

        private void storeSunnySrSkillAxes(BeatmapInfo beatmapInfo, EzManiaSR4PatternSnapshot snapshot)
        {
            if (string.IsNullOrEmpty(beatmapInfo.Hash))
                return;

            skillStore.WriteBeatmapSystemSkills(
                beatmapInfo.Hash,
                EzAnalysisSystem.SSR,
                EzSunnySrSkillValues.FromSnapshot(snapshot),
                beatmapInfo.ID,
                algorithmVersion: VERSION,
                rulesetOnlineId: EzLocalProfileConstants.MANIA_RULESET_ID);
        }
    }
}
