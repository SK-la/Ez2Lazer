// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.Rulesets.Mania;

namespace osu.Game.Tests.EzOsuGame.Metrics
{
    /// <summary>
    /// One chart carries a star rating and pp per analysis system. The writer is the only way they are stored, and
    /// the official pair is mirrored onto the beatmap's own columns so upstream code keeps reading them.
    /// </summary>
    [TestFixture]
    public class EzRatingDataTest
    {
        [Test]
        public void Every_system_and_field_round_trips()
        {
            var data = new EzRatingData();
            int value = 1;

            foreach (var system in new[] { EzRatingSystem.Official, EzRatingSystem.Sunny, EzRatingSystem.Ppp, EzRatingSystem.Mina })
            {
                foreach (var field in new[] { EzRatingField.SR, EzRatingField.PP })
                {
                    data.Set(system, field, value);
                    Assert.That(data.Get(system, field), Is.EqualTo(value));
                    ++value;
                }
            }
        }

        [Test]
        public void Absent_system_reads_as_negative_one()
        {
            var data = new EzRatingData();

            Assert.That(data.Get(EzRatingSystem.Mina, EzRatingField.SR), Is.EqualTo(-1));
            Assert.That(data.Get(EzRatingSystem.None, EzRatingField.SR), Is.EqualTo(-1));
        }

        [Test]
        public void Official_write_mirrors_onto_the_beatmap_columns()
        {
            var beatmap = maniaBeatmap();

            EzRatingWriter.WriteSR(beatmap, EzRatingSystem.Official, 4.5);
            EzRatingWriter.WritePP(beatmap, EzRatingSystem.Official, 120);

            Assert.That(beatmap.StarRating, Is.EqualTo(4.5));
            Assert.That(beatmap.PerformancePoints, Is.EqualTo(120));
            Assert.That(beatmap.EzRatingData.OfficialSR, Is.EqualTo(4.5));
            Assert.That(beatmap.EzRatingData.OfficialPP, Is.EqualTo(120));
        }

        [Test]
        public void Other_systems_do_not_touch_the_official_columns()
        {
            var beatmap = maniaBeatmap();
            beatmap.StarRating = 3;

            EzRatingWriter.WriteSR(beatmap, EzRatingSystem.Sunny, 6);

            Assert.That(beatmap.StarRating, Is.EqualTo(3));
            Assert.That(beatmap.EzRatingData.SunnySR, Is.EqualTo(6));
        }

        [Test]
        public void Stored_official_rating_reads_from_the_beatmap_columns()
        {
            var beatmap = maniaBeatmap();
            beatmap.StarRating = 4.2;

            Assert.That(EzRulesetReadout.StoredRating(beatmap, EzRatingSystem.Official, EzRatingField.SR), Is.EqualTo(4.2));
        }

        [Test]
        public void Mina_star_is_stored_on_the_star_scale()
        {
            // What the write path persists for the Mina system: the engine's headline converted once, so the stored
            // value is already a star and readers never scale it again.
            double overall = 27;

            Assert.That(EzMinaRatingScale.ToStarRating(overall), Is.EqualTo(6));
        }

        [Test]
        public void A_new_beatmap_starts_with_every_system_absent()
        {
            var beatmap = maniaBeatmap();

            foreach (var system in new[] { EzRatingSystem.Official, EzRatingSystem.Sunny, EzRatingSystem.Ppp, EzRatingSystem.Mina })
            {
                Assert.That(beatmap.EzRatingData.Get(system, EzRatingField.SR), Is.EqualTo(-1));
                Assert.That(beatmap.EzRatingData.Get(system, EzRatingField.PP), Is.EqualTo(-1));
            }
        }

        private static BeatmapInfo maniaBeatmap() => new BeatmapInfo
        {
            Ruleset = new ManiaRuleset().RulesetInfo,
            Difficulty = new BeatmapDifficulty { CircleSize = 4 },
            DifficultyName = "test",
        };
    }
}
