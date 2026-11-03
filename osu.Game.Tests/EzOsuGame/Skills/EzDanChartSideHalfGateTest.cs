// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Mods;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzDanChartSideHalfGateTest
    {
        [TestCase(0.1, false)]
        [TestCase(0.44, false)]
        [TestCase(0.45, true)]
        [TestCase(0.9, true)]
        public void Hub_exclusive_ln_half_only_at_or_above_primary_ratio_4k(double hold, bool expected)
        {
            Assert.That(EzDanAlgorithm.AllowsChartSideHalf(EzDanSide.Ln, 4, hold), Is.EqualTo(expected));
            Assert.That(EzDanAlgorithm.AllowsChartSideHalf(EzDanSide.Rc, 4, hold), Is.EqualTo(!expected));
        }

        [Test]
        public void Hub_exclusive_ln_half_uses_lower_7k_threshold()
        {
            Assert.That(EzDanAlgorithm.AllowsChartSideHalf(EzDanSide.Ln, 7, 0.37), Is.False);
            Assert.That(EzDanAlgorithm.AllowsChartSideHalf(EzDanSide.Ln, 7, 0.375), Is.True);
            Assert.That(EzDanAlgorithm.AllowsChartSideHalf(EzDanSide.Rc, 7, 0.37), Is.True);
            Assert.That(EzDanAlgorithm.AllowsChartSideHalf(EzDanSide.Rc, 7, 0.375), Is.False);
        }

        [Test]
        public void Persisted_ln_half_allows_hold_count_over_100_even_below_ratio()
        {
            // Pin the threshold instead of relying on the default: the configurable-threshold test
            // below mutates the shared config, so without this the two tests pass or fail by order.
            using (new HoldThresholdScope(100))
            {
                Assert.That(EzDanAlgorithm.AllowsPersistedChartLnHalf(4, holdRatio: 0.1, holdCount: 100), Is.False);
                Assert.That(EzDanAlgorithm.AllowsPersistedChartLnHalf(4, holdRatio: 0.1, holdCount: 101), Is.True);
            }
        }

        [Test]
        public void Persisted_ln_half_allows_hub_ratio_without_hold_count()
        {
            Assert.That(EzDanAlgorithm.AllowsPersistedChartLnHalf(4, holdRatio: 0.45, holdCount: -1), Is.True);
            Assert.That(EzDanAlgorithm.AllowsPersistedChartLnHalf(4, holdRatio: 0.44, holdCount: -1), Is.False);
            Assert.That(EzDanAlgorithm.AllowsPersistedChartLnHalf(7, holdRatio: 0.375, holdCount: -1), Is.True);
        }

        [Test]
        public void Persisted_ln_half_uses_configurable_hold_threshold()
        {
            var config = GlobalConfigStore.EzConfig;
            GlobalConfigStore.EzConfig = config;

            try
            {
                config.SetValue(Ez2Setting.SkillLnChartMinHoldObjects, 50);
                Assert.That(EzDanAlgorithm.AllowsPersistedChartLnHalf(4, holdRatio: 0.1, holdCount: 51), Is.True);
                Assert.That(EzDanAlgorithm.AllowsPersistedChartLnHalf(4, holdRatio: 0.1, holdCount: 50), Is.False);

                config.SetValue(Ez2Setting.SkillLnChartMinHoldObjects, 500);
                Assert.That(EzDanAlgorithm.AllowsPersistedChartLnHalf(4, holdRatio: 0.1, holdCount: 101), Is.False);

                // 或关系：比例线不受阈值影响，7K 用 37.5% 线。
                Assert.That(EzDanAlgorithm.AllowsPersistedChartLnHalf(7, holdRatio: 0.375, holdCount: 0), Is.True);
            }
            finally
            {
                config.SetValue(Ez2Setting.SkillLnChartMinHoldObjects, 150);
            }
        }

        /// <summary>
        /// Sets the shared LN hold threshold and restores the previous value on dispose, so a fixture
        /// that asserts the default does not depend on the order its tests run in.
        /// </summary>
        private sealed class HoldThresholdScope : IDisposable
        {
            private readonly int previous;

            public HoldThresholdScope(int value)
            {
                var config = GlobalConfigStore.EzConfig;
                GlobalConfigStore.EzConfig = config;

                previous = config.Get<int>(Ez2Setting.SkillLnChartMinHoldObjects);
                config.SetValue(Ez2Setting.SkillLnChartMinHoldObjects, value);
            }

            public void Dispose()
                => GlobalConfigStore.EzConfig.SetValue(Ez2Setting.SkillLnChartMinHoldObjects, previous);
        }

        [Test]
        public void AffectsChartSkills_detects_beatmap_converter_mod()
        {
            Assert.That(EzModRate.AffectsChartSkills((IReadOnlyList<Mod>?)null), Is.False);
            Assert.That(EzModRate.AffectsChartSkills(Array.Empty<Mod>()), Is.False);
            Assert.That(EzModRate.AffectsChartSkills(new ModBeatmapConverterStub()), Is.True);
        }

        private sealed class ModBeatmapConverterStub : Mod, IApplicableToBeatmapConverter
        {
            public override string Name => "stub";
            public override string Acronym => "ST";
            public override LocalisableString Description => "stub";
#pragma warning disable CS0672
            public override double ScoreMultiplier => 1;
#pragma warning restore CS0672

            public void ApplyToBeatmapConverter(IBeatmapConverter beatmapConverter)
            {
            }
        }
    }
}
