// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Skills.Mina
{
    /// <summary>
    /// Runs the Mina system for one chart: gate, convert, call the Mina engine, hand the vector to the store.
    /// </summary>
    /// <remarks>
    /// Write path: rates through <see cref="EzAnalysisComputation.ComputeMinaAxes"/>, the same entry the live pass
    /// uses, then stores. Live reads still go through the analysis cache.
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
        public int Revision => EzMinaEngine.VERSION;

        public IReadOnlyDictionary<string, double>? ComputeAndStore(BeatmapInfo beatmapInfo)
        {
            var section = TryCompute(beatmapInfo);

            if (section == null)
                return null;

            switch (section)
            {
                case EzMinaMaintenanceSection.Vector(var vector, _):
                    skillStore.ApplyMinaSection(beatmapInfo.Hash, beatmapInfo.ID, section);
                    return toDictionary(vector);

                case EzMinaMaintenanceSection.Unrateable:
                    skillStore.ApplyMinaSection(beatmapInfo.Hash, beatmapInfo.ID, section);
                    return null;

                case EzMinaMaintenanceSection.Skipped:
                case EzMinaMaintenanceSection.TransientFailure:
                default:
                    return null;
            }
        }

        /// <summary>CPU-only half of Mina maintenance for parallel passes.</summary>
        public EzMinaMaintenanceSection? TryCompute(BeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return new EzMinaMaintenanceSection.Skipped();

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
                Logger.Log($"[EzSkills] Mina cannot load {beatmapInfo} ({e.Message}); settling as unrateable.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                return new EzMinaMaintenanceSection.Unrateable();
            }

            int keyCount = EzHoldMetrics.ResolveKeyCount(playable);

            if (!EzMinaEngine.IsSupportedKeyCount(keyCount))
            {
                Logger.Log($"[EzSkills] Mina unsupported keymode {keyCount} for {beatmapInfo}; settling as unrateable.", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                return new EzMinaMaintenanceSection.Unrateable();
            }

            var mina = EzAnalysisComputation.ComputeMinaAxes(beatmapInfo.Ruleset.OnlineID, playable, 1);

            if (mina is not { } result)
            {
                Logger.Log($"[EzSkills] Mina compute failed for {beatmapInfo} (keys={keyCount})", Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                return new EzMinaMaintenanceSection.TransientFailure();
            }

            if (!result.HasRating)
            {
                Logger.Log($"[EzSkills] Mina zero vector for {beatmapInfo} (keys={keyCount})", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                return new EzMinaMaintenanceSection.Unrateable();
            }

            double? star = result.Vector.Overall > 0
                ? EzMinaStarRatingScale.ToStarRating(result.Vector.Overall)
                : null;

            return new EzMinaMaintenanceSection.Vector(result.Vector, star);
        }

        private static Dictionary<string, double> toDictionary(EzMinaSkillAxisData vector)
        {
            var result = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var (axis, value) in vector.Enumerate())
                result[axis.ToMetaId()] = value;

            return result;
        }

        public static IReadOnlyDictionary<string, double> ToSkillDictionary(EzMinaSkillAxisData vector) => toDictionary(vector);

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
                if (!existing.ContainsKey(axis.ToMetaId()))
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
