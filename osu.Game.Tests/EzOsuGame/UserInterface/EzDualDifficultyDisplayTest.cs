// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Screens.Rotation;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.EzOsuGame.UserInterface
{
    [TestFixture]
    public class EzDualDifficultyDisplayTest
    {
        [Test]
        public void CardBadgeIcon_uses_moon_for_mania_with_xxy_support()
        {
            var beatmap = createManiaBeatmap(xxyStarRating: 5.2);

            Assert.That(EzQuickRotationCardDifficultyDisplay.ResolveBadgeIcon(beatmap), Is.EqualTo(FontAwesome.Solid.Moon));
        }

        [Test]
        public void CardBadgeIcon_uses_star_for_osu()
        {
            var beatmap = new BeatmapInfo { Ruleset = new OsuRuleset().RulesetInfo, StarRating = 4.5 };

            Assert.That(EzQuickRotationCardDifficultyDisplay.ResolveBadgeIcon(beatmap), Is.EqualTo(FontAwesome.Solid.Star));
        }

        [Test]
        public void CardBadgeRating_prefers_persisted_xxy_for_mania()
        {
            var beatmap = createManiaBeatmap(starRating: 3.1, xxyStarRating: 5.8);

            Assert.That(EzQuickRotationCardDifficultyDisplay.ResolveBadgeRating(beatmap), Is.EqualTo(5.8));
        }

        [Test]
        public void CardBadgeRating_falls_back_to_official_star_when_xxy_missing()
        {
            var beatmap = createManiaBeatmap(starRating: 3.1, xxyStarRating: -1);

            Assert.That(EzQuickRotationCardDifficultyDisplay.ResolveBadgeRating(beatmap), Is.EqualTo(3.1));
        }

        [Test]
        public void ShouldShowXxyDisplay_true_for_mania()
        {
            var beatmap = createManiaBeatmap();

            Assert.That(EzDualDifficultyDisplay.ShouldShowXxyDisplay(beatmap), Is.True);
        }

        [Test]
        public void ShouldShowXxyDisplay_false_for_osu()
        {
            var beatmap = new BeatmapInfo { Ruleset = new OsuRuleset().RulesetInfo };

            Assert.That(EzDualDifficultyDisplay.ShouldShowXxyDisplay(beatmap), Is.False);
        }

        private static BeatmapInfo createManiaBeatmap(double starRating = 0, double xxyStarRating = 4.2)
        {
            return new BeatmapInfo
            {
                Ruleset = new ManiaRuleset().RulesetInfo,
                StarRating = starRating,
                XxyStarRating = xxyStarRating,
            };
        }
    }
}
