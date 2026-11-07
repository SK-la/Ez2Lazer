// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.EzOsuGame.Metrics
{
    /// <summary>
    /// A clone of a detached beatmap must not share the rating record with its source, or a write through one
    /// would be visible on the other. The generic detach guarantee lives in <see cref="RealmObjectDetachTests"/>.
    /// </summary>
    [TestFixture]
    public class EzRatingDataRealmTest : RealmTest
    {
        [Test]
        public void A_detached_beatmap_clone_does_not_share_its_record()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid id = Guid.NewGuid();

                realm.Write(r =>
                {
                    var ruleset = new RulesetInfo { OnlineID = 3, ShortName = "mania", Available = true };
                    r.Add(ruleset);

                    r.Add(new BeatmapInfo(ruleset, new BeatmapDifficulty { CircleSize = 4 }, new BeatmapMetadata())
                    {
                        ID = id,
                        Hash = "rating-clone-hash",
                        DifficultyName = "test",
                    });
                });

                realm.Write(r => r.Find<BeatmapInfo>(id)!.EzRatingData.MinaSR = 6.4);

                var detached = realm.Run(r => r.Find<BeatmapInfo>(id)!.Detach());
                var clone = detached.Clone();

                clone.EzRatingData.MinaSR = 1;

                Assert.That(detached.EzRatingData.MinaSR, Is.EqualTo(6.4));
            });
        }
    }
}
