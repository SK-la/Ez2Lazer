// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzDanClearWindowTest
    {
        [Test]
        public void FamilyDecay_second_clear_same_chart_weighs_less()
        {
            var clears = new List<EzDanClearEvidenceRow>
            {
                row("a", 12),
                row("a", 11.5), // same family, lower — still in pool after sort
                row("b", 10),
                row("c", 9),
                row("d", 8),
            };

            var (_, window, have) = EzDanClearWindow.Select(clears, need: 20);
            Assert.That(have, Is.GreaterThan(4));

            var sameFamily = window.Where(e => e.Clear.BeatmapHash == "a").ToList();
            Assert.That(sameFamily.Count, Is.EqualTo(2));
            Assert.That(sameFamily[0].RepeatWeight, Is.EqualTo(1).Within(1e-9));
            Assert.That(sameFamily[1].RepeatWeight, Is.EqualTo(0.9).Within(1e-9));
        }

        [Test]
        public void Average_under_quorum_is_null()
        {
            var clears = Enumerable.Range(0, 3).Select(i => row($"h{i}", 10 + i)).ToList();
            Assert.That(EzDanClearWindow.AverageRawDan(clears), Is.Null);
        }

        [Test]
        public void Average_uses_weights_not_bare_top10()
        {
            // Five distinct charts at 10 — mean 10.
            var clears = Enumerable.Range(0, 5).Select(i => row($"h{i}", 10)).ToList();
            Assert.That(EzDanClearWindow.AverageRawDan(clears), Is.EqualTo(10).Within(1e-6));
        }

        private static EzDanClearEvidenceRow row(string hash, double dan, double rate = 1) => new EzDanClearEvidenceRow
        {
            BeatmapHash = hash,
            KeyCount = 4,
            Side = "rc",
            Rate = rate,
            CreditedDan = dan,
            AlgorithmVersion = EzDanAlgorithm.VERSION,
        };
    }
}
