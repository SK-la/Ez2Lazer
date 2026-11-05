// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    /// <summary>
    /// Dan set assembly: which sides resolve, which table each one reads, and where the value came from.
    /// </summary>
    [TestFixture]
    public class EzDanSetTest
    {
        [Test]
        public void A_rated_chart_produces_one_entry_per_side_it_can_be_rated_on()
        {
            // Hold ratio above the LN line, so both sides resolve.
            var set = EzDanSet.Create(axes(overall: 20), keyCount: 4, holdRatio: 0.6, xxySr: 6, chartInfo: null, holdCount: 101);

            Assert.That(set, Is.Not.Null);
            Assert.That(set!.KeyCount, Is.EqualTo(4));
            Assert.That(set.XxySr, Is.EqualTo(6));
            Assert.That(set.For(EzDanSide.Rc), Is.Not.Null);
            Assert.That(set.For(EzDanSide.Ln), Is.Not.Null);
        }

        [Test]
        public void The_ln_side_is_omitted_when_the_hold_gate_rejects_it()
        {
            // Below both the ratio line and the absolute hold count.
            var set = EzDanSet.Create(axes(overall: 20), keyCount: 4, holdRatio: 0.1, xxySr: 6, chartInfo: null, holdCount: 10);

            Assert.That(set, Is.Not.Null);
            Assert.That(set!.For(EzDanSide.Ln), Is.Null);
            Assert.That(set.For(EzDanSide.Rc), Is.Not.Null);
        }

        [Test]
        public void Each_side_reports_the_ladder_it_was_read_off()
        {
            var set = EzDanSet.Create(axes(overall: 20), keyCount: 4, holdRatio: 0.6, xxySr: 6, chartInfo: null, holdCount: 101);

            Assert.That(set, Is.Not.Null);
            Assert.That(set!.For(EzDanSide.Rc)!.Ladder, Is.EqualTo(EzDanLadderKind.Reform4K));
            Assert.That(set.For(EzDanSide.Ln)!.Ladder, Is.EqualTo(EzDanLadderKind.Ln4K));
        }

        [Test]
        public void A_star_rating_from_the_key_modes_own_table_is_reported_as_such()
        {
            var set = EzDanSet.Create(axes(overall: 20), keyCount: 4, holdRatio: 0.1, xxySr: 6, chartInfo: null, holdCount: 101);

            Assert.That(set!.For(EzDanSide.Rc)!.Value, Is.EqualTo(EzDanValueSource.SunnyTable));
        }

        [Test]
        public void A_key_mode_without_a_table_reports_a_borrowed_value()
        {
            // 5K has no Sunny table of its own and borrows the 4K curve.
            var set = EzDanSet.Create(axes(overall: 20), keyCount: 5, holdRatio: 0.1, xxySr: 6, chartInfo: null, holdCount: 101);

            Assert.That(set, Is.Not.Null);
            Assert.That(set!.For(EzDanSide.Rc)!.Value, Is.EqualTo(EzDanValueSource.BorrowedTable));
        }

        [Test]
        public void Means_derived_values_are_withheld_when_the_caller_refuses_them()
        {
            var live = EzDanSet.Create(axes(overall: 20), keyCount: 4, holdRatio: 0.1, xxySr: null, chartInfo: null, allowMsdMeans: false);
            var persisted = EzDanSet.Create(axes(overall: 20), keyCount: 4, holdRatio: 0.1, xxySr: null, chartInfo: null, allowMsdMeans: true);

            Assert.That(live, Is.Null);
            Assert.That(persisted, Is.Not.Null);
            Assert.That(persisted!.For(EzDanSide.Rc)!.Value, Is.EqualTo(EzDanValueSource.MsdMeans));
        }

        [Test]
        public void A_chart_without_any_rating_axis_yields_nothing()
        {
            Assert.That(EzDanSet.Create(new Dictionary<string, double>(), keyCount: 4, holdRatio: 0.1, xxySr: 6, chartInfo: null), Is.Null);
            Assert.That(EzDanSet.Create(axes(overall: 0), keyCount: 4, holdRatio: 0.1, xxySr: 6, chartInfo: null), Is.Null);
        }

        private static Dictionary<string, double> axes(double overall)
        {
            return new Dictionary<string, double>
            {
                [EzMinaSkillAxis.Overall.ToMsdSkillId()] = overall,
                [EzMinaSkillAxis.Stream.ToMsdSkillId()] = overall * 0.9,
                [EzMinaSkillAxis.Technical.ToMsdSkillId()] = overall * 0.95,
            };
        }
    }
}
