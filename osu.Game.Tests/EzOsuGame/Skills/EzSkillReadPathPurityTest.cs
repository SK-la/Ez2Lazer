// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    /// <summary>
    /// The read paths must not persist. Durable rows are owned by BDSP, <see cref="Beatmaps.BeatmapUpdater"/> and the
    /// manual recompute, so a HUD read that computes a value has to keep it in memory.
    /// </summary>
    [TestFixture]
    public class EzSkillReadPathPurityTest : RealmTest
    {
        [Test]
        public void Computing_skillsets_on_a_read_does_not_write_verdicts()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);
                var provider = new EzSkillProvider(store);

                // No cached verdicts and no clears: the read computes a miss and must leave the table empty.
                var verdicts = provider.GetDanSkillsets("pure-read-user", 4, EzDanSide.Rc.ToId());

                Assert.That(verdicts, Is.Empty);
                Assert.That(store.HasDanSkillsetCache("pure-read-user", 4, EzDanSide.Rc.ToId()), Is.False);

                Assert.That(realm.Run(r => r.All<EzPlayerDanSkillsetValue>().ToList()), Is.Empty);
                Assert.That(realm.Run(r => r.All<EzDanEstimate>().ToList()), Is.Empty);
            });
        }

        [Test]
        public void Repeated_skillset_reads_use_the_session_memo_not_the_store()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);
                var provider = new EzSkillProvider(store);

                var first = provider.GetDanSkillsets("memo-user", 4, EzDanSide.Ln.ToId());
                var second = provider.GetDanSkillsets("memo-user", 4, EzDanSide.Ln.ToId());

                Assert.That(second, Is.SameAs(first));
                Assert.That(store.HasDanSkillsetCache("memo-user", 4, EzDanSide.Ln.ToId()), Is.False);
            });
        }

        [Test]
        public void Invalidating_the_skillset_memo_forces_a_recompute()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);
                var provider = new EzSkillProvider(store);

                var first = provider.GetDanSkillsets("memo-invalidate-user", 6, EzDanSide.Rc.ToId());
                provider.InvalidateDanSkillsets();
                var second = provider.GetDanSkillsets("memo-invalidate-user", 6, EzDanSide.Rc.ToId());

                Assert.That(second, Is.Not.SameAs(first));
                Assert.That(second.Count, Is.EqualTo(first.Count));
            });
        }

        [Test]
        public void Session_skillset_memo_is_per_side_and_keycount()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);
                var provider = new EzSkillProvider(store);

                var rc4 = provider.GetDanSkillsets("memo-key-user", 4, EzDanSide.Rc.ToId());
                var ln4 = provider.GetDanSkillsets("memo-key-user", 4, EzDanSide.Ln.ToId());
                var rc7 = provider.GetDanSkillsets("memo-key-user", 7, EzDanSide.Rc.ToId());

                // Different keys must not share one dictionary instance.
                Assert.That(ln4, Is.Not.SameAs(rc4));
                Assert.That(rc7, Is.Not.SameAs(rc4));

                Assert.That(store.HasDanSkillsetCache("memo-key-user", 4, EzDanSide.Rc.ToId()), Is.False);
                Assert.That(store.HasDanSkillsetCache("memo-key-user", 4, EzDanSide.Ln.ToId()), Is.False);
                Assert.That(store.HasDanSkillsetCache("memo-key-user", 7, EzDanSide.Rc.ToId()), Is.False);
            });
        }

        [Test]
        public void Reading_stored_skillsets_still_prefers_the_persisted_rows()
        {
            RunTestWithRealm((realm, _) =>
            {
                var store = new EzSkillStore(realm);
                store.WriteDanSkillsetVerdicts("persisted-user", 4, EzDanSide.Rc.ToId(), new Dictionary<string, EzDanSkillsetVerdict>
                {
                    [EzDanSkillsetBuckets.TECH] = new EzDanSkillsetVerdict(EzDanSkillsetBuckets.TECH, 8.0, "Dan", 3),
                });

                var provider = new EzSkillProvider(store);
                var verdicts = provider.GetDanSkillsets("persisted-user", 4, EzDanSide.Rc.ToId());

                Assert.That(verdicts[EzDanSkillsetBuckets.TECH].RawDan, Is.EqualTo(8.0).Within(1e-9));
            });
        }
    }
}
