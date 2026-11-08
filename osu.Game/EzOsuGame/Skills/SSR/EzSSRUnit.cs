// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills.SkillTag;

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
        private const int BINDING_SCOPE = 3000;

        /// <summary>
        /// Rates one chart and writes its star and pp. Returns false when the chart could not be loaded, so a later
        /// pass retries it.
        /// </summary>
        public bool ComputeAndStore(BeatmapInfo beatmapInfo, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (!SupportsRuleset(beatmapInfo.Ruleset.OnlineID))
                return true;

            IBeatmap playable;

            try
            {
                // A default token stays null (no bound of its own) so the conversion keeps WorkingBeatmap's own
                // bounded wait, exactly as the shared lookup did.
                playable = EzPlayableBeatmapCache.GetBound(beatmapManager.GetWorkingBeatmap(beatmapInfo), beatmapInfo.Ruleset, null,
                    BINDING_SCOPE, static _ => { }, cancellationToken == CancellationToken.None ? null : cancellationToken);
            }
            catch (Exception e) when (e is not ObjectDisposedException)
            {
                return false;
            }

            int keyCount = EzHoldMetrics.ResolveKeyCount(playable);

            if (!EzSSREngine.IsPatternSupported(keyCount))
                return true;

            double star = playable.HitObjects.Count == 0
                ? 0
                : EzSSREngine.CalculateSR(EzCSIEngine.FromPlayable(playable, cancellationToken), keyCount, playable.Difficulty.OverallDifficulty);

            if (!double.IsFinite(star) || star < 0)
                return false;

            // Scored at the rating this call just measured, on the conversion it just used. Reading the stored SunnySR
            // instead scored the previous pass's value - absent on a first rating - which is how the Sunny pp column
            // ended up needing a second full-library pass that converted every chart again.
            double pp = EzAnalysisComputation.ComputeBaselinePerformancePointsForRating(beatmapManager, beatmapInfo, star, playable, cancellationToken);
            skillStore.WriteRating(beatmapInfo.ID, EzAnalysisSystem.SSR, star, pp >= 0 ? pp : null);

            return true;
        }
    }
}
