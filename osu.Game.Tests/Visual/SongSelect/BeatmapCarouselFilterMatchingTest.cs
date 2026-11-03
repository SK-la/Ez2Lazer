// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Carousel;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.SongSelect
{
    [TestFixture]
    public partial class BeatmapCarouselFilterMatchingTest
    {
        [Test]
        public async Task TestStarFilterUsesXxyWhenSortByXxyStarRating()
        {
            var inRange = createBeatmap(xxy: 5.0, starRating: 1.0);
            var outOfRange = createBeatmap(xxy: 1.0, starRating: 9.0);

            var results = await runMatching(
                new FilterCriteria
                {
                    Sort = SortMode.XxyStarRating,
                    StarDifficulty = new FilterCriteria.OptionalRange<double> { Min = 4, Max = 6, IsLowerInclusive = true, IsUpperInclusive = true },
                },
                [inRange, outOfRange],
                beatmaps => beatmaps.ToDictionary(b => b, resolveXxy));

            Assert.That(results, Is.EquivalentTo(new[] { inRange }));
        }

        [Test]
        public async Task TestStarFilterUsesOfficialStarRatingWhenSortByDifficulty()
        {
            var inRange = createBeatmap(xxy: 1.0, starRating: 5.0);
            var outOfRange = createBeatmap(xxy: 9.0, starRating: 1.0);

            var results = await runMatching(
                new FilterCriteria
                {
                    Sort = SortMode.Difficulty,
                    StarDifficulty = new FilterCriteria.OptionalRange<double> { Min = 4, Max = 6, IsLowerInclusive = true, IsUpperInclusive = true },
                },
                [inRange, outOfRange],
                beatmaps => beatmaps.ToDictionary(b => b, resolveXxy));

            Assert.That(results, Is.EquivalentTo(new[] { inRange }));
        }

        [Test]
        public async Task TestStarFilterUsesBranchXxyWhenProvided()
        {
            var inRange = createBeatmap(xxy: 1.0, starRating: 1.0);
            var outOfRange = createBeatmap(xxy: 9.0, starRating: 9.0);

            var results = await runMatching(
                new FilterCriteria
                {
                    Sort = SortMode.XxyStarRating,
                    StarDifficulty = new FilterCriteria.OptionalRange<double> { Min = 4, Max = 6, IsLowerInclusive = true, IsUpperInclusive = true },
                },
                [inRange, outOfRange],
                beatmaps => beatmaps.ToDictionary(b => b, b => b == inRange ? 5.0 : 1.0));

            Assert.That(results, Is.EquivalentTo(new[] { inRange }));
        }

        private static BeatmapInfo createBeatmap(double xxy, double starRating)
        {
            var set = TestResources.CreateTestBeatmapSetInfo(1);
            var beatmap = set.Beatmaps[0];
            beatmap.XxyStarRating = xxy;
            beatmap.StarRating = starRating;
            return beatmap;
        }

        private static double resolveXxy(BeatmapInfo beatmap) => beatmap.XxyStarRating >= 0 ? beatmap.XxyStarRating : beatmap.StarRating;

        private static async Task<IEnumerable<BeatmapInfo>> runMatching(
            FilterCriteria criteria,
            IReadOnlyList<BeatmapInfo> beatmaps,
            Func<IEnumerable<BeatmapInfo>, IReadOnlyDictionary<BeatmapInfo, double>>? xxyValueProvider = null)
        {
            var matcher = new BeatmapCarouselFilterMatching(
                () => criteria,
                (items, _) => Task.FromResult<IReadOnlyDictionary<BeatmapInfo, double>>(xxyValueProvider?.Invoke(items) ?? items.ToDictionary(b => b, resolveXxy)),
                (_, _) => Task.FromResult<IReadOnlyDictionary<BeatmapInfo, double>>(new Dictionary<BeatmapInfo, double>()));

            var results = await matcher.Run(beatmaps.Select(b => new CarouselItem(b)), CancellationToken.None);
            return results.Select(i => (BeatmapInfo)i.Model);
        }
    }
}
