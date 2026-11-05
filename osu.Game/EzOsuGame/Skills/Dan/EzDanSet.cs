// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.EzOsuGame.Skills.Dan
{
    /// <summary>
    /// Where a dan value came from, in decreasing order of trustworthiness.
    /// </summary>
    /// <remarks>
    /// Lets a reader decide whether to show a derived value at all, instead of the producer deciding
    /// that upfront for everyone.
    /// </remarks>
    public enum EzDanValueSource
    {
        /// <summary>The key mode's own Sunny interval table.</summary>
        SunnyTable,

        /// <summary>Another key mode's table, used because this one has none of its own.</summary>
        BorrowedTable,

        /// <summary>Derived from the rating axis means, with no star rating to read off.</summary>
        MsdMeans,
    }

    /// <summary>
    /// One dan estimate for one side of a chart, together with the table it was read off.
    /// </summary>
    public sealed class EzDanEntry
    {
        public double RawDan { get; init; } = -1;

        public string Label { get; init; } = string.Empty;

        public IReadOnlyDictionary<string, string> SkillsetLabels { get; init; }
            = new Dictionary<string, string>();

        /// <summary>Ladder the label belongs to. Naming a side does not determine it: a key mode may offer several.</summary>
        public EzDanLadderKind Ladder { get; init; }

        public EzDanValueSource Value { get; init; }
    }

    /// <summary>
    /// Every dan estimate for one chart under one mod set, one entry per side.
    /// </summary>
    /// <remarks>
    /// A chart's sides do not share a ladder, so this is a set of independent estimates rather than a
    /// single number. Estimates earned by clearing specific charts are a separate concern and do not
    /// belong here.
    /// </remarks>
    public sealed class EzDanSet
    {
        public int KeyCount { get; init; }

        public double HoldRatio { get; init; }

        public double? XxySr { get; init; }

        public IReadOnlyDictionary<EzDanSide, EzDanEntry> Sides { get; init; }
            = new Dictionary<EzDanSide, EzDanEntry>();

        /// <summary>The estimate for a side, or null when that side was not rated.</summary>
        public EzDanEntry? For(EzDanSide side)
            => Sides.TryGetValue(side, out var entry) ? entry : null;

        /// <summary>
        /// Resolves every side this chart can be rated on.
        /// </summary>
        /// <param name="holdCount">Column hold objects; pass &lt; 0 when unknown (ratio-only LN gate).</param>
        /// <param name="allowMsdMeans">
        /// When false, a side with no star rating is left out rather than derived from the rating axis means.
        /// </param>
        public static EzDanSet? Create(
            IReadOnlyDictionary<string, double> axes,
            int keyCount,
            double holdRatio,
            double? xxySr,
            EzChartSkillInfo? chartInfo,
            int holdCount = -1,
            bool allowMsdMeans = true)
        {
            ArgumentNullException.ThrowIfNull(axes);

            if (keyCount <= 0 || axes.Count == 0)
                return null;

            var primary = EzChartDanEstimator.FromMsd(axes, keyCount, holdRatio, xxySr);

            if (primary == null)
                return null;

            double? lengthSeconds = chartInfo?.LengthSeconds;
            var dominantAxis = primary.DominantAxis;
            var sides = new Dictionary<EzDanSide, EzDanEntry>();

            foreach (var side in EzDanSideExtensions.All)
            {
                if (side == EzDanSide.Ln && !EzDanAlgorithm.AllowsPersistedChartLnHalf(keyCount, holdRatio, holdCount))
                    continue;

                var resolved = resolve(side);
                if (resolved == null)
                    continue;

                var buckets = EzDanSkillsetFiling.BucketsForValues(keyCount, side, axes, lengthSeconds, rate: 1, chartInfo);
                var stamps = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (string id in buckets)
                    stamps[id] = resolved.Value.Label;

                sides[side] = new EzDanEntry
                {
                    RawDan = resolved.Value.RawDan,
                    Label = resolved.Value.Label,
                    SkillsetLabels = stamps,
                    Ladder = EzDanLadders.For(keyCount, side).Kind,
                    Value = resolved.Value.Source,
                };
            }

            if (sides.Count == 0)
                return null;

            return new EzDanSet
            {
                KeyCount = keyCount,
                HoldRatio = holdRatio,
                XxySr = xxySr,
                Sides = sides,
            };

            (double RawDan, string Label, EzDanValueSource Source)? resolve(EzDanSide side)
            {
                if (xxySr is double sr
                    && sr >= 0 && double.IsFinite(sr)
                    && EzSunnyDanIntervals.TryLookup(keyCount, side.ToId(), sr, out var sunny)
                    && !string.IsNullOrEmpty(sunny.DisplayLabel))
                {
                    return (sunny.RawDan, sunny.DisplayLabel, EzDanValueSource.SunnyTable);
                }

                if (!allowMsdMeans)
                    return null;

                if (primary.Side == side && !string.IsNullOrEmpty(primary.Label))
                    return (primary.RawDan, primary.Label, fallbackSource());

                if (EzDanLabels.TryResolveFallbackDan(keyCount, side, primary.OverallMsd, dominantAxis, xxySr) is not { } fallback)
                    return null;

                return (fallback.RawDan, fallback.Label, fallbackSource());
            }

            // Reaching a fallback means the key mode's own table had nothing to say for this side.
            EzDanValueSource fallbackSource()
                => EzDanLabels.HasXxyDanTable(keyCount) ? EzDanValueSource.MsdMeans : EzDanValueSource.BorrowedTable;
        }
    }
}
