// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzDanSideHeadlineTest
    {
        [Test]
        public void SevenKeyLn_anchor_pulls_release_up_to_clamp()
        {
            // Hub corpus example: General 13 + Release 9 → headline 12 (not plain mean 11).
            var skillsets = new Dictionary<string, EzDanSkillsetVerdict>
            {
                ["lngeneral"] = new EzDanSkillsetVerdict("lngeneral", 13, "x", 10),
                ["lnrelease"] = new EzDanSkillsetVerdict("lnrelease", 9, "y", 6),
            };

            var result = EzDanSideHeadline.FromSkillsets(7, EzDanSide.Ln, skillsets, new[] { 13.0, 9.0 });
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Value.RawDan, Is.EqualTo(12).Within(1e-6));
        }

        [Test]
        public void SevenKeyLn_near_tech_mean_pull()
        {
            // General 13 + Tech 12.5 → 13 + 0.5 * (-0.5) = 12.75
            var skillsets = new Dictionary<string, EzDanSkillsetVerdict>
            {
                ["lngeneral"] = new EzDanSkillsetVerdict("lngeneral", 13, "x", 10),
                ["lntech"] = new EzDanSkillsetVerdict("lntech", 12.5, "y", 8),
            };

            var result = EzDanSideHeadline.FromSkillsets(7, EzDanSide.Ln, skillsets, new[] { 13.0, 12.5 });
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Value.RawDan, Is.EqualTo(12.75).Within(1e-6));
        }

        [Test]
        public void Plain_mean_when_no_anchor_bucket()
        {
            var skillsets = new Dictionary<string, EzDanSkillsetVerdict>
            {
                ["jack"] = new EzDanSkillsetVerdict("jack", 10, "a", 5),
                ["tech"] = new EzDanSkillsetVerdict("tech", 12, "b", 5),
            };

            var result = EzDanSideHeadline.FromSkillsets(4, EzDanSide.Rc, skillsets, new[] { 10.0, 12.0 });
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Value.RawDan, Is.EqualTo(11).Within(1e-6));
        }

        [Test]
        public void Side_clears_fallback_needs_quorum()
        {
            Assert.That(EzDanSideHeadline.FromSideClears(4, EzDanSide.Rc, new[] { 1.0, 2.0, 3.0 }), Is.Null);

            var ok = EzDanSideHeadline.FromSideClears(4, EzDanSide.Rc, new[] { 8.0, 9.0, 10.0, 11.0 });
            Assert.That(ok, Is.Not.Null);
            Assert.That(ok!.Value.RawDan, Is.GreaterThan(0));
        }
    }
}
