// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Screens.Rotation;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.EzOsuGame.Rotation
{
    [TestFixture]
    public class TestEzQuickRotationPoolBuilder
    {
        private RulesetInfo maniaRuleset = null!;

        [SetUp]
        public void SetUp()
        {
            maniaRuleset = new ManiaRuleset().RulesetInfo;
        }

        [Test]
        public void TestCrossKeyModeExcludesTwelveKey()
        {
            var constraints = new EzQuickRotationPoolConstraints(null, null, maniaRuleset, null, CrossKeyMode: true);

            Assert.That(EzQuickRotationPoolBuilder.matchesKeyCount(createManiaBeatmap(4), constraints), Is.True);
            Assert.That(EzQuickRotationPoolBuilder.matchesKeyCount(createManiaBeatmap(7), constraints), Is.True);
            Assert.That(EzQuickRotationPoolBuilder.matchesKeyCount(createManiaBeatmap(10), constraints), Is.True);
            Assert.That(EzQuickRotationPoolBuilder.matchesKeyCount(createManiaBeatmap(12), constraints), Is.False);
            Assert.That(EzQuickRotationPoolBuilder.matchesKeyCount(createManiaBeatmap(3), constraints), Is.False);
        }

        [Test]
        public void TestLockedKeyModeOnlyAllowsMatchingKeyCount()
        {
            var constraints = new EzQuickRotationPoolConstraints(null, null, maniaRuleset, LockedKeyCount: 7, CrossKeyMode: false);

            Assert.That(EzQuickRotationPoolBuilder.matchesKeyCount(createManiaBeatmap(7), constraints), Is.True);
            Assert.That(EzQuickRotationPoolBuilder.matchesKeyCount(createManiaBeatmap(4), constraints), Is.False);
            Assert.That(EzQuickRotationPoolBuilder.matchesKeyCount(createManiaBeatmap(12), constraints), Is.False);
        }

        [Test]
        public void TestNonManiaRulesetIgnoresKeyCount()
        {
            var osuRuleset = new OsuRuleset().RulesetInfo;
            var constraints = new EzQuickRotationPoolConstraints(null, null, osuRuleset, LockedKeyCount: 4, CrossKeyMode: true);

            Assert.That(EzQuickRotationPoolBuilder.matchesKeyCount(createManiaBeatmap(12), constraints), Is.True);
        }

        private static BeatmapInfo createManiaBeatmap(int keyCount) => new BeatmapInfo
        {
            Difficulty = new BeatmapDifficulty
            {
                CircleSize = keyCount,
            },
        };
    }
}
