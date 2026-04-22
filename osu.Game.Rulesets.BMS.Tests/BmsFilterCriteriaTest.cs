// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BmsFilterCriteriaTest
    {
        [Test]
        public void Matches_uses_circle_size_as_key_count_not_mania_heuristic()
        {
            var filter = new BmsFilterCriteria();
            var criteria = new FilterCriteria();

            filter.TryParseCustomKeywordCriteria("keys", Operator.Equal, "14");

            var beatmap = new BeatmapInfo(new BMSRuleset().RulesetInfo, new BeatmapDifficulty { CircleSize = 14 }, new BeatmapMetadata())
            {
                TotalObjectCount = 100,
                EndTimeObjectCount = 10,
            };

            Assert.That(filter.Matches(beatmap, criteria), Is.True);

            beatmap.Difficulty.CircleSize = 7;
            Assert.That(filter.Matches(beatmap, criteria), Is.False);
        }
    }
}
