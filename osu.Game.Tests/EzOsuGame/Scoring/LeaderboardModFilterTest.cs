// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Mods.LAsMods;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Taiko.Mods;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    [TestFixture]
    public class LeaderboardModFilterTest
    {
        private readonly LeaderboardModImpactUnit impactUnit = LeaderboardModImpactUnit.CreateDefault();

        [Test]
        public void TestLoopPlayClipIsConversionNotRate()
        {
            var loop = new UniversalLoopPlayClip();

            Assert.That(impactUnit.GetRelevantMods(new[] { loop }, LeaderboardModFilter.Rate), Is.Empty);
            Assert.That(impactUnit.GetRelevantMods(new[] { loop }, LeaderboardModFilter.Conversion), Is.EqualTo(new[] { loop }));
            Assert.That(impactUnit.ModsMatch(new Mod[] { loop }, Array.Empty<Mod>(), LeaderboardModFilter.Rate), Is.True);
            Assert.That(impactUnit.ModsMatch(new Mod[] { loop }, Array.Empty<Mod>(), LeaderboardModFilter.Conversion), Is.False);
        }

        [Test]
        public void TestJudgementModsAreDifficultyNotConversion()
        {
            var mods = new Mod[]
            {
                new ManiaModJudgmentsAdjust(),
                new ManiaModLNJudgementAdjust(),
                new ManiaModRemedy()
            };

            Assert.That(impactUnit.GetRelevantMods(mods, LeaderboardModFilter.Difficulty), Is.EqualTo(mods));
            Assert.That(impactUnit.GetRelevantMods(mods, LeaderboardModFilter.Conversion), Is.Empty);
        }

        [Test]
        public void TestAllCombinesCategoryExceptions()
        {
#pragma warning disable CS0618
            var health = new ManiaModO2Health();
#pragma warning restore CS0618
            var maniaConstantSpeed = new ManiaModConstantSpeed();
            var taikoConstantSpeed = new TaikoModConstantSpeed();
            var classic = new OsuModClassic();
            var loop = new UniversalLoopPlayClip();

            Assert.That(impactUnit.GetRelevantMods(new[] { maniaConstantSpeed }, LeaderboardModFilter.Rate), Is.Empty);
            Assert.That(impactUnit.GetRelevantMods(new[] { maniaConstantSpeed }, LeaderboardModFilter.Conversion), Is.Empty);
            Assert.That(impactUnit.GetRelevantMods(new[] { taikoConstantSpeed }, LeaderboardModFilter.Conversion), Is.Empty);
            Assert.That(impactUnit.GetRelevantMods(new[] { classic }, LeaderboardModFilter.Difficulty | LeaderboardModFilter.Conversion), Is.Empty);
            Assert.That(impactUnit.GetRelevantMods(new[] { health }, LeaderboardModFilter.Conversion), Is.Empty);

            Mod[] exceptions = [new ModNoMod(), maniaConstantSpeed, taikoConstantSpeed, classic, health];
            Assert.That(impactUnit.GetRelevantMods(exceptions, LeaderboardModFilter.All), Is.Empty);
            Assert.That(impactUnit.ModsMatch(exceptions, Array.Empty<Mod>(), LeaderboardModFilter.All), Is.True);
            Assert.That(impactUnit.ModsMatch(new Mod[] { new ModScoreV2() }, Array.Empty<Mod>(), LeaderboardModFilter.All), Is.True);
            Assert.That(impactUnit.ModsMatch(new Mod[] { new ManiaModScoreV2() }, Array.Empty<Mod>(), LeaderboardModFilter.All), Is.True);
            Assert.That(impactUnit.ModsMatch(new Mod[] { loop }, Array.Empty<Mod>(), LeaderboardModFilter.All), Is.False);
        }

        [Test]
        public void TestAllMatchesModsOutsideIndividualCategories()
        {
            Mod[] hidden = [new OsuModHidden()];

            Assert.That(impactUnit.ModsMatch(hidden, Array.Empty<Mod>(), LeaderboardModFilter.Rate | LeaderboardModFilter.Difficulty), Is.True);
            Assert.That(impactUnit.ModsMatch(hidden, Array.Empty<Mod>(), LeaderboardModFilter.All), Is.False);
        }

        [TestCase(LeaderboardModFilter.None, "原版匹配")]
        [TestCase(LeaderboardModFilter.Rate, "变速")]
        [TestCase(LeaderboardModFilter.Difficulty, "难度")]
        [TestCase(LeaderboardModFilter.Conversion, "谱面转换")]
        [TestCase(LeaderboardModFilter.All, "全部已选模组")]
        public void TestFilterDescriptionsAreLocalised(LeaderboardModFilter filter, string expected)
        {
            CultureInfo previousCulture = CultureInfo.CurrentUICulture;

            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                Assert.That(filter.GetLocalisableDescription().ToString(), Is.EqualTo(expected));
            }
            finally
            {
                CultureInfo.CurrentUICulture = previousCulture;
            }
        }

        [Test]
        public void TestConstantSpeedSettingDifferenceKeepsModRelevant()
        {
            var selected = new ManiaModAdjust();
            var score = new ManiaModAdjust
            {
                ConstantSpeed = { Value = !selected.ConstantSpeed.Value },
            };

            Assert.That(impactUnit.ModsMatch(new Mod[] { score }, new Mod[] { selected }, LeaderboardModFilter.Difficulty), Is.False);
        }

        [Test]
        public void TestNoSvSettingDoesNotFilterLeaderboardScores()
        {
            var selected = new ManiaModAdjust
            {
                OverallDifficulty = { Value = 4 },
            };
            var score = new ManiaModAdjust
            {
                ConstantSpeed = { Value = !selected.ConstantSpeed.Value },
                OverallDifficulty = { Value = 4 },
            };

            Assert.That(impactUnit.ModsMatch(new Mod[] { score }, new Mod[] { selected }, LeaderboardModFilter.All), Is.False);

            score.OverallDifficulty.Value = 5;
            Assert.That(impactUnit.ModsMatch(new Mod[] { score }, new Mod[] { selected }, LeaderboardModFilter.Difficulty), Is.False);
        }

        [Test]
        public void TestOnlySelectedCategoriesParticipateInMatching()
        {
            var selected = new ManiaModJudgmentsAdjust
            {
                PerfectHit = { Value = 20 },
            };
            var score = new ManiaModJudgmentsAdjust
            {
                PerfectHit = { Value = 25 },
            };

            Assert.That(impactUnit.ModsMatch(new Mod[] { score }, new Mod[] { selected }, LeaderboardModFilter.Rate), Is.True);
            Assert.That(impactUnit.ModsMatch(new Mod[] { score }, new Mod[] { selected }, LeaderboardModFilter.Difficulty), Is.False);
        }

        [Test]
        public void TestRateSettingsParticipateInRateMatching()
        {
            var selected = new OsuModDoubleTime();
            var score = new OsuModDoubleTime
            {
                SpeedChange = { Value = 1.75 },
            };

            Assert.That(impactUnit.ModsMatch(new Mod[] { selected }, new Mod[] { score }, LeaderboardModFilter.Rate), Is.False);

            score.SpeedChange.Value = selected.SpeedChange.Value;
            Assert.That(impactUnit.ModsMatch(new Mod[] { selected }, new Mod[] { score }, LeaderboardModFilter.Rate), Is.True);
        }

        [Test]
        public void TestCombinedCategoriesAndUnrelatedMods()
        {
            var selected = new Mod[] { new ManiaModJudgmentsAdjust(), new OsuModDoubleTime() };
            var scoreWithUnrelated = new Mod[] { new ManiaModJudgmentsAdjust(), new OsuModDoubleTime(), new OsuModNoFail() };

            Assert.That(impactUnit.ModsMatch(scoreWithUnrelated, selected, LeaderboardModFilter.Rate | LeaderboardModFilter.Difficulty), Is.True);

            var differentRate = new OsuModDoubleTime
            {
                SpeedChange = { Value = 1.75 },
            };
            var scoreWithDifferentSelectedCategory = new Mod[] { new ManiaModJudgmentsAdjust(), differentRate, new OsuModNoFail() };

            Assert.That(impactUnit.ModsMatch(scoreWithDifferentSelectedCategory, selected, LeaderboardModFilter.Rate | LeaderboardModFilter.Difficulty), Is.False);
            Assert.That(impactUnit.ModsMatch(scoreWithDifferentSelectedCategory, selected, LeaderboardModFilter.Difficulty), Is.True);
        }

        [Test]
        public void TestSelectedDifficultyCategoryMatchesJudgeMode()
        {
            Assert.That(EzLocalScoreQueries.JudgeModeMatches((int)EzEnumJudgeMode.StableClassic, EzEnumJudgeMode.StableClassic, LeaderboardModFilter.Difficulty), Is.True);
            Assert.That(EzLocalScoreQueries.JudgeModeMatches((int)EzEnumJudgeMode.Lazer, EzEnumJudgeMode.StableClassic, LeaderboardModFilter.Difficulty), Is.False);
            Assert.That(EzLocalScoreQueries.JudgeModeMatches((int)EzEnumJudgeMode.Lazer, EzEnumJudgeMode.StableClassic, LeaderboardModFilter.Rate), Is.True);
            Assert.That(EzLocalScoreQueries.JudgeModeMatches(-1, EzEnumJudgeMode.Lazer, LeaderboardModFilter.Difficulty), Is.True);
        }
    }
}
