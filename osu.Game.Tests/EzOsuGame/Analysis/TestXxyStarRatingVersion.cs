// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Difficulty;
using osu.Game.Rulesets.Mania.EzMania.Analysis;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class TestXxyStarRatingVersion
    {
        [Test]
        public void Mania_provider_exposes_date_version()
        {
            var provider = new EzManiaAnalysisProvider();
            Assert.That(provider.XxyStarRatingVersion, Is.EqualTo(EzManiaXxyStarRating.VERSION));
            Assert.That(provider.XxyStarRatingVersion, Is.GreaterThanOrEqualTo(20200101));
        }

        [Test]
        public void TryGetXxyStarRatingVersion_reads_from_provider_not_difficulty_calculator()
        {
            var rulesetInfo = new ManiaRuleset().RulesetInfo;
            rulesetInfo.Available = true;

            Assert.That(EzXxyStarRatingSupport.TryGetXxyStarRatingVersion(rulesetInfo, out int xxyVersion), Is.True);
            Assert.That(xxyVersion, Is.EqualTo(EzManiaXxyStarRating.VERSION));
        }
    }

    [TestFixture]
    public class TestPerformancePointsVersion
    {
        [Test]
        public void TryGetPerformancePointsVersion_uses_official_difficulty_calculator_version()
        {
            var rulesetInfo = new ManiaRuleset().RulesetInfo;
            rulesetInfo.Available = true;
            var workingBeatmap = new FlatWorkingBeatmap(new Beatmap());

            Assert.That(EzPerformancePointsSupport.TryGetPerformancePointsVersion(rulesetInfo, workingBeatmap, out int ppVersion), Is.True);
            Assert.That(ppVersion, Is.EqualTo(new ManiaDifficultyCalculator(rulesetInfo, workingBeatmap).Version));
            Assert.That(ppVersion, Is.Not.EqualTo(EzManiaXxyStarRating.VERSION));
        }
    }
}
