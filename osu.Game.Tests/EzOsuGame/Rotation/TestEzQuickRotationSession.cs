// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Screens.Rotation;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.EzOsuGame.Rotation
{
    [TestFixture]
    public class TestEzQuickRotationSession
    {
        [Test]
        public void End_clears_baseline_and_played_state()
        {
            var session = new EzQuickRotationSession();
            var beatmap = new BeatmapInfo
            {
                ID = Guid.NewGuid(),
                Ruleset = new ManiaRuleset().RulesetInfo,
                StarRating = 4.2,
                XxyStarRating = 5.5,
            };

            session.Begin(null!, new FilterCriteria(), beatmap, beatmap.Ruleset, Array.Empty<Mod>());
            session.MarkPlayed(beatmap);

            Assert.That(session.IsActive, Is.True);
            Assert.That(session.BaselineDifficulty, Is.GreaterThan(0));
            Assert.That(session.PlayedBeatmapIds, Contains.Item(beatmap.ID));

            session.End();

            Assert.That(session.IsActive, Is.False);
            Assert.That(session.BaselineDifficulty, Is.EqualTo(0));
            Assert.That(session.PlayedBeatmapIds, Is.Empty);
            Assert.That(session.CachedPool, Is.Empty);
            Assert.That(session.IsPoolReady, Is.False);
        }

        [Test]
        public void Begin_after_end_recomputes_provisional_baseline_from_new_first_beatmap()
        {
            var session = new EzQuickRotationSession();

            var first = new BeatmapInfo
            {
                ID = Guid.NewGuid(),
                Ruleset = new ManiaRuleset().RulesetInfo,
                StarRating = 2.0,
                XxyStarRating = 3.0,
            };

            var second = new BeatmapInfo
            {
                ID = Guid.NewGuid(),
                Ruleset = new OsuRuleset().RulesetInfo,
                StarRating = 6.5,
            };

            session.Begin(null!, new FilterCriteria(), first, first.Ruleset, Array.Empty<Mod>());
            double firstBaseline = session.BaselineDifficulty;

            session.End();
            session.Begin(null!, new FilterCriteria(), second, second.Ruleset, Array.Empty<Mod>());

            Assert.That(firstBaseline, Is.EqualTo(3.0));
            Assert.That(session.BaselineDifficulty, Is.EqualTo(6.5));
        }
    }
}
