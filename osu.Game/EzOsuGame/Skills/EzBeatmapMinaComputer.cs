// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Skills.Mina;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Computes NoMod 1.0x Mina axes for a beatmap and writes each axis as an independent Realm skill.
    /// </summary>
    public sealed class EzBeatmapMinaComputer
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzSkillStore skillStore;

        public EzBeatmapMinaComputer(BeatmapManager beatmapManager, EzSkillStore skillStore)
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

            var existing = skillStore.GetBeatmapSkills(beatmapInfo.Hash, EzSkillSystems.BEATMAP_MINA);
            if (IsCurrentMinaCache(existing))
                return existing;

            if (IsUnrateableMina(existing))
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

            IBeatmap playable;
            EzCalcNote[] notes;

            try
            {
                var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);

                if (!working.BeatmapInfo.Ruleset.Available)
                    working.BeatmapInfo.Ruleset.Available = true;

                playable = EzPlayableBeatmapCache.GetShared(working, working.BeatmapInfo.Ruleset);
                notes = EzMinaNoteConverter.Convert(playable);
            }
            catch (Exception e)
            {
                // Content-addressed: the file backing this hash will not become loadable on its own, so it is
                // settled rather than retried on every backfill (mirrors the CSI stage). Re-importing produces a
                // new hash and 「完全重算」 clears the marker.
                Logger.Log($"[EzSkills] MSD cannot load {beatmapInfo} ({e.Message}); settling as unrateable.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                skillStore.WriteBeatmapMsdUnrateable(beatmapInfo.Hash, beatmapInfo.ID);
                return null;
            }

            int keyCount = EzMinaNoteConverter.ResolveKeyCount(playable);

            // The engine returns a zero vector for a column count outside its range rather than throwing, so the
            // gate has to be read here: without it those charts stay row-less and every reader counts them as
            // "still missing" forever.
            if (!EzMinaEngine.IsSupportedKeyCount(keyCount))
            {
                Logger.Log($"[EzSkills] MSD unsupported keymode {keyCount} for {beatmapInfo}; settling as unrateable.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                skillStore.WriteBeatmapMsdUnrateable(beatmapInfo.Hash, beatmapInfo.ID);
                return null;
            }

            using var calc = new EzMinaEngine();
            EzMinaSkillAxisVector vector;

            try
            {
                vector = calc.CalculateMsd(notes, keyCount, rate: 1f);
            }
            catch (Exception e)
            {
                // Transient: the engine rebuilds itself and the next pass retries. Settling here would retire a
                // chart the engine can very likely still rate.
                Logger.Log($"[EzSkills] MSD compute failed for {beatmapInfo} (keys={keyCount}): {e.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                return null;
            }

            if (vector.Overall <= 0 && vector.Stream <= 0)
            {
                Logger.Log($"[EzSkills] MSD zero vector for {beatmapInfo} (keys={keyCount})", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                skillStore.WriteBeatmapMsdUnrateable(beatmapInfo.Hash, beatmapInfo.ID);
                return null;
            }

            double holdRatio = EzHoldMetrics.ComputeHoldRatio(playable);

            skillStore.WriteBeatmapMsd(beatmapInfo.Hash, vector, beatmapInfo.ID, holdRatio: holdRatio);

            // Return the just-written values without a second Realm round-trip.
            var result = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var (axis, value) in vector.Enumerate())
                result[axis.ToMsdSkillId()] = value;

            if (double.IsFinite(holdRatio))
                result[EzSkillSystems.MinaHoldRatioSkillId] = Math.Clamp(holdRatio, 0, 1);

            return result;
        }

        /// <summary>
        /// Requires every current Mina axis id plus hold ratio.
        /// Incomplete caches fail and are recomputed via <see cref="TryGetOrCompute"/>.
        /// </summary>
        public static bool IsCurrentMinaCache(IReadOnlyDictionary<string, double> existing)
        {
            if (!existing.ContainsKey(EzSkillSystems.MinaHoldRatioSkillId))
                return false;

            foreach (var axis in EzMinaSkillAxisExtensions.All)
            {
                if (!existing.ContainsKey(axis.ToMsdSkillId()))
                    return false;
            }

            return true;
        }

        /// <summary>True when BDSP has recorded a zero-vector settled miss for this hash.</summary>
        public static bool IsUnrateableMina(IReadOnlyDictionary<string, double> existing)
            => existing.ContainsKey(EzSkillSystems.MinaUnrateableSkillId);

        /// <summary>
        /// Complete MSD or settled <c>__unrateable</c> — BDSP should not reprocess.
        /// Unrateable is never a valid read cache (<see cref="IsCurrentMinaCache"/> stays false).
        /// </summary>
        public static bool IsSettledMinaCache(IReadOnlyDictionary<string, double> existing)
            => IsCurrentMinaCache(existing) || IsUnrateableMina(existing);
    }
}
