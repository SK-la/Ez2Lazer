// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Carousel;
using osu.Game.Screens.Select.Filter;
using osu.Game.Utils;

namespace osu.Game.Screens.Select
{
    public class BeatmapCarouselFilterSorting : ICarouselFilter
    {
        public int BeatmapItemsCount { get; private set; }

        private readonly Func<FilterCriteria> getCriteria;
        private readonly Func<bool> shouldUseXxySrForDifficultyOperations;
        private readonly Func<IEnumerable<BeatmapInfo>, CancellationToken, Task<IReadOnlyDictionary<BeatmapInfo, double>>> getDifficultiesForOperationsAsync;

        public BeatmapCarouselFilterSorting(Func<FilterCriteria> getCriteria, Func<bool> shouldUseXxySrForDifficultyOperations,
                                            Func<IEnumerable<BeatmapInfo>, CancellationToken, Task<IReadOnlyDictionary<BeatmapInfo, double>>> getDifficultiesForOperationsAsync)
        {
            this.getCriteria = getCriteria;
            this.shouldUseXxySrForDifficultyOperations = shouldUseXxySrForDifficultyOperations;
            this.getDifficultiesForOperationsAsync = getDifficultiesForOperationsAsync;
        }

        public async Task<List<CarouselItem>> Run(IEnumerable<CarouselItem> items, CancellationToken cancellationToken)
        {
            var criteria = getCriteria();
            var itemList = items.ToList();

            bool groupedSets = BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(criteria);
            // xxy_SR is only required when difficulty is the active sort key.
            // For 0/1 item, sorting does not require any key fetches.
            bool useXxyDifficulty = shouldUseXxySrForDifficultyOperations()
                                    && criteria.Sort == SortMode.Difficulty
                                    && itemList.Count > 1;

            IReadOnlyDictionary<BeatmapInfo, double>? operationDifficulties = null;

            if (useXxyDifficulty)
            {
                var uniqueBeatmaps = itemList.Select(i => (BeatmapInfo)i.Model).Distinct().ToList();
                operationDifficulties = await getDifficultiesForOperationsAsync(uniqueBeatmaps, cancellationToken).ConfigureAwait(false);
            }

            double getDifficulty(BeatmapInfo beatmap)
            {
                if (operationDifficulties != null && operationDifficulties.TryGetValue(beatmap, out double difficulty))
                    return difficulty;

                return beatmap.StarRating;
            }

            BeatmapItemsCount = itemList.Count;

            return itemList.Order(Comparer<CarouselItem>.Create((a, b) =>
            {
                var ab = (BeatmapInfo)a.Model;
                var bb = (BeatmapInfo)b.Model;

                if (groupedSets)
                {
                    if (ab.BeatmapSet!.Equals(bb.BeatmapSet))
                        return compareDifficulty(ab, bb, criteria.Sort, getDifficulty);

                    // If we're grouping by sets, all fallback sorts need to be aggregates for the set.
                    return compare(ab, bb, criteria.Sort, aggregate: true, getDifficulty);
                }

                return compare(ab, bb, criteria.Sort, aggregate: false, getDifficulty);
            })).ToList();
        }

        private static int compare(BeatmapInfo a, BeatmapInfo b, SortMode sort, bool aggregate, Func<BeatmapInfo, double> getDifficulty)
        {
            int comparison;

            switch (sort)
            {
                case SortMode.Artist:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Artist, b.BeatmapSet!.Metadata.Artist);
                    if (comparison == 0)
                        goto case SortMode.Title;
                    break;

                case SortMode.Title:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Title, b.BeatmapSet!.Metadata.Title);
                    break;

                case SortMode.Author:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Author.Username, b.BeatmapSet!.Metadata.Author.Username);
                    break;

                case SortMode.Source:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Source, b.BeatmapSet!.Metadata.Source);
                    break;

                case SortMode.Difficulty:
                    comparison = getDifficulty(a).CompareTo(getDifficulty(b));
                    break;

                case SortMode.DateAdded:
                    comparison = b.BeatmapSet!.DateAdded.CompareTo(a.BeatmapSet!.DateAdded);
                    break;

                case SortMode.DateRanked:
                    comparison = Nullable.Compare(b.BeatmapSet!.DateRanked, a.BeatmapSet!.DateRanked);
                    break;

                case SortMode.DateSubmitted:
                    comparison = Nullable.Compare(b.BeatmapSet!.DateSubmitted, a.BeatmapSet!.DateSubmitted);
                    break;

                case SortMode.LastPlayed:
                    if (aggregate)
                        comparison = compareUsingAggregateMax(b, a, static b => (b.LastPlayed ?? DateTimeOffset.MinValue).ToUnixTimeSeconds());
                    else
                        comparison = Nullable.Compare(b.LastPlayed, a.LastPlayed);
                    break;

                case SortMode.BPM:
                    if (aggregate)
                        comparison = compareUsingAggregateMax(a, b, static b => b.BPM);
                    else
                        comparison = a.BPM.CompareTo(b.BPM);
                    break;

                case SortMode.Length:
                    if (aggregate)
                        comparison = compareUsingAggregateMax(a, b, static b => b.Length);
                    else
                        comparison = a.Length.CompareTo(b.Length);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            // If the initial sort could not differentiate, attempt to use DateAdded to order sets in a stable fashion.
            // The directionality of this matches the current SortMode.DateAdded, but we may want to reconsider if that becomes a user decision (ie. asc / desc).
            if (comparison == 0)
                comparison = b.BeatmapSet!.DateAdded.CompareTo(a.BeatmapSet!.DateAdded);

            // If DateAdded fails to break the tie, fallback to our internal GUID for stability.
            // This basically means it's a stable random sort.
            if (comparison == 0)
                comparison = b.BeatmapSet!.ID.CompareTo(a.BeatmapSet!.ID);

            return comparison;
        }

        private static int compareDifficulty(BeatmapInfo a, BeatmapInfo b, SortMode sort, Func<BeatmapInfo, double> getDifficulty)
        {
            int comparison = a.Ruleset.CompareTo(b.Ruleset);

            if (comparison == 0)
                comparison = getDifficulty(a).CompareTo(getDifficulty(b));

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
