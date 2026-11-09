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
        public async Task TestStarFilterUsesTheOfficialBaselineByDefault()
        {
            var inRange = createBeatmap(ssr: 1.0, starRating: 5.0);
            var outOfRange = createBeatmap(ssr: 9.0, starRating: 1.0);

            var results = await runMatching(
                criteriaFor(SortMode.Difficulty),
                [inRange, outOfRange]);

            Assert.That(results, Is.EquivalentTo(new[] { inRange }));
        }

        [Test]
        public async Task TestStarFilterUsesTheOperationsValueWhenProvided()
        {
            var inRange = createBeatmap(ssr: 1.0, starRating: 1.0);
            var outOfRange = createBeatmap(ssr: 9.0, starRating: 9.0);

            // A branch snapshot (or any other system-supplied set) overrides the stored baseline.
            var results = await runMatching(
                criteriaFor(SortMode.Difficulty),
                [inRange, outOfRange],
                beatmaps => beatmaps.ToDictionary(b => b, b => b.Equals(inRange) ? 5.0 : 1.0));

            Assert.That(results, Is.EquivalentTo(new[] { inRange }));
        }

        [Test]
        public async Task TestPpFilterUsesTheOperationsValueWhenProvided()
        {
            var inRange = createBeatmap(ssr: 1.0, starRating: 5.0);
            var outOfRange = createBeatmap(ssr: 1.0, starRating: 5.0);

            var criteria = new FilterCriteria
            {
                Sort = SortMode.PP,
                Pp = new FilterCriteria.OptionalRange<double> { Min = 100, Max = 200, IsLowerInclusive = true, IsUpperInclusive = true },
            };

            var matcher = new BeatmapCarouselFilterMatching(
                () => criteria,
                (_, _) => Task.FromResult<IReadOnlyDictionary<BeatmapInfo, double>>(new Dictionary<BeatmapInfo, double>()),
                (items, _) => Task.FromResult<IReadOnlyDictionary<BeatmapInfo, double>>(
                    items.ToDictionary(b => b, b => b.Equals(inRange) ? 150.0 : 50.0)));

            var results = await matcher.Run(new[] { inRange, outOfRange }.Select(b => new CarouselItem(b)), CancellationToken.None);

            Assert.That(results.Select(i => (BeatmapInfo)i.Model), Is.EquivalentTo(new[] { inRange }));
        }

        [Test]
        public async Task TestSkillTagFilterMatchesExactIdsOnly()
        {
            var jackOnly = createBeatmap(ssr: 1.0, starRating: 5.0);
            var chordjack = createBeatmap(ssr: 1.0, starRating: 5.0);
            var both = createBeatmap(ssr: 1.0, starRating: 5.0);

            var criteria = new FilterCriteria
            {
                SkillTags =
                {
                    new FilterCriteria.OptionalTextFilter { SearchTerm = "jack" },
                },
            };

            var tags = new Dictionary<BeatmapInfo, string[]>
            {
                [jackOnly] = ["jack"],
                [chordjack] = ["chordjack"],
                [both] = ["jack", "chordjack"],
            };

            var matcher = new BeatmapCarouselFilterMatching(
                () => criteria,
                (_, _) => Task.FromResult<IReadOnlyDictionary<BeatmapInfo, double>>(new Dictionary<BeatmapInfo, double>()),
                (_, _) => Task.FromResult<IReadOnlyDictionary<BeatmapInfo, double>>(new Dictionary<BeatmapInfo, double>()),
                (items, _) => Task.FromResult<IReadOnlyDictionary<BeatmapInfo, string[]>>(
                    items.ToDictionary(b => b, b => tags.TryGetValue(b, out var t) ? t : [])));

            var results = await matcher.Run(new[] { jackOnly, chordjack, both }.Select(b => new CarouselItem(b)), CancellationToken.None);

            Assert.That(results.Select(i => (BeatmapInfo)i.Model), Is.EquivalentTo(new[] { jackOnly, both }));
        }

        [Test]
        public void TestSkillTagExcludeRejectsExactId()
        {
            var jack = createBeatmap(ssr: 1.0, starRating: 5.0);
            var criteria = new FilterCriteria
            {
                SkillTags =
                {
                    new FilterCriteria.OptionalTextFilter { SearchTerm = "jack", ExcludeTerm = true },
                },
            };

            Assert.That(BeatmapCarouselFilterMatching.CheckCriteriaMatch(jack, criteria, starDifficultyForFilter: 5, skillTags: ["jack"]), Is.False);
            Assert.That(BeatmapCarouselFilterMatching.CheckCriteriaMatch(jack, criteria, starDifficultyForFilter: 5, skillTags: ["chordjack"]), Is.True);
        }

        private static FilterCriteria criteriaFor(SortMode sort) => new FilterCriteria
        {
            Sort = sort,
            StarDifficulty = new FilterCriteria.OptionalRange<double> { Min = 4, Max = 6, IsLowerInclusive = true, IsUpperInclusive = true },
        };

        private static BeatmapInfo createBeatmap(double ssr, double starRating)
        {
            var set = TestResources.CreateTestBeatmapSetInfo(1);
            var beatmap = set.Beatmaps[0];
            beatmap.EzRatingData.SunnySR = ssr;
            beatmap.StarRating = starRating;
            return beatmap;
        }

        private static async Task<IEnumerable<BeatmapInfo>> runMatching(
            FilterCriteria criteria,
            IReadOnlyList<BeatmapInfo> beatmaps,
            Func<IEnumerable<BeatmapInfo>, IReadOnlyDictionary<BeatmapInfo, double>>? difficultyValueProvider = null)
        {
            var matcher = new BeatmapCarouselFilterMatching(
                () => criteria,
                (items, _) => Task.FromResult<IReadOnlyDictionary<BeatmapInfo, double>>(difficultyValueProvider?.Invoke(items) ?? new Dictionary<BeatmapInfo, double>()),
                (_, _) => Task.FromResult<IReadOnlyDictionary<BeatmapInfo, double>>(new Dictionary<BeatmapInfo, double>()));

            var results = await matcher.Run(beatmaps.Select(b => new CarouselItem(b)), CancellationToken.None);
            return results.Select(i => (BeatmapInfo)i.Model);
        }
    }
}
