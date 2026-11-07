// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills.Mina;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Computes and persists NoMod 1.0x Mina axes for the background backfill. This is a write path only: live
    /// values come from the shared analysis pass (<see cref="Analysis.IEzChartAnalysisLookup"/>), never from here.
    /// </summary>
    public sealed class EzBeatmapMinaComputer
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzChartSkillStore skillStore;
        private readonly EzMetricRevisionResolver resolver;

        public EzBeatmapMinaComputer(BeatmapManager beatmapManager, EzChartSkillStore skillStore, EzMetricRevisionResolver? resolver = null)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
            this.resolver = resolver ?? EzMetricRegistries.Current;
        }

        /// <summary>Revision the stored Mina rows are stamped with.</summary>
        public int Revision => resolver.RevisionOf(EzChartMetricProducer.Mina.Id(), LocalProfile.EzLocalProfileConstants.MANIA_RULESET_ID);

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
        /// </summary>
        /// <summary>
        /// Requires every axis the Mina producer declares. The declaration is the single source, so adding an
        /// axis cannot leave this check silently accepting a partial row set.
        /// </summary>
        public static bool IsCurrentMinaCache(IReadOnlyDictionary<string, double> existing)
        {
            if (existing.Count == 0)
                return false;

            if (EzMetricRegistries.ChartRegistry.Get(EzChartMetricProducer.Mina.Id()) is not { } producer)
                return false;

            return producer.IsCompleteFor(EzSkillSystems.UNSLICED_KEY, LocalProfile.EzLocalProfileConstants.MANIA_RULESET_ID, existing);
        }

        /// <summary>
        /// Complete MSD or a settled miss — BDSP should not reprocess. A settled miss is never a valid read
        /// cache (<see cref="IsCurrentMinaCache"/> stays false).
        /// </summary>
        public static bool IsSettledMinaCache(EzMetricState state, IReadOnlyDictionary<string, double> existing) => state == EzMetricState.Unrateable || IsCurrentMinaCache(existing);
    }
}
