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
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.EzOsuGame.Skills.SSR;
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
                var store = new EzChartSkillStore(realm);
                const string hash = "chart-dan-hash-1";

                seedChart(realm, hash);

                var dto = new EzPersistedChartDan
                {
                    BeatmapHash = hash,
                    BeatmapId = Guid.NewGuid(),
                    AlgorithmVersion = EzDanEngine.VERSION,
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

                stampWholeChain(store);

                Assert.That(store.CollectChartChainState().CompleteChartDan, Does.Contain(hash));
            });
        }

        /// <summary>
        /// A row carries no revision of its own: whether it counts is decided by whether the system that owns it has
        /// run at its current revision, not by what the row was stamped with.
        /// </summary>
        [Test]
        public void A_row_reads_regardless_of_the_revision_it_was_written_with()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzChartSkillStore(realm);
                const string hash = "chart-dan-stale-version";

                seedChart(realm, hash);

                realm.Write(r =>
                {
                    r.Add(new EzChartDan
                    {
                        BeatmapHash = hash,
                        AlgorithmVersion = EzDanEngine.VERSION - 1,
                        KeyCount = 4,
                        HoldRatio = 0.1,
                        OverallMsd = 10,
                        RcRawDan = 5,
                        RcLabel = "Old",
                        LnRawDan = -1,
                        ComputedAt = DateTimeOffset.UtcNow,
                    });
                });

                Assert.That(store.TryGetChartDan(hash, out var loaded), Is.True);
                Assert.That(loaded, Is.Not.Null);

                stampWholeChain(store);

                Assert.That(store.CollectChartChainState().CompleteChartDan, Does.Contain(hash));
            });
        }

        /// <summary>
        /// Retiring a downstream row is the job of the applied-version stamp: a system that has not run at its
        /// current revision holds no results, and running it again is what brings the rows back.
        /// </summary>
        [Test]
        public void A_system_that_has_not_run_retires_its_rows()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzChartSkillStore(realm);
                const string hash = "chart-dan-retired";

                seedChart(realm, hash);

                realm.Write(r =>
                {
                    r.Add(new EzChartDan
                    {
                        BeatmapHash = hash,
                        AlgorithmVersion = EzDanEngine.VERSION,
                        KeyCount = 4,
                        HoldRatio = 0.1,
                        OverallMsd = 10,
                        RcRawDan = 5,
                        RcLabel = "Retired",
                        LnRawDan = -1,
                        ComputedAt = DateTimeOffset.UtcNow,
                    });
                });

                Assert.That(store.IsExpired(EzAnalysisSystem.Dan), Is.True, "no pass has recorded a revision for Dan");
                Assert.That(store.CollectChartChainState().CompleteChartDan, Does.Not.Contain(hash));

                store.SetAppliedVersion(EzAnalysisSystem.Dan, EzDanEngine.VERSION);

                // Dan's own stamp is not the whole answer: it reads the Sunny rating, so that system has to have run
                // at its current revision too before a stored row counts as a result.
                Assert.That(store.CollectChartChainState().CompleteChartDan, Does.Not.Contain(hash), "the upstreams have not run here");

                stampWholeChain(store);

                Assert.That(store.IsExpired(EzAnalysisSystem.Dan), Is.False);
                Assert.That(store.CollectChartChainState().CompleteChartDan, Does.Contain(hash));
            });
        }

        [Test]
        public void Invalidating_a_system_also_marks_the_systems_derived_from_it()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzChartSkillStore(realm);

                store.SetAppliedVersion(EzAnalysisSystem.Mina, EzMinaEngine.VERSION);
                store.SetAppliedVersion(EzAnalysisSystem.CSI, EzCSIEngine.VERSION);
                store.SetAppliedVersion(EzAnalysisSystem.Dan, EzDanEngine.VERSION);

                var invalidated = store.Invalidate(EzAnalysisSystem.CSI);

                Assert.That(invalidated, Does.Contain(EzAnalysisSystem.CSI));
                Assert.That(invalidated, Does.Contain(EzAnalysisSystem.Dan), "Dan reads CSI");
                Assert.That(invalidated, Does.Not.Contain(EzAnalysisSystem.Mina), "Mina is upstream of CSI");

                Assert.That(store.IsExpired(EzAnalysisSystem.CSI), Is.True);
                Assert.That(store.IsExpired(EzAnalysisSystem.Dan), Is.True);
                Assert.That(store.IsExpired(EzAnalysisSystem.Mina), Is.False);
            });
        }

        [Test]
        public void FromMsd_compute_then_provider_read_only_labels()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var store = new EzChartSkillStore(realm);
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
                };

                store.WriteBeatmapMsd(hash, new EzMinaSkillAxisVector(20, 18, 16, 14, 12, 10, 8, 6));

                var computed = EzPersistedChartDan.TryComputeFromStored(
                    hash,
                    Guid.NewGuid(),
                    msd,
                    keyCount: 4,
                    holdRatio: 0.1,
                    sunnySR: null,
                    chartInfo: null);

                Assert.That(computed, Is.Not.Null);
                Assert.That(computed!.HasSide(EzDanSide.Rc), Is.True);
                Assert.That(computed.HasSide(EzDanSide.Ln), Is.False);
                Assert.That(computed.RcSkillsetLabels, Is.Not.Empty);

                store.UpsertChartDan(computed);

                var provider = new EzSkillProvider(store, new EzPlayerSkillStore(storage));
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
                var store = new EzChartSkillStore(realm);
                const string hash = "msd-unrateable-hash";

                seedChart(realm, hash);

                stampWholeChain(store);

                store.WriteBeatmapMsdUnrateable(hash, Guid.NewGuid());

                Assert.That(store.CollectChartChainState().CompleteMsd, Does.Not.Contain(hash));
                Assert.That(store.CollectChartChainState().SettledMsd, Does.Contain(hash));

                var skills = store.GetBeatmapSkills(hash, EzAnalysisSystem.Mina);
                Assert.That(store.GetBeatmapSystemState(hash, EzAnalysisSystem.Mina), Is.EqualTo(EzSkillRowState.Unrateable));
                Assert.That(skills, Is.Empty);
                Assert.That(EzMinaUnit.IsCurrentMinaCache(skills), Is.False);

                // Successful Mina write replaces the marker.
                store.WriteBeatmapMsd(hash, new EzMinaSkillAxisVector(20, 18, 16, 14, 12, 10, 8, 6));
                Assert.That(store.CollectChartChainState().CompleteMsd, Does.Contain(hash));
                Assert.That(store.GetBeatmapSystemState(hash, EzAnalysisSystem.Mina), Is.EqualTo(EzSkillRowState.Current));
            });
        }

        /// <summary>
        /// A chart that is rateable and has a complete Mina, yet whose stored inputs resolve to no dan (a keymode with
        /// no table of its own and no star rating to borrow one from), is settled for the dan facet rather than
        /// missing: counting it as missing is what made the backfill re-attempt and re-report it on every launch.
        /// </summary>
        [Test]
        public void Chart_whose_inputs_resolve_to_no_dan_is_settled_not_owed()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzChartSkillStore(realm);
                const string hash = "chart-dan-unresolvable";

                // 5K: not a 4/6/7K interval table, and no SunnyStarRating to borrow one from.
                seedChart(realm, hash, circleSize: 5);
                store.WriteBeatmapMsd(hash, new EzMinaSkillAxisVector(20, 18, 16, 14, 12, 10, 8, 6));

                var chain = store.CollectChartChainState();

                Assert.That(chain.CompleteMsd, Does.Contain(hash));
                Assert.That(chain.UnresolvableChartDan, Does.Contain(hash));
                Assert.That(chain.ChartDanOwed, Does.Not.Contain(hash));

                var status = store.GetSkillDataStatus();
                Assert.That(status["Dan"].Missing, Is.EqualTo(0));
                Assert.That(status["Dan"].Settled, Is.EqualTo(1));
            });
        }

        /// <summary>
        /// A revision bump has to reach the systems that read the bumped one, even when their own revision did not
        /// move: their stored rows were derived from the old values.
        /// </summary>
        [Test]
        public void A_stale_upstream_makes_the_systems_reading_it_stale()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzChartSkillStore(realm);
                const string hash = "chart-dan-upstream";

                seedChart(realm, hash);
                store.WriteBeatmapMsd(hash, new EzMinaSkillAxisVector(20, 18, 16, 14, 12, 10, 8, 6));
                store.UpsertChartDan(new EzPersistedChartDan
                {
                    BeatmapHash = hash,
                    KeyCount = 4,
                    HoldRatio = 0.1,
                    OverallMsd = 20,
                    RcRawDan = 9,
                    RcLabel = "Shodan",
                    LnRawDan = -1,
                    ComputedAt = DateTimeOffset.UtcNow,
                });

                // The chart chain's own three systems are all current, and so is the Sunny coefficient record.
                store.SetAppliedVersion(EzAnalysisSystem.Mina, EzMinaEngine.VERSION);
                store.SetAppliedVersion(EzAnalysisSystem.CSI, EzCSIEngine.VERSION);
                store.SetAppliedVersion(EzAnalysisSystem.Dan, EzDanEngine.VERSION);
                EzSSRTuningSnapshot.Record();

                // Dan reads the Sunny rating, which has never run here, so the stored row is not a result yet.
                var withoutSunny = store.CollectChartChainState();

                Assert.That(withoutSunny.CompleteChartDan, Is.Empty, "a dan derived from an absent Sunny rating is not a result");
                Assert.That(withoutSunny.ChartDanOwed, Does.Contain(hash));

                store.SetAppliedVersion(EzAnalysisSystem.SSR, EzSSRUnit.VERSION);

                // Once the upstream is current the same row counts again: nothing had to be re-stamped for Dan.
                Assert.That(store.CollectChartChainState().CompleteChartDan, Does.Contain(hash));
                Assert.That(store.CollectChartChainState().ChartDanOwed, Does.Not.Contain(hash));
            });
        }

        /// <summary>
        /// Records the whole chart chain as current. Dan's rows are derived from Mina, the chart structure and the
        /// Sunny rating, so a test about what a Dan row means has to have all of them settled first.
        /// </summary>
        private static void stampWholeChain(EzChartSkillStore store)
        {
            store.SetAppliedVersion(EzAnalysisSystem.Mina, EzMinaEngine.VERSION);
            store.SetAppliedVersion(EzAnalysisSystem.CSI, EzCSIEngine.VERSION);
            store.SetAppliedVersion(EzAnalysisSystem.Dan, EzDanEngine.VERSION);
            store.SetAppliedVersion(EzAnalysisSystem.SSR, EzSSRUnit.VERSION);
            EzSSRTuningSnapshot.Record();
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
        public void File_schema_version_is_ez12()
        {
            Assert.That(RealmAccess.EZ_REALM_SCHEMA_VERSION, Is.EqualTo(12));
            Assert.That(RealmAccess.EzFileSchemaVersion, Is.EqualTo(RealmAccess.UpstreamSchemaVersion * 1000 + 12));
        }
    }
}
