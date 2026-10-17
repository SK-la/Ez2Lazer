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
    /// Computes NoMod osu PP+-shaped chart axes via <see cref="IEzPpPlusEngine"/> and writes Realm skills.
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
            this.engine = engine ?? new EzPpPlusStubEngine();
        }

        public IReadOnlyDictionary<string, double>? TryGetOrCompute(BeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != EzLocalProfileConstants.OSU_RULESET_ID)
                return null;

            var existing = skillStore.GetBeatmapSkills(beatmapInfo.Hash, EzSkillSystems.BEATMAP_PPPLUS);

            if (EzPpPlusAttributes.IsCompleteChartCache(existing))
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
                double lengthSeconds = Math.Max(0, beatmapInfo.Length / 1000.0);
                var chart = engine.CalculateChart(attributes, beatmapInfo.Difficulty, lengthSeconds);
                var skills = chart.ToChartSkills();

                skillStore.WriteBeatmapSystemSkills(
                    beatmapInfo.Hash,
                    EzSkillSystems.BEATMAP_PPPLUS,
                    skills,
                    beatmapInfo.ID);

                return skills;
            }
            catch (Exception e)
            {
                Logger.Log($"[EzSkills] osu PP+ chart compute failed for {beatmapInfo}: {e.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                return null;
            }
        }
    }
}
