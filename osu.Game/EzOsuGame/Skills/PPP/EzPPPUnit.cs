// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Skills.PPP
{
    /// <summary>
    /// Runs the PPP system for one chart: gate, convert, call the engine, hand the vector to the store.
    /// </summary>
    public sealed class EzPPPUnit
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzChartSkillStore skillStore;
        private readonly IEzPPPEngine engine;

        public EzPPPUnit(BeatmapManager beatmapManager, EzChartSkillStore skillStore, IEzPPPEngine? engine = null)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
            this.engine = engine ?? new EzPPPEngine();
        }

        public IReadOnlyDictionary<string, double>? ComputeAndStore(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods = null)
        {
            var section = TryCompute(beatmapInfo, mods);

            if (section is not EzPppMaintenanceSection.Skills skills)
                return null;

            skillStore.ApplyPppSection(beatmapInfo.ID, beatmapInfo.Hash, skills);
            return skills.Values;
        }

        /// <summary>CPU-only half of PPP maintenance.</summary>
        public EzPppMaintenanceSection? TryCompute(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods = null)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            int rulesetId = beatmapInfo.Ruleset.OnlineID;

            if (!EzPPPSkillCatalog.SupportsRuleset(rulesetId))
                return new EzPppMaintenanceSection.Skipped();

            if (!beatmapInfo.Ruleset.Available)
                beatmapInfo.Ruleset.Available = true;

            try
            {
                var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);

                if (!working.BeatmapInfo.Ruleset.Available)
                    working.BeatmapInfo.Ruleset.Available = true;

                IReadOnlyList<Mod> modList = mods ?? Array.Empty<Mod>();
                var playable = EzPlayableBeatmapCache.GetShared(working, beatmapInfo.Ruleset, modList);
                var skills = engine.CalculateChart(playable, modList);

                double? star = skills.TryGetValue(EzPPPSkillCatalog.STAR_RATING, out double starValue) && starValue > 0 ? starValue : null;
                double? pp = null;

                if (star != null)
                {
                    double computed = EzPPPPerformanceCalculators.Calculate(rulesetId, skills, playable, modList, 1);
                    pp = computed > 0 ? computed : null;
                }

                return new EzPppMaintenanceSection.Skills(skills, rulesetId, star, pp);
            }
            catch (Exception e) when (e is not ObjectDisposedException)
            {
                Logger.Log($"[EzSkills] PPP chart compute failed for {beatmapInfo}: {e.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                return new EzPppMaintenanceSection.TransientFailure();
            }
        }
    }
}