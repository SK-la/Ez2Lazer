// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.Carousel;
using osu.Game.Overlays;
using osu.Game.Screens.Select.Filter;

namespace osu.Game.Screens.Select
{
    public class BeatmapCarouselFilterSorting : ICarouselFilter
    {
        public int BeatmapItemsCount { get; private set; }

        private readonly Func<FilterCriteria> getCriteria;
        private readonly Func<IEnumerable<BeatmapInfo>, CancellationToken, Task<IReadOnlyDictionary<BeatmapInfo, double>>> getDifficultiesForOperationsAsync;
        private readonly Func<IEnumerable<BeatmapInfo>, CancellationToken, Task<IReadOnlyDictionary<BeatmapInfo, double>>> getPpForOperationsAsync;
        private readonly Func<IEnumerable<BeatmapInfo>, EzAnalysisSystem, string, CancellationToken, Task<IReadOnlyDictionary<BeatmapInfo, double>>> getSkillAxisForOperationsAsync;

        public BeatmapCarouselFilterSorting(Func<FilterCriteria> getCriteria,
                                            Func<IEnumerable<BeatmapInfo>, CancellationToken, Task<IReadOnlyDictionary<BeatmapInfo, double>>> getDifficultiesForOperationsAsync,
                                            Func<IEnumerable<BeatmapInfo>, CancellationToken, Task<IReadOnlyDictionary<BeatmapInfo, double>>>? getPpForOperationsAsync = null,
                                            Func<IEnumerable<BeatmapInfo>, EzAnalysisSystem, string, CancellationToken, Task<IReadOnlyDictionary<BeatmapInfo, double>>>? getSkillAxisForOperationsAsync = null)
        {
            this.getCriteria = getCriteria;
            this.getDifficultiesForOperationsAsync = getDifficultiesForOperationsAsync;
            this.getPpForOperationsAsync = getPpForOperationsAsync ?? ((_, _) => Task.FromResult<IReadOnlyDictionary<BeatmapInfo, double>>(new Dictionary<BeatmapInfo, double>()));
            this.getSkillAxisForOperationsAsync = getSkillAxisForOperationsAsync
                                                  ?? ((_, _, _, _) => Task.FromResult<IReadOnlyDictionary<BeatmapInfo, double>>(new Dictionary<BeatmapInfo, double>()));
        }

        public async Task<List<CarouselItem>> Run(IEnumerable<CarouselItem> items, CancellationToken cancellationToken)
        {
            var criteria = getCriteria();
            var itemList = items.ToList();

            bool groupedSets = BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(criteria);
            bool usesDifficultyValues = criteria.Sort == SortMode.Difficulty;
            bool usePpValues = criteria.Sort == SortMode.PP;
            bool useSkillAxisValues = criteria.Sort == SortMode.SkillAxis
                                      && !string.IsNullOrEmpty(criteria.SkillAxisSortId)
                                      && criteria.SkillAxisSortSystem != EzAnalysisSystem.None;
            bool useSortOperationValues = itemList.Count > 1
                                          && (usesDifficultyValues || usePpValues || useSkillAxisValues);

            IReadOnlyDictionary<BeatmapInfo, double>? operationSortValues = null;

            if (useSortOperationValues)
            {
                var uniqueBeatmaps = itemList.Select(i => (BeatmapInfo)i.Model).Distinct().ToList();

                if (useSkillAxisValues)
                {
                    operationSortValues = await getSkillAxisForOperationsAsync(
                        uniqueBeatmaps,
                        criteria.SkillAxisSortSystem,
                        criteria.SkillAxisSortId!,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    operationSortValues = usePpValues
                        ? await getPpForOperationsAsync(uniqueBeatmaps, cancellationToken).ConfigureAwait(false)
                        : await getDifficultiesForOperationsAsync(uniqueBeatmaps, cancellationToken).ConfigureAwait(false);
                }
            }

            double getSortValue(BeatmapInfo beatmap)
            {
                if (operationSortValues != null && operationSortValues.TryGetValue(beatmap, out double sortValue))
                    return sortValue;

                if (criteria.Sort == SortMode.SkillAxis)
                    return double.NegativeInfinity;

                // Difficulty and PP are read from the analysis system this beatmap's ruleset is set to. The pair is
                // read once so a beatmap cannot be sorted by one system's star next to another's pp.
                var values = EzRulesetReadout.SelectedValues(beatmap);

                if (criteria.Sort == SortMode.PP)
                    return values.PerformancePoints ?? (beatmap.PerformancePoints >= 0 ? beatmap.PerformancePoints : -1);

                return values.Star ?? beatmap.StarRating;
            }

            BeatmapItemsCount = itemList.Count;

            var stringComparer = EzSortComparer.ForCurrentUiCulture;

            return itemList.Order(Comparer<CarouselItem>.Create((a, b) =>
            {
                var ab = (BeatmapInfo)a.Model;
                var bb = (BeatmapInfo)b.Model;

                if (groupedSets)
                {
                    if (ab.BeatmapSet!.Equals(bb.BeatmapSet))
                        return compareDifficulty(ab, bb, criteria.Sort, getSortValue);

                    // If we're grouping by sets, all fallback sorts need to be aggregates for the set.
                    return compare(ab, bb, criteria.Sort, criteria.SortDirection, aggregate: true, getSortValue, stringComparer);
                }

                return compare(ab, bb, criteria.Sort, criteria.SortDirection, aggregate: false, getSortValue, stringComparer);
            })).ToList();
        }

        private static int compare(BeatmapInfo a, BeatmapInfo b, SortMode sort, SortDirection direction, bool aggregate, Func<BeatmapInfo, double> getSortValue, IComparer<string> stringComparer)
        {
            int comparison;

            switch (sort)
            {
                case SortMode.Artist:
                    comparison = compareDirected(stringComparer.Compare(a.BeatmapSet!.Metadata.Artist, b.BeatmapSet!.Metadata.Artist), direction);
                    break;

                case SortMode.Title:
                    comparison = compareDirected(stringComparer.Compare(a.BeatmapSet!.Metadata.Title, b.BeatmapSet!.Metadata.Title), direction);
                    break;

                case SortMode.Author:
                    comparison = compareDirected(stringComparer.Compare(a.BeatmapSet!.Metadata.Author.Username, b.BeatmapSet!.Metadata.Author.Username), direction);
                    break;

                case SortMode.Source:
                    comparison = compareDirected(stringComparer.Compare(a.BeatmapSet!.Metadata.Source, b.BeatmapSet!.Metadata.Source), direction);
                    break;

                case SortMode.Difficulty:
                    comparison = aggregate
                        ? compareUsingAggregateMax(a, b, getSortValue, direction)
                        : compareDirected(getSortValue(a).CompareTo(getSortValue(b)), direction);
                    break;

                case SortMode.PP:
                {
                    static bool isMissingPp(double value) => value < 0 || !double.IsFinite(value);

                    comparison = aggregate
                        ? compareUsingAggregateMax(a, b, getSortValue, direction, isMissingPp)
                        : compareNumericSortValues(getSortValue(a), getSortValue(b), direction, isMissingPp);
                    break;
                }

                case SortMode.SkillAxis:
                {
                    static bool isMissingAxis(double value) => !double.IsFinite(value);

                    comparison = aggregate
                        ? compareUsingAggregateMax(a, b, getSortValue, direction, isMissingAxis)
                        : compareNumericSortValues(getSortValue(a), getSortValue(b), direction, isMissingAxis);
                    break;
                }

                case SortMode.DateAdded:
                    comparison = compareDirected(a.BeatmapSet!.DateAdded.CompareTo(b.BeatmapSet!.DateAdded), reverseDateDirection(direction));
                    break;

                case SortMode.DateRanked:
                    comparison = compareNullable(a.BeatmapSet!.DateRanked, b.BeatmapSet!.DateRanked, reverseDateDirection(direction));
                    break;

                case SortMode.DateSubmitted:
                    comparison = compareNullable(a.BeatmapSet!.DateSubmitted, b.BeatmapSet!.DateSubmitted, reverseDateDirection(direction));
                    break;

                case SortMode.LastPlayed:
                    if (aggregate)
                    {
                        static double getLastPlayed(BeatmapInfo beatmap) => beatmap.LastPlayed?.ToUnixTimeMilliseconds() ?? double.NegativeInfinity;
                        static bool isMissingLastPlayed(double value) => double.IsNegativeInfinity(value);

                        comparison = compareUsingAggregateMax(a, b, getLastPlayed, reverseDateDirection(direction), isMissingLastPlayed);
                    }
                    else
                        comparison = compareNullable(a.LastPlayed, b.LastPlayed, reverseDateDirection(direction));
                    break;

                case SortMode.BPM:
                    comparison = aggregate
                        ? compareUsingAggregateMax(a, b, static beatmap => beatmap.BPM, direction)
                        : compareDirected(a.BPM.CompareTo(b.BPM), direction);
                    break;

                case SortMode.Length:
                    comparison = aggregate
                        ? compareUsingAggregateMax(a, b, static beatmap => beatmap.Length, direction)
                        : compareDirected(a.Length.CompareTo(b.Length), direction);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (comparison != 0)
                return comparison;

            // Tie-breakers keep their existing direction so changing the primary sort does not destabilise equal items.
            comparison = stringComparer.Compare(a.BeatmapSet!.Metadata.Title, b.BeatmapSet!.Metadata.Title);

            if (comparison == 0)
                comparison = b.BeatmapSet!.DateAdded.CompareTo(a.BeatmapSet!.DateAdded);

            if (comparison == 0)
                comparison = b.BeatmapSet!.ID.CompareTo(a.BeatmapSet!.ID);

            return comparison;
        }

        private static SortDirection reverseDateDirection(SortDirection direction) =>
            direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;

        private static int compareDirected(int comparison, SortDirection direction) =>
            direction == SortDirection.Ascending ? comparison : -comparison;

        private static int compareNullable<T>(T? a, T? b, SortDirection direction)
            where T : struct, IComparable<T>
        {
            if (!a.HasValue || !b.HasValue)
            {
                if (a.HasValue == b.HasValue)
                    return 0;

                return a.HasValue ? -1 : 1;
            }

            return compareDirected(a.Value.CompareTo(b.Value), direction);
        }

        private static int compareNumericSortValues(double a, double b, SortDirection direction, Func<double, bool> isMissing)
        {
            bool aMissing = isMissing(a);
            bool bMissing = isMissing(b);

            if (aMissing || bMissing)
            {
                if (aMissing == bMissing)
                    return 0;

                return aMissing ? 1 : -1;
            }

            return compareDirected(a.CompareTo(b), direction);
        }

        private static int compareUsingAggregateMax(BeatmapInfo a, BeatmapInfo b, Func<BeatmapInfo, double> func, SortDirection direction, Func<double, bool>? isMissing = null)
        {
            static double? getMax(BeatmapInfo beatmap, Func<BeatmapInfo, double> selector, Func<double, bool>? isMissingValue)
            {
                bool hasVisibleBeatmap = false;
                bool hasValue = false;
                double max = double.NegativeInfinity;

                foreach (var matchedBeatmap in beatmap.BeatmapSet!.Beatmaps.Where(bb => !bb.Hidden))
                {
                    hasVisibleBeatmap = true;
                    double value = selector(matchedBeatmap);

                    if (isMissingValue?.Invoke(value) == true)
                        continue;

                    if (!hasValue || value > max)
                        max = value;

                    hasValue = true;
                }

                return hasVisibleBeatmap && hasValue ? max : null;
            }

            double? aMax = getMax(a, func, isMissing);
            double? bMax = getMax(b, func, isMissing);

            if (!aMax.HasValue || !bMax.HasValue)
            {
                if (aMax.HasValue == bMax.HasValue)
                    return 0;

                return aMax.HasValue ? -1 : 1;
            }

            return compareDirected(aMax.Value.CompareTo(bMax.Value), direction);
        }

        private static int compareDifficulty(BeatmapInfo a, BeatmapInfo b, SortMode sort, Func<BeatmapInfo, double> getSortValue)
        {
            int comparison = a.Ruleset.CompareTo(b.Ruleset);

            if (comparison == 0)
                comparison = getSortValue(a).CompareTo(getSortValue(b));

            return comparison;
        }

        private static int compareUsingAggregateMax(BeatmapInfo a, BeatmapInfo b, Func<BeatmapInfo, double> func)
        {
            var aMatchedBeatmaps = a.BeatmapSet!.Beatmaps.Where(bb => !bb.Hidden);
            var bMatchedBeatmaps = b.BeatmapSet!.Beatmaps.Where(bb => !bb.Hidden);

            bool aAny = aMatchedBeatmaps.Any();
            bool bAny = bMatchedBeatmaps.Any();

            if (!aAny && !bAny) return 0;
            if (!aAny) return -1;
            if (!bAny) return 1;

            return aMatchedBeatmaps.Max(func).CompareTo(bMatchedBeatmaps.Max(func));
        }
    }
}
