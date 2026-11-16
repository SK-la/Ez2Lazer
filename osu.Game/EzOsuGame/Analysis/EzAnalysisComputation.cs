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
using osu.Game.EzOsuGame.Mods;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.EzOsuGame.Skills.SkillTag.KeyPattern;
using osu.Game.EzOsuGame.Skills.SSR;
using osu.Game.EzOsuGame.Skills.SSR.SR4Pattern;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osu.Game.Utils;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// L3 pass: one <see cref="EzAnalysisResult"/> for a chart + mods.
    /// When mods allow, L1 Sunny star (Realm) is chosen before Dan and scalar ratings so those fields share the headline;
    /// <see cref="EzAnalysisResult.SsrPatternRadar"/> stays the live snapshot.
    /// KPS/KPC NoMod slices are <c>ComputePersistedSqliteSlice</c>, not this method.
    /// </summary>
    // 不主动释放 workingCache，让框架自行决定。主动释放会频繁触发大量 Invalidating 日志。
    internal static class EzAnalysisComputation
    {
        public static bool TryComputePerformancePoints(BeatmapManager beatmapManager, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken, out double performancePoints)
        {
            performancePoints = 0;

            IWorkingBeatmap workingBeatmap = beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo);
            IBeatmap analysisBeatmap = workingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (tryComputePerfectPp(workingBeatmap, analysisBeatmap, lookup, cancellationToken) is not double resolvedPp)
                return false;

            performancePoints = resolvedPp;
            return true;
        }

        public static bool TryComputeSunnySR(BeatmapManager beatmapManager, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken, out double sunnySR)
        {
            sunnySR = 0;

            IBeatmap analysisBeatmap = beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo)
                .GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (!tryComputeSunnySR(analysisBeatmap, lookup, cancellationToken, out double? computedSunnySR)
                || computedSunnySR is not double resolvedSunnySR)
                return false;

            sunnySR = resolvedSunnySR;
            return true;
        }

        /// <summary>
        /// Same ssrpath as song-select analysis panel, but reuses an already-built playable
        /// (e.g. live ChartDan after key/rate mods). Does not write Realm.
        /// </summary>
        public static bool TryComputeSunnySRFromPlayable(
            IBeatmap playable,
            IRulesetInfo ruleset,
            IReadOnlyList<Mod> mods,
            CancellationToken cancellationToken,
            out double sunnySR)
        {
            sunnySR = 0;

            if (!EzSSRUnit.SupportsRuleset(ruleset.OnlineID))
                return false;

            if (playable.HitObjects.Count == 0)
                return true;

            if (computeSunnySR(playable, getRateAdjustMultiplier(mods), cancellationToken) is not double sr)
                return false;

            sunnySR = sr;
            return true;
        }

        /// <summary>
        /// Computes baseline NoMod SunnySR for persisting to the Sunny star slot.
        /// Returns -1 when unsupported or failed, 0 for empty beatmaps.
        /// </summary>
        public static double ComputeBaselineSunnySRForRealm(BeatmapManager beatmapManager, BeatmapInfo beatmapInfo, CancellationToken cancellationToken = default)
        {
            var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);

            if (!EzSSRUnit.SupportsRuleset(beatmapInfo.Ruleset.OnlineID))
                return -1;

            if (working.Beatmap.HitObjects.Count == 0)
                return 0;

            var lookup = new EzAnalysisLookupCache(beatmapInfo, beatmapInfo.Ruleset, mods: null);

            return TryComputeSunnySR(beatmapManager, lookup, cancellationToken, out double sunnySR)
                ? sunnySR
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
            IWorkingBeatmap playable = beatmapManager.GetWorkingBeatmap(beatmapInfo);
            IBeatmap analysisBeatmap = playable.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            return computeBaselinePerformancePoints(playable, analysisBeatmap, lookup, starRating, cancellationToken);
        }

        /// <summary>
        /// Same value from a chart the caller already converted, so measuring a rating and scoring it do not convert
        /// the chart twice.
        /// </summary>
        /// <remarks>
        /// The caller's instance must be one it owns (a bound cache entry, not a shared one): scoring binds
        /// judgements in place.
        /// </remarks>
        internal static double ComputeBaselinePerformancePointsForRating(BeatmapManager beatmapManager, BeatmapInfo beatmapInfo, double starRating, IBeatmap analysisBeatmap,
                                                                         CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(analysisBeatmap);

            if (!double.IsFinite(starRating) || starRating <= 0)
                return -1;

            var lookup = new EzAnalysisLookupCache(beatmapInfo, beatmapInfo.Ruleset, mods: null);
            var playable = new SeededPlayableWorkingBeatmap(beatmapManager.GetWorkingBeatmap(beatmapInfo), analysisBeatmap);

            return computeBaselinePerformancePoints(playable, analysisBeatmap, lookup, starRating, cancellationToken);
        }

        private static double computeBaselinePerformancePoints(IWorkingBeatmap playable, IBeatmap analysisBeatmap, in EzAnalysisLookupCache lookup, double starRating,
                                                               CancellationToken cancellationToken)
            => analysisBeatmap.HitObjects.Count == 0
                ? -1
                : calculatePerformanceAtRating(playable, analysisBeatmap, lookup, starRating, cancellationToken);

        /// <summary>
        /// The official star and perfect-play pp, plus the pp implied by <paramref name="starRating"/>, from one
        /// difficulty calculation on a chart the caller already converted.
        /// </summary>
        /// <remarks>
        /// The official pair is what the rating-substituted pp is scored from: computing it twice would repeat the
        /// whole performance calculation for a number the caller is already holding. Each half is <c>-1</c> when the
        /// ruleset cannot express it.
        /// </remarks>
        internal static (double Star, double Pp, double SubstitutedPp) ComputeOfficialBaseline(BeatmapManager beatmapManager, BeatmapInfo beatmapInfo,
                                                                                               double starRating, IBeatmap analysisBeatmap,
                                                                                               CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(analysisBeatmap);

            if (!double.IsFinite(starRating) || starRating <= 0 || analysisBeatmap.HitObjects.Count == 0)
                return (-1, -1, -1);

            var lookup = new EzAnalysisLookupCache(beatmapInfo, beatmapInfo.Ruleset, mods: null);
            var playable = new SeededPlayableWorkingBeatmap(beatmapManager.GetWorkingBeatmap(beatmapInfo), analysisBeatmap);

            if (computeOfficial(playable, analysisBeatmap, lookup, cancellationToken) is not { Calculator: { } calculator, Score: { } score, Attributes: { } attributes } official)
                return (-1, -1, -1);

            double substituted = performanceAtRating(calculator, score, attributes, starRating) ?? -1;
            return (official.StarRating, official.PerformancePoints ?? -1, substituted);
        }

        /// <summary>
        /// The official star and perfect-play pp for a chart, from one difficulty calculation.
        /// </summary>
        /// <remarks>
        /// One pass, because both come from the same calculation: asking for them separately converted the chart, ran
        /// the difficulty calculator and scored the performance twice for every chart in the library.
        /// </remarks>
        internal static bool TryComputeOfficialBaseline(BeatmapManager beatmapManager, BeatmapInfo beatmapInfo, CancellationToken cancellationToken,
                                                        out double starRating, out double performancePoints)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            starRating = -1;
            performancePoints = -1;

            var lookup = new EzAnalysisLookupCache(beatmapInfo, beatmapInfo.Ruleset, mods: null);
            IWorkingBeatmap playable = beatmapManager.GetWorkingBeatmap(beatmapInfo);
            IBeatmap analysisBeatmap = playable.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            if (computeOfficial(playable, analysisBeatmap, lookup, cancellationToken) is not { } official)
                return false;

            starRating = official.StarRating;
            performancePoints = official.PerformancePoints ?? -1;

            return true;
        }

        /// <summary>
        /// Computes the baseline performance points implied by the stored Sunny star rating, for persisting to the
        /// Sunny pp slot. Returns -1 when the ruleset cannot express it.
        /// </summary>
        public static double ComputeBaselineSunnyPerformancePointsForRealm(BeatmapManager beatmapManager, BeatmapInfo beatmapInfo,
                                                                           CancellationToken cancellationToken = default)
            => ComputeBaselinePerformancePointsForRating(beatmapManager, beatmapInfo, beatmapInfo.EzRatingData.SunnySR, cancellationToken);

        public static bool TryComputeSunnySRAndPp(BeatmapManager beatmapManager, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken,
                                                  out double? sunnySR, out double? pp)
        {
            IWorkingBeatmap workingBeatmap = beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo);
            IBeatmap analysisBeatmap = workingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            return tryComputeOperationValues(workingBeatmap, analysisBeatmap, lookup, cancellationToken, out sunnySR, out pp);
        }

        /// <summary>
        /// Mod-sensitive mirrored scalars and axis rows for a songs branch snapshot. CSI / dan stay on the NoMod Realm projection.
        /// </summary>
        internal readonly record struct ModSensitiveBranchMirror(
            double? OfficialSr,
            double? MinaSr,
            double? MinaPp,
            double? PppSr,
            double? PppPp,
            IReadOnlyDictionary<string, double>? MinaAxes,
            IReadOnlyDictionary<string, double>? PppAxes);

        internal static bool TryComputeModSensitiveBranchMirror(
            BeatmapManager beatmapManager,
            in EzAnalysisLookupCache lookup,
            CancellationToken cancellationToken,
            out ModSensitiveBranchMirror mirror)
        {
            mirror = default;

            try
            {
                IWorkingBeatmap playable = beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo);
                IBeatmap analysisBeatmap = playable.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                double? officialSr = null;

                if (computeOfficial(playable, analysisBeatmap, lookup, cancellationToken) is { StarRating: var osr }
                    && double.IsFinite(osr) && osr >= 0)
                {
                    officialSr = osr;
                }

                double? pppSr = null;
                double? pppPp = null;
                IReadOnlyDictionary<string, double>? pppAxes = null;

                if (computePpp(lookup.Ruleset.OnlineID, analysisBeatmap, lookup.OrderedMods) is { } ppp)
                {
                    pppAxes = ppp.Axes;

                    if (ppp.Axes.TryGetValue(EzPPPSkillCatalog.STAR_RATING, out double star) && double.IsFinite(star) && star > 0)
                    {
                        pppSr = star;

                        double pp = EzPPPPerformanceCalculators.Calculate(lookup.Ruleset.OnlineID, ppp.Skills, analysisBeatmap,
                            lookup.OrderedMods, getRateAdjustMultiplier(lookup.OrderedMods));

                        if (double.IsFinite(pp) && pp > 0)
                            pppPp = pp;
                    }
                }

                double? minaSr = null;
                IReadOnlyDictionary<string, double>? minaAxes = null;

                if (lookup.Ruleset.OnlineID == EzLocalProfileConstants.MANIA_RULESET_ID)
                {
                    var measurements = EzChartMeasurements.FromPlayable(analysisBeatmap, cancellationToken);

                    if (ComputeMinaAxes(lookup.Ruleset.OnlineID, measurements, getRateAdjustMultiplier(lookup.OrderedMods), cancellationToken) is { } mina)
                    {
                        minaAxes = mina.Axes;

                        if (mina.Vector.Overall > 0)
                            minaSr = EzMinaStarRatingScale.ToStarRating(mina.Vector.Overall);
                    }
                }

                mirror = new ModSensitiveBranchMirror(officialSr, minaSr, null, pppSr, pppPp, minaAxes, pppAxes);
                return officialSr != null || minaSr != null || pppSr != null || pppAxes != null || minaAxes != null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        public static EzAnalysisResult Compute(
            BeatmapManager beatmapManager,
            in EzAnalysisLookupCache lookup,
            CancellationToken cancellationToken = default)
            => Compute(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo), lookup, cancellationToken);

        /// <summary>
        /// 主 SQLite 预热/回填专用：仅计算 kps 与 mania 列统计（NoMod），不写 ssr/PP（由 Realm 维护）。
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
                analysisBeatmap = workingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

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
                maniaSummary = new EzManiaSummary(columnCounts, holdNoteCounts, sunnySR: null);
            }

            return new EzAnalysisResult(commonSummary, maniaSummary);
        }

        public static EzAnalysisResult Compute(
            WorkingBeatmap workingBeatmap,
            in EzAnalysisLookupCache lookup,
            CancellationToken cancellationToken = default)
        {
            // The engines that produce skill axes are independent of a ruleset exposing an analysis provider, so
            // the pass always measures them. Products that do need the provider (official performance, the Sunny
            // rating and the ruleset radar) are guarded where they are produced.
            int rulesetOnlineId = lookup.Ruleset.OnlineID;
            bool columnRuleset = rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID;

            IBeatmap analysisBeatmap;

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Convert))
                analysisBeatmap = workingBeatmap.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

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

            SsrAndRadar ssrAndRadar;

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Radar))
                ssrAndRadar = computeSsrAndRadar(lookup, analysisBeatmap, measurements, cancellationToken);

            double? sunnySR = persistedSunnyStar(lookup) ?? ssrAndRadar.SunnySR;

            MinaResult? mina;

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Mina))
                mina = ComputeMinaAxes(rulesetOnlineId, measurements, rate, cancellationToken);

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

            var maniaSummary = new EzManiaSummary(columnCounts, holdNoteCounts, sunnySR);

            IReadOnlyDictionary<EzAnalysisSystem, IReadOnlyDictionary<string, double>> axes;
            IReadOnlyDictionary<EzAnalysisSystem, EzScalarRating> ratings;
            EzDanSet? dan;

            EzRadarChartData<string>? quantifiedKeyPattern = ssrAndRadar.KeyPatternRadar;

            if (quantifiedKeyPattern is { } relativeRadar && sunnySR is double quantifiedSunny && EzKeyPatternUnit.IsQuantifiableSunny(quantifiedSunny))
                quantifiedKeyPattern = EzKeyPatternUnit.Quantify(relativeRadar, quantifiedSunny);

            EzRadarChartData<string>? keyPatternRadar = quantifiedKeyPattern is { } persistable
                ? EzKeyPatternRadar.WithHoldRatio(persistable, measurements?.HoldRatio ?? 0)
                : null;

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Axes))
            {
                var computed = computeAxes(lookup, workingBeatmap, analysisBeatmap, mina, structure?.Axes, ppp, sunnySR, cancellationToken);
                axes = withKeyPatternAxes(computed.Axes, quantifiedKeyPattern);
                ratings = computed.Ratings;
            }

            using (EzAnalysisPassPerf.Measure(EzAnalysisPassPerf.Stage.Dan))
                dan = computeDan(rulesetOnlineId, measurements, mina?.Axes, structure?.Info, sunnySR);

            var result = new EzAnalysisResult(commonSummary, maniaSummary, axes: axes, ratings: ratings, dan: dan,
                keyPatternRadar: keyPatternRadar, ssrPatternRadar: ssrAndRadar.SsrPatternRadar, chartSkillInfo: structure?.Info);

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
        /// Every producer's vector axes and scalar ratings for this chart under the current mods, keyed by system.
        /// Takes the products this pass already measured so no producer is computed twice.
        /// </summary>
        private static AxesResult computeAxes(
            in EzAnalysisLookupCache lookup, IWorkingBeatmap playable, IBeatmap analysisBeatmap,
            MinaResult? mina, IReadOnlyDictionary<string, double>? structure, PppResult? ppp,
            double? sunnySR, CancellationToken cancellationToken)
        {
            var axes = new Dictionary<EzAnalysisSystem, IReadOnlyDictionary<string, double>>();
            var ratings = new Dictionary<EzAnalysisSystem, EzScalarRating>();

            if (mina != null)
                axes[EzAnalysisSystem.Mina] = mina.Value.Axes;

            if (structure != null)
                axes[EzAnalysisSystem.CSI] = structure;

            if (ppp != null)
                addPppChart(ppp.Value, axes, ratings);

            OfficialResult? official = addOfficial(playable, analysisBeatmap, lookup, cancellationToken, ratings);
            addSsr(sunnySR, official, ratings);
            addPppPerformance(lookup, analysisBeatmap, ppp, cancellationToken, ratings);

            return new AxesResult(axes, ratings);
        }

        private readonly record struct AxesResult(
            IReadOnlyDictionary<EzAnalysisSystem, IReadOnlyDictionary<string, double>> Axes,
            IReadOnlyDictionary<EzAnalysisSystem, EzScalarRating> Ratings);

        private static IReadOnlyDictionary<EzAnalysisSystem, IReadOnlyDictionary<string, double>> withKeyPatternAxes(
            IReadOnlyDictionary<EzAnalysisSystem, IReadOnlyDictionary<string, double>> axes,
            EzRadarChartData<string>? keyPatternRadar)
        {
            if (keyPatternRadar == null)
                return axes;

            var copy = new Dictionary<EzAnalysisSystem, IReadOnlyDictionary<string, double>>(axes)
            {
                [EzAnalysisSystem.KeyPattern] = EzKeyPatternUnit.ToSkills(keyPatternRadar.Value),
            };
            return copy;
        }

        /// <summary>
        /// The PP+ chart vector, with its star separated out: the engine reports the star beside the vector axes,
        /// but a star is one number and belongs in the rating slot rather than in the axis table.
        /// </summary>
        private static void addPppChart(
            in PppResult ppp,
            Dictionary<EzAnalysisSystem, IReadOnlyDictionary<string, double>> axes,
            Dictionary<EzAnalysisSystem, EzScalarRating> ratings)
        {
            if (!ppp.Axes.TryGetValue(EzPPPSkillCatalog.STAR_RATING, out double star))
            {
                axes[EzAnalysisSystem.PPP] = ppp.Axes;
                return;
            }

            var vector = new Dictionary<string, double>(ppp.Axes.Count - 1, StringComparer.Ordinal);

            foreach (var (axisId, value) in ppp.Axes)
            {
                if (axisId != EzPPPSkillCatalog.STAR_RATING)
                    vector[axisId] = value;
            }

            axes[EzAnalysisSystem.PPP] = vector;
            ratings[EzAnalysisSystem.PPP] = new EzScalarRating(star, null);
        }

        /// <summary>
        /// Performance points implied by the PP+ chart vector, using the mode's own PP+ formula.
        /// </summary>
        private static void addPppPerformance(in EzAnalysisLookupCache lookup, IBeatmap analysisBeatmap,
                                              PppResult? ppp, CancellationToken cancellationToken,
                                              Dictionary<EzAnalysisSystem, EzScalarRating> ratings)
        {
            if (ppp is not { } values
                || !values.Axes.TryGetValue(EzPPPSkillCatalog.STAR_RATING, out double star)
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

            ratings[EzAnalysisSystem.PPP] = ratings.TryGetValue(EzAnalysisSystem.PPP, out var existing)
                ? existing with { PerformancePoints = pp }
                : new EzScalarRating(null, pp);
        }

        /// <summary>
        /// The ruleset's perfect-play performance for an arbitrary star rating.
        /// </summary>
        private static double calculatePerformanceAtRating(IWorkingBeatmap playable, IBeatmap analysisBeatmap, in EzAnalysisLookupCache lookup,
                                                           double starRating, CancellationToken cancellationToken)
        {
            if (computeOfficial(playable, analysisBeatmap, lookup, cancellationToken) is not { Calculator: { } calculator, Score: { } score, Attributes: { } attributes })
                return -1;

            return performanceAtRating(calculator, score, attributes, starRating) ?? -1;
        }

        /// <summary>
        /// Official difficulty and performance for the current mods. Both come from one difficulty calculation,
        /// which is why they are produced together rather than by two independent passes.
        /// </summary>
        private static OfficialResult? addOfficial(IWorkingBeatmap playable, IBeatmap analysisBeatmap, in EzAnalysisLookupCache lookup,
                                                   CancellationToken cancellationToken, Dictionary<EzAnalysisSystem, EzScalarRating> ratings)
        {
            if (computeOfficial(playable, analysisBeatmap, lookup, cancellationToken) is not { } official)
                return null;

            if (!double.IsFinite(official.StarRating) || official.StarRating < 0)
                return null;

            ratings[EzAnalysisSystem.Official] = new EzScalarRating(official.StarRating, official.PerformancePoints);
            return official;
        }

        /// <summary>
        /// One difficulty calculation together with the inputs a substituted rating is scored with.
        /// </summary>
        private readonly record struct OfficialResult(double StarRating, double? PerformancePoints, DifficultyAttributes Attributes, PerformanceCalculator? Calculator, ScoreInfo? Score);

        /// <summary>
        /// The ruleset's own difficulty and perfect-play performance for the current mods.
        /// </summary>
        /// <remarks>
        /// The only place that builds the calculator/score-processor pair. The live pass and the songs-branch
        /// build both read the pair from here, so the same chart and mods cannot yield two different numbers.
        /// </remarks>
        private static OfficialResult? computeOfficial(IWorkingBeatmap playable, IBeatmap analysisBeatmap, in EzAnalysisLookupCache lookup,
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
                return new OfficialResult(difficulty.StarRating, null, difficulty, null, null);

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

            return new OfficialResult(difficulty.StarRating, pp, difficulty, performanceCalculator, perfectScore);
        }

        /// <summary>
        /// L1 Sunny star when this request may use the Realm baseline. Null means the live L3 star stays.
        /// </summary>
        private static double? persistedSunnyStar(in EzAnalysisLookupCache lookup)
        {
            if (!EzModRate.CanUsePersistedSunnySR(lookup.OrderedMods))
                return null;

            double stored = lookup.BeatmapInfo.EzRatingData.SunnySR;

            if (stored < 0 || !double.IsFinite(stored) || stored <= 0)
                return null;

            return stored;
        }

        /// <summary>
        /// Sunny star rating and the pp it implies for the current mods. A star is one number, not an axis.
        /// </summary>
        private static void addSsr(double? sunnySR, OfficialResult? official, Dictionary<EzAnalysisSystem, EzScalarRating> ratings)
        {
            if (sunnySR is not double value || !double.IsFinite(value))
                return;

            double? performance = official is { Calculator: { } calculator, Score: { } score, Attributes: { } attributes }
                ? performanceAtRating(calculator, score, attributes, value)
                : null;

            ratings[EzAnalysisSystem.SSR] = new EzScalarRating(value, performance);
        }

        /// <summary>
        /// Dan estimates for the sides this chart can be rated on, from the axis table and the Sunny headline
        /// already resolved for this pass (L1 overlay when allowed).
        /// </summary>
        private static EzDanSet? computeDan(int rulesetOnlineId, EzChartMeasurements? measurements,
                                            IReadOnlyDictionary<string, double>? bareAxes, EzChartSkillInfo? chartInfo, double? sunnySR)
        {
            if (rulesetOnlineId != EzLocalProfileConstants.MANIA_RULESET_ID || bareAxes == null || measurements is not { } measured)
                return null;

            // The estimator addresses axes by bare axis id; the prefix is now the system column.
            var axes = new Dictionary<string, double>(bareAxes.Count, StringComparer.Ordinal);

            foreach (var (axisId, value) in bareAxes)
                axes[axisId] = value;

            return EzDanSet.Create(
                axes,
                measured.KeyCount,
                measured.HoldRatio,
                sunnySR,
                chartInfo,
                holdCount: measured.HoldCount);
        }

        /// <summary>
        /// The one Mina measurement in the codebase. Every consumer (axes table, dan, radar, player aggregation)
        /// goes through here, so no second implementation of the engine call can drift from this one.
        /// </summary>
        internal static MinaResult? ComputeMinaAxes(int rulesetOnlineId, EzChartMeasurements? measurements, double rate)
            => ComputeMinaAxes(rulesetOnlineId, measurements, rate, CancellationToken.None);

        internal static MinaResult? ComputeMinaAxes(int rulesetOnlineId, EzChartMeasurements? measurements, double rate, CancellationToken cancellationToken)
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
                var vector = engine.CalculateChartMinaSkillAxis(EzMinaNoteConverter.Convert(measured.Chart.Notes), keyCount, (float)rate);

                if (vector.Overall <= 0 && vector.Stream <= 0)
                    return null;

                var axes = new Dictionary<string, double>(StringComparer.Ordinal);

                foreach (var (axis, value) in vector.Enumerate())
                    axes[axis.ToMetaId()] = value;

                return new MinaResult(axes, vector);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Bare-axis table plus the engine vector it came from.</summary>
        internal readonly record struct MinaResult(IReadOnlyDictionary<string, double> Axes, EzMinaSkillAxisData Vector);

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

            // Axis ids are bare now: the system is a column, not a prefix, so the table and the stored rows
            // address an axis by the same string.
            var axes = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var (metricId, value) in skills)
                axes[metricId] = value;

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

            var axes = EzCSIEngine.ComputeAxes(measured.Chart, cancellationToken, out var info);

            return new StructureResult(axes, info);
        }

        private readonly record struct SsrAndRadar(double? SunnySR, EzRadarChartData<string>? KeyPatternRadar, EzRadarChartData<string>? SsrPatternRadar);

        /// <summary>
        /// The Sunny rating and the two chart views this pass can draw, from one derivation of the chart.
        /// </summary>
        /// <remarks>
        /// The rating and its pattern breakdown come from one engine call, so asking for both does not run the
        /// difficulty core twice for the same chart and mods.
        /// </remarks>
        private static SsrAndRadar computeSsrAndRadar(
            in EzAnalysisLookupCache lookup,
            IBeatmap analysisBeatmap,
            EzChartMeasurements? measurements,
            CancellationToken cancellationToken)
        {
            if (!EzSSRUnit.SupportsRuleset(lookup.Ruleset.OnlineID) || measurements is not { } measured)
                return default;

            var chart = measured.Chart;
            int keyCount = measured.KeyCount;

            EzRadarChartData<string>? keyPatternRadar = EzKeyPatternUnit.ComputeRadar(chart, cancellationToken);

            if (!EzSSREngine.IsPatternSupported(keyCount))
                return new SsrAndRadar(null, keyPatternRadar, null);

            if (chart.Notes.Count == 0)
                return new SsrAndRadar(0, keyPatternRadar, null);

            // L3 SunnySRSkill spokes always from this live snapshot; L1 Sunny star is resolved later in Compute.
            var combined = EzSSREngine.CalculateSRWithPatternSnapshot(chart, keyCount, analysisBeatmap.Difficulty.OverallDifficulty,
                getRateAdjustMultiplier(lookup.OrderedMods));

            double? sunnySR = double.IsFinite(combined.Sr) && combined.Sr >= 0 ? combined.Sr : null;

            return new SsrAndRadar(sunnySR, keyPatternRadar, EzManiaSR4PatternRadarBuilder.Create(combined.PatternSnapshot));
        }

        /// <summary>
        /// 把稀疏列统计补 0 到真实列数（仅 Mania 分析路径；0 note 谱面的空字典不补）。
        /// </summary>
        private static void padColumnCounts(in EzAnalysisLookupCache lookup, Dictionary<int, int> columnCounts, Dictionary<int, int> holdNoteCounts)
        {
            int realColumnCount = EzManiaColumnLayout.ResolveRealColumnCount(lookup.Ruleset, lookup.BeatmapInfo, lookup.OrderedMods);
            EzManiaColumnLayout.PadToRealColumnCount(columnCounts, holdNoteCounts, realColumnCount);
        }

        /// <summary>
        /// The Sunny rating of an already-built playable, or null when the chart has no layout this engine rates.
        /// </summary>
        private static double? computeSunnySR(IBeatmap playable, double rate, CancellationToken cancellationToken)
        {
            int keyCount = EzHoldMetrics.ResolveKeyCount(playable);

            if (!EzSSREngine.IsPatternSupported(keyCount))
                return null;

            var chart = EzCSIEngine.FromPlayable(playable, cancellationToken);
            double sr = EzSSREngine.CalculateSR(chart, keyCount, playable.Difficulty.OverallDifficulty, rate);

            return double.IsFinite(sr) && sr >= 0 ? sr : null;
        }

        /// <summary>
        /// The ruleset's perfect-play performance at a substituted rating, reusing the attributes the official pass
        /// already produced. The star is restored afterwards so the shared attributes still describe the chart.
        /// </summary>
        private static double? performanceAtRating(PerformanceCalculator calculator, ScoreInfo perfectScore, DifficultyAttributes attributes, double starRating)
        {
            double original = attributes.StarRating;
            attributes.StarRating = starRating;

            try
            {
                var performance = calculator.Calculate(perfectScore, attributes);
                return double.IsFinite(performance.Total) && performance.Total > 0 ? performance.Total : null;
            }
            finally
            {
                attributes.StarRating = original;
            }
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

        private static bool tryComputeOperationValues(IWorkingBeatmap playableWorkingBeatmap, IBeatmap analysisBeatmap,
                                                      in EzAnalysisLookupCache lookup, CancellationToken cancellationToken,
                                                      out double? sunnySR, out double? pp)
        {
            pp = tryComputePerfectPp(playableWorkingBeatmap, analysisBeatmap, lookup, cancellationToken);

            return tryComputeSunnySR(analysisBeatmap, lookup, cancellationToken, out sunnySR) || pp != null;
        }

        private static bool tryComputeSunnySR(IBeatmap analysisBeatmap, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken, out double? sunnySR)
        {
            sunnySR = null;

            if (!EzSSRUnit.SupportsRuleset(lookup.Ruleset.OnlineID))
                return false;

            if (analysisBeatmap.HitObjects.Count == 0)
            {
                sunnySR = 0;
                return true;
            }

            sunnySR = computeSunnySR(analysisBeatmap, getRateAdjustMultiplier(lookup.OrderedMods), cancellationToken);
            return sunnySR != null;
        }

        private static double? tryComputePerfectPp(IWorkingBeatmap playableWorkingBeatmap, IBeatmap analysisBeatmap,
                                                   in EzAnalysisLookupCache lookup, CancellationToken cancellationToken)
            => computeOfficial(playableWorkingBeatmap, analysisBeatmap, lookup, cancellationToken)?.PerformancePoints;

        /// <summary>
        /// For callers that already hold the converted chart and must not convert again for this pass.
        /// </summary>
        private sealed class SeededPlayableWorkingBeatmap : IWorkingBeatmap
        {
            private readonly IWorkingBeatmap working;
            private readonly IBeatmap seededPlayable;

            public SeededPlayableWorkingBeatmap(IWorkingBeatmap working, IBeatmap seededPlayable)
            {
                this.working = working;
                this.seededPlayable = seededPlayable;
            }

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods) => seededPlayable;

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken) => seededPlayable;

            public IBeatmap GetMutablePlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods) => working.GetMutablePlayableBeatmap(ruleset, mods);

            public IBeatmap GetMutablePlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken, Action<IBeatmap>? postConvert = null)
                => working.GetMutablePlayableBeatmap(ruleset, mods, cancellationToken, postConvert);

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
