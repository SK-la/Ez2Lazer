// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Skills.Ppp;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Computes NoMod PPP chart skills and writes one Realm row per skill id, each with its own algorithm version.
    /// </summary>
    public sealed class EzBeatmapPpPlusComputer
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzSkillStore skillStore;
        private readonly IEzPpPlusEngine engine;

        public EzBeatmapPpPlusComputer(BeatmapManager beatmapManager, EzSkillStore skillStore, IEzPpPlusEngine? engine = null)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
            this.engine = engine ?? new EzPpPlusEngine();
        }

        public IReadOnlyDictionary<string, double>? TryGetOrCompute(BeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (!EzPppSkillCatalog.SupportsRuleset(beatmapInfo.Ruleset.OnlineID))
                return null;

            var existing = skillStore.GetBeatmapSkillsAtCurrentVersions(beatmapInfo.Hash, EzSkillSystems.BEATMAP_PPPLUS);

            if (EzPppSkillCatalog.IsCompleteChart(beatmapInfo.Ruleset.OnlineID, existing))
                return existing;

            return ComputeAndStore(beatmapInfo);
        }

        public IReadOnlyDictionary<string, double>? ComputeAndStore(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods = null)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            int rulesetId = beatmapInfo.Ruleset.OnlineID;

            if (!EzPppSkillCatalog.SupportsRuleset(rulesetId))
                return null;

            if (!beatmapInfo.Ruleset.Available)
                beatmapInfo.Ruleset.Available = true;

            try
            {
                var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);

                if (!working.BeatmapInfo.Ruleset.Available)
                    working.BeatmapInfo.Ruleset.Available = true;

                IReadOnlyList<Mod> modList = mods ?? Array.Empty<Mod>();
                var playable = working.GetPlayableBeatmap(beatmapInfo.Ruleset, modList);
                var skills = engine.CalculateChart(playable, modList);

                skillStore.WriteBeatmapSystemSkills(
                    beatmapInfo.Hash,
                    EzSkillSystems.BEATMAP_PPPLUS,
                    skills,
                    beatmapInfo.ID,
                    skillVersions: EzPppSkillVersions.For(skills.Keys));

                return skills;
            }
            catch (Exception e)
            {
                Logger.Log($"[EzSkills] PPP chart compute failed for {beatmapInfo}: {e.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                return null;
            }
        }
    }
}
