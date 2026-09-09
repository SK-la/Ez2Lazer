// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzChartSkillInfoRealmStoreTest : RealmTest
    {
        [Test]
        public void Upsert_round_trips_typed_columns_without_json_payload()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);
                const string hash = "chart-skill-roundtrip-hash";

                var original = new EzChartSkillInfo
                {
                    Patterns = new[] { "chordjack", "delay" },
                    JackDemand = true,
                    JackShare = 0.42,
                    StreamShare = null,
                    TechCategory = true,
                    ClusterTrill = false,
                    HandstreamCluster = null,
                    HandstreamEndurance = true,
                    TechScore = 0.7,
                    ChordjackScore = 0.9,
                    JackScore = 0.55,
                    Motion = new EzMotionFeatures
                    {
                        RhythmBreak = 0.1,
                        CrossHandTrill = 0.2,
                        MiniJack = 0.3,
                        SameHand = 0.4,
                    },
                    LnRatio = 0.12,
                    Vibro = false,
                    DanEligible = true,
                    LengthSeconds = 98.5,
                    KeyCount = 4,
                };

                store.UpsertChartSkillInfo(hash, original);

                Assert.That(store.TryGetChartSkillInfo(hash, out var loaded), Is.True);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded!.Patterns, Is.EquivalentTo(original.Patterns));
                Assert.That(loaded.JackDemand, Is.True);
                Assert.That(loaded.JackShare, Is.EqualTo(0.42).Within(1e-9));
                Assert.That(loaded.StreamShare, Is.Null);
                Assert.That(loaded.TechCategory, Is.True);
                Assert.That(loaded.ClusterTrill, Is.False);
                Assert.That(loaded.HandstreamCluster, Is.Null);
                Assert.That(loaded.HandstreamEndurance, Is.True);
                Assert.That(loaded.TechScore, Is.EqualTo(0.7).Within(1e-9));
                Assert.That(loaded.ChordjackScore, Is.EqualTo(0.9).Within(1e-9));
                Assert.That(loaded.JackScore, Is.EqualTo(0.55).Within(1e-9));
                Assert.That(loaded.Motion, Is.Not.Null);
                Assert.That(loaded.Motion!.RhythmBreak, Is.EqualTo(0.1).Within(1e-9));
                Assert.That(loaded.Motion.SameHand, Is.EqualTo(0.4).Within(1e-9));
                Assert.That(loaded.LnRatio, Is.EqualTo(0.12).Within(1e-9));
                Assert.That(loaded.LengthSeconds, Is.EqualTo(98.5).Within(1e-9));
                Assert.That(loaded.KeyCount, Is.EqualTo(4));

                realm.Run(r =>
                {
                    var row = r.All<EzBeatmapChartSkillInfo>().Single(v => v.BeatmapHash == hash);
                    Assert.That(row.GetType().GetProperty("PayloadJson"), Is.Null);
                    Assert.That(row.PatternTagsJoined.Split('\u001f'), Is.EquivalentTo(new[] { "chordjack", "delay" }));
                    Assert.That(row.StreamShare, Is.EqualTo(-1));
                });
            });
        }

        [Test]
        public void Skillset_cache_table_round_trips()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);

                store.WriteDanSkillsetValues("tester", 4, DanSkillSystem.SIDE_RC, new[]
                {
                    new EzPlayerDanSkillsetValue
                    {
                        SkillsetId = EzDanSkillsetBuckets.JACK,
                        RawDan = 12.5,
                        Label = "Chuudan",
                        Clears = 3,
                        AlgorithmVersion = EzDanAlgorithm.VERSION,
                        ComputedAt = DateTimeOffset.UtcNow,
                    },
                });

                var rows = store.GetDanSkillsetValues("tester", 4, DanSkillSystem.SIDE_RC);
                Assert.That(rows, Has.Count.EqualTo(1));
                Assert.That(rows[0].SkillsetId, Is.EqualTo(EzDanSkillsetBuckets.JACK));
                Assert.That(rows[0].RawDan, Is.EqualTo(12.5).Within(1e-9));
                Assert.That(rows[0].Label, Is.EqualTo("Chuudan"));
                Assert.That(rows[0].Clears, Is.EqualTo(3));
            });
        }

        [Test]
        public void File_schema_version_is_ez9()
        {
            Assert.That(RealmAccess.EZ_REALM_SCHEMA_VERSION, Is.EqualTo(9));
            Assert.That(RealmAccess.EzFileSchemaVersion, Is.EqualTo(RealmAccess.UpstreamSchemaVersion * 1000 + 9));
        }
    }
}
