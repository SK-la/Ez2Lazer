// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Skills.Dan
{
    /// <summary>
    /// Runs the dan system against the shared analysis: reads the verdict the pass already resolved for a chart.
    /// </summary>
    /// <remarks>
    /// This never measures anything itself: the Mina vector and the Sunny rating are produced once per chart and
    /// mods by the analysis pass, and the pass resolves both sides through one entry point. Reading it here is
    /// what keeps the HUD, the player credit and the persisted row from disagreeing about the same chart.
    /// </remarks>
    public sealed class EzDanUnit
    {
        private readonly IEzChartAnalysisLookup? analysisLookup;
        private readonly EzChartSkillStore? skillStore;

        public EzDanUnit(IEzChartAnalysisLookup? analysisLookup = null, EzChartSkillStore? skillStore = null)
        {
            this.analysisLookup = analysisLookup;
            this.skillStore = skillStore;
        }

        /// <summary>
        /// Derives and stores one chart's dan from what is currently persisted. Returns false when the stored inputs
        /// do not resolve to a verdict, so the next pass retries it.
        /// </summary>
        public bool ComputeAndStoreFromStored(BeatmapInfo beatmapInfo)
        {
            var section = TryComputeFromStored(beatmapInfo);

            if (section is EzDanMaintenanceSection.Upsert(var persisted))
            {
                skillStore?.UpsertChartDan(persisted);
                return true;
            }

            return section is not EzDanMaintenanceSection.TransientFailure;
        }

        /// <summary>Reads Mina / CSI from Realm — use only when those stages were not computed in the same iteration.</summary>
        public EzDanMaintenanceSection? TryComputeFromStored(BeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (skillStore == null)
                return new EzDanMaintenanceSection.TransientFailure();

            string hash = beatmapInfo.Hash;
            var msd = skillStore.GetBeatmapSkills(hash, EzAnalysisSystem.Mina);

            if (!EzMinaUnit.IsCurrentMinaCache(msd))
                return new EzDanMaintenanceSection.TransientFailure();

            skillStore.TryGetChartSkillInfo(hash, out var chartInfo);

            if (chartInfo is { IsUnavailable: true })
                chartInfo = null;

            return tryComputeFromInputs(beatmapInfo, msd, chartInfo);
        }

        /// <summary>
        /// Derives dan from in-memory Mina / CSI sections when the chart chain computed them in the same iteration.
        /// </summary>
        public EzDanMaintenanceSection? TryComputeFromInputs(
            BeatmapInfo beatmapInfo,
            EzMinaMaintenanceSection? inMemoryMina,
            EzCsiMaintenanceSection? inMemoryCsi,
            bool allowStoredMinaFallback,
            IBeatmap? playable = null)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (inMemoryMina is EzMinaMaintenanceSection.Unrateable)
                return new EzDanMaintenanceSection.Skipped();

            IReadOnlyDictionary<string, double>? msd = resolveMsd(beatmapInfo.Hash, inMemoryMina, allowStoredMinaFallback);
            EzChartSkillInfo? chartInfo = resolveChartInfo(beatmapInfo.Hash, inMemoryCsi, allowStoredCsiFallback: inMemoryCsi == null);

            if (msd == null || !EzMinaUnit.IsCurrentMinaCache(msd))
                return new EzDanMaintenanceSection.TransientFailure();

            return tryComputeFromInputs(beatmapInfo, msd, chartInfo, playable);
        }

        private EzDanMaintenanceSection? tryComputeFromInputs(BeatmapInfo beatmapInfo, IReadOnlyDictionary<string, double> msd, EzChartSkillInfo? chartInfo, IBeatmap? playable = null)
        {
            int keyCount;
            double holdRatio;
            double? sunnySR;
            int holdCount;

            if (playable != null)
            {
                keyCount = EzHoldMetrics.ResolveKeyCount(playable);
                holdRatio = EzHoldMetrics.HoldRatioOr(playable, beatmapInfo);

                if (double.IsNaN(holdRatio))
                    holdRatio = 0;

                sunnySR = beatmapInfo.EzRatingData.SunnySR >= 0 ? beatmapInfo.EzRatingData.SunnySR : null;
                holdCount = EzHoldMetrics.ComputeHoldCount(playable);
            }
            else if (!EzDanEngine.TryResolveStoredInputs(beatmapInfo, out keyCount, out holdRatio, out sunnySR, out holdCount))
            {
                return new EzDanMaintenanceSection.TransientFailure();
            }

            var persisted = EzPersistedChartDan.TryComputeFromStored(beatmapInfo.Hash, beatmapInfo.ID, msd, keyCount, holdRatio, sunnySR, chartInfo, holdCount);

            if (persisted == null)
                return new EzDanMaintenanceSection.TransientFailure();

            return new EzDanMaintenanceSection.Upsert(persisted);
        }

        private IReadOnlyDictionary<string, double>? resolveMsd(string hash, EzMinaMaintenanceSection? inMemoryMina, bool allowStoredFallback)
        {
            if (inMemoryMina is EzMinaMaintenanceSection.Vector(var data, _))
                return EzMinaUnit.ToSkillDictionary(data);

            if (inMemoryMina is EzMinaMaintenanceSection.Unrateable)
                return null;

            if (!allowStoredFallback || skillStore == null)
                return null;

            var msd = skillStore.GetBeatmapSkills(hash, EzAnalysisSystem.Mina);
            return EzMinaUnit.IsCurrentMinaCache(msd) ? msd : null;
        }

        private EzChartSkillInfo? resolveChartInfo(string hash, EzCsiMaintenanceSection? inMemoryCsi, bool allowStoredCsiFallback)
        {
            if (inMemoryCsi is EzCsiMaintenanceSection.Computed(var info, _))
                return info;

            if (inMemoryCsi is EzCsiMaintenanceSection.Unavailable)
                return null;

            if (!allowStoredCsiFallback || skillStore == null)
                return null;

            if (skillStore.TryGetChartSkillInfo(hash, out var chartInfo) && chartInfo is { IsUnavailable: false })
                return chartInfo;

            return null;
        }

        /// <summary>
        /// Estimates chart dan for a read path (HUD, labels): only ever uses an analysis that already exists.
        /// Null means the pass has not produced this chart yet; the caller treats that as "not available".
        /// </summary>
        public EzChartDanVerdict? TryEstimate(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods = null)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            return analysisLookup?.TryGet(beatmapInfo, mods) is { } analysis
                ? fromAnalysis(analysis)
                : null;
        }

        /// <summary>
        /// Same estimate, but fills the shared analysis store when the inputs have not been measured yet.
        /// </summary>
        /// <remarks>
        /// Batch callers only (player aggregation), which run off the update thread. The measurement is the one the
        /// analysis pass would do, so a later HUD read of the same chart is a cache hit rather than a second pass.
        /// </remarks>
        public EzChartDanVerdict? TryEstimateOrCompute(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods = null)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            return analysisLookup?.GetOrCompute(beatmapInfo, beatmapInfo.Ruleset, mods) is { } analysis
                ? fromAnalysis(analysis)
                : null;
        }

        private static EzChartDanVerdict? fromAnalysis(EzAnalysisResult analysis)
        {
            if (analysis.Dan is not { } dan || dan.Sides.Count == 0)
                return null;

            var side = EzDanAlgorithm.AllowsPersistedChartLnHalf(dan.KeyCount, dan.HoldRatio, dan.HoldCount)
                ? EzDanSide.Ln
                : EzDanSide.Rc;

            var entry = dan.For(side);
            var resolvedSide = side;

            if (entry == null)
            {
                resolvedSide = side == EzDanSide.Ln ? EzDanSide.Rc : EzDanSide.Ln;
                entry = dan.For(resolvedSide);
            }

            if (entry == null)
                return null;

            return new EzChartDanVerdict
            {
                RawDan = entry.RawDan,
                Label = entry.Label,
                KeyCount = dan.KeyCount,
                Side = resolvedSide,
                DominantAxis = dan.DominantAxis,
                OverallMsd = dan.OverallMsd,
                HoldRatio = dan.HoldRatio,
                AlgorithmVersion = EzDanEngine.VERSION,
            };
        }
    }
}
