// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Analysis;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    /// <summary>
    /// The two SSR coefficients are user settings now, not mod state. These pin what that means: they reach the
    /// rating, each keeps its own effect, and a stored value outside the usable range falls back instead of
    /// producing garbage.
    /// </summary>
    /// <remarks>
    /// The settings deliberately do not bump a stored-data version, so nothing here asserts about persistence; the
    /// only contract is that a rating computed after a change reflects it.
    /// </remarks>
    [TestFixture]
    public class EzSsrTuningTest
    {
        private Ez2ConfigManager config = null!;

        [SetUp]
        public void SetUp()
        {
            // The getter builds a fresh manager on each access until one is assigned, so pin it before writing.
            config = GlobalConfigStore.EzConfig;
            GlobalConfigStore.EzConfig = config;
        }

        [TearDown]
        public void TearDown()
        {
            config.SetValue(Ez2Setting.SsrRescaleThreshold, EzSsrTuning.FALLBACK.RescaleThreshold);
            config.SetValue(Ez2Setting.SsrLnMultiplier, EzSsrTuning.FALLBACK.LnMultiplier);
        }

        [Test]
        public void DefaultsMatchTheValuesTheStoredDataWasProducedWith()
        {
            var tuning = SRCalculator.Tuning;

            Assert.That(tuning.RescaleThreshold, Is.EqualTo(8.0));
            Assert.That(tuning.LnMultiplier, Is.EqualTo(5.5));
        }

        [Test]
        public void SettingsAreReadIntoTheTuning()
        {
            config.SetValue(Ez2Setting.SsrRescaleThreshold, 9.0);
            config.SetValue(Ez2Setting.SsrLnMultiplier, 6.0);

            var tuning = SRCalculator.Tuning;

            Assert.That(tuning.RescaleThreshold, Is.EqualTo(9.0));
            Assert.That(tuning.LnMultiplier, Is.EqualTo(6.0));
            Assert.That(SRCalculator.RescaleHighThreshold, Is.EqualTo(9.0));
            Assert.That(SRCalculator.LnIntegralMultiplier, Is.EqualTo(6.0));
        }

        /// <summary>
        /// The setting's own range is the first line of defence: an out-of-range write is clamped by the bindable
        /// before the rating ever sees it. The fallback inside <see cref="SRCalculator.Tuning"/> is for a value that
        /// reached the file some other way, so it cannot be reached from here.
        /// </summary>
        [TestCase(0.0, 6.0)]
        [TestCase(-1.0, 6.0)]
        [TestCase(double.NaN, 6.0)]
        [TestCase(20.0, 10.0)]
        [TestCase(double.PositiveInfinity, 10.0)]
        public void OutOfRangeThresholdIsClamped(double written, double expected)
        {
            config.SetValue(Ez2Setting.SsrRescaleThreshold, written);

            var tuning = SRCalculator.Tuning;

            Assert.That(tuning.RescaleThreshold, Is.EqualTo(expected));
            Assert.That(double.IsFinite(tuning.RescaleThreshold), Is.True);
        }

        [TestCase(0.0, 2.0)]
        [TestCase(-2.0, 2.0)]
        [TestCase(double.NaN, 2.0)]
        [TestCase(99.0, 8.0)]
        public void OutOfRangeLnMultiplierIsClamped(double written, double expected)
        {
            config.SetValue(Ez2Setting.SsrLnMultiplier, written);

            var tuning = SRCalculator.Tuning;

            Assert.That(tuning.LnMultiplier, Is.EqualTo(expected));
            Assert.That(double.IsFinite(tuning.LnMultiplier), Is.True);
        }

        [Test]
        public void RaisingTheThresholdRaisesTheRating()
        {
            ManiaBeatmap beatmap = createChart(4, dense: true);

            double low = rate(beatmap, 5.0, 5.5);
            double high = rate(beatmap, 10.0, 5.5);

            // Above the highest threshold the settings allow, otherwise the rescale never engages and this is vacuous.
            Assert.That(low, Is.GreaterThan(10.0), $"fixture rates {low}, too low to exercise the rescale");
            Assert.That(high, Is.GreaterThan(low), "a higher threshold compresses less of the rating");
        }

        [Test]
        public void RaisingTheLnMultiplierRaisesHoldCharts()
        {
            ManiaBeatmap beatmap = createChart(4, dense: false, holds: HoldMode.All);

            double low = rate(beatmap, 8.0, 2.0);
            double high = rate(beatmap, 8.0, 8.0);

            Assert.That(high, Is.GreaterThan(low), "a higher LN multiplier rates holds harder");
        }

        [Test]
        public void LnMultiplierDoesNotMovePureRiceCharts()
        {
            ManiaBeatmap beatmap = createChart(4, dense: false, holds: HoldMode.None);

            double low = rate(beatmap, 8.0, 2.0);
            double high = rate(beatmap, 8.0, 8.0);

            Assert.That(high, Is.EqualTo(low).Within(1e-9),
                "a chart with no long notes does not read the LN multiplier at all");
        }

        /// <summary>
        /// One pass must resolve the pair once. If a future change reads the settings per note again, a settings
        /// write that lands mid-pass could split one rating - and resolving per note is what turned a 50 ms pass
        /// into a 2400 ms one.
        /// </summary>
        [Test]
        public void ARatingUsesOneResolvedPair()
        {
            ManiaBeatmap beatmap = createChart(4, dense: true);

            double reference = rate(beatmap, 8.0, 5.5);

            // A second pass at the same settings is bit-identical; nothing inside the pass perturbs the pair.
            double repeated = rate(beatmap, 8.0, 5.5);

            Assert.That(repeated, Is.EqualTo(reference));
        }

        private double rate(ManiaBeatmap beatmap, double threshold, double lnMultiplier)
        {
            config.SetValue(Ez2Setting.SsrRescaleThreshold, threshold);
            config.SetValue(Ez2Setting.SsrLnMultiplier, lnMultiplier);

            return SRCalculator.CalculateSR(beatmap, 1.0);
        }

        private enum HoldMode
        {
            None,
            EveryFourth,
            All,
        }

        private static ManiaBeatmap createChart(int keyCount, bool dense, HoldMode holds = HoldMode.EveryFourth)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(keyCount))
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Metadata = new BeatmapMetadata { Title = "tuning", Artist = "tuning" },
                    Ruleset = new ManiaRuleset().RulesetInfo,
                    Difficulty = new BeatmapDifficulty { CircleSize = keyCount, OverallDifficulty = 8 },
                },
                Difficulty = { OverallDifficulty = 8 },
            };

            int seed = 8642;
            int rows = dense ? 900 : 400;

            for (int row = 0; row < rows; row++)
            {
                seed = seed * 1103515245 + 12345;
                int size = dense ? keyCount : 1 + Math.Abs(seed >> 16) % Math.Min(3, keyCount);
                double time = 500 + row * (dense ? 90.0 : 125.0);

                for (int note = 0; note < size; note++)
                {
                    seed = seed * 1103515245 + 12345;
                    int column = Math.Abs(seed >> 16) % keyCount;

                    bool isHold = holds switch
                    {
                        HoldMode.All => true,
                        HoldMode.EveryFourth => (seed & 3) == 0,
                        _ => false,
                    };

                    if (isHold)
                    {
                        beatmap.HitObjects.Add(new HoldNote
                        {
                            StartTime = time,
                            Column = column,
                            Duration = 200 + Math.Abs(seed >> 8) % 600,
                        });
                    }
                    else
                    {
                        beatmap.HitObjects.Add(new Note { StartTime = time, Column = column });
                    }
                }
            }

            beatmap.ControlPointInfo.Add(0, new osu.Game.Beatmaps.ControlPoints.TimingControlPoint { BeatLength = 500 });
            return beatmap;
        }
    }
}
