// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSRulesetKeyCountDisplayTest
    {
        private static float invokeGetDisplayKeyCount(BeatmapInfo beatmapInfo)
        {
            var method = typeof(BMSRuleset).GetMethod("getDisplayKeyCount", BindingFlags.Static | BindingFlags.NonPublic)!;
            return (float)method.Invoke(null, new object?[] { beatmapInfo, null })!;
        }

        [Test]
        public void TestBmsBeatmapUsesStoredCircleSizeForKeyCount()
        {
            var ruleset = new BMSRuleset();
            var beatmapInfo = new BeatmapInfo(ruleset.RulesetInfo, new BeatmapDifficulty { CircleSize = 10, OverallDifficulty = 7 }, new BeatmapMetadata());

            Assert.That(invokeGetDisplayKeyCount(beatmapInfo), Is.EqualTo(10).Within(0.01));
        }

        [Test]
        public void TestBmsBeatmapDoesNotCollapseToManiaHeuristicKeyCount()
        {
            var ruleset = new BMSRuleset();
            var beatmapInfo = new BeatmapInfo(ruleset.RulesetInfo, new BeatmapDifficulty { CircleSize = 14, OverallDifficulty = 7 }, new BeatmapMetadata())
            {
                TotalObjectCount = 800,
                EndTimeObjectCount = 800,
            };

            Assert.That(invokeGetDisplayKeyCount(beatmapInfo), Is.EqualTo(14).Within(0.01));
        }

        [Test]
        public void TestGetAdjustedDisplayDifficultyPreservesKeyCount()
        {
            var ruleset = new BMSRuleset();
            var beatmapInfo = new BeatmapInfo(ruleset.RulesetInfo, new BeatmapDifficulty { CircleSize = 10, OverallDifficulty = 7 }, new BeatmapMetadata());

            var adjusted = ruleset.GetAdjustedDisplayDifficulty(beatmapInfo, []);

            Assert.That(adjusted.CircleSize, Is.EqualTo(10).Within(0.01));
        }
    }
}
