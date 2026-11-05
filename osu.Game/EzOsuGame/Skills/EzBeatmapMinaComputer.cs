// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Skills.Mina;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Computes NoMod 1.0x Mina axes. Persisting is a separate entry point: read paths resolve live into a
    /// session cache only.
    /// </summary>
    public sealed class EzBeatmapMinaComputer
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzSkillStore skillStore;

        private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, double>> liveCache
            = new ConcurrentDictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.Ordinal);

        public EzBeatmapMinaComputer(BeatmapManager beatmapManager, EzSkillStore skillStore)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
        }

        /// <summary>Drop the session-only axis set for a hash, e.g. after a backfill wrote a newer row.</summary>
        public void InvalidateLive(string? beatmapHash = null)
        {
            if (string.IsNullOrEmpty(beatmapHash))
            {
                liveCache.Clear();
                return;
            }

            liveCache.TryRemove(beatmapHash, out _);
        }

        /// <summary>
        /// Current Realm rows when present, otherwise a session-only compute. Never persists.
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

            if (liveCache.TryGetValue(beatmapInfo.Hash, out var live))
                return live;

            try
            {
                var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                var playable = EzPlayableBeatmapCache.GetShared(working, working.BeatmapInfo.Ruleset);
                var computed = compute(playable, beatmapInfo);

                if (computed != null)
                    liveCache[beatmapInfo.Hash] = computed;

                return computed;
            }
            catch (Exception e)
            {
                Logger.Log($"[EzSkills] MSD live compute failed for {beatmapInfo}: {e.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                return null;
            }
        }

        public IReadOnlyDictionary<string, double>? ComputeAndStore(BeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            if (!beatmapInfo.Ruleset.Available)
                beatmapInfo.Ruleset.Available = true;

            IBeatmap playable;

            try
            {
                var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);

                if (!working.BeatmapInfo.Ruleset.Available)
                    working.BeatmapInfo.Ruleset.Available = true;

                playable = EzPlayableBeatmapCache.GetShared(working, working.BeatmapInfo.Ruleset);
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

            int keyCount = EzHoldMetrics.ResolveKeyCount(playable);

            // The engine returns a zero vector for a column count outside its range rather than throwing, so the
            // gate has to be read here: without it those charts stay row-less and every reader counts them as
            // "still missing" forever.
            if (!EzMinaEngine.IsSupportedKeyCount(keyCount))
            {
                Logger.Log($"[EzSkills] MSD unsupported keymode {keyCount} for {beatmapInfo}; settling as unrateable.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                skillStore.WriteBeatmapMsdUnrateable(beatmapInfo.Hash, beatmapInfo.ID);
                return null;
            }

            var computed = computeVector(playable, beatmapInfo, keyCount, vector => skillStore.WriteBeatmapMsdUnrateable(beatmapInfo.Hash, beatmapInfo.ID));

            if (computed == null)
                return null;

            skillStore.WriteBeatmapMsd(beatmapInfo.Hash, computed.Value, beatmapInfo.ID);

            return toDictionary(computed.Value);
        }

        /// <summary>
        /// Axis vector for a loaded chart, or null when the chart cannot be rated. <paramref name="onSettledMiss"/>
        /// runs for verdicts that will never change for this hash; transient engine failures return null without
        /// calling it.
        /// </summary>
        private IReadOnlyDictionary<string, double>? compute(IBeatmap playable, BeatmapInfo beatmapInfo)
        {
            int keyCount = EzHoldMetrics.ResolveKeyCount(playable);

            if (!EzMinaEngine.IsSupportedKeyCount(keyCount))
                return null;

            var vector = computeVector(playable, beatmapInfo, keyCount, _ => { });

            return vector == null ? null : toDictionary(vector.Value);
        }

        private static EzMinaSkillAxisVector? computeVector(
            IBeatmap playable,
            BeatmapInfo beatmapInfo,
            int keyCount,
            Action<EzMinaSkillAxisVector> onSettledMiss)
        {
            using var calc = new EzMinaEngine();
            EzMinaSkillAxisVector vector;

            try
            {
                vector = calc.CalculateMsd(EzMinaNoteConverter.Convert(playable), keyCount, rate: 1f);
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
                onSettledMiss(vector);
                return null;
            }

            return vector;
        }

        private static Dictionary<string, double> toDictionary(EzMinaSkillAxisVector vector)
        {
            var result = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var (axis, value) in vector.Enumerate())
                result[axis.ToMsdSkillId()] = value;

            return result;
        }

        /// <summary>
        /// Requires every current Mina axis id. Incomplete caches fail and are recomputed via
        /// <see cref="TryGetOrCompute"/>.
        /// </summary>
        public static bool IsCurrentMinaCache(IReadOnlyDictionary<string, double> existing)
        {
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
