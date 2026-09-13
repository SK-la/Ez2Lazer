// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Mods;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Chart-dan entry for song select and player credit.
    /// Prefer xxySR → Sunny interval tables when available; else MSD Overall heuristic.
    /// Skill family / OverallMsd always come from MSD when present.
    /// </summary>
    public sealed class EzChartDanEstimator
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzBeatmapMsdComputer msdComputer;

        public EzChartDanEstimator(BeatmapManager beatmapManager, EzBeatmapMsdComputer msdComputer)
        {
            this.beatmapManager = beatmapManager;
            this.msdComputer = msdComputer;
        }

        /// <summary>
        /// Estimates chart dan for player clear credit (hub exclusive RC/LN primary).
        /// Mods that affect playable/rate recompute MSD from playable; nomod uses persisted MSD when available.
        /// </summary>
        public EzChartDanVerdict? TryEstimate(BeatmapInfo beatmapInfo, IReadOnlyList<Mod>? mods = null)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            mods ??= Array.Empty<Mod>();
            bool live = EzModRate.AffectsChartSkills(mods);

            var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
            var playable = working.GetPlayableBeatmap(beatmapInfo.Ruleset, mods);

            IReadOnlyDictionary<string, double>? msd;

            if (!live)
            {
                msd = msdComputer.TryGetOrCompute(beatmapInfo);
            }
            else
            {
                var snap = TryComputeLiveSnapshot(beatmapInfo, mods);
                if (snap == null)
                    return null;

                return FromMsdAndPlayable(snap.Msd, playable, xxySr: snap.XxySr);
            }

            if (msd == null || msd.Count == 0)
                return null;

            double? xxySr = beatmapInfo.XxyStarRating >= 0 ? beatmapInfo.XxyStarRating : null;
            return FromMsdAndPlayable(msd, playable, xxySr);
        }

        /// <summary>
        /// Song-select DualPanel / Skill radar: temporary MSD + dual-half ChartDan from playable + mods.
        /// Always computes (including empty mods) — same rhythm as xxySR analysis on the selected chart.
        /// Never writes Realm.
        /// </summary>
        /// <param name="chartInfo">Optional CSI for skillset stamps; omit when live keyCount differs from CS.</param>
        public EzLiveChartSkillSnapshot? TryComputeLiveSnapshot(
            BeatmapInfo beatmapInfo,
            IReadOnlyList<Mod>? mods = null,
            EzChartSkillInfo? chartInfo = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Ruleset.OnlineID != 3)
                return null;

            mods ??= Array.Empty<Mod>();
            // EzModSeed.Resolve makes Seed writes update-thread safe; use SelectedMods as-is so UI seed matches.
            float rate = EzModRate.Resolve(mods);

            var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
            var playable = working.GetPlayableBeatmap(beatmapInfo.Ruleset, mods);

            cancellationToken.ThrowIfCancellationRequested();

            int keyCount = EzMinaNoteConverter.ResolveKeyCount(playable);
            if (keyCount <= 0)
                return null;

            using var calc = new EzNKeyMsdEngine();
            EzSkillsetVector vector;

            if (!calc.SupportsKeyCount(keyCount))
                return null;

            try
            {
                vector = calc.CalculateMsd(EzMinaNoteConverter.Convert(playable), keyCount, rate);
            }
            catch (Exception)
            {
                return null;
            }

            // The Mina pass above is not interruptible; bail before the (also expensive) xxy/pattern work
            // if the caller already moved on to another chart.
            cancellationToken.ThrowIfCancellationRequested();

            if (vector.Overall <= 0 && vector.Stream <= 0)
                return null;

            var msd = withHoldRatio(VectorToMsdDict(vector), ComputeHoldRatio(playable));
            double holdRatio = ComputeHoldRatio(playable);
            int holdCount = ComputeHoldCount(playable);

            // Same live xxy as song-select analysis panel (playable + rate/key mods). Fallback to Realm nomod xxy only when usable.
            double? xxySr = null;

            if (EzAnalysisComputation.TryComputeXxySrFromPlayable(playable, beatmapInfo.Ruleset, mods, CancellationToken.None, out double liveXxy)
                && liveXxy > 0)
            {
                xxySr = liveXxy;
            }
            else if (EzModRate.CanUsePersistedXxy(mods) && beatmapInfo.XxyStarRating >= 0)
            {
                xxySr = beatmapInfo.XxyStarRating;
            }

            // Without Sunny/xxy, MSD→SrToRawDan invents finish/stellium — never for live DualPanel.
            var chartDan = EzPersistedChartDan.TryComputeFromStored(
                beatmapInfo.Hash,
                beatmapInfo.ID,
                msd,
                keyCount,
                holdRatio,
                xxySr,
                chartInfo,
                holdCount,
                allowMsdHeuristicLabels: xxySr != null);

            var chartInput = EzChartSkillInfoComputer.FromPlayable(playable);
            var features = EzDanFeatureExtractor.Extract(chartInput, rate);
            var patternAnalysis = EzManiaPatternAnalyzer.Analyze(chartInput, rate, features);
            var lnSubtypeScores = collectLnSubtypeScores(patternAnalysis);
            var rcPatternScores = collectRcPatternScores(patternAnalysis);

            return new EzLiveChartSkillSnapshot
            {
                Msd = msd,
                ChartDan = chartDan,
                KeyCount = keyCount,
                HoldRatio = holdRatio,
                HoldCount = holdCount,
                XxySr = xxySr,
                IsLiveFromMods = true,
                LnMetrics = features.Metrics,
                LnSubtypeScores = lnSubtypeScores,
                RcPatternScores = rcPatternScores,
            };
        }

        private static IReadOnlyDictionary<string, double> collectLnSubtypeScores(EzManiaPatternAnalysis analysis)
        {
            var result = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var hit in analysis.AllPatterns)
            {
                if (hit.Id is not ("lngeneral" or "lntech" or "lninverse" or "lnrelease"))
                    continue;

                if (!double.IsFinite(hit.Score) || hit.Score <= 0)
                    continue;

                result[hit.Id] = hit.Score;
            }

            return result;
        }

        private static IReadOnlyDictionary<string, double> collectRcPatternScores(EzManiaPatternAnalysis analysis)
        {
            var result = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var axis in EzPlayerPatternAxisExtensions.All)
            {
                string id = axis.ToId();

                foreach (var hit in analysis.AllPatterns)
                {
                    if (!string.Equals(hit.Id, id, StringComparison.Ordinal))
                        continue;

                    if (!double.IsFinite(hit.Score) || hit.Score <= 0)
                        continue;

                    result[id] = hit.Score;
                    break;
                }
            }

            return result;
        }

        private static IReadOnlyDictionary<string, double> withHoldRatio(IReadOnlyDictionary<string, double> msd, double holdRatio)
        {
            if (!double.IsFinite(holdRatio))
                return msd;

            var copy = new Dictionary<string, double>(msd, StringComparer.Ordinal)
            {
                [EzSkillSystems.MsdHoldRatioSkillId] = Math.Clamp(holdRatio, 0, 1),
            };
            return copy;
        }

        public static EzChartDanVerdict? FromMsdAndPlayable(
            IReadOnlyDictionary<string, double> msd,
            IBeatmap playable,
            double? xxySr = null)
        {
            ArgumentNullException.ThrowIfNull(msd);
            ArgumentNullException.ThrowIfNull(playable);

            if (!msd.TryGetValue(EzMinaSkillAxis.Overall.ToMsdSkillId(), out double overall) || overall <= 0)
                return null;

            int keyCount = EzMinaNoteConverter.ResolveKeyCount(playable);
            if (keyCount <= 0)
                return null;

            double holdRatio = ComputeHoldRatio(playable);
            return FromMsd(msd, keyCount, holdRatio, xxySr);
        }

        public static EzChartDanVerdict? FromMsd(
            IReadOnlyDictionary<string, double> msd,
            int keyCount,
            double holdRatio,
            double? xxySr = null)
        {
            ArgumentNullException.ThrowIfNull(msd);

            if (keyCount <= 0)
                return null;

            if (!msd.TryGetValue(EzMinaSkillAxis.Overall.ToMsdSkillId(), out double overall) || overall <= 0)
                return null;

            var dominantAxis = EzDanLabels.DominantAxis(msd);
            var side = holdRatio >= EzDanAlgorithm.LnPrimaryMinRatioFor(keyCount)
                ? EzDanSide.Ln
                : EzDanSide.Rc;

            double rawDan;
            string label;

            if (xxySr is double sr
                && EzSunnyDanIntervals.TryLookup(keyCount, side.ToId(), sr, out var sunny))
            {
                rawDan = sunny.RawDan;
                label = sunny.DisplayLabel;
            }
            else if (EzDanLabels.TryFallbackRawDan(keyCount, overall, dominantAxis, xxySr) is double fallbackRawDan)
            {
                rawDan = fallbackRawDan;
                label = EzDanLadders.For(keyCount, side).ParseLabel(rawDan);
            }
            else
            {
                // 5K / 8K+: no xxy table and no star rating to fit → no invented dan.
                return null;
            }

            return new EzChartDanVerdict
            {
                RawDan = rawDan,
                Label = label,
                KeyCount = keyCount,
                Side = side,
                DominantAxis = dominantAxis,
                OverallMsd = overall,
                HoldRatio = holdRatio,
                AlgorithmVersion = EzDanAlgorithm.VERSION,
            };
        }

        public static double ComputeHoldRatio(IBeatmap playable)
        {
            var (holds, total) = countHoldAndTotal(playable);
            return total <= 0 ? 0 : (double)holds / total;
        }

        /// <summary>Column hold (LN) object count on <paramref name="playable"/>.</summary>
        public static int ComputeHoldCount(IBeatmap playable)
            => countHoldAndTotal(playable).Holds;

        private static (int Holds, int Total) countHoldAndTotal(IBeatmap playable)
        {
            ArgumentNullException.ThrowIfNull(playable);

            int total = 0;
            int holds = 0;

            foreach (HitObject obj in playable.HitObjects)
            {
                if (obj is not IHasColumn)
                    continue;

                total++;

                if (obj is IHasDuration duration && duration.Duration > 0)
                    holds++;
            }

            return (holds, total);
        }

        /// <summary>Hold ratio from cached mania column/LN counts when available.</summary>
        public static double? TryHoldRatioFromManiaSummary(EzManiaSummary summary)
        {
            if (!summary.HasHoldNoteCounts)
                return null;

            int total = 0;
            int holds = 0;

            foreach (var kvp in summary.ColumnCounts)
                total += kvp.Value;

            foreach (var kvp in summary.HoldNoteCounts)
                holds += kvp.Value;

            if (total <= 0)
                return null;

            return (double)holds / total;
        }

        /// <summary>Hold object count from mania summary when available.</summary>
        public static int? TryHoldCountFromManiaSummary(EzManiaSummary summary)
        {
            if (!summary.HasHoldNoteCounts)
                return null;

            int holds = 0;

            foreach (var kvp in summary.HoldNoteCounts)
                holds += kvp.Value;

            return holds;
        }

        /// <summary>
        /// Same LN count source as song-select <c>ln&gt;</c> (<see cref="BeatmapInfo.EndTimeObjectCount"/>).
        /// Returns &lt; 0 when unset (−1 sentinel). No playable load.
        /// </summary>
        public static int TryHoldCountFromBeatmapInfo(BeatmapInfo beatmapInfo)
            => beatmapInfo.EndTimeObjectCount >= 0 ? beatmapInfo.EndTimeObjectCount : -1;

        public static IReadOnlyDictionary<string, double> VectorToMsdDict(EzSkillsetVector vector)
            => vector.Enumerate().ToDictionary(p => p.Axis.ToMsdSkillId(), p => p.Value, StringComparer.Ordinal);
    }
}
