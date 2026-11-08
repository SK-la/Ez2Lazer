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
using osu.Game.EzOsuGame.Mods;
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
        /// Computes baseline NoMod xxy SR for persisting to <see cref="BeatmapInfo.SunnyStarRating"/>.
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

        public static bool TryComputeXxySrAndPp(BeatmapManager beatmapManager, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken, out double? xxySr, out double? pp)
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
        public static EzAnalysisResult ComputePersistedSqliteSlice(BeatmapManager beatmapManager, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken = default)
            => ComputePersistedSqliteSlice(beatmapManager.GetWorkingBeatmap(lookup.BeatmapInfo), lookup, cancellationToken);

        public static EzAnalysisResult ComputePersistedSqliteSlice(WorkingBeatmap workingBeatmap, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken = default)
        {
            bool onlyKps = !EzAnalysisProviderBridge.HasAnalysisProvider(lookup.Ruleset);
            IBeatmap analysisBeatmap = EzPlayableBeatmapCache.GetShared(workingBeatmap, lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var (averageKps, maxKps, kpsList, columnCounts, holdNoteCounts) = OptimizedBeatmapCalculator.GetAllDataOptimized(analysisBeatmap, onlyKps: onlyKps);

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

            if (!onlyKps)
            {
                padColumnCounts(lookup, columnCounts, holdNoteCounts);
                maniaSummary = new EzManiaSummary(columnCounts, holdNoteCounts, xxySr: null);
            }

            return new EzAnalysisResult(commonSummary, maniaSummary);
        }

        public static EzAnalysisResult Compute(WorkingBeatmap workingBeatmap, in EzAnalysisLookupCache lookup, CancellationToken cancellationToken = default)
        {
            bool onlyKps = !EzAnalysisProviderBridge.HasAnalysisProvider(lookup.Ruleset);

            // One conversion serves this whole pass: the framework's shared cache is skipped here because the
            // difficulty calculator needs an instance it may hand to sibling calculators repeatedly.
            var playable = new PlayableCachedWorkingBeatmap(workingBeatmap);
            IBeatmap analysisBeatmap = playable.GetPlayableBeatmap(lookup.Ruleset, lookup.OrderedMods, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var (averageKps, maxKps, kpsList, columnCounts, holdNoteCounts) = OptimizedBeatmapCalculator.GetAllDataOptimized(analysisBeatmap, onlyKps: onlyKps);

            // 将速率调整 mod 应用到分析结果，保持与实际游玩时长一致。
            double rate = getRateAdjustMultiplier(lookup.OrderedMods);

            if (!Precision.AlmostEquals(rate, 1.0))
            {
                averageKps *= rate;
                maxKps *= rate;

                for (int i = 0; i < kpsList.Count; i++)
                    kpsList[i] *= rate;
            }

            double? xxySr = null;

            if (!onlyKps)
                tryComputeXxySr(analysisBeatmap, lookup, cancellationToken, out xxySr);

            cancellationToken.ThrowIfCancellationRequested();

            kpsList = OptimizedBeatmapCalculator.DownsampleToFixedCount(kpsList, OptimizedBeatmapCalculator.DEFAULT_KPS_GRAPH_POINTS);

            var commonSummary = new KpsSummary(averageKps, maxKps, kpsList);
            EzManiaSummary? maniaSummary = null;

            if (!onlyKps)
            {
                padColumnCounts(lookup, columnCounts, holdNoteCounts);
                maniaSummary = new EzManiaSummary(columnCounts, holdNoteCounts, xxySr);
            }

            return new EzAnalysisResult(commonSummary, maniaSummary,
                axes: computeAxes(lookup, playable, analysisBeatmap, rate, cancellationToken),
                dan: computeDan(lookup, analysisBeatmap),
                radar: computeRadar(lookup, analysisBeatmap, rate, cancellationToken));
        }

        /// <summary>
        /// Every producer's axis values for this chart under the current mods, keyed by producer.
        /// </summary>
        private static IReadOnlyDictionary<EzMetricProducerId, IReadOnlyDictionary<string, double>> computeAxes(
            in EzAnalysisLookupCache lookup, PlayableCachedWorkingBeatmap playable, IBeatmap analysisBeatmap, double rate, CancellationToken cancellationToken)
        {
            var result = new Dictionary<EzMetricProducerId, IReadOnlyDictionary<string, double>>();
            int rulesetOnlineId = lookup.Ruleset.OnlineID;

            var mina = computeMina(rulesetOnlineId, analysisBeatmap, rate);
            if (mina != null)
                result[EzMetricProducers.MINA_CHART] = mina;

            var ppp = computePpp(rulesetOnlineId, analysisBeatmap, lookup.OrderedMods);
            if (ppp != null)
                result[EzMetricProducers.PPP_CHART] = ppp;

            var structure = computeStructure(rulesetOnlineId, analysisBeatmap);
            if (structure != null)
                result[EzMetricProducers.CSI_CHART] = structure;

            addOfficial(playable, analysisBeatmap, lookup, result);
            addSsr(lookup, analysisBeatmap, cancellationToken, result);

            return result;
        }

        /// <summary>
        /// Official difficulty and performance for the current mods. Both come from one difficulty calculation,
        /// which is why they are produced together rather than by two independent passes.
        /// </summary>
        private static void addOfficial(PlayableCachedWorkingBeatmap playable, IBeatmap analysisBeatmap, in EzAnalysisLookupCache lookup,
                                        Dictionary<EzMetricProducerId, IReadOnlyDictionary<string, double>> result)
        {
            var ruleset = lookup.Ruleset.CreateInstance();
            var difficulty = ruleset.CreateDifficultyCalculator(playable).Calculate(lookup.OrderedMods, CancellationToken.None);

            if (double.IsFinite(difficulty.StarRating) && difficulty.StarRating >= 0)
            {
                result[EzMetricProducers.OFFICIAL_CHART] = new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    [EzMetricProducers.STAR_RATING] = difficulty.StarRating,
                };
            }

            if (!double.IsFinite(difficulty.StarRating))
                return;

            var performanceCalculator = ruleset.CreatePerformanceCalculator();

            if (performanceCalculator == null)
                return;

            ScoreProcessor scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = lookup.OrderedMods;
            scoreProcessor.ApplyBeatmap(analysisBeatmap);

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

            if (!double.IsFinite(performance.Total) || performance.Total <= 0)
                return;

            result[EzMetricProducers.OFFICIAL_PERFORMANCE] = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzMetricProducers.PERFORMANCE_POINTS] = performance.Total,
            };
        }

        /// <summary>
        /// Sunny star rating for the current mods. The rating is a single number, so it is stored under the same
        /// star axis id every other star-tier producer uses.
        /// </summary>
        private static void addSsr(in EzAnalysisLookupCache lookup, IBeatmap analysisBeatmap, CancellationToken cancellationToken,
                                   Dictionary<EzMetricProducerId, IReadOnlyDictionary<string, double>> result)
        {
            if (!tryComputeXxySr(analysisBeatmap, lookup, cancellationToken, out double? xxySr) || xxySr is not double value)
                return;

            result[EzMetricProducers.SSR_CHART] = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzMetricProducers.STAR_RATING] = value,
            };

            // Performance from the substituted rating, when the ruleset can express it.
            if (EzAnalysisProviderBridge.TryGetValue(lookup.Ruleset,
                    new EzAnalysisRequest(analysisBeatmap, getRateAdjustMultiplier(lookup.OrderedMods), EzAnalysisScope.PerformancePoints, value),
                    EzAnalysisFields.PERFORMANCE_POINTS, cancellationToken, out double pp) && double.IsFinite(pp) && pp > 0)
            {
                result[EzMetricProducers.SSR_PERFORMANCE] = new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    [EzMetricProducers.PERFORMANCE_POINTS] = pp,
                };
            }
        }

        /// <summary>
        /// Dan estimates for the sides this chart can be rated on. Reads the same axis tables this pass produced,
        /// so the live value and the stored row are derived from one input.
        /// </summary>
        private static EzDanSet? computeDan(in EzAnalysisLookupCache lookup, IBeatmap analysisBeatmap)
        {
            if (lookup.Ruleset.OnlineID != EzLocalProfileConstants.MANIA_RULESET_ID)
                return null;

            var bareAxes = computeMina(lookup.Ruleset.OnlineID, analysisBeatmap, EzModRate.Resolve(lookup.OrderedMods));

            if (bareAxes == null)
                return null;

            // The estimator addresses axes by their stored skill id.
            var axes = new Dictionary<string, double>(bareAxes.Count, StringComparer.Ordinal);

            foreach (var (axisId, value) in bareAxes)
                axes[$"{EzSkillSystems.BEATMAP_MINA}.{axisId}"] = value;

            int keyCount = EzHoldMetrics.ResolveKeyCount(analysisBeatmap);
            double holdRatio = EzHoldMetrics.ComputeHoldRatio(analysisBeatmap);

            if (tryComputeXxySr(analysisBeatmap, lookup, CancellationToken.None, out double? xxySr) == false)
                xxySr = null;

            return EzDanSet.Create(
                axes,
                keyCount,
                holdRatio,
                xxySr,
                chartInfo: null,
                holdCount: EzHoldMetrics.ComputeHoldCount(analysisBeatmap),
                // Without a star rating the means-derived estimate is not trustworthy enough to display.
                allowMsdMeans: xxySr != null);
        }

        /// <summary>Mina axis values, keyed by bare axis id. Null when the key mode is outside the engine's range.</summary>
        private static IReadOnlyDictionary<string, double>? computeMina(int rulesetOnlineId, IBeatmap analysisBeatmap, double rate)
        {
            if (rulesetOnlineId != EzLocalProfileConstants.MANIA_RULESET_ID)
                return null;

            int keyCount = EzHoldMetrics.ResolveKeyCount(analysisBeatmap);
            if (!EzMinaEngine.IsSupportedKeyCount(keyCount))
                return null;

            try
            {
                using var engine = new EzMinaEngine();
                var vector = engine.CalculateMsd(EzMinaNoteConverter.Convert(analysisBeatmap), keyCount, (float)rate);

                if (vector.Overall <= 0 && vector.Stream <= 0)
                    return null;

                var axes = new Dictionary<string, double>(StringComparer.Ordinal);

                foreach (var (axis, value) in vector.Enumerate())
                    axes[axis.ToId()] = value;

                return axes;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// PP+ chart vector for the current mods. Independent of the ruleset analysis provider: PP+ covers
        /// osu / taiko / catch / mania, only one of which exposes a provider.
        /// </summary>
        private static IReadOnlyDictionary<string, double>? computePpp(int rulesetOnlineId, IBeatmap analysisBeatmap, IReadOnlyList<Mod> mods)
        {
            if (!EzPPPSkillCatalog.SupportsRuleset(rulesetOnlineId))
                return null;

            double clockRate = ModUtils.CalculateRateWithMods(mods);

            if (!EzPPPCalculators.TryCalculate(rulesetOnlineId, analysisBeatmap, mods, clockRate, out var skills))
                return null;

            // Stored ids carry the system prefix; a producer's axis table addresses axes by bare id.
            var axes = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var (skillId, value) in skills)
            {
                if (EzMetricSkillId.TrySplit(skillId, out _, out string axisId))
                    axes[axisId] = value;
            }

            return axes;
        }

        /// <summary>
        /// Chart structure values: the drawn long-note measures plus the pattern and subtype scores.
        /// Rate is not folded in; the values are rate-invariant.
        /// </summary>
        private static IReadOnlyDictionary<string, double>? computeStructure(int rulesetOnlineId, IBeatmap analysisBeatmap)
        {
            if (rulesetOnlineId != EzLocalProfileConstants.MANIA_RULESET_ID)
                return null;

            var input = EzChartSkillInfoComputer.FromPlayable(analysisBeatmap);
            var features = EzChartFeatureExtractor.Extract(input, rate: 1);
            var patterns = EzPatternAnalyzer.Analyze(input, rate: 1, precomputed: features);
            var axes = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var (id, value) in EzManiaLNAxisExtensions.FromFeatures(features.Metrics))
                axes[id] = value;

            foreach (var hit in patterns.AllPatterns)
            {
                if (!double.IsFinite(hit.Score) || hit.Score <= 0)
                    continue;

                if (!EzManiaLNAxisExtensions.SubtypeIds.Contains(hit.Id) && !EzManiaRCAxisExtensions.AllIds.Contains(hit.Id))
                    continue;

                if (!axes.TryGetValue(hit.Id, out double existing) || hit.Score > existing)
                    axes[hit.Id] = hit.Score;
            }

            return axes;
        }

        private static EzRulesetSpecificRadarResult? computeRadar(in EzAnalysisLookupCache lookup, IBeatmap analysisBeatmap, double rate, CancellationToken cancellationToken)
            => EzAnalysisProviderBridge.TryGetValue(lookup.Ruleset, new EzAnalysisRequest(analysisBeatmap, rate), EzAnalysisFields.RULESET_SPECIFIC_RADAR_RESULT,
                cancellationToken, out EzRulesetSpecificRadarResult radar)
                ? radar
                : null;

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
        {
            var ruleset = lookup.Ruleset.CreateInstance();

            var performanceCalculator = ruleset.CreatePerformanceCalculator();

            if (performanceCalculator == null)
                return null;

            var difficulty = ruleset.CreateDifficultyCalculator(playableWorkingBeatmap).Calculate(lookup.OrderedMods, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

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
                MaximumStatistics = scoreProcessor.MaximumStatistics
            };

            var performance = performanceCalculator.Calculate(perfectScore, difficulty);
            cancellationToken.ThrowIfCancellationRequested();

            return double.IsFinite(performance.Total) ? performance.Total : null;
        }

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
