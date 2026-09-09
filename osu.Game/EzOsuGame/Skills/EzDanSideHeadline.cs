// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Skills.Dan;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Side-level player-dan headline fold (mania-hub <c>danSideFromClears</c> / <c>anchoredSkillsetDans</c>).
    /// </summary>
    public static class EzDanSideHeadline
    {
        public readonly record struct Result(double RawDan, string Label, int ClearsCounted, bool BeyondTable);

        /// <summary>
        /// Prefer anchored skillset fold (7K LN), else mean of rated skillsets, else null
        /// (caller falls back to side-wide clear window).
        /// </summary>
        public static Result? FromSkillsets(
            int keyCount,
            EzDanSide side,
            IReadOnlyDictionary<string, EzDanSkillsetVerdict> skillsets,
            IReadOnlyList<double> clearDans)
        {
            ArgumentNullException.ThrowIfNull(skillsets);
            ArgumentNullException.ThrowIfNull(clearDans);

            return anchoredSkillsetDans(keyCount, side, skillsets, clearDans)
                   ?? averageSkillsetDans(keyCount, side, skillsets, clearDans);
        }

        /// <summary>Side-wide top-N average (hub quorum path / no skillset buckets).</summary>
        public static Result? FromSideClears(int keyCount, EzDanSide side, IReadOnlyList<double> clearDans)
        {
            ArgumentNullException.ThrowIfNull(clearDans);

            if (clearDans.Count < EzDanAlgorithm.CLEAR_QUORUM)
                return null;

            var window = clearDans
                         .OrderByDescending(v => v)
                         .Take(EzDanAlgorithm.CLEAR_WINDOW)
                         .ToList();

            double rawDan = round2(window.Average());
            return toResult(keyCount, side, rawDan, clearDans);
        }

        private static Result? averageSkillsetDans(
            int keyCount,
            EzDanSide side,
            IReadOnlyDictionary<string, EzDanSkillsetVerdict> skillsets,
            IReadOnlyList<double> clearDans)
        {
            if (skillsets.Count < EzDanAlgorithm.SKILLSET_AVERAGE_MIN_BUCKETS)
                return null;

            double rawDan = round2(skillsets.Values.Average(v => v.RawDan));
            return toResult(keyCount, side, rawDan, clearDans);
        }

        private static Result? anchoredSkillsetDans(
            int keyCount,
            EzDanSide side,
            IReadOnlyDictionary<string, EzDanSkillsetVerdict> skillsets,
            IReadOnlyList<double> clearDans)
        {
            var buckets = EzDanSkillsetFiling.Buckets(keyCount, side);
            string? anchorId = null;

            foreach (var bucket in buckets)
            {
                if (!bucket.Anchor)
                    continue;

                anchorId = bucket.Id;
                break;
            }

            if (anchorId == null || !skillsets.TryGetValue(anchorId, out var anchor))
                return null;

            if (skillsets.Count < EzDanAlgorithm.SKILLSET_AVERAGE_MIN_BUCKETS)
                return null;

            var others = skillsets.Where(kv => !string.Equals(kv.Key, anchorId, StringComparison.Ordinal)).ToList();
            if (others.Count == 0)
                return null;

            double distance = 0;

            foreach (var (_, verdict) in others)
            {
                double raw = verdict.RawDan - anchor.RawDan;
                double capped = Math.Clamp(raw, -EzDanAlgorithm.ANCHOR_CLAMP, EzDanAlgorithm.ANCHOR_CLAMP);
                distance += capped;
            }

            double rawDan = round2(anchor.RawDan + EzDanAlgorithm.ANCHOR_PULL * (distance / others.Count));
            return toResult(keyCount, side, rawDan, clearDans);
        }

        private static Result toResult(int keyCount, EzDanSide side, double rawDan, IReadOnlyList<double> clearDans)
        {
            var ladder = EzDanLadders.For(keyCount, side);
            string label = ladder.ParseLabel(rawDan);
            double? ceiling = ladder.Ceiling;
            int clearsCounted = clearDans.Count(v => v >= rawDan - EzDanAlgorithm.ROUNDING_EPSILON);
            bool beyond = ceiling is double c && rawDan >= c;
            return new Result(rawDan, label, clearsCounted, beyond);
        }

        private static double round2(double value) => Math.Round(value * 100) / 100;
    }
}
