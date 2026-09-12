// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.IO.Stores;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.Resources;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzSunnyDanIntervalsTest
    {
        [Test]
        public void SixKRcLookupMapsMidLowTierToMinus()
        {
            Assert.That(EzSunnyDanIntervals.TryLookup(6, DanSkillSystem.SIDE_RC, 7.03, out var result), Is.True);
            Assert.That(result.DisplayLabel, Is.EqualTo("7-"));
            Assert.That(result.IntervalName, Is.EqualTo("Regular 7 mid/low"));
            Assert.That(result.RawDan, Is.EqualTo(6.8).Within(0.001));
        }

        [Test]
        public void FourKReformLookupUsesNumericBareLabel()
        {
            Assert.That(EzSunnyDanIntervals.TryLookup(4, DanSkillSystem.SIDE_RC, 3.2, out var result), Is.True);
            Assert.That(result.DisplayLabel, Does.Match(@"^\d"));
            Assert.That(result.IntervalName, Does.Contain("Reform"));
        }

        [Test]
        public void UnsupportedKeyCountFallsBack()
        {
            Assert.That(EzSunnyDanIntervals.TryLookup(5, DanSkillSystem.SIDE_RC, 7.0, out _), Is.False);
        }

        [Test]
        public void ChartDanPrefersXxyIntervalLabelOverMsdHeuristic()
        {
            var msd = new Dictionary<string, double>
            {
                [EzMinaSkillAxis.Overall.ToMsdSkillId()] = 20.5,
                [EzMinaSkillAxis.Technical.ToMsdSkillId()] = 22.0,
                [EzMinaSkillAxis.Stream.ToMsdSkillId()] = 10.0,
            };

            var withXxy = EzChartDanEstimator.FromMsd(msd, keyCount: 6, holdRatio: 0.1, xxySr: 7.03);
            var withoutXxy = EzChartDanEstimator.FromMsd(msd, keyCount: 6, holdRatio: 0.1);

            Assert.That(withXxy, Is.Not.Null);
            Assert.That(withoutXxy, Is.Not.Null);
            Assert.That(withXxy!.Label, Is.EqualTo("7-"));
            Assert.That(withXxy.OverallMsd, Is.EqualTo(20.5).Within(0.001));
            Assert.That(withXxy.DominantAxis, Is.EqualTo(EzMinaSkillAxis.Technical));
            Assert.That(withXxy.Label, Is.Not.EqualTo(withoutXxy!.Label));
        }

        [Test]
        public void LiveWithoutXxyDoesNotInventMsdHeuristicDanLabels()
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
                xxySr: null,
                chartInfo: null,
                holdCount: 0,
                allowMsdHeuristicLabels: false);

            Assert.That(live, Is.Not.Null);
            Assert.That(live!.OverallMsd, Is.EqualTo(20.5).Within(0.001));
            Assert.That(live.HasSide(EzDanSide.Rc), Is.False);
            Assert.That(live.HasSide(EzDanSide.Ln), Is.False);

            var persistFallback = EzPersistedChartDan.TryComputeFromStored(
                "hash",
                Guid.NewGuid(),
                msd,
                keyCount: 6,
                holdRatio: 0.1,
                xxySr: null,
                chartInfo: null,
                holdCount: 0,
                allowMsdHeuristicLabels: true);

            Assert.That(persistFallback, Is.Not.Null);
            Assert.That(persistFallback!.HasSide(EzDanSide.Rc), Is.True);
        }

        [Test]
        public void RateMergeKeepsSunnyLabelsAndUpdatesMsd()
        {
            var baseline = new EzPersistedChartDan
            {
                BeatmapHash = "h",
                RcRawDan = 6.8,
                RcLabel = "7-",
                OverallMsd = 18,
                KeyCount = 6,
            };
            var live = new EzPersistedChartDan
            {
                BeatmapHash = "h",
                RcRawDan = 14,
                RcLabel = "finish",
                OverallMsd = 24,
                KeyCount = 6,
                HoldRatio = 0.2,
            };

            var merged = EzPersistedChartDan.MergeKeepSunnyLabelsUpdateMsd(baseline, live);
            Assert.That(merged.RcLabel, Is.EqualTo("7-"));
            Assert.That(merged.RcRawDan, Is.EqualTo(6.8).Within(0.001));
            Assert.That(merged.OverallMsd, Is.EqualTo(24).Within(0.001));
            Assert.That(merged.HoldRatio, Is.EqualTo(0.2).Within(0.001));
        }

        [Test]
        public void TexturePathsResolveForBundledLadders()
        {
            Assert.That(EzDanLadders.TryGetTexturePath(4, DanSkillSystem.SIDE_RC, "7++"), Is.EqualTo("Dans/reform/7"));
            Assert.That(EzDanLadders.TryGetTexturePath(6, DanSkillSystem.SIDE_RC, "7-"), Is.EqualTo("Dans/6k/7"));
            Assert.That(EzDanLadders.TryGetTexturePath(6, DanSkillSystem.SIDE_LN, "7"), Is.EqualTo("Dans/6k/ln-7"));
            Assert.That(EzDanLadders.TryGetTexturePath(7, DanSkillSystem.SIDE_RC, "zenith"), Is.EqualTo("Dans/7k/zenith"));
            Assert.That(EzDanLadders.TryGetEmbeddedTexturePath("Dans/6k/7"), Is.EqualTo("Dans/_6k/7"));
            Assert.That(EzDanLadders.TryGetEmbeddedTexturePath("Dans/reform/7"), Is.Null);
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
            Assert.That(EzPatternAxisExtensions.TryParse("LN Hold", out var pattern), Is.True);
            Assert.That(pattern, Is.EqualTo(EzPatternAxis.LnHold));
            Assert.That(pattern.DisplayName().ToString(), Is.EqualTo("长条按住").Or.EqualTo("LN Hold"));
            Assert.That(EzDanSide.Ln.ToId(), Is.EqualTo(DanSkillSystem.SIDE_LN));
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
