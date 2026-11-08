// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Skills
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
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            int rulesetId = beatmapInfo.Ruleset.OnlineID;

            if (!EzPPPSkillCatalog.SupportsRuleset(rulesetId))
                return null;

            if (!beatmapInfo.Ruleset.Available)
                beatmapInfo.Ruleset.Available = true;

            try
            {
                var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);

                if (!working.BeatmapInfo.Ruleset.Available)
                    working.BeatmapInfo.Ruleset.Available = true;

                IReadOnlyList<Mod> modList = mods ?? Array.Empty<Mod>();
                // One conversion per chart: the shared cache is the same conversion this would have run, and CSi /
                // Mina already converted the same inputs, so re-converting here doubled the load cost per chart.
                var playable = EzPlayableBeatmapCache.GetShared(working, beatmapInfo.Ruleset, modList);
                var skills = engine.CalculateChart(playable, modList);

                skillStore.WriteBeatmapSystemSkills(
                    beatmapInfo.Hash,
                    EzAnalysisSystem.PPP,
                    skills,
                    beatmapInfo.ID,
                    rulesetOnlineId: rulesetId);

                return skills;
            }
            catch (Exception e) when (e is not ObjectDisposedException)
            {
                Logger.Log($"[EzSkills] PPP chart compute failed for {beatmapInfo}: {e.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                return null;
            }
        }
    }
}
