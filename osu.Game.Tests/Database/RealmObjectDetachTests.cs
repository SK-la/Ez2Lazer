// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using Realms;

namespace osu.Game.Tests.Database
{
    /// <summary>
    /// A detached beatmap must not hold a reference into the realm it came from: the realm closes once the read
    /// ends, and any later access throws <see cref="Realms.Exceptions.RealmClosedException"/>.
    /// </summary>
    /// <remarks>
    /// The detach mapper only copies nested objects it has an explicit <c>CreateMap</c> for, and silently leaves
    /// the rest managed. This walks the whole object graph instead of naming the types, so a new embedded or
    /// related object is covered the moment it is added to the model.
    /// </remarks>
    [TestFixture]
    public class RealmObjectDetachTests : RealmTest
    {
        [Test]
        public void Detached_beatmap_holds_no_managed_object()
        {
            RunTestWithRealm((realm, _) =>
            {
                var ids = seedBeatmapSet(realm);

                var detached = realm.Run(r => r.Find<BeatmapInfo>(ids.BeatmapId)!.Detach());

                assertNoManagedObject(detached);
            });
        }

        [Test]
        public void Detached_beatmap_set_holds_no_managed_object()
        {
            RunTestWithRealm((realm, _) =>
            {
                var ids = seedBeatmapSet(realm);

                var detached = realm.Run(r => r.Find<BeatmapSetInfo>(ids.SetId)!.Detach());

                assertNoManagedObject(detached);
            });
        }

        [Test]
        public void Detached_objects_carry_their_values()
        {
            RunTestWithRealm((realm, _) =>
            {
                var ids = seedBeatmapSet(realm);

                var detached = realm.Run(r => r.Find<BeatmapInfo>(ids.BeatmapId)!.Detach());

                Assert.That(detached.Difficulty.ApproachRate, Is.EqualTo(9.3f));
                Assert.That(detached.Metadata.Title, Is.EqualTo("Detach Title"));
                Assert.That(detached.EzRatingData.MinaSR, Is.EqualTo(6.4));
            });
        }

        private static (Guid BeatmapId, Guid SetId) seedBeatmapSet(RealmAccess realm)
        {
            Guid beatmapId = Guid.NewGuid();
            Guid setId = Guid.NewGuid();

            realm.Write(r =>
            {
                var ruleset = new RulesetInfo { OnlineID = 3, ShortName = "mania", Available = true };
                r.Add(ruleset);

                var metadata = new BeatmapMetadata { Title = "Detach Title", Artist = "Detach Artist" };
                var set = new BeatmapSetInfo { ID = setId };

                r.Add(set);

                r.Add(new BeatmapInfo(ruleset, new BeatmapDifficulty { CircleSize = 4, ApproachRate = 9.3f }, metadata)
                {
                    ID = beatmapId,
                    BeatmapSet = set,
                    Hash = "detach-graph-hash",
                    DifficultyName = "detach",
                });
            });

            realm.Write(r => r.Find<BeatmapInfo>(beatmapId)!.EzRatingData.MinaSR = 6.4);

            return (beatmapId, setId);
        }

        /// <summary>
        /// Walks the graph the same way a reader would and asserts that nothing reachable is still realm-managed.
        /// </summary>
        private static void assertNoManagedObject(object root)
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var pending = new Queue<object>();

            pending.Enqueue(root);

            while (pending.Count > 0)
            {
                object current = pending.Dequeue();

                if (!visited.Add(current))
                    continue;

                if (current is RealmObjectBase realmObject)
                    Assert.That(realmObject.IsManaged, Is.False, $"{realmObject.GetType().Name} is still managed by realm");

                foreach (var property in current.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    // A backlink is a live query into the realm by definition, and an ignored member is not state.
                    if (property.GetIndexParameters().Length > 0
                        || property.GetMethod == null
                        || !property.GetMethod.IsPublic
                        || property.GetCustomAttribute<IgnoredAttribute>() != null
                        || property.GetCustomAttribute<BacklinkAttribute>() != null)
                    {
                        continue;
                    }

                    object? value;

                    try
                    {
                        value = property.GetValue(current);
                    }
                    catch
                    {
                        // A detached object can still expose members that only answer while managed; skip those.
                        continue;
                    }

                    if (value is string || value is RealmObjectBase)
                    {
                        if (value is RealmObjectBase related)
                            pending.Enqueue(related);

                        continue;
                    }

                    if (value is not IEnumerable enumerable)
                        continue;

                    foreach (object? item in enumerable)
                    {
                        if (item is RealmObjectBase related)
                            pending.Enqueue(related);
                    }
                }
            }
        }
    }
}
