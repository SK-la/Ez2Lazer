// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;

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
            if (IsCurrentMsdCache(existing))
                return existing;

            return ComputeAndStore(beatmapInfo);
        }

        public IReadOnlyDictionary<string, double>? ComputeAndStore(BeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            if (!beatmapInfo.Ruleset.Available)
                beatmapInfo.Ruleset.Available = true;

            var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);

            if (!working.BeatmapInfo.Ruleset.Available)
                working.BeatmapInfo.Ruleset.Available = true;

            var playable = working.GetPlayableBeatmap(working.BeatmapInfo.Ruleset);

            int keyCount = EzMinaNoteConverter.ResolveKeyCount(playable);

            using var calc = new EzMinaCalcFacade();
            EzSkillsetVector vector;

            try
            {
                vector = calculateMsd(calc, working, playable, beatmapInfo, keyCount);
            }
            catch (Exception e)
            {
                Logger.Log($"MSD compute failed for {beatmapInfo} (keys={keyCount}): {e.Message}");
                return null;
            }

            if (vector.Overall <= 0 && vector.Stream <= 0)
            {
                if (!EzMinaCalcFacade.SupportsOsuTextKeyCount(keyCount) && !EzMinaCalcFacade.SupportsNoteArrayKeyCount(keyCount))
                    Logger.Log($"MSD skip unsupported keymode {keyCount}K for {beatmapInfo}");
                else
                    Logger.Log($"MSD zero vector for {beatmapInfo} (keys={keyCount})");

                return null;
            }

            double holdRatio = EzChartDanEstimator.ComputeHoldRatio(playable);
            skillStore.WriteBeatmapMsd(beatmapInfo.Hash, vector, beatmapInfo.ID, holdRatio: holdRatio);

            // Return the just-written values without a second Realm round-trip.
            var result = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var (axis, value) in vector.Enumerate())
                result[axis.ToMsdSkillId()] = value;

            if (double.IsFinite(holdRatio))
                result[EzSkillSystems.MsdHoldRatioSkillId] = Math.Clamp(holdRatio, 0, 1);

            return result;
        }

        private static EzSkillsetVector calculateMsd(
            EzMinaCalcFacade calc,
            WorkingBeatmap working,
            IBeatmap playable,
            BeatmapInfo beatmapInfo,
            int keyCount)
        {
            // Prefer .osu text so MinaCalc reads CircleSize (required for 6K/7K on 0.4.2).
            if (EzMinaCalcFacade.SupportsOsuTextKeyCount(keyCount))
            {
                string? osuText = tryReadOsuText(working, beatmapInfo) ?? tryEncodePlayable(working, playable);

                if (!string.IsNullOrWhiteSpace(osuText))
                    return calc.CalculateMsdFromOsuText(osuText, beatmapInfo.Path ?? $"{keyCount}k.osu");
            }

            // Note-array path: 4K only on this package.
            return calc.CalculateMsd(playable);
        }

        private static string? tryReadOsuText(WorkingBeatmap working, BeatmapInfo beatmapInfo)
        {
            string? chartPath = beatmapInfo.Path;
            if (string.IsNullOrEmpty(chartPath))
                return null;

            try
            {
                string? storagePath = beatmapInfo.BeatmapSet?.GetPathForFile(chartPath);
                Stream? stream = null;

                if (!string.IsNullOrEmpty(storagePath))
                    stream = working.GetStream(storagePath);

                stream ??= working.GetStream(chartPath);

                if (stream == null)
                    return null;

                using (stream)
                using (var reader = new LineBufferedReader(stream))
                    return reader.ReadToEnd();
            }
            catch (Exception e)
            {
                Logger.Log($"MSD: failed reading .osu for {beatmapInfo}: {e.Message}");
                return null;
            }
        }

        private static string? tryEncodePlayable(WorkingBeatmap working, IBeatmap playable)
        {
            try
            {
                var sb = new StringBuilder();
                using (var writer = new StringWriter(sb))
                    new LegacyBeatmapEncoder(playable, working.Skin, null).Encode(writer);

                return sb.ToString();
            }
            catch (Exception e)
            {
                Logger.Log($"MSD: failed encoding playable: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Requires every current Mina axis id plus hold ratio.
        /// Legacy-only caches (jack_speed / technical) fail and are recomputed.
        /// </summary>
        public static bool IsCurrentMsdCache(IReadOnlyDictionary<string, double> existing)
        {
            if (!existing.ContainsKey(EzSkillSystems.MsdHoldRatioSkillId))
                return false;

            foreach (var axis in EzMinaSkillAxisExtensions.All)
            {
                if (!existing.ContainsKey(axis.ToMsdSkillId()))
                    return false;
            }

            return true;
        }
    }
}
