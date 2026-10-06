// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Rulesets;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzChartDanRealmStoreTest : RealmTest
    {
        [Test]
        public void Upsert_and_get_round_trip_with_skillset_stamps()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);
                const string hash = "chart-dan-hash-1";

                seedChart(realm, hash);

                var dto = new EzPersistedChartDan
                {
                    BeatmapHash = hash,
                    BeatmapId = Guid.NewGuid(),
                    AlgorithmVersion = EzDanAlgorithm.VERSION,
                    KeyCount = 4,
                    HoldRatio = 0.1,
                    OverallMsd = 18.5,
                    RcRawDan = 9.0,
                    RcLabel = "Shodan",
                    LnRawDan = -1,
                    LnLabel = string.Empty,
                    RcSkillsetLabels = new Dictionary<string, string>
                    {
                        ["jack"] = "Shodan",
                        ["tech"] = "Shodan",
                    },
                    LnSkillsetLabels = new Dictionary<string, string>(),
                    ComputedAt = DateTimeOffset.UtcNow,
                };

                store.UpsertChartDan(dto);

                Assert.That(store.TryGetChartDan(hash, out var loaded), Is.True);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded!.KeyCount, Is.EqualTo(4));
                Assert.That(loaded.RcLabel, Is.EqualTo("Shodan"));
                Assert.That(loaded.RcRawDan, Is.EqualTo(9.0).Within(1e-9));
                Assert.That(loaded.LnRawDan, Is.EqualTo(-1));
                Assert.That(loaded.RcSkillsetLabels["jack"], Is.EqualTo("Shodan"));
                Assert.That(loaded.RcSkillsetLabels["tech"], Is.EqualTo("Shodan"));
                Assert.That(store.CollectChartChainState().CompleteChartDan, Does.Contain(hash));
            });
        }

        [Test]
        public void Wrong_algorithm_version_is_miss()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);
                const string hash = "chart-dan-stale-version";

                seedChart(realm, hash);

                realm.Write(r =>
                {
                    r.Add(new EzBeatmapChartDan
                    {
                        BeatmapHash = hash,
                        AlgorithmVersion = EzDanAlgorithm.VERSION - 1,
                        KeyCount = 4,
                        HoldRatio = 0.1,
                        OverallMsd = 10,
                        RcRawDan = 5,
                        RcLabel = "Stale",
                        LnRawDan = -1,
                        ComputedAt = DateTimeOffset.UtcNow,
                    });
                });

                Assert.That(store.TryGetChartDan(hash, out var missing), Is.False);
                Assert.That(missing, Is.Null);
                Assert.That(store.CollectChartChainState().CompleteChartDan, Does.Not.Contain(hash));
            });
        }

        [Test]
        public void Upstream_facets_retire_a_chart_dan_row()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);

                // A bare-constant stamp is what a pre-revision (or MSD-only side-upsert) row carries.
                // Both it and a CSI-only stamp must read as a miss, because Dan folds CSI and MSD.
                foreach ((string hash, int stamp) in new[]
                         {
                             ("chart-dan-bare-dan-version", EzDanAlgorithm.VERSION),
                             ("chart-dan-missing-csi", EzAnalysisRevision.ChartSkillInfo),
                             ("chart-dan-missing-msd-upstream", EzChartSkillInfo.VERSION),
                         })
                {
                    seedChart(realm, hash);

                    realm.Write(r =>
                    {
                        r.Add(new EzBeatmapChartDan
                        {
                            BeatmapHash = hash,
                            AlgorithmVersion = stamp,
                            KeyCount = 4,
                            HoldRatio = 0.1,
                            OverallMsd = 10,
                            RcRawDan = 5,
                            RcLabel = "Stale",
                            LnRawDan = -1,
                            ComputedAt = DateTimeOffset.UtcNow,
                        });
                    });

                    Assert.That(store.TryGetChartDan(hash, out var stale), Is.False, $"revision {stamp} should be stale");
                    Assert.That(stale, Is.Null);
                    Assert.That(store.CollectChartChainState().CompleteChartDan, Does.Not.Contain(hash));
                }
            });
        }

        [Test]
        public void FromMsd_compute_then_provider_read_only_labels()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);
                const string hash = "chart-dan-from-msd";

                var msd = new Dictionary<string, double>
                {
                    [EzMinaSkillAxis.Overall.ToMsdSkillId()] = 20,
                    [EzMinaSkillAxis.Stream.ToMsdSkillId()] = 18,
                    [EzMinaSkillAxis.Jumpstream.ToMsdSkillId()] = 16,
                    [EzMinaSkillAxis.Handstream.ToMsdSkillId()] = 14,
                    [EzMinaSkillAxis.Stamina.ToMsdSkillId()] = 12,
                    [EzMinaSkillAxis.JackSpeed.ToMsdSkillId()] = 10,
                    [EzMinaSkillAxis.Chordjack.ToMsdSkillId()] = 8,
                    [EzMinaSkillAxis.Technical.ToMsdSkillId()] = 6,
                    [EzSkillSystems.MsdHoldRatioSkillId] = 0.1,
                };

                store.WriteBeatmapMsd(hash, new EzSkillsetVector(20, 18, 16, 14, 12, 10, 8, 6), holdRatio: 0.1);

                var computed = EzPersistedChartDan.TryComputeFromStored(
                    hash,
                    Guid.NewGuid(),
                    msd,
                    keyCount: 4,
                    holdRatio: 0.1,
                    xxySr: null,
                    chartInfo: null);

                Assert.That(computed, Is.Not.Null);
                Assert.That(computed!.HasSide(EzDanSide.Rc), Is.True);
                Assert.That(computed.HasSide(EzDanSide.Ln), Is.False);
                Assert.That(computed.RcSkillsetLabels, Is.Not.Empty);

                store.UpsertChartDan(computed);

                var provider = new EzSkillProvider(store);
                var info = new BeatmapInfo
                {
                    Hash = hash,
                    Ruleset = new RulesetInfo { OnlineID = 3, Available = true },
                    Difficulty = new BeatmapDifficulty { CircleSize = 4 },
                };

                Assert.That(provider.TryGetPersistedChartDan(info, out var persisted), Is.True);
                Assert.That(persisted!.RcLabel, Is.EqualTo(computed.RcLabel));

                var labels = provider.GetChartDanSkillsetLabelsReadOnly(info, 4, EzDanSide.Rc);
                Assert.That(labels, Is.Not.Empty);
                Assert.That(labels.Values, Has.All.EqualTo(computed.RcLabel));
            });
        }

        [Test]
        public void Unrateable_msd_is_settled_but_not_complete()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);
                const string hash = "msd-unrateable-hash";

                seedChart(realm, hash);

                store.WriteBeatmapMsdUnrateable(hash, Guid.NewGuid());

                Assert.That(store.CollectChartChainState().CompleteMsd, Does.Not.Contain(hash));
                Assert.That(store.CollectChartChainState().SettledMsd, Does.Contain(hash));

                var skills = store.GetBeatmapSkills(hash, EzSkillSystems.BEATMAP_MSD);
                Assert.That(EzBeatmapMsdComputer.IsUnrateableMsd(skills), Is.True);
                Assert.That(EzBeatmapMsdComputer.IsCurrentMsdCache(skills), Is.False);

                // Successful MSD write replaces the marker.
                store.WriteBeatmapMsd(hash, new EzSkillsetVector(20, 18, 16, 14, 12, 10, 8, 6), holdRatio: 0.1);
                Assert.That(store.CollectChartChainState().CompleteMsd, Does.Contain(hash));
                Assert.That(EzBeatmapMsdComputer.IsUnrateableMsd(store.GetBeatmapSkills(hash, EzSkillSystems.BEATMAP_MSD)), Is.False);
            });
        }

        /// <summary>
        /// A chart that is rateable and has a complete MSD, yet whose stored inputs resolve to no dan (a keymode with
        /// no table of its own and no star rating to borrow one from), is settled for the dan facet rather than
        /// missing: counting it as missing is what made the backfill re-attempt and re-report it on every launch.
        /// </summary>
        [Test]
        public void Chart_whose_inputs_resolve_to_no_dan_is_settled_not_owed()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);
                const string hash = "chart-dan-unresolvable";

                // 5K: not a 4/6/7K interval table, and no xxy star rating to borrow one from.
                seedChart(realm, hash, circleSize: 5);
                store.WriteBeatmapMsd(hash, new EzSkillsetVector(20, 18, 16, 14, 12, 10, 8, 6), holdRatio: 0.1);

                var chain = store.CollectChartChainState();

                Assert.That(chain.CompleteMsd, Does.Contain(hash));
                Assert.That(chain.UnresolvableChartDan, Does.Contain(hash));
                Assert.That(chain.ChartDanOwed, Does.Not.Contain(hash));

                var status = store.GetSkillDataStatus();
                Assert.That(status["Dan"].Missing, Is.EqualTo(0));
                Assert.That(status["Dan"].Settled, Is.EqualTo(1));
            });
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

        [Test]
        public void Join_parse_skillset_labels_round_trip()
        {
            var labels = new Dictionary<string, string>
            {
                ["jack"] = "Shodan",
                ["chordjack"] = "1st Kyu",
            };

            string joined = EzPersistedChartDan.JoinSkillsetLabels(labels);
            var parsed = EzPersistedChartDan.ParseSkillsetLabels(joined);

            Assert.That(parsed, Is.EquivalentTo(labels));
        }

        [Test]
        public void File_schema_version_is_ez10()
        {
            Assert.That(RealmAccess.EZ_REALM_SCHEMA_VERSION, Is.EqualTo(10));
            Assert.That(RealmAccess.EzFileSchemaVersion, Is.EqualTo(RealmAccess.UpstreamSchemaVersion * 1000 + 10));
        }
    }
}
