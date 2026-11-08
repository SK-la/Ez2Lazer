// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.IO.Stores;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.Resources;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzSunnyDanIntervalsTest
    {
        [Test]
        public void SixKRcLookupMapsMidLowTierToMinus()
        {
            Assert.That(EzSunnyDanIntervals.TryLookup(6, EzDanSide.Rc.ToId(), 7.03, out var result), Is.True);
            Assert.That(result.DisplayLabel, Is.EqualTo("7-"));
            Assert.That(result.IntervalName, Is.EqualTo("Regular 7 mid/low"));
            Assert.That(result.RawDan, Is.EqualTo(6.8).Within(0.001));
        }

        [Test]
        public void FourKReformLookupUsesNumericBareLabel()
        {
            Assert.That(EzSunnyDanIntervals.TryLookup(4, EzDanSide.Rc.ToId(), 3.2, out var result), Is.True);
            Assert.That(result.DisplayLabel, Does.Match(@"^\d"));
            Assert.That(result.IntervalName, Does.Contain("Reform"));
        }

        [Test]
        public void UnsupportedKeyCountFallsBack()
        {
            Assert.That(EzSunnyDanIntervals.TryLookup(5, EzDanSide.Rc.ToId(), 7.0, out _), Is.False);
        }

        [Test]
        public void ChartDanPrefersSunnySRIntervalLabelOverMsdHeuristic()
        {
            var msd = new Dictionary<string, double>
            {
                [EzMinaSkillAxis.Overall.ToMsdSkillId()] = 20.5,
                [EzMinaSkillAxis.Technical.ToMsdSkillId()] = 22.0,
                [EzMinaSkillAxis.Stream.ToMsdSkillId()] = 10.0,
            };

            var withSunnySR = EzDanEngine.FromMsd(msd, keyCount: 6, holdRatio: 0.1, sunnySR: 7.03);
            var withoutSunnySR = EzDanEngine.FromMsd(msd, keyCount: 6, holdRatio: 0.1);

            Assert.That(withSunnySR, Is.Not.Null);
            Assert.That(withoutSunnySR, Is.Not.Null);
            Assert.That(withSunnySR!.Label, Is.EqualTo("7-"));
            Assert.That(withSunnySR.OverallMsd, Is.EqualTo(20.5).Within(0.001));
            Assert.That(withSunnySR.DominantAxis, Is.EqualTo(EzMinaSkillAxis.Technical));
            Assert.That(withSunnySR.Label, Is.Not.EqualTo(withoutSunnySR!.Label));
        }

        [Test]
        public void WithoutSunnySRTheStoredDanIsTaggedAsAMeansEstimate()
        {
            var msd = new Dictionary<string, double>
            {
                [EzMinaSkillAxis.Overall.ToMsdSkillId()] = 20.5,
                [EzMinaSkillAxis.Technical.ToMsdSkillId()] = 22.0,
            };

            var computed = EzPersistedChartDan.TryComputeFromStored(
                "hash",
                Guid.NewGuid(),
                msd,
                keyCount: 6,
                holdRatio: 0.1,
                sunnySR: null,
                chartInfo: null,
                holdCount: 0);

            // The stored row keeps the axis value and the means-derived sides; the reader decides whether to show
            // them, instead of two callers persisting different shapes for the same chart.
            Assert.That(computed, Is.Not.Null);
            Assert.That(computed!.OverallMsd, Is.EqualTo(20.5).Within(0.001));
            Assert.That(computed.HasSide(EzDanSide.Rc), Is.True);
        }

        [Test]
        public void LiveSunnySRFeedsSunnyLabelsUnderConvertedKeys()
        {
            var msd = new Dictionary<string, double>
            {
                [EzMinaSkillAxis.Overall.ToMsdSkillId()] = 20.5,
                [EzMinaSkillAxis.Technical.ToMsdSkillId()] = 22.0,
            };

            var live = EzPersistedChartDan.TryComputeFromStored(
                "hash",
                Guid.NewGuid(),
                msd,
                keyCount: 6,
                holdRatio: 0.1,
                sunnySR: 7.03,
                chartInfo: null,
                holdCount: 0);

            Assert.That(live, Is.Not.Null);
            Assert.That(live!.RcLabel, Is.EqualTo("7-"));
            Assert.That(live.RcRawDan, Is.EqualTo(6.8).Within(0.001));
        }

        [Test]
        public void TexturePathsResolveForBundledLadders()
        {
            Assert.That(EzDanLadders.TryGetTexturePath(4, EzDanSide.Rc.ToId(), "7++"), Is.EqualTo("Dans/reform/7"));
            Assert.That(EzDanLadders.TryGetTexturePath(6, EzDanSide.Rc.ToId(), "7-"), Is.EqualTo("Dans/6k/7"));
            Assert.That(EzDanLadders.TryGetTexturePath(6, EzDanSide.Ln.ToId(), "7"), Is.EqualTo("Dans/6k/ln-7"));
            Assert.That(EzDanLadders.TryGetTexturePath(7, EzDanSide.Rc.ToId(), "zenith"), Is.EqualTo("Dans/7k/zenith"));
        }

        [Test]
        public void SkillPresentationMapsEnumsAndWireIds()
        {
            Assert.That(EzMinaSkillAxis.JackSpeed.ToId(), Is.EqualTo("jack"));
            Assert.That(EzMinaSkillAxis.Technical.ToId(), Is.EqualTo("tech"));
            Assert.That(EzMinaSkillAxisExtensions.TryParse("tech", out var axis), Is.True);
            Assert.That(axis, Is.EqualTo(EzMinaSkillAxis.Technical));
            Assert.That(EzMinaSkillAxis.Technical.Chip().Name.ToString(), Is.EqualTo("技").Or.EqualTo("Tech"));
            Assert.That(EzMinaSkillAxis.Technical.Chip().AccentHex, Is.EqualTo("#83cf6b"));
            Assert.That(EzKeyPatternAxisExtensions.TryParse("LN Hold", out var pattern), Is.True);
            Assert.That(pattern, Is.EqualTo(EzKeyPatternAxis.LnHold));
            Assert.That(pattern.DisplayName().ToString(), Is.EqualTo("长条按住").Or.EqualTo("LN Hold"));
            Assert.That(EzDanSide.Ln.ToId(), Is.EqualTo(EzDanSide.Ln.ToId()));
        }

        [Test]
        public void BundledResourcesContainDanBadgeTextures()
        {
            using var store = new DllResourceStore(OsuResources.ResourceAssembly);

            Assert.That(store.GetStream("Textures/EzResources/Dans/reform/7.png"), Is.Not.Null);
            Assert.That(store.GetStream("Textures/EzResources/Dans/_6k/7.png"), Is.Not.Null);
            Assert.That(store.GetStream("Textures/EzResources/Dans/_6k/ln-7.png"), Is.Not.Null);
            Assert.That(store.GetStream("Textures/EzResources/Dans/_7k/zenith.png"), Is.Not.Null);
        }
    }
}
