// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osu.Game.Utils;

namespace osu.Game.EzOsuGame.Analysis
{
    // 不主动释放 workingCache，让框架自行决定。主动释放会频繁触发大量 Invalidating 日志。
    internal static class EzAnalysisComputation
    {
        public static bool TryComputePerformancePoints(BeatmapManager beatmapManager, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken, out double performancePoints)
        {
            performancePoints = 0;

            PlayableCachedWorkingBeatmap workingBeatmap = new PlayableCachedWorkingBeatmap(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo));
            IBeatmap analysisBeatmap = workingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (tryComputePerfectPp(workingBeatmap, analysisBeatmap, lookup, cancellationToken) is not double resolvedPp)
                return false;

            performancePoints = resolvedPp;
            return true;
        }

        public static bool TryComputeXxySr(BeatmapManager beatmapManager, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken, out double xxySr)
        {
            xxySr = 0;

            IBeatmap analysisBeatmap = EzPlayableBeatmapCache.GetShared(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo), lookup.Ruleset, lookup.OrderedMods,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (!tryComputeXxySr(analysisBeatmap, lookup, cancellationToken, out double? computedXxySr)
                || computedXxySr is not double resolvedXxySr)
                return false;

            xxySr = resolvedXxySr;
            return true;
        }

        /// <summary>
        /// Same xxy path as song-select analysis panel, but reuses an already-built playable
        /// (e.g. live ChartDan after key/rate mods). Does not write Realm.
        /// </summary>
        public static bool TryComputeXxySrFromPlayable(
            IBeatmap playable,
            IRulesetInfo ruleset,
            IReadOnlyList<Mod> mods,
            CancellationToken cancellationToken,
            out double xxySr)
        {
            xxySr = 0;

            if (!EzSunnyStarRatingSupport.SupportsBeatmap(playable, ruleset))
                return false;

            if (playable.HitObjects.Count == 0)
                return true;

            double rate = getRateAdjustMultiplier(mods);

            if (!EzAnalysisProviderBridge.TryGetValue(ruleset, new EzAnalysisRequest(playable, rate), EzAnalysisFields.XXY_SR, cancellationToken, out double sr)
                || !double.IsFinite(sr)
                || sr < 0)
            {
                return false;
            }

            xxySr = sr;
            return true;
        }

        /// <summary>
        /// Computes baseline NoMod xxy SR for persisting to the Sunny star slot.
        /// Returns -1 when unsupported or failed, 0 for empty beatmaps.
        /// </summary>
        public static double ComputeBaselineSSRForRealm(BeatmapManager beatmapManager, BeatmapInfo beatmapInfo, CancellationToken cancellationToken = default)
        {
            var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);

            if (!EzSunnyStarRatingSupport.SupportsBeatmap(working.Beatmap, beatmapInfo.Ruleset))
                return -1;

            if (working.Beatmap.HitObjects.Count == 0)
                return 0;

            var lookup = new EzAnalysisLookupCache(beatmapInfo, beatmapInfo.Ruleset, mods: null);

            return TryComputeXxySr(beatmapManager, lookup, cancellationToken, out double xxySr)
                ? xxySr
                : -1;
        }

        /// <summary>
        /// The baseline performance points implied by <paramref name="starRating"/>, for persisting to a per-system
        /// pp column. Returns -1 when the ruleset cannot express it or the rating is unset.
        /// </summary>
        public static double ComputeBaselinePerformancePointsForRating(BeatmapManager beatmapManager, BeatmapInfo beatmapInfo, double starRating,
                                                                      CancellationToken cancellationToken = default)
        {
            if (!double.IsFinite(starRating) || starRating <= 0)
                return -1;

            var lookup = new EzAnalysisLookupCache(beatmapInfo, beatmapInfo.Ruleset, mods: null);
            IBeatmap analysisBeatmap = EzPlayableBeatmapCache.GetShared(beatmapManager.GetWorkingBeatmap(beatmapInfo), lookup.Ruleset, lookup.OrderedMods,
                cancellationToken);

            if (analysisBeatmap.HitObjects.Count == 0)
                return -1;

            return calculatePerformanceAtRating(lookup, analysisBeatmap, starRating, cancellationToken);
        }

        /// <summary>
        /// Computes the baseline performance points implied by the stored Sunny star rating, for persisting to the
        /// Sunny pp slot. Returns -1 when the ruleset cannot express it.
        /// </summary>
        public static double ComputeBaselineSunnyPerformancePointsForRealm(BeatmapManager beatmapManager, BeatmapInfo beatmapInfo,
                                                                           CancellationToken cancellationToken = default)
            => ComputeBaselinePerformancePointsForRating(beatmapManager, beatmapInfo, beatmapInfo.EzRatingData.SunnySR, cancellationToken);

        public static bool TryComputeXxySrAndPp(BeatmapManager beatmapManager, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken,
                                                out double? xxySr, out double? pp)
        {
            PlayableCachedWorkingBeatmap workingBeatmap = new PlayableCachedWorkingBeatmap(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo));
            IBeatmap analysisBeatmap = workingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            return tryComputeOperationValues(workingBeatmap, analysisBeatmap, lookup, cancellationToken, out xxySr, out pp);
        }

        public static EzAnalysisResult Compute(BeatmapManager beatmapManager, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken = default)
            => Compute(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo), lookup, cancellationToken);

        /// <summary>
        /// 主 SQLite 预热/回填专用：仅计算 kps 与 mania 列统计（NoMod），不写 xxy/PP（由 Realm 维护）。
        /// </summary>
        public static EzAnalysisResult ComputePersistedSqliteSlice(BeatmapManager beatmapManager, in EzAnalysisLookupCache lookup,
                                                                   CancellationToken cancellationToken = default)
            => ComputePersistedSqliteSlice(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo), lookup, cancellationToken);

        public static EzAnalysisResult ComputePersistedSqliteSlice(WorkingBeatmap workingBeatmap, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken = default)
        {
            // Column statistics describe the layout of a column-based chart; every other ruleset has none to store.
            bool columnRuleset = lookup.Ruleset.OnlineID == EzLocalProfileConstants.MANIA_RULESET_ID;
            IBeatmap analysisBeatmap;

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Convert))
                analysisBeatmap = EzPlayableBeatmapCache.GetShared(workingBeatmap, lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            (double averageKps, double maxKps, List<double> kpsList, Dictionary<int, int> columnCounts, Dictionary<int, int> holdNoteCounts) data;

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Kps))
                data = OptimizedBeatmapCalculator.GetAllDataOptimized(analysisBeatmap, onlyKps: !columnRuleset);

            var (averageKps, maxKps, kpsList, columnCounts, holdNoteCounts) = data;

            double rate = getRateAdjustMultiplier(lookup.OrderedMods);

            if (!Precision.AlmostEquals(rate, 1.0))
            {
                averageKps *= rate;
                maxKps *= rate;

                for (int i = 0; i < kpsList.Count; i++)
                    kpsList[i] *= rate;
            }

            cancellationToken.ThrowIfCancellationRequested();

            kpsList = OptimizedBeatmapCalculator.DownsampleToFixedCount(kpsList, OptimizedBeatmapCalculator.DEFAULT_KPS_GRAPH_POINTS);

            var commonSummary = new KpsSummary(averageKps, maxKps, kpsList);
            EzManiaSummary? maniaSummary = null;

            if (columnRuleset)
            {
                padColumnCounts(lookup, columnCounts, holdNoteCounts);
                maniaSummary = new EzManiaSummary(columnCounts, holdNoteCounts, xxySr: null);
            }

            return new EzAnalysisResult(commonSummary, maniaSummary);
        }

        public static EzAnalysisResult Compute(WorkingBeatmap workingBeatmap, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken = default)
        {
            // The engines that produce skill axes are independent of a ruleset exposing an analysis provider, so
            // the pass always measures them. Products that do need the provider (official performance, the Sunny
            // rating and the ruleset radar) are guarded where they are produced.
            int rulesetOnlineId = lookup.Ruleset.OnlineID;
            bool columnRuleset = rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID;

            // One conversion serves this whole pass: the framework's shared cache is skipped here because the
            // difficulty calculator needs an instance it may hand to sibling calculators repeatedly.
            var playable = new PlayableCachedWorkingBeatmap(workingBeatmap);
            IBeatmap analysisBeatmap;

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Convert))
                analysisBeatmap = playable.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            (double averageKps, double maxKps, List<double> kpsList, Dictionary<int, int> columnCounts, Dictionary<int, int> holdNoteCounts) measured;

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Kps))
                measured = OptimizedBeatmapCalculator.GetAllDataOptimized(analysisBeatmap, onlyKps: !columnRuleset);

            var (averageKps, maxKps, kpsList, columnCounts, holdNoteCounts) = measured;

            // 将速率调整 mod 应用到分析结果，保持与实际游玩时长一致。
            double rate = getRateAdjustMultiplier(lookup.OrderedMods);

            if (!Precision.AlmostEquals(rate, 1.0))
            {
                averageKps *= rate;
                maxKps *= rate;

                for (int i = 0; i < kpsList.Count; i++)
                    kpsList[i] *= rate;
            }

            cancellationToken.ThrowIfCancellationRequested();

            kpsList = OptimizedBeatmapCalculator.DownsampleToFixedCount(kpsList, OptimizedBeatmapCalculator.DEFAULT_KPS_GRAPH_POINTS);

            var commonSummary = new KpsSummary(averageKps, maxKps, kpsList);

            // Every product below is measured once and shared by the axes table, the dan set and the mania summary.
            // Deriving them per consumer is what previously ran the Mina engine and the Sunny rating twice per pass.
            // The rating and the radar come from one provider call: the radar payload embeds the same rating, so
            // asking twice made the ruleset recompute its own difficulty core.
            // One note extraction serves the structure pass, the rating engine, the dan inputs below and, when it is
            // lossless, the radar's row view. It is taken first so the radar can share it instead of walking the
            // beatmap a second time.
            EzChartMeasurements? measurements;

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Measurements))
                measurements = columnRuleset ? EzChartMeasurements.FromPlayable(analysisBeatmap, cancellationToken) : null;

            RadarAndXxy radarAndXxy;

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Radar))
                radarAndXxy = computeRadarAndXxy(lookup, analysisBeatmap, measurements?.Chart, cancellationToken);

            double? xxySr = radarAndXxy.XxySr;

            MinaResult? mina;

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Mina))
                mina = computeMinaAxes(rulesetOnlineId, measurements, rate, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            StructureResult? structure;

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Structure))
                structure = computeStructure(rulesetOnlineId, measurements, cancellationToken);

            PppResult? ppp;

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Ppp))
                ppp = computePpp(rulesetOnlineId, analysisBeatmap, lookup.OrderedMods);

            cancellationToken.ThrowIfCancellationRequested();

            if (columnRuleset)
                padColumnCounts(lookup, columnCounts, holdNoteCounts);

            var maniaSummary = new EzManiaSummary(columnCounts, holdNoteCounts, xxySr);

            IReadOnlyDictionary<EzMetricProducerId, IReadOnlyDictionary<string, double>> axes;
            EzDanSet? dan;

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Axes))
                axes = computeAxes(lookup, playable, analysisBeatmap, mina, structure?.Axes, ppp, xxySr, cancellationToken);

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Dan))
                dan = computeDan(rulesetOnlineId, measurements, mina?.Axes, structure?.Info, xxySr);

            var result = new EzAnalysisResult(commonSummary, maniaSummary, axes: axes, dan: dan, radar: radarAndXxy.Radar, chartSkillInfo: structure?.Info);

            EzAnalysisPassPerf.RecordPass(succeeded: true);
            EzAnalysisPassPerf.MaybeLog();

            return result;
        }

        private readonly record struct StructureResult(IReadOnlyDictionary<string, double> Axes, EzChartSkillInfo Info);

        /// <summary>
        /// The PP+ vector in both key spaces this pass needs: bare axis ids for the producer axis table, and the
        /// engine's stored skill ids for the performance formula, which addresses ratings by stored id.
        /// </summary>
        private readonly record struct PppResult(IReadOnlyDictionary<string, double> Axes, IReadOnlyDictionary<string, double> Skills);

        /// <summary>
        /// Every producer's axis values for this chart under the current mods, keyed by producer. Takes the products
        /// this pass already measured so no producer is computed twice.
        /// </summary>
        private static IReadOnlyDictionary<EzMetricProducerId, IReadOnlyDictionary<string, double>> computeAxes(
            in EzAnalysisLookupCache lookup, PlayableCachedWorkingBeatmap playable, IBeatmap analysisBeatmap,
            MinaResult? mina, IReadOnlyDictionary<string, double>? structure, PppResult? ppp,
            double? xxySr, CancellationToken cancellationToken)
        {
            var result = new Dictionary<EzMetricProducerId, IReadOnlyDictionary<string, double>>();

            if (mina != null)
                result[EzChartMetricProducer.Mina.Id()] = mina.Value.Axes;

            if (ppp != null)
                result[EzChartMetricProducer.Ppp.Id()] = ppp.Value.Axes;

            if (structure != null)
                result[EzChartMetricProducer.Csi.Id()] = structure;

            addOfficial(playable, analysisBeatmap, lookup, cancellationToken, result);
            addSsr(lookup, analysisBeatmap, xxySr, cancellationToken, result);
            addPppPerformance(lookup, analysisBeatmap, ppp, cancellationToken, result);

            return result;
        }

        /// <summary>
        /// Performance points implied by the PP+ chart vector, using the mode's own PP+ formula.
        /// </summary>
        private static void addPppPerformance(in EzAnalysisLookupCache lookup, IBeatmap analysisBeatmap,
                                              PppResult? ppp, CancellationToken cancellationToken,
                                              Dictionary<EzMetricProducerId, IReadOnlyDictionary<string, double>> result)
        {
            if (ppp is not { } values
                || !values.Axes.TryGetValue(EzMetricAxisIds.STAR_RATING, out double star)
                || !double.IsFinite(star) || star <= 0)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var scope = EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Ppp);
            double pp = EzPPPPerformanceCalculators.Calculate(lookup.Ruleset.OnlineID, values.Skills, analysisBeatmap,
                lookup.OrderedMods, getRateAdjustMultiplier(lookup.OrderedMods));

            if (!double.IsFinite(pp) || pp <= 0)
                return;

            result[EzChartMetricProducer.PppPerformance.Id()] = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzMetricAxisIds.PERFORMANCE_POINTS] = pp,
            };
        }

        /// <summary>
        /// The ruleset's perfect-play performance for an arbitrary star rating, via the ruleset analysis provider.
        /// </summary>
        private static double calculatePerformanceAtRating(in EzAnalysisLookupCache lookup, IBeatmap analysisBeatmap, double starRating,
                                                           CancellationToken cancellationToken)
            => EzAnalysisProviderBridge.TryGetValue(lookup.Ruleset,
                new EzAnalysisRequest(analysisBeatmap, getRateAdjustMultiplier(lookup.OrderedMods), EzAnalysisScope.PerformancePoints, starRating),
                EzAnalysisFields.PERFORMANCE_POINTS, cancellationToken, out double pp)
                ? pp
                : -1;

        /// <summary>
        /// Official difficulty and performance for the current mods. Both come from one difficulty calculation,
        /// which is why they are produced together rather than by two independent passes.
        /// </summary>
        private static void addOfficial(PlayableCachedWorkingBeatmap playable, IBeatmap analysisBeatmap, in EzAnalysisLookupCache lookup,
                                        CancellationToken cancellationToken, Dictionary<EzMetricProducerId, IReadOnlyDictionary<string, double>> result)
        {
            if (computeOfficial(playable, analysisBeatmap, lookup, cancellationToken) is not { } official)
                return;

            if (double.IsFinite(official.StarRating) && official.StarRating >= 0)
            {
                result[EzChartMetricProducer.Official.Id()] = new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    [EzMetricAxisIds.STAR_RATING] = official.StarRating,
                };
            }

            if (official.PerformancePoints is not double pp)
                return;

            result[EzChartMetricProducer.OfficialPerformance.Id()] = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzMetricAxisIds.PERFORMANCE_POINTS] = pp,
            };
        }

        private readonly record struct OfficialResult(double StarRating, double? PerformancePoints);

        /// <summary>
        /// The ruleset's own difficulty and perfect-play performance for the current mods.
        /// </summary>
        /// <remarks>
        /// The only place that builds the calculator/score-processor pair. The live pass and the songs-branch
        /// build both read the pair from here, so the same chart and mods cannot yield two different numbers.
        /// </remarks>
        private static OfficialResult? computeOfficial(PlayableCachedWorkingBeatmap playable, IBeatmap analysisBeatmap, in EzAnalysisLookupCache lookup,
                                                       CancellationToken cancellationToken)
        {
            using var scope = EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Official);
            var ruleset = lookup.Ruleset.CreateInstance();
            var difficulty = ruleset.CreateDifficultyCalculator(playable).Calculate(lookup.OrderedMods, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (!double.IsFinite(difficulty.StarRating) || difficulty.StarRating < 0)
                return null;

            var performanceCalculator = ruleset.CreatePerformanceCalculator();

            if (performanceCalculator == null)
                return new OfficialResult(difficulty.StarRating, null);

            ScoreProcessor scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = lookup.OrderedMods;
            scoreProcessor.ApplyBeatmap(analysisBeatmap);
            cancellationToken.ThrowIfCancellationRequested();

            ScoreInfo perfectScore = new ScoreInfo(lookup.BeatmapInfo, ruleset.RulesetInfo)
            {
                Passed = true,
                Accuracy = 1,
                Mods = lookup.OrderedMods,
                MaxCombo = scoreProcessor.MaximumCombo,
                Combo = scoreProcessor.MaximumCombo,
                TotalScore = scoreProcessor.MaximumTotalScore,
                Statistics = scoreProcessor.MaximumStatistics,
                MaximumStatistics = scoreProcessor.MaximumStatistics,
            };

            var performance = performanceCalculator.Calculate(perfectScore, difficulty);
            cancellationToken.ThrowIfCancellationRequested();

            double? pp = double.IsFinite(performance.Total) && performance.Total > 0 ? performance.Total : null;

            return new OfficialResult(difficulty.StarRating, pp);
        }

        /// <summary>
        /// Sunny star rating for the current mods. The rating is a single number, so it is stored under the same
        /// star axis id every other star-tier producer uses.
        /// </summary>
        private static void addSsr(in EzAnalysisLookupCache lookup, IBeatmap analysisBeatmap, double? xxySr, CancellationToken cancellationToken,
                                   Dictionary<EzMetricProducerId, IReadOnlyDictionary<string, double>> result)
        {
            if (xxySr is not double value || !double.IsFinite(value))
                return;

            result[EzChartMetricProducer.Ssr.Id()] = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzMetricAxisIds.STAR_RATING] = value,
            };

            double pp = calculatePerformanceAtRating(lookup, analysisBeatmap, value, cancellationToken);

            if (!double.IsFinite(pp) || pp <= 0)
                return;

            result[EzChartMetricProducer.SsrPerformance.Id()] = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzMetricAxisIds.PERFORMANCE_POINTS] = pp,
            };
        }

        /// <summary>
        /// Dan estimates for the sides this chart can be rated on, from the axis table and Sunny rating this pass
        /// already measured.
        /// </summary>
        private static EzDanSet? computeDan(int rulesetOnlineId, EzChartMeasurements? measurements,
                                            IReadOnlyDictionary<string, double>? bareAxes, EzChartSkillInfo? chartInfo, double? xxySr)
        {
            if (rulesetOnlineId != EzLocalProfileConstants.MANIA_RULESET_ID || bareAxes == null || measurements is not { } measured)
                return null;

            // The estimator addresses axes by their stored skill id.
            var axes = new Dictionary<string, double>(bareAxes.Count, StringComparer.Ordinal);

            foreach (var (axisId, value) in bareAxes)
                axes[$"{EzSkillSystems.BEATMAP_MINA}.{axisId}"] = value;

            return EzDanSet.Create(
                axes,
                measured.KeyCount,
                measured.HoldRatio,
                xxySr,
                chartInfo,
                holdCount: measured.HoldCount);
        }

        /// <summary>
        /// The one Mina measurement in the codebase. Every consumer (axes table, dan, radar, player aggregation)
        /// goes through here, so no second implementation of the engine call can drift from this one.
        /// </summary>
        internal static MinaResult? computeMinaAxes(int rulesetOnlineId, EzChartMeasurements? measurements, double rate)
            => computeMinaAxes(rulesetOnlineId, measurements, rate, CancellationToken.None);

        internal static MinaResult? computeMinaAxes(int rulesetOnlineId, EzChartMeasurements? measurements, double rate, CancellationToken cancellationToken)
        {
            if (rulesetOnlineId != EzLocalProfileConstants.MANIA_RULESET_ID || measurements is not { } measured)
                return null;

            int keyCount = measured.KeyCount;
            if (!EzMinaEngine.IsSupportedKeyCount(keyCount))
                return null;

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var engine = EzMinaEnginePool.Rent();
                var vector = engine.CalculateMsd(EzMinaNoteConverter.Convert(measured.Chart.Notes), keyCount, (float)rate);

                if (vector.Overall <= 0 && vector.Stream <= 0)
                    return null;

                var axes = new Dictionary<string, double>(StringComparer.Ordinal);

                foreach (var (axis, value) in vector.Enumerate())
                    axes[axis.ToId()] = value;

                return new MinaResult(axes, vector);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Bare-axis table plus the engine vector it came from.</summary>
        internal readonly record struct MinaResult(IReadOnlyDictionary<string, double> Axes, EzMinaSkillAxisVector Vector);

        /// <summary>
        /// PP+ chart vector for the current mods. Independent of the ruleset analysis provider: PP+ covers
        /// osu / taiko / catch / mania, only one of which exposes a provider.
        /// </summary>
        private static PppResult? computePpp(int rulesetOnlineId, IBeatmap analysisBeatmap, IReadOnlyList<Mod> mods)
        {
            if (!EzPPPSkillCatalog.SupportsRuleset(rulesetOnlineId))
                return null;

            double clockRate = ModUtils.CalculateRateWithMods(mods);

            if (!EzPPPCalculators.TryCalculate(rulesetOnlineId, analysisBeatmap, mods, clockRate, out var skills))
                return null;

            // Stored ids carry the system prefix; a producer's axis table addresses axes by bare id. The
            // performance formula addresses them by stored id, so both shapes leave this method together.
            var axes = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var (skillId, value) in skills)
            {
                if (EzMetricSkillId.TrySplit(skillId, out _, out string axisId))
                    axes[axisId] = value;
            }

            return new PppResult(axes, skills);
        }

        /// <summary>
        /// Chart structure values: the drawn long-note measures plus the pattern and subtype scores, together with
        /// the typed summary the same pass produced. Rate is not folded in; the values are rate-invariant.
        /// </summary>
        private static StructureResult? computeStructure(int rulesetOnlineId, EzChartMeasurements? measurements)
            => computeStructure(rulesetOnlineId, measurements, CancellationToken.None);

        private static StructureResult? computeStructure(int rulesetOnlineId, EzChartMeasurements? measurements, CancellationToken cancellationToken)
        {
            if (rulesetOnlineId != EzLocalProfileConstants.MANIA_RULESET_ID || measurements is not { } measured)
                return null;

            var axes = EzChartSkillInfoComputer.ComputeAxes(measured.Chart, cancellationToken, out var info);

            return new StructureResult(axes, info);
        }

        private readonly record struct RadarAndXxy(double? XxySr, EzRulesetSpecificRadarResult? Radar);

        /// <summary>
        /// The Sunny rating and the ruleset radar from one provider call.
        /// </summary>
        /// <remarks>
        /// The radar payload already carries the rating the provider measured, so asking for the two scopes in
        /// separate calls ran the ruleset's difficulty core twice for the same chart and mods.
        /// </remarks>
        private static RadarAndXxy computeRadarAndXxy(in EzAnalysisLookupCache lookup, IBeatmap analysisBeatmap, EzManiaChartInput? chart,
                                                       CancellationToken cancellationToken)
        {
            bool supportsXxy = EzSunnyStarRatingSupport.SupportsBeatmap(analysisBeatmap, lookup.Ruleset);

            var scopes = EzAnalysisScope.RulesetSpecificRadarData
                         | (supportsXxy ? EzAnalysisScope.XxySr : EzAnalysisScope.None);

            var request = new EzAnalysisRequest(analysisBeatmap, getRateAdjustMultiplier(lookup.OrderedMods), scopes, Chart: chart);

            if (!EzAnalysisProviderBridge.TryCompute(lookup.Ruleset, request, cancellationToken, out IEzAnalysis analysis))
                return default;

            double? xxySr = null;

            if (supportsXxy)
            {
                if (analysisBeatmap.HitObjects.Count == 0)
                    xxySr = 0;
                else if (analysis.TryGetValue(EzAnalysisFields.XXY_SR, out double sr) && double.IsFinite(sr) && sr >= 0)
                    xxySr = sr;
            }

            EzRulesetSpecificRadarResult? radar = analysis.TryGetValue(EzAnalysisFields.RULESET_SPECIFIC_RADAR_RESULT, out EzRulesetSpecificRadarResult value)
                ? value
                : null;

            return new RadarAndXxy(xxySr, radar);
        }

        /// <summary>
        /// 把稀疏列统计补 0 到真实列数（仅 Mania 分析路径；0 note 谱面的空字典不补）。
        /// </summary>
        private static void padColumnCounts(in EzAnalysisLookupCache lookup, Dictionary<int, int> columnCounts, Dictionary<int, int> holdNoteCounts)
        {
            int realColumnCount = EzManiaColumnLayout.ResolveRealColumnCount(lookup.Ruleset, lookup.BeatmapInfo, lookup.OrderedMods);
            EzManiaColumnLayout.PadToRealColumnCount(columnCounts, holdNoteCounts, realColumnCount);
        }

        public static bool TryComputeRulesetSpecificRadarData(WorkingBeatmap workingBeatmap, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken,
                                                              out EzRulesetSpecificRadarResult radarResult)
        {
            radarResult = default;

            IBeatmap analysisBeatmap = EzPlayableBeatmapCache.GetShared(workingBeatmap, lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            double rate = getRateAdjustMultiplier(lookup.OrderedMods);

            return EzAnalysisProviderBridge.TryGetValue(lookup.Ruleset, new EzAnalysisRequest(analysisBeatmap, rate), EzAnalysisFields.RULESET_SPECIFIC_RADAR_RESULT,
                cancellationToken, out radarResult);
        }

        private static double getRateAdjustMultiplier(IReadOnlyList<Mod> mods)
        {
            try
            {
                double rate = 1.0;

                for (int i = 0; i < mods.Count; i++)
                {
                    if (mods[i] is IApplicableToRate applicableToRate)
                        rate = applicableToRate.ApplyToRate(0, rate);
                }

                if (double.IsNaN(rate) || double.IsInfinity(rate) || rate <= 0)
                    return 1.0;

                return rate;
            }
            catch
            {
                return 1.0;
            }
        }

        private static bool tryComputeOperationValues(PlayableCachedWorkingBeatmap playableWorkingBeatmap, IBeatmap analysisBeatmap,
                                                      in EzAnalysisLookupCache lookup, CancellationToken cancellationToken,
                                                      out double? xxySr, out double? pp)
        {
            pp = tryComputePerfectPp(playableWorkingBeatmap, analysisBeatmap, lookup, cancellationToken);

            return tryComputeXxySr(analysisBeatmap, lookup, cancellationToken, out xxySr) || pp != null;
        }

        private static bool tryComputeXxySr(IBeatmap analysisBeatmap, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken, out double? xxySr)
        {
            xxySr = null;

            if (!EzSunnyStarRatingSupport.SupportsBeatmap(analysisBeatmap, lookup.Ruleset))
                return false;

            if (analysisBeatmap.HitObjects.Count == 0)
            {
                xxySr = 0;
                return true;
            }

            double rate = getRateAdjustMultiplier(lookup.OrderedMods);

            if (EzAnalysisProviderBridge.TryGetValue(lookup.Ruleset, new EzAnalysisRequest(analysisBeatmap, rate), EzAnalysisFields.XXY_SR, cancellationToken, out double sr))
                xxySr = sr;

            return xxySr != null;
        }

        private static double? tryComputePerfectPp(PlayableCachedWorkingBeatmap playableWorkingBeatmap, IBeatmap analysisBeatmap,
                                                   in EzAnalysisLookupCache lookup, CancellationToken cancellationToken)
            => computeOfficial(playableWorkingBeatmap, analysisBeatmap, lookup, cancellationToken)?.PerformancePoints;

        private class PlayableCachedWorkingBeatmap : IWorkingBeatmap
        {
            private readonly IWorkingBeatmap working;
            private IBeatmap? playable;

            public PlayableCachedWorkingBeatmap(IWorkingBeatmap working)
            {
                this.working = working;
            }

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods) => playable ??= working.GetPlayableBeatmap(ruleset, mods);

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken) =>
                playable ??= working.GetPlayableBeatmap(ruleset, mods, cancellationToken);

            IBeatmapInfo IWorkingBeatmap.BeatmapInfo => working.BeatmapInfo;
            bool IWorkingBeatmap.BeatmapLoaded => working.BeatmapLoaded;
            bool IWorkingBeatmap.TrackLoaded => working.TrackLoaded;
            IBeatmap IWorkingBeatmap.Beatmap => working.Beatmap;
            Texture IWorkingBeatmap.GetBackground() => working.GetBackground();
            Texture IWorkingBeatmap.GetPanelBackground() => working.GetPanelBackground();
            Waveform IWorkingBeatmap.Waveform => working.Waveform;
            Storyboard IWorkingBeatmap.Storyboard => working.Storyboard;
            ISkin IWorkingBeatmap.Skin => working.Skin;
            Track IWorkingBeatmap.Track => working.Track;
            Track IWorkingBeatmap.LoadTrack() => working.LoadTrack();
            Stream IWorkingBeatmap.GetStream(string storagePath) => working.GetStream(storagePath);
            void IWorkingBeatmap.BeginAsyncLoad() => working.BeginAsyncLoad();
            void IWorkingBeatmap.CancelAsyncLoad() => working.CancelAsyncLoad();
            void IWorkingBeatmap.PrepareTrackForPreview(bool looping, double? offsetFromPreviewPoint) => working.PrepareTrackForPreview(looping, offsetFromPreviewPoint);
        }
    }
}
