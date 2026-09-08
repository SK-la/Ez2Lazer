// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Computes NoMod 1.0x MSD axes for a beatmap and writes each axis as an independent Realm skill.
    /// </summary>
    public sealed class EzBeatmapMsdComputer
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzSkillStore skillStore;

        public EzBeatmapMsdComputer(BeatmapManager beatmapManager, EzSkillStore skillStore)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
        }

        /// <summary>
        /// Returns existing MSD skills when present and current; otherwise computes and persists.
        /// </summary>
        public IReadOnlyDictionary<string, double>? TryGetOrCompute(BeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            var existing = skillStore.GetBeatmapSkills(beatmapInfo.Hash, EzSkillSystems.BEATMAP_MSD);
            if (existing.Count >= EzMinaSkillAxisExtensions.All.Length
                && existing.ContainsKey(EzSkillSystems.MsdHoldRatioSkillId))
                return existing;

            return ComputeAndStore(beatmapInfo);
        }

        public IReadOnlyDictionary<string, double>? ComputeAndStore(BeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
            var playable = working.GetPlayableBeatmap(beatmapInfo.Ruleset);

            using var calc = new EzMinaCalcFacade();
            var vector = calc.CalculateMsd(playable);

            if (vector.Overall <= 0 && vector.Stream <= 0)
                return null;

            double holdRatio = EzChartDanEstimator.ComputeHoldRatio(playable);
            skillStore.WriteBeatmapMsd(beatmapInfo.Hash, vector, beatmapInfo.ID, holdRatio: holdRatio);
            return skillStore.GetBeatmapSkills(beatmapInfo.Hash, EzSkillSystems.BEATMAP_MSD);
        }
    }
}
