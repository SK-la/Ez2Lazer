// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Computes NoMod osu!standard difficulty skill axes and writes them as independent Realm skills.
    /// </summary>
    public sealed class EzBeatmapOsuDiffComputer
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzSkillStore skillStore;

        public EzBeatmapOsuDiffComputer(BeatmapManager beatmapManager, EzSkillStore skillStore)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
        }

        public IReadOnlyDictionary<string, double>? TryGetOrCompute(BeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != EzLocalProfileConstants.OSU_RULESET_ID)
                return null;

            var existing = skillStore.GetBeatmapSkills(beatmapInfo.Hash, EzSkillSystems.BEATMAP_OSU_DIFF);

            if (EzOsuSkillMapping.IsCurrentDiffCache(existing))
                return existing;

            return ComputeAndStore(beatmapInfo);
        }

        public IReadOnlyDictionary<string, double>? ComputeAndStore(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods = null)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != EzLocalProfileConstants.OSU_RULESET_ID)
                return null;

            if (!beatmapInfo.Ruleset.Available)
                beatmapInfo.Ruleset.Available = true;

            try
            {
                var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);

                if (!working.BeatmapInfo.Ruleset.Available)
                    working.BeatmapInfo.Ruleset.Available = true;

                var ruleset = beatmapInfo.Ruleset.CreateInstance();
                var calculator = ruleset.CreateDifficultyCalculator(working);
                var attributes = calculator.Calculate(mods ?? Array.Empty<Mod>());
                var skills = EzOsuSkillMapping.FromDifficultyAttributes(attributes);

                skillStore.WriteBeatmapSystemSkills(
                    beatmapInfo.Hash,
                    EzSkillSystems.BEATMAP_OSU_DIFF,
                    skills,
                    beatmapInfo.ID);

                return skills;
            }
            catch (Exception e)
            {
                Logger.Log($"[EzSkills] osu diff compute failed for {beatmapInfo}: {e.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                return null;
            }
        }
    }
}
