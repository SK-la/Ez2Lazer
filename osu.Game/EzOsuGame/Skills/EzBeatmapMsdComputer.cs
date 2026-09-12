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

        /// <summary>
        /// When true, <see cref="ComputeAndStore"/> skips side-effect ChartDan upsert so a same-job
        /// CSI → ChartDan pass can write stamped rows. Hot-path / MSD-only jobs leave this false.
        /// </summary>
        public bool SuppressChartDanSideUpsert { get; set; }

        public EzBeatmapMsdComputer(BeatmapManager beatmapManager, EzSkillStore skillStore)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
        }

        /// <summary>
        /// Returns existing MSD skills when present and current; otherwise computes and persists.
        /// Settled <c>__unrateable</c> markers return null without recomputing.
        /// </summary>
        public IReadOnlyDictionary<string, double>? TryGetOrCompute(BeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            var existing = skillStore.GetBeatmapSkills(beatmapInfo.Hash, EzSkillSystems.BEATMAP_MSD);
            if (IsCurrentMsdCache(existing))
                return existing;

            if (IsUnrateableMsd(existing))
                return null;

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
                bool supportedKeymode = EzMinaCalcFacade.SupportsOsuTextKeyCount(keyCount)
                                        || EzMinaCalcFacade.SupportsNoteArrayKeyCount(keyCount);

                if (supportedKeymode)
                {
                    Logger.Log($"MSD zero vector for {beatmapInfo} (keys={keyCount})");
                    skillStore.WriteBeatmapMsdUnrateable(beatmapInfo.Hash, beatmapInfo.ID);
                }
                // else
                //     Logger.Log($"MSD skip unsupported keymode {keyCount}K for {beatmapInfo}");

                return null;
            }

            double holdRatio = EzChartDanEstimator.ComputeHoldRatio(playable);
            int holdCount = EzChartDanEstimator.TryHoldCountFromBeatmapInfo(beatmapInfo);
            if (holdCount < 0)
                holdCount = EzChartDanEstimator.ComputeHoldCount(playable);

            skillStore.WriteBeatmapMsd(beatmapInfo.Hash, vector, beatmapInfo.ID, holdRatio: holdRatio);

            // Return the just-written values without a second Realm round-trip.
            var result = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var (axis, value) in vector.Enumerate())
                result[axis.ToMsdSkillId()] = value;

            if (double.IsFinite(holdRatio))
                result[EzSkillSystems.MsdHoldRatioSkillId] = Math.Clamp(holdRatio, 0, 1);

            // Same as xxy import hot-write: when MSD lands, persist ChartDan so select can read Realm.
            // Skipped when BDSP will run ChartDan after CSI in the same job (stamp-complete rows).
            if (!SuppressChartDanSideUpsert)
                tryUpsertChartDanFromMsd(beatmapInfo, result, keyCount, holdRatio, holdCount);

            return result;
        }

        private void tryUpsertChartDanFromMsd(
            BeatmapInfo beatmapInfo,
            IReadOnlyDictionary<string, double> msd,
            int keyCount,
            double holdRatio,
            int holdCount)
        {
            try
            {
                skillStore.TryGetChartSkillInfo(beatmapInfo.Hash, out var chartInfo);
                if (chartInfo is { IsUnavailable: true })
                    chartInfo = null;

                double? xxySr = beatmapInfo.XxyStarRating >= 0 ? beatmapInfo.XxyStarRating : null;
                var persisted = EzPersistedChartDan.TryComputeFromStored(
                    beatmapInfo.Hash,
                    beatmapInfo.ID,
                    msd,
                    keyCount > 0 ? keyCount : 4,
                    holdRatio,
                    xxySr,
                    chartInfo,
                    holdCount);

                if (persisted != null)
                    skillStore.UpsertChartDan(persisted);
            }
            catch (Exception e)
            {
                Logger.Log($"ChartDan upsert after MSD failed for {beatmapInfo}: {e.Message}");
            }
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
        /// Incomplete caches fail and are recomputed via <see cref="TryGetOrCompute"/>.
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

        /// <summary>True when BDSP has recorded a zero-vector settled miss for this hash.</summary>
        public static bool IsUnrateableMsd(IReadOnlyDictionary<string, double> existing)
            => existing.ContainsKey(EzSkillSystems.MsdUnrateableSkillId);

        /// <summary>
        /// Complete MSD or settled <c>__unrateable</c> — BDSP should not reprocess.
        /// Unrateable is never a valid read cache (<see cref="IsCurrentMsdCache"/> stays false).
        /// </summary>
        public static bool IsSettledMsdCache(IReadOnlyDictionary<string, double> existing)
            => IsCurrentMsdCache(existing) || IsUnrateableMsd(existing);
    }
}
