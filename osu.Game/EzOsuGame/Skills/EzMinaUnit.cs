// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Skills.Mina;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Runs the Mina system for one chart: gate, convert, call the Mina engine, hand the vector to the store.
    /// </summary>
    /// <remarks>
    /// Write path only: live values come from the shared analysis pass
    /// (<see cref="IEzChartAnalysisLookup"/>), never from here.
    /// </remarks>
    public sealed class EzMinaUnit
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzChartSkillStore skillStore;

        public EzMinaUnit(BeatmapManager beatmapManager, EzChartSkillStore skillStore)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
        }

        /// <summary>Revision the stored Mina rows are stamped with.</summary>
        public int Revision => EzAnalysis.VERSION;

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
            catch (Exception e) when (e is not ObjectDisposedException)
            {
                // Content-addressed: the file backing this hash will not become loadable on its own, so it is
                // settled rather than retried on every backfill (mirrors the CSI stage). Re-importing produces a
                // new hash and 「完全重算」 clears the marker.
                Logger.Log($"[EzSkills] Mina cannot load {beatmapInfo} ({e.Message}); settling as unrateable.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                skillStore.WriteBeatmapMsdUnrateable(beatmapInfo.Hash, beatmapInfo.ID);
                return null;
            }

            int keyCount = EzHoldMetrics.ResolveKeyCount(playable);

            // The engine returns a zero vector for a column count outside its range rather than throwing, so the
            // gate has to be read here: without it those charts stay row-less and every reader counts them as
            // "still missing" forever.
            if (!EzMinaEngine.IsSupportedKeyCount(keyCount))
            {
                Logger.Log($"[EzSkills] Mina unsupported keymode {keyCount} for {beatmapInfo}; settling as unrateable.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                skillStore.WriteBeatmapMsdUnrateable(beatmapInfo.Hash, beatmapInfo.ID);
                return null;
            }

            var computed = computeVector(playable, beatmapInfo, keyCount, _ => skillStore.WriteBeatmapMsdUnrateable(beatmapInfo.Hash, beatmapInfo.ID));

            if (computed == null)
                return null;

            skillStore.WriteBeatmapMsd(beatmapInfo.Hash, computed.Value, beatmapInfo.ID);

            return toDictionary(computed.Value);
        }

        private static EzMinaSkillAxisVector? computeVector(
            IBeatmap playable,
            BeatmapInfo beatmapInfo,
            int keyCount,
            Action<EzMinaSkillAxisVector> onSettledMiss)
        {
            var calc = EzMinaEnginePool.Rent();
            EzMinaSkillAxisVector vector;

            try
            {
                vector = calc.CalculateMsd(EzMinaNoteConverter.Convert(playable), keyCount, rate: 1f);
            }
            catch (Exception e)
            {
                // Transient: the engine rebuilds itself and the next pass retries. Settling here would retire a
                // chart the engine can very likely still rate.
                Logger.Log($"[EzSkills] Mina compute failed for {beatmapInfo} (keys={keyCount}): {e.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                return null;
            }

            if (vector.Overall <= 0 && vector.Stream <= 0)
            {
                Logger.Log($"[EzSkills] Mina zero vector for {beatmapInfo} (keys={keyCount})", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
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
        /// Requires every axis the Mina engine declares. Adding an axis cannot leave this check silently accepting
        /// a partial row set.
        /// </summary>
        public static bool IsCurrentMinaCache(IReadOnlyDictionary<string, double> existing)
        {
            if (existing.Count == 0)
                return false;

            foreach (var axis in EzMinaSkillAxisExtensions.All)
            {
                if (!existing.ContainsKey(axis.ToId()))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Complete Mina or a settled miss — BDSP should not reprocess. A settled miss is never a valid read
        /// cache (<see cref="IsCurrentMinaCache"/> stays false).
        /// </summary>
        public static bool IsSettledMinaCache(EzSkillRowState state, IReadOnlyDictionary<string, double> existing) => state == EzSkillRowState.Unrateable || IsCurrentMinaCache(existing);
    }
}
