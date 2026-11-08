// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Analysis;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Tests.Analysis
{
    /// <summary>
    /// The radar may be built from an extraction the caller already holds, but only when that extraction describes the
    /// whole chart. This pins the shared path to the beatmap path: every axis must come out identical, not merely
    /// close, because the axes are published rounded to two decimals.
    /// </summary>
    [TestFixture]
    public class EzManiaRadarEquivalenceTest
    {
        [TestCase(4, false)]
        [TestCase(4, true)]
        [TestCase(7, false)]
        [TestCase(7, true)]
        public void SharedExtractionProducesTheSameRadar(int keyCount, bool holdHeavy)
        {
            ManiaBeatmap beatmap = createChart(keyCount, holdHeavy);
            var input = EzChartSkillInfoComputer.FromPlayable(beatmap);

            Assert.That(input.FullyRepresented, Is.True, "a native mania chart must extract losslessly, or the shared path never runs");

            var provider = new EzManiaAnalysisProvider();

            bool computed = provider.TryCompute(
                new EzAnalysisRequest(beatmap, 1.0, EzAnalysisScope.RulesetSpecificRadarData),
                System.Threading.CancellationToken.None, out IEzAnalysis fromBeatmap);

            bool shared = provider.TryCompute(
                new EzAnalysisRequest(beatmap, 1.0, EzAnalysisScope.RulesetSpecificRadarData, Chart: input),
                System.Threading.CancellationToken.None, out IEzAnalysis fromExtraction);

            Assert.That(computed && shared, Is.True);

            assertRadarMatches(fromBeatmap, fromExtraction, "key pattern radar");
        }

        /// <summary>
        /// A view that dropped something must not be trusted; the provider has to fall back to the beatmap.
        /// </summary>
        [Test]
        public void FilteredExtractionFallsBackToTheBeatmap()
        {
            ManiaBeatmap beatmap = createChart(4, holdHeavy: true);
            var lossless = EzChartSkillInfoComputer.FromPlayable(beatmap);

            // Drop every long note: the extraction no longer describes the chart, so the provider must ignore it.
            var notes = new List<EzManiaNote>();

            foreach (var note in lossless.Notes)
            {
                if (!note.IsHold)
                    notes.Add(note);
            }

            var filtered = new EzManiaChartInput
            {
                KeyCount = lossless.KeyCount,
                Bpm = lossless.Bpm,
                TotalLengthMs = lossless.TotalLengthMs,
                Notes = notes,
                TimingPoints = lossless.TimingPoints,
                FullyRepresented = false,
            };

            var provider = new EzManiaAnalysisProvider();

            provider.TryCompute(
                new EzAnalysisRequest(beatmap, 1.0, EzAnalysisScope.RulesetSpecificRadarData),
                System.Threading.CancellationToken.None, out IEzAnalysis reference);

            provider.TryCompute(
                new EzAnalysisRequest(beatmap, 1.0, EzAnalysisScope.RulesetSpecificRadarData, Chart: filtered),
                System.Threading.CancellationToken.None, out IEzAnalysis withFiltered);

            assertRadarMatches(reference, withFiltered, "filtered fallback");
        }

        private static void assertRadarMatches(IEzAnalysis expected, IEzAnalysis actual, string label)
        {
            Assert.That(expected.TryGetValue(EzAnalysisFields.RULESET_SPECIFIC_RADAR_RESULT, out EzRulesetSpecificRadarResult expectedRadar), Is.True);
            Assert.That(actual.TryGetValue(EzAnalysisFields.RULESET_SPECIFIC_RADAR_RESULT, out EzRulesetSpecificRadarResult actualRadar), Is.True);

            assertAxes(expectedRadar.KeyPatternRadarData, actualRadar.KeyPatternRadarData, $"{label} key pattern");
            assertAxes(expectedRadar.SunnySRPatternRadarData, actualRadar.SunnySRPatternRadarData, $"{label} ssrpattern");
        }

        private static void assertAxes(EzRadarChartData<string> expected, EzRadarChartData<string> actual, string label)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count), $"{label} axis count");

            for (int i = 0; i < expected.Count; i++)
            {
                var left = expected[i];
                var right = actual[i];

                Assert.That(right.Axis, Is.EqualTo(left.Axis), $"{label} axis {i} name");
                Assert.That(right.Value, Is.EqualTo(left.Value), $"{label} axis {left.Axis}");
            }
        }

        private static ManiaBeatmap createChart(int keyCount, bool holdHeavy)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(keyCount))
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Metadata = new BeatmapMetadata { Title = "radar", Artist = "radar" },
                    Ruleset = new ManiaRuleset().RulesetInfo,
                    Difficulty = new BeatmapDifficulty { CircleSize = keyCount, OverallDifficulty = 8 },
                },
                Difficulty = { OverallDifficulty = 8 },
            };

            int seed = 2024;

            for (int row = 0; row < 700; row++)
            {
                seed = seed * 1103515245 + 12345;
                int size = 1 + Math.Abs(seed >> 16) % Math.Min(3, keyCount);
                double time = 500 + row * 125.0;

                for (int note = 0; note < size; note++)
                {
                    seed = seed * 1103515245 + 12345;
                    int column = Math.Abs(seed >> 16) % keyCount;

                    if (holdHeavy || (seed & 3) == 0)
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
            beatmap.ControlPointInfo.Add(30_000, new osu.Game.Beatmaps.ControlPoints.TimingControlPoint { BeatLength = 400 });
            return beatmap;
        }
    }
}
