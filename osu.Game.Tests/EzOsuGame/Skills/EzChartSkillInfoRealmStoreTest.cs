// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.Rulesets;
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
                var store = new EzChartSkillStore(realm);
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
            RunTestWithRealm((_, storage) =>
            {
                var store = new EzPlayerSkillStore(storage);

                store.WriteDanSkillsetVerdicts("tester", 4, EzDanSide.Rc.ToId(), new Dictionary<string, EzDanSkillsetVerdict>
                {
                    [EzDanSkillsetBuckets.JACK] = new EzDanSkillsetVerdict(EzDanSkillsetBuckets.JACK, 12.5, "Chuudan", 3),
                });

                Assert.That(store.HasDanSkillsetCache("tester", 4, EzDanSide.Rc.ToId()), Is.True);
                var rows = store.GetDanSkillsetValues("tester", 4, EzDanSide.Rc.ToId());
                Assert.That(rows, Has.Count.EqualTo(1));
                Assert.That(rows[0].SkillsetId, Is.EqualTo(EzDanSkillsetBuckets.JACK));
                Assert.That(rows[0].RawDan, Is.EqualTo(12.5).Within(1e-9));
                Assert.That(rows[0].Label, Is.EqualTo("Chuudan"));
                Assert.That(rows[0].Clears, Is.EqualTo(3));
            });
        }

        [Test]
        public void Skillset_empty_verdicts_do_not_write_sentinel()
        {
            RunTestWithRealm((_, storage) =>
            {
                var store = new EzPlayerSkillStore(storage);

                store.WriteDanSkillsetVerdicts("tester", 4, EzDanSide.Rc.ToId(), new Dictionary<string, EzDanSkillsetVerdict>
                {
                    [EzDanSkillsetBuckets.JACK] = new EzDanSkillsetVerdict(EzDanSkillsetBuckets.JACK, 10, "Shodan", 4),
                });
                Assert.That(store.HasDanSkillsetCache("tester", 4, EzDanSide.Rc.ToId()), Is.True);

                store.WriteDanSkillsetVerdicts("tester", 4, EzDanSide.Rc.ToId(), new Dictionary<string, EzDanSkillsetVerdict>());

                Assert.That(store.HasDanSkillsetCache("tester", 4, EzDanSide.Rc.ToId()), Is.False);
                Assert.That(store.GetDanSkillsetValues("tester", 4, EzDanSide.Rc.ToId()), Is.Empty);
            });
        }

        [Test]
        public void Skillset_legacy_empty_sentinel_is_purged_as_miss()
        {
            RunTestWithRealm((_, storage) =>
            {
                var store = new EzPlayerSkillStore(storage);

                store.WriteDanSkillsetValues("legacy", 7, EzDanSide.Ln.ToId(), new[]
                {
                    new EzPlayerDanSkillsetRow(EzDanSide.Ln.ToId(), EzDanSkillsetBuckets.CACHE_EMPTY_SENTINEL, -1, string.Empty, 0, EzDanEngine.VERSION,
                        DateTimeOffset.UtcNow),
                });

                Assert.That(store.HasDanSkillsetCache("legacy", 7, EzDanSide.Ln.ToId()), Is.True);
                Assert.That(store.GetDanSkillsetValues("legacy", 7, EzDanSide.Ln.ToId()).Single().SkillsetId,
                    Is.EqualTo(EzDanSkillsetBuckets.CACHE_EMPTY_SENTINEL));
            });
        }

        [Test]
        public void Provider_GetDanSkillsets_uses_cache_without_clears()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var store = new EzChartSkillStore(realm);
                var playerStore = new EzPlayerSkillStore(storage);
                playerStore.WriteDanSkillsetVerdicts("cached-user", 4, EzDanSide.Rc.ToId(), new Dictionary<string, EzDanSkillsetVerdict>
                {
                    [EzDanSkillsetBuckets.TECH] = new EzDanSkillsetVerdict(EzDanSkillsetBuckets.TECH, 9.1, "Shodan", 5),
                });

                var provider = new EzSkillProvider(store, playerStore);
                var verdicts = provider.GetDanSkillsets("cached-user", 4, EzDanSide.Rc.ToId());

                Assert.That(verdicts, Has.Count.EqualTo(1));
                Assert.That(verdicts[EzDanSkillsetBuckets.TECH].RawDan, Is.EqualTo(9.1).Within(1e-9));
                Assert.That(verdicts[EzDanSkillsetBuckets.TECH].Label, Is.EqualTo("Shodan"));
            });
        }

        [Test]
        public void Provider_GetDanSkillsets_miss_with_no_clears_does_not_write_empty()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var store = new EzChartSkillStore(realm);
                var playerStore = new EzPlayerSkillStore(storage);
                var provider = new EzSkillProvider(store, playerStore);

                var verdicts = provider.GetDanSkillsets("no-clears-user", 4, EzDanSide.Rc.ToId());

                Assert.That(verdicts, Is.Empty);
                Assert.That(playerStore.HasDanSkillsetCache("no-clears-user", 4, EzDanSide.Rc.ToId()), Is.False);
            });
        }

        [Test]
        public void Unavailable_csi_is_not_persisted_and_excluded_from_hash_set()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzChartSkillStore(realm);
                const string stub_hash = "unavailable-stub-hash";
                const string real_hash = "real-csi-hash";
                const string legacy_stub_hash = "legacy-wrong-version-stub";

                seedChart(realm, stub_hash);
                seedChart(realm, real_hash);
                seedChart(realm, legacy_stub_hash);

                // Implicit stub upsert is a no-op: an Unavailable DTO must never fake "already processed".
                store.UpsertChartSkillInfo(stub_hash, EzChartSkillInfo.Unavailable);
                Assert.That(store.TryGetChartSkillInfo(stub_hash, out var missing), Is.False);
                Assert.That(missing, Is.Null);

                // The explicit settle write is the only way a stub lands, and it is keyed to the
                // settled set (so BDSP stops retrying) but stays out of the "complete" set.
                store.WriteChartSkillInfoUnavailable(stub_hash, Guid.NewGuid());

                Assert.That(store.TryGetChartSkillInfo(stub_hash, out var settled), Is.True);
                Assert.That(settled, Is.Not.Null);
                Assert.That(settled!.IsUnavailable, Is.True);

                realm.Write(r =>
                {
                    r.Add(new EzBeatmapChartSkillInfo
                    {
                        BeatmapHash = legacy_stub_hash,
                        InfoVersion = EzCSIEngine.VERSION - 1,
                        ComputedAt = DateTimeOffset.UtcNow,
                        KeyCount = -1,
                        DanEligible = false,
                    });
                });

                store.UpsertChartSkillInfo(real_hash, new EzChartSkillInfo
                {
                    Patterns = new[] { "jack" },
                    DanEligible = true,
                    KeyCount = 4,
                });

                var chain = store.CollectChartChainState();

                Assert.That(chain.CompleteChartSkillInfo.Contains(real_hash), Is.True);
                Assert.That(chain.CompleteChartSkillInfo.Contains(stub_hash), Is.False);

                Assert.That(chain.SettledChartSkillInfo.Contains(real_hash), Is.True);
                Assert.That(chain.SettledChartSkillInfo.Contains(stub_hash), Is.True);
                // A distinct stub row is settled too: the read no longer filters on the stamp it was written with.
                Assert.That(chain.SettledChartSkillInfo.Contains(legacy_stub_hash), Is.True);
            });
        }

        [Test]
        public void Batch_prefetch_msd_and_chart_skill_info_for_hashes()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzChartSkillStore(realm);
                const string hash_a = "batch-hash-a";
                const string hash_b = "batch-hash-b";

                store.WriteBeatmapMsd(hash_a, new EzMinaSkillAxisVector(10, 1, 2, 3, 4, 5, 6, 7));
                store.WriteBeatmapMsd(hash_b, new EzMinaSkillAxisVector(11, 1.1, 2.1, 3.1, 4.1, 5.1, 6.1, 7.1));

                store.UpsertChartSkillInfo(hash_a, new EzChartSkillInfo
                {
                    Patterns = new[] { "jack" },
                    TechScore = 0.5,
                    ChordjackScore = 0.8,
                    LnRatio = 0.1,
                    DanEligible = true,
                    KeyCount = 4,
                });

                var msdBatch = store.GetBeatmapSkillsForHashes(new[] { hash_a, hash_b, "missing" }, EzAnalysisSystem.Mina);
                Assert.That(msdBatch.Keys, Is.EquivalentTo(new[] { hash_a, hash_b }));
                Assert.That(msdBatch[hash_a][EzMinaSkillAxis.Overall.ToMsdSkillId()], Is.EqualTo(10).Within(1e-9));
                Assert.That(msdBatch[hash_b][EzMinaSkillAxis.Overall.ToMsdSkillId()], Is.EqualTo(11).Within(1e-9));

                var chartBatch = store.GetChartSkillInfoForHashes(new[] { hash_a, hash_b, "missing" });
                Assert.That(chartBatch.Keys, Is.EquivalentTo(new[] { hash_a }));
                Assert.That(chartBatch[hash_a].Patterns, Is.EquivalentTo(new[] { "jack" }));
                Assert.That(chartBatch[hash_a].ChordjackScore, Is.EqualTo(0.8).Within(1e-9));
            });
        }

        /// <summary>
        /// The structure axis table is what a radar draws, so it has to be readable without running the pass again.
        /// Reading resolves the revision from the system id while the write states it, so this also pins those two
        /// in step.
        /// </summary>
        [Test]
        public void Structure_axes_are_persisted_and_read_back_by_bare_axis_id()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var store = new EzChartSkillStore(realm);
                var provider = new EzSkillProvider(store, new EzPlayerSkillStore(storage));
                const string hash = "structure-axis-hash";

                string densityId = EzManiaLNAxis.Density.ToId();
                string chordId = EzManiaLNAxis.Chord.ToId();

                store.WriteBeatmapSystemSkills(
                    hash,
                    EzAnalysisSystem.CSI,
                    new Dictionary<string, double>(StringComparer.Ordinal)
                    {
                        [$"{EzAnalysisSystem.CSI}.{densityId}"] = 0.5,
                        [$"{EzAnalysisSystem.CSI}.{chordId}"] = 0.25,
                    },
                    algorithmVersion: EzCSIEngine.VERSION,
                    rulesetOnlineId: EzLocalProfileConstants.MANIA_RULESET_ID);

                var axes = provider.GetBeatmapStructureAxes(maniaBeatmap(hash, Guid.NewGuid()));

                Assert.That(axes[densityId], Is.EqualTo(0.5).Within(1e-9));
                Assert.That(axes[chordId], Is.EqualTo(0.25).Within(1e-9));
                Assert.That(axes.ContainsKey($"{EzAnalysisSystem.CSI}.{densityId}"), Is.False,
                    "consumers address structure axes by the id the axis declaration uses");
            });
        }

        /// <summary>
        /// A settled miss means the structure pass declined to rate the chart. Leaving its frame behind would let a
        /// reader draw axes that no longer stand behind a row.
        /// </summary>
        [Test]
        public void Settling_a_structure_miss_drops_the_persisted_axes()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var store = new EzChartSkillStore(realm);
                var provider = new EzSkillProvider(store, new EzPlayerSkillStore(storage));
                const string hash = "settled-structure-hash";
                var beatmap = maniaBeatmap(hash, Guid.NewGuid());

                store.WriteBeatmapSystemSkills(
                    hash,
                    EzAnalysisSystem.CSI,
                    new Dictionary<string, double>(StringComparer.Ordinal) { [$"{EzAnalysisSystem.CSI}.{EzManiaLNAxis.Density.ToId()}"] = 0.5 },
                    algorithmVersion: EzCSIEngine.VERSION,
                    rulesetOnlineId: EzLocalProfileConstants.MANIA_RULESET_ID);

                Assert.That(provider.GetBeatmapStructureAxes(beatmap), Is.Not.Empty);

                store.WriteChartSkillInfoUnavailable(hash, beatmap.ID);

                Assert.That(provider.GetBeatmapStructureAxes(beatmap), Is.Empty);
            });
        }

        /// <summary>
        /// A complete CSI row on a chart whose Mina settled as unrateable is exactly what BDSP re-queues on every
        /// run: its repair set is "unrateable Mina and still a complete CSI row". The backfill has to replace it
        /// with the stub form, which both takes it out of the complete set and leaves it in the settled set.
        /// </summary>
        [Test]
        public void Unrateable_msd_settles_a_complete_csi_row_instead_of_requeueing_it()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var store = new EzChartSkillStore(realm);
                var provider = new EzSkillProvider(store, new EzPlayerSkillStore(storage));
                const string hash = "unrateable-with-complete-csi";
                var id = Guid.NewGuid();

                seedChart(realm, hash);

                // What an earlier pass stamped from the stub axis.
                store.UpsertChartSkillInfo(hash, new EzChartSkillInfo { Patterns = new[] { "jack" }, KeyCount = 4, DanEligible = true });
                store.WriteBeatmapMsdUnrateable(hash, id);

                var beatmap = maniaBeatmap(hash, id);

                Assert.That(provider.TryEnsureChartSkillInfoForBackfill(beatmap), Is.True);
                Assert.That(store.CollectChartChainState().CompleteChartSkillInfo.Contains(hash), Is.False,
                    "a complete row on an unrateable chart re-queues the backfill forever");
                Assert.That(store.CollectChartChainState().SettledChartSkillInfo.Contains(hash), Is.True);

                // The stub then answers the next run, so the repair set stays empty.
                Assert.That(provider.TryEnsureChartSkillInfoForBackfill(beatmap), Is.True);
                Assert.That(store.TryGetChartSkillInfo(hash, out var settled), Is.True);
                Assert.That(settled!.IsUnavailable, Is.True);
            });
        }

        /// <summary>
        /// The live read path must not rebuild the row the backfill just retired: with a settled unrateable Mina there
        /// are no axes to compute from, so it declines instead of stamping a full row off the stub marker.
        /// </summary>
        [Test]
        public void Hot_path_does_not_stamp_a_csi_row_for_an_unrateable_chart()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var store = new EzChartSkillStore(realm);
                var provider = new EzSkillProvider(store, new EzPlayerSkillStore(storage));
                const string hash = "unrateable-hot-path";

                store.WriteBeatmapMsdUnrateable(hash, Guid.NewGuid());

                Assert.That(provider.TryGetOrComputeChartSkillInfo(maniaBeatmap(hash, Guid.NewGuid())), Is.Null);
                Assert.That(store.TryGetChartSkillInfo(hash, out var hotPathRow), Is.False,
                    "the stub marker is a verdict, not chart axes: it must not become a full CSI row");
                Assert.That(hotPathRow, Is.Null);
            });
        }

        [Test]
        public void A_settled_unrateable_msd_is_not_served_as_axis_input()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var store = new EzChartSkillStore(realm);
                const string dead_hash = "unrateable-msd-hash";
                const string live_hash = "rated-msd-hash";

                store.WriteBeatmapMsdUnrateable(dead_hash, Guid.NewGuid());
                store.WriteBeatmapMsd(live_hash, new EzMinaSkillAxisVector(10, 1, 2, 3, 4, 5, 6, 7));

                var provider = new EzSkillProvider(store, new EzPlayerSkillStore(storage));

                Assert.That(provider.GetBeatmapMsd(dead_hash), Is.Empty,
                    "the settled marker is a verdict, not an axis set: feeding it to the CSI computer is what stamped a row for a chart the engine refused");
                Assert.That(provider.GetBeatmapMsd(live_hash)[EzMinaSkillAxis.Overall.ToMsdSkillId()], Is.EqualTo(10).Within(1e-9));
            });
        }

        [Test]
        public void File_schema_version_is_ez12()
        {
            Assert.That(RealmAccess.EZ_REALM_SCHEMA_VERSION, Is.EqualTo(12));
            Assert.That(RealmAccess.EzFileSchemaVersion, Is.EqualTo(RealmAccess.UpstreamSchemaVersion * 1000 + 12));
        }

        private static void seedChart(RealmAccess realm, string hash, float circleSize = 4)
        {
            realm.Write(r =>
            {
                var ruleset = r.All<RulesetInfo>().FirstOrDefault(s => s.ShortName == "mania");

                if (ruleset == null)
                {
                    ruleset = new RulesetInfo { OnlineID = 3, ShortName = "mania", Available = true };
                    r.Add(ruleset);
                }

                var set = new BeatmapSetInfo();
                r.Add(set);

                r.Add(new BeatmapInfo
                {
                    Hash = hash,
                    BeatmapSet = set,
                    Ruleset = ruleset,
                    DifficultyName = hash,
                    Difficulty = new BeatmapDifficulty { CircleSize = circleSize },
                });
            });
        }

        private static BeatmapInfo maniaBeatmap(string hash, Guid id) => new BeatmapInfo(new RulesetInfo { OnlineID = 3 }, new BeatmapDifficulty { CircleSize = 4 })
        {
            ID = id,
            Hash = hash,
        };
    }
}
