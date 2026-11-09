// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    [TestFixture]
    public class EzRatingBackfillGateTest : RealmTest
    {
        [Test]
        public void Official_failure_stamp_leaves_automatic_backfill()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid id = seedBeatmap(realm, starRating: -1);

                realm.Write(r =>
                {
                    var beatmap = r.Find<BeatmapInfo>(id)!;
                    Assert.That(EzRatingWriter.NeedsOfficialBackfill(beatmap), Is.True);

                    EzRatingWriter.MarkProcessingFailed(beatmap, EzAnalysisSystem.Official);

                    Assert.That(beatmap.StarRating, Is.EqualTo(0));
                    Assert.That(beatmap.PerformancePoints, Is.EqualTo(0));
                    Assert.That(EzRatingWriter.NeedsOfficialBackfill(beatmap), Is.False);
                });
            });
        }

        [Test]
        public void Official_clear_after_failure_stamp_requeues_for_manual_rebuild()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid id = seedBeatmap(realm, starRating: -1);

                realm.Write(r =>
                {
                    var beatmap = r.Find<BeatmapInfo>(id)!;
                    EzRatingWriter.MarkProcessingFailed(beatmap, EzAnalysisSystem.Official);
                    EzRatingWriter.Clear(beatmap, EzAnalysisSystem.Official);

                    Assert.That(beatmap.StarRating, Is.EqualTo(-1));
                    Assert.That(EzRatingWriter.NeedsOfficialBackfill(beatmap), Is.True);
                });
            });
        }

        [Test]
        public void Official_star_present_with_missing_pp_is_not_owed()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid id = seedBeatmap(realm, starRating: 4.5);

                realm.Write(r =>
                {
                    var beatmap = r.Find<BeatmapInfo>(id)!;
                    beatmap.PerformancePoints = -1;

                    Assert.That(EzRatingWriter.NeedsOfficialBackfill(beatmap), Is.False);
                });
            });
        }

        [Test]
        public void Sunny_failure_stamp_leaves_missing_set()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid id = seedBeatmap(realm, starRating: -1);

                realm.Write(r =>
                {
                    var beatmap = r.Find<BeatmapInfo>(id)!;
                    Assert.That(beatmap.EzRatingData.SunnySR, Is.EqualTo(-1));

                    EzRatingWriter.MarkProcessingFailed(beatmap, EzAnalysisSystem.SSR);

                    Assert.That(beatmap.EzRatingData.SunnySR, Is.EqualTo(0));
                });
            });
        }

        [Test]
        public void Multi_system_baseline_owes_sunny_pp_only_when_sunny_star_is_stored()
        {
            var withSource = EzRatingWriter.MultiSystemBaselineDebt(sunnySr: 5, sunnyPp: -1, minaSr: 0, minaOverall: null);
            Assert.That(withSource.NeedSunnyPp, Is.True);
            Assert.That(withSource.NeedMinaSr, Is.False);

            var withoutSource = EzRatingWriter.MultiSystemBaselineDebt(sunnySr: -1, sunnyPp: -1, minaSr: 0, minaOverall: null);
            Assert.That(withoutSource.NeedSunnyPp, Is.False);
            Assert.That(withoutSource.NeedMinaSr, Is.False);
        }

        [Test]
        public void Multi_system_baseline_owes_mina_sr_only_when_overall_axis_is_stored()
        {
            var withAxis = EzRatingWriter.MultiSystemBaselineDebt(sunnySr: 0, sunnyPp: 0, minaSr: -1, minaOverall: 18);
            Assert.That(withAxis.NeedSunnyPp, Is.False);
            Assert.That(withAxis.NeedMinaSr, Is.True);

            var withoutAxis = EzRatingWriter.MultiSystemBaselineDebt(sunnySr: 0, sunnyPp: 0, minaSr: -1, minaOverall: null);
            Assert.That(withoutAxis.NeedSunnyPp, Is.False);
            Assert.That(withoutAxis.NeedMinaSr, Is.False);
        }

        [Test]
        public void Multi_system_baseline_ignores_charts_with_neither_source()
        {
            var debt = EzRatingWriter.MultiSystemBaselineDebt(sunnySr: -1, sunnyPp: -1, minaSr: -1, minaOverall: null);
            Assert.That(debt.NeedSunnyPp, Is.False);
            Assert.That(debt.NeedMinaSr, Is.False);
        }

        private static Guid seedBeatmap(RealmAccess realm, double starRating)
        {
            return realm.Write(r =>
            {
                var ruleset = r.All<RulesetInfo>().FirstOrDefault(s => s.ShortName == "osu");

                if (ruleset == null)
                {
                    ruleset = new RulesetInfo { OnlineID = 0, ShortName = "osu", Available = true };
                    r.Add(ruleset);
                }

                var set = new BeatmapSetInfo();
                r.Add(set);

                var beatmap = new BeatmapInfo
                {
                    BeatmapSet = set,
                    Ruleset = ruleset,
                    DifficultyName = "gate-test",
                    Difficulty = new BeatmapDifficulty(),
                    StarRating = starRating,
                    EzRatingData = new EzRatingData(),
                };

                r.Add(beatmap);
                return beatmap.ID;
            });
        }
    }
}
