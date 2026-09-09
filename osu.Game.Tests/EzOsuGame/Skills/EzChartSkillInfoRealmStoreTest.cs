// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
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

                store.WriteDanSkillsetVerdicts("tester", 4, DanSkillSystem.SIDE_RC, new Dictionary<string, EzDanSkillsetVerdict>
                {
                    [EzDanSkillsetBuckets.JACK] = new EzDanSkillsetVerdict(EzDanSkillsetBuckets.JACK, 12.5, "Chuudan", 3),
                });

                Assert.That(store.HasDanSkillsetCache("tester", 4, DanSkillSystem.SIDE_RC), Is.True);
                var rows = store.GetDanSkillsetValues("tester", 4, DanSkillSystem.SIDE_RC);
                Assert.That(rows, Has.Count.EqualTo(1));
                Assert.That(rows[0].SkillsetId, Is.EqualTo(EzDanSkillsetBuckets.JACK));
                Assert.That(rows[0].RawDan, Is.EqualTo(12.5).Within(1e-9));
                Assert.That(rows[0].Label, Is.EqualTo("Chuudan"));
                Assert.That(rows[0].Clears, Is.EqualTo(3));
            });
        }

        [Test]
        public void Skillset_empty_verdicts_write_sentinel_so_has_cache_is_true()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);

                store.WriteDanSkillsetVerdicts("tester", 7, DanSkillSystem.SIDE_LN, new Dictionary<string, EzDanSkillsetVerdict>());

                Assert.That(store.HasDanSkillsetCache("tester", 7, DanSkillSystem.SIDE_LN), Is.True);
                Assert.That(store.GetDanSkillsetValues("tester", 7, DanSkillSystem.SIDE_LN), Is.Empty);

                store.ClearDanSkillsetValues("tester");
                Assert.That(store.HasDanSkillsetCache("tester", 7, DanSkillSystem.SIDE_LN), Is.False);
            });
        }

        [Test]
        public void Provider_GetDanSkillsets_uses_cache_without_clears()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);
                store.WriteDanSkillsetVerdicts("cached-user", 4, DanSkillSystem.SIDE_RC, new Dictionary<string, EzDanSkillsetVerdict>
                {
                    [EzDanSkillsetBuckets.TECH] = new EzDanSkillsetVerdict(EzDanSkillsetBuckets.TECH, 9.1, "Shodan", 5),
                });

                var provider = new EzSkillProvider(store);
                var verdicts = provider.GetDanSkillsets("cached-user", 4, DanSkillSystem.SIDE_RC);

                Assert.That(verdicts, Has.Count.EqualTo(1));
                Assert.That(verdicts[EzDanSkillsetBuckets.TECH].RawDan, Is.EqualTo(9.1).Within(1e-9));
                Assert.That(verdicts[EzDanSkillsetBuckets.TECH].Label, Is.EqualTo("Shodan"));
            });
        }

        [Test]
        public void Batch_prefetch_msd_and_chart_skill_info_for_hashes()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);
                const string hash_a = "batch-hash-a";
                const string hash_b = "batch-hash-b";

                store.WriteBeatmapMsd(hash_a, new EzSkillsetVector(10, 1, 2, 3, 4, 5, 6, 7), holdRatio: 0.2);
                store.WriteBeatmapMsd(hash_b, new EzSkillsetVector(11, 1.1, 2.1, 3.1, 4.1, 5.1, 6.1, 7.1), holdRatio: 0.3);

                store.UpsertChartSkillInfo(hash_a, new EzChartSkillInfo
                {
                    Patterns = new[] { "jack" },
                    TechScore = 0.5,
                    ChordjackScore = 0.8,
                    LnRatio = 0.1,
                    DanEligible = true,
                    KeyCount = 4,
                });

                var msdBatch = store.GetBeatmapSkillsForHashes(new[] { hash_a, hash_b, "missing" }, EzSkillSystems.BEATMAP_MSD);
                Assert.That(msdBatch.Keys, Is.EquivalentTo(new[] { hash_a, hash_b }));
                Assert.That(msdBatch[hash_a][EzSkillSystems.MsdHoldRatioSkillId], Is.EqualTo(0.2).Within(1e-9));
                Assert.That(msdBatch[hash_b][EzMinaSkillAxis.Overall.ToMsdSkillId()], Is.EqualTo(11).Within(1e-9));

                var chartBatch = store.GetChartSkillInfoForHashes(new[] { hash_a, hash_b, "missing" });
                Assert.That(chartBatch.Keys, Is.EquivalentTo(new[] { hash_a }));
                Assert.That(chartBatch[hash_a].Patterns, Is.EquivalentTo(new[] { "jack" }));
                Assert.That(chartBatch[hash_a].ChordjackScore, Is.EqualTo(0.8).Within(1e-9));
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
