// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Skills.Dan;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// In-memory DTO for <see cref="EzBeatmapChartDan"/> (nomod baseline + skillset stamps).
    /// </summary>
    public sealed class EzPersistedChartDan
    {
        public const char ENTRY_SEPARATOR = '\u001e';
        public const char FIELD_SEPARATOR = '\u001f';

        public string BeatmapHash { get; init; } = string.Empty;

        public Guid BeatmapId { get; init; }

        public int AlgorithmVersion { get; init; } = EzDanAlgorithm.VERSION;

        public int KeyCount { get; init; }

        public double HoldRatio { get; init; }

        public double OverallMsd { get; init; }

        public double RcRawDan { get; init; } = -1;

        public string RcLabel { get; init; } = string.Empty;

        public double LnRawDan { get; init; } = -1;

        public string LnLabel { get; init; } = string.Empty;

        public IReadOnlyDictionary<string, string> RcSkillsetLabels { get; init; }
            = new Dictionary<string, string>();

        public IReadOnlyDictionary<string, string> LnSkillsetLabels { get; init; }
            = new Dictionary<string, string>();

        public DateTimeOffset ComputedAt { get; init; }

        public bool HasSide(EzDanSide side)
            => side == EzDanSide.Ln ? LnRawDan >= 0 : RcRawDan >= 0;

        public string? LabelFor(EzDanSide side)
        {
            if (side == EzDanSide.Ln)
                return LnRawDan >= 0 && !string.IsNullOrEmpty(LnLabel) ? LnLabel : null;

            return RcRawDan >= 0 && !string.IsNullOrEmpty(RcLabel) ? RcLabel : null;
        }

        public IReadOnlyDictionary<string, string> SkillsetLabelsFor(EzDanSide side)
            => side == EzDanSide.Ln ? LnSkillsetLabels : RcSkillsetLabels;

        /// <summary>
        /// Nomod DualPanel overlay: keep baseline Sunny RC/LN when present; only take missing halves from <paramref name="live"/>.
        /// Avoids MSD <c>SrToRawDan</c> clamping to stellium (star badge) overwriting good Realm labels.
        /// </summary>
        public static EzPersistedChartDan MergeNomodBaselineWithLive(EzPersistedChartDan baseline, EzPersistedChartDan live)
        {
            bool keepRc = baseline.HasSide(EzDanSide.Rc);
            bool keepLn = baseline.HasSide(EzDanSide.Ln);

            return new EzPersistedChartDan
            {
                BeatmapHash = baseline.BeatmapHash,
                BeatmapId = baseline.BeatmapId,
                AlgorithmVersion = baseline.AlgorithmVersion,
                KeyCount = baseline.KeyCount > 0 ? baseline.KeyCount : live.KeyCount,
                HoldRatio = live.HoldRatio > 0 ? live.HoldRatio : baseline.HoldRatio,
                OverallMsd = baseline.OverallMsd > 0 ? baseline.OverallMsd : live.OverallMsd,
                RcRawDan = keepRc ? baseline.RcRawDan : live.RcRawDan,
                RcLabel = keepRc ? baseline.RcLabel : live.RcLabel,
                RcSkillsetLabels = keepRc ? baseline.RcSkillsetLabels : live.RcSkillsetLabels,
                LnRawDan = keepLn ? baseline.LnRawDan : live.LnRawDan,
                LnLabel = keepLn ? baseline.LnLabel : live.LnLabel,
                LnSkillsetLabels = keepLn ? baseline.LnSkillsetLabels : live.LnSkillsetLabels,
                ComputedAt = DateTimeOffset.UtcNow,
            };
        }

        public EzChartDanVerdict? ToVerdict(EzDanSide side)
        {
            string? label = LabelFor(side);
            if (label == null)
                return null;

            double raw = side == EzDanSide.Ln ? LnRawDan : RcRawDan;

            return new EzChartDanVerdict
            {
                RawDan = raw,
                Label = label,
                KeyCount = KeyCount,
                Side = side,
                OverallMsd = OverallMsd,
                HoldRatio = HoldRatio,
                AlgorithmVersion = AlgorithmVersion,
            };
        }

        /// <summary>
        /// Build nomod persisted chart dan from stored MSD (+ optional CSI / xxy). No playable / Mina / LeoBlack.
        /// RC and LN halves may both be written (Ez DualPanel). LN when
        /// <see cref="EzDanAlgorithm.AllowsPersistedChartLnHalf"/>; RC always attempted.
        /// </summary>
        /// <param name="holdCount">Column hold objects; pass &lt; 0 when unknown (ratio-only LN gate).</param>
        public static EzPersistedChartDan? TryComputeFromStored(
            string beatmapHash,
            Guid beatmapId,
            IReadOnlyDictionary<string, double> msd,
            int keyCount,
            double holdRatio,
            double? xxySr,
            EzChartSkillInfo? chartInfo,
            int holdCount = -1)
        {
            if (string.IsNullOrEmpty(beatmapHash) || keyCount <= 0 || msd.Count == 0)
                return null;

            var primary = EzChartDanEstimator.FromMsd(msd, keyCount, holdRatio, xxySr);
            if (primary == null)
                return null;

            double? lengthSeconds = chartInfo?.LengthSeconds;
            var dominantAxis = primary.DominantAxis;

            double rcRaw = -1;
            string rcLabel = string.Empty;
            double lnRaw = -1;
            string lnLabel = string.Empty;
            var rcLabels = new Dictionary<string, string>(StringComparer.Ordinal);
            var lnLabels = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (EzDanSide side in new[] { EzDanSide.Rc, EzDanSide.Ln })
            {
                if (side == EzDanSide.Ln
                    && !EzDanAlgorithm.AllowsPersistedChartLnHalf(keyCount, holdRatio, holdCount))
                {
                    continue;
                }

                double rawDan = -1;
                string? aggregateLabel = null;

                if (xxySr is double sr
                    && sr >= 0 && double.IsFinite(sr)
                    && EzSunnyDanIntervals.TryLookup(keyCount, side.ToId(), sr, out var sunny)
                    && !string.IsNullOrEmpty(sunny.DisplayLabel))
                {
                    rawDan = sunny.RawDan;
                    aggregateLabel = sunny.DisplayLabel;
                }
                else if (primary.Side == side && !string.IsNullOrEmpty(primary.Label))
                {
                    rawDan = primary.RawDan;
                    aggregateLabel = primary.Label;
                }
                else if (primary.OverallMsd > 0 && double.IsFinite(primary.OverallMsd))
                {
                    // Other side: same MSD overall on that side's ladder (Ez dual halves).
                    rawDan = EzDanLabels.SrToRawDan(primary.OverallMsd, dominantAxis);
                    aggregateLabel = EzDanLadders.For(keyCount, side).ParseLabel(rawDan);
                }

                if (string.IsNullOrEmpty(aggregateLabel) || rawDan < 0)
                    continue;

                var buckets = EzDanSkillsetFiling.BucketsForValues(
                    keyCount, side, msd, lengthSeconds, rate: 1, chartInfo);

                var stamps = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string id in buckets)
                    stamps[id] = aggregateLabel;

                if (side == EzDanSide.Ln)
                {
                    lnRaw = rawDan;
                    lnLabel = aggregateLabel;
                    lnLabels = stamps;
                }
                else
                {
                    rcRaw = rawDan;
                    rcLabel = aggregateLabel;
                    rcLabels = stamps;
                }
            }

            return new EzPersistedChartDan
            {
                BeatmapHash = beatmapHash,
                BeatmapId = beatmapId,
                AlgorithmVersion = EzDanAlgorithm.VERSION,
                KeyCount = keyCount,
                HoldRatio = holdRatio,
                OverallMsd = primary.OverallMsd,
                RcRawDan = rcRaw,
                RcLabel = rcLabel,
                LnRawDan = lnRaw,
                LnLabel = lnLabel,
                RcSkillsetLabels = rcLabels,
                LnSkillsetLabels = lnLabels,
                ComputedAt = DateTimeOffset.UtcNow,
            };
        }

        public static string JoinSkillsetLabels(IReadOnlyDictionary<string, string>? labels)
        {
            if (labels == null || labels.Count == 0)
                return string.Empty;

            return string.Join(ENTRY_SEPARATOR, labels
                                                .Where(static kvp => !string.IsNullOrEmpty(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
                                                .Select(static kvp => kvp.Key + FIELD_SEPARATOR + kvp.Value));
        }

        public static IReadOnlyDictionary<string, string> ParseSkillsetLabels(string? joined)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(joined))
                return result;

            foreach (string entry in joined.Split(ENTRY_SEPARATOR, StringSplitOptions.RemoveEmptyEntries))
            {
                int sep = entry.IndexOf(FIELD_SEPARATOR);
                if (sep <= 0 || sep >= entry.Length - 1)
                    continue;

                string id = entry[..sep];
                string label = entry[(sep + 1)..];
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(label))
                    result[id] = label;
            }

            return result;
        }
    }
}
