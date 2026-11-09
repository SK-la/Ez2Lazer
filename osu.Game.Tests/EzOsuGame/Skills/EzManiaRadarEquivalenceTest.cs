// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.EzOsuGame.Skills.SSR;
using osu.Game.EzOsuGame.Skills.SSR.SR4Pattern;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    /// <summary>
    /// The SSR radar is fed by a chart extraction rather than by the beatmap. This pins the extraction to the
    /// objects it was taken from: a hand-built input with the same notes and timing must rate identically, so a
    /// dropped note or a misread timing point shows up here instead of as a shifted radar.
    /// </summary>
    [TestFixture]
    public class EzManiaRadarEquivalenceTest
    {
        [TestCase(4, false)]
        [TestCase(4, true)]
        [TestCase(7, false)]
        [TestCase(7, true)]
        public void ExtractionRatesLikeTheBeatmapItCameFrom(int keyCount, bool holdHeavy)
        {
            ManiaBeatmap beatmap = createChart(keyCount, holdHeavy);
            var extracted = EzCSIEngine.FromPlayable(beatmap);
            var manual = buildChart(beatmap, keyCount);

            Assert.That(extracted.FullyRepresented, Is.True, "a native mania chart must extract losslessly");

            double overallDifficulty = beatmap.Difficulty.OverallDifficulty;

            Assert.That(extracted.Notes.Count, Is.EqualTo(manual.Notes.Count), "note count");
            Assert.That(extracted.KeyCount, Is.EqualTo(manual.KeyCount), "key count");

            var fromExtraction = EzSSREngine.CalculateSRWithPatternSnapshot(extracted, keyCount, overallDifficulty);
            var fromManual = EzSSREngine.CalculateSRWithPatternSnapshot(manual, keyCount, overallDifficulty);

            assertBitIdentical(fromManual.Sr, fromExtraction.Sr, "total SR");

            EzManiaSR4PatternSnapshot expected = fromManual.PatternSnapshot;
            EzManiaSR4PatternSnapshot actual = fromExtraction.PatternSnapshot;

            assertBitIdentical(expected.BracketStar, actual.BracketStar, "bracket star");
            assertBitIdentical(expected.JackStar, actual.JackStar, "jack star");
            assertBitIdentical(expected.BurstStar, actual.BurstStar, "burst star");
            assertBitIdentical(expected.LongNoteUsageStar, actual.LongNoteUsageStar, "LN usage star");
            assertBitIdentical(expected.LongNoteReleaseStar, actual.LongNoteReleaseStar, "LN release star");
            assertBitIdentical(expected.AnchorStar, actual.AnchorStar, "anchor star");
            assertBitIdentical(expected.EffectiveWeights, actual.EffectiveWeights, "weights");
        }

        private static void assertBitIdentical(double expected, double actual, string label)
            => Assert.That(BitConverter.DoubleToInt64Bits(actual), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected)), label);

        private static void assertBitIdentical(IReadOnlyList<double> expected, IReadOnlyList<double> actual, string label)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count), $"{label} length");

            for (int i = 0; i < expected.Count; i++)
                assertBitIdentical(expected[i], actual[i], $"{label}[{i}]");
        }

        private static EzManiaChartInput buildChart(ManiaBeatmap beatmap, int keyCount)
        {
            var notes = new List<EzManiaNote>(beatmap.HitObjects.Count);
            double lastTime = 0;
            double maxEndTime = 0;

            foreach (var hitObject in beatmap.HitObjects)
            {
                maxEndTime = Math.Max(maxEndTime, hitObject.GetEndTime());

                if (hitObject is not IHasColumn columnObject)
                    continue;

                double endTime = hitObject.GetEndTime();
                bool isHold = hitObject is IHasDuration duration && duration.Duration > 0;

                notes.Add(new EzManiaNote(hitObject.StartTime, columnObject.Column, isHold, endTime));
                lastTime = Math.Max(lastTime, endTime);
            }

            var timingPoints = new List<EzManiaTimingPoint>();

            foreach (var point in beatmap.ControlPointInfo.TimingPoints)
                timingPoints.Add(new EzManiaTimingPoint(point.Time, point.BeatLength));

            return new EzManiaChartInput
            {
                KeyCount = keyCount,
                Bpm = 60000 / 500.0,
                TotalLengthMs = Math.Max(lastTime, maxEndTime),
                Notes = notes,
                TimingPoints = timingPoints,
                FullyRepresented = true,
            };
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
