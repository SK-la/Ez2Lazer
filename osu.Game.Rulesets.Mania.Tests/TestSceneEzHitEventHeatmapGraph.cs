// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEzMania.Analysis;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public partial class TestSceneEzHitEventHeatmapGraph : OsuTestScene
    {
        private ScoreInfo testScore;
        private IBeatmap testBeatmap;

        private const int RANDOM_SEED = 1234; // Fixed seed for consistent random data
        private const int COLUMNS = 7; // 7k
        private const int NOTES_PER_COLUMN = 50; // 50 notes per column

        [SetUp]
        public void SetUp()
        {
            // Create a test beatmap: 7k with 50 notes per column (350 total)
            testBeatmap = createTestBeatmap();
            // Create a test score with specific hit event distribution
            testScore = createTestScore(testBeatmap);
        }

        [Test]
        public void TestEmptyScore()
        {
            EzManiaScoreGraph graph = null;

            AddStep("Create graph with empty score", () =>
            {
                var emptyScore = new ScoreInfo
                {
                    BeatmapInfo = testBeatmap.BeatmapInfo,
                    Ruleset = testBeatmap.BeatmapInfo.Ruleset,
                    HitEvents = new List<HitEvent>(),
                    Accuracy = 1.0,
                    TotalScore = 0,
                    Mods = Array.Empty<Mod>()
                };

                Child = graph = new EzManiaScoreGraph(emptyScore, testBeatmap)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(800, 400),
                };
            });

            AddAssert("Graph created successfully", () => graph != null);
            AddAssert("Graph is visible", () => graph.IsPresent);
        }

        [Test]
        public void TestPerfectScore()
        {
            EzManiaScoreGraph graph = null;

            AddStep("Create graph with perfect score", () =>
            {
                var perfectScore = createPerfectScore(testBeatmap);

                Child = graph = new EzManiaScoreGraph(perfectScore, testBeatmap)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(800, 400),
                };
            });

            AddAssert("Graph created successfully", () => graph != null);
            AddAssert("Graph is visible", () => graph.IsPresent);
            AddAssert("Graph has hit events", () => graph != null && graph.DrawWidth > 0);
        }

        [Test]
        public void TestMixedResults()
        {
            EzManiaScoreGraph graph = null;

            AddStep("Create graph with test score", () =>
            {
                Child = graph = new EzManiaScoreGraph(testScore, testBeatmap)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(800, 400),
                };
            });

            AddAssert("Graph created successfully", () => graph != null);
            AddAssert("Graph is visible", () => graph.IsPresent);
            AddAssert("Graph height is reasonable", () => graph != null && graph.DrawHeight > 0);
        }

        /// <summary>
        /// Creates a 7k beatmap with 50 notes per column using fixed seed randomization.
        /// Total: 7 columns * 50 notes = 350 notes
        /// </summary>
        private IBeatmap createTestBeatmap()
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(COLUMNS))
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Ruleset = new ManiaRuleset().RulesetInfo,
                    Difficulty = new BeatmapDifficulty
                    {
                        DrainRate = 8,
                        OverallDifficulty = 8,
                        ApproachRate = 8,
                        CircleSize = 4,
                    }
                },
                ControlPointInfo = new ControlPointInfo()
            };

            var random = new Random(RANDOM_SEED);

            // Create 50 notes per column
            for (int column = 0; column < COLUMNS; column++)
            {
                for (int i = 0; i < NOTES_PER_COLUMN; i++)
                {
                    // Use fixed seed random for note timing within each column
                    double timeOffset = random.NextDouble() * 500; // Random offset between 0-500ms for each note

                    beatmap.HitObjects.Add(new Note
                    {
                        StartTime = column * 500 + i * 200 + timeOffset,
                        Column = column
                    });
                }
            }

            return beatmap;
        }

        /// <summary>
        /// Creates a test score with segmented symmetric normal distribution of timing offsets.
        /// - [-40, 40]ms: 200 hits centered at 0ms (σ=20ms)
        /// - [40, 100] and [-100, -40]ms: 50 hits centered at ±40ms (σ=30ms)
        /// - [100, 150] and [-150, -100]ms: 20 hits centered at ±100ms (σ=25ms)
        /// - [150, 200] and [-200, -150]ms: 10 hits centered at ±150ms (σ=25ms)
        /// </summary>
        private ScoreInfo createTestScore(IBeatmap beatmap)
        {
            var hitEvents = new List<HitEvent>();
            var random = new Random(RANDOM_SEED);

            // Initialize hit windows based on beatmap difficulty
            var hitWindows = new ManiaHitWindows();
            hitWindows.SetDifficulty(beatmap.Difficulty.OverallDifficulty);

            // Collect all offsets with their results
            var allOffsetsWithResults = new List<(double offset, HitResult result)>();

            // Segment 1: [-40, 40]ms with 200 hits
            // Normal distribution centered at 0ms with σ=20ms (symmetric around 0)
            for (int i = 0; i < 200; i++)
            {
                double offset = GenerateNormalOffset(random, 0, 20);
                // Clamp to [-40, 40] range
                offset = Math.Max(-40, Math.Min(40, offset));

                HitResult result = hitWindows.ResultFor(offset);
                if (result == HitResult.None)
                    result = HitResult.Miss;

                allOffsetsWithResults.Add((offset, result));
            }

            // Segment 2: [40, 100]ms and [-100, -40]ms with 50 hits
            // Normal distribution centered at ±40ms with σ=30ms
            for (int i = 0; i < 25; i++)
            {
                // Positive side [40, 100]
                double offset = GenerateNormalOffset(random, 40, 30);
                offset = Math.Max(40, Math.Min(100, offset));

                HitResult result = hitWindows.ResultFor(offset);
                if (result == HitResult.None)
                    result = HitResult.Miss;

                allOffsetsWithResults.Add((offset, result));
            }

            for (int i = 0; i < 25; i++)
            {
                // Negative side [-100, -40]
                double offset = GenerateNormalOffset(random, -40, 30);
                offset = Math.Max(-100, Math.Min(-40, offset));

                HitResult result = hitWindows.ResultFor(offset);
                if (result == HitResult.None)
                    result = HitResult.Miss;

                allOffsetsWithResults.Add((offset, result));
            }

            // Segment 3: [100, 150]ms and [-150, -100]ms with 20 hits
            // Normal distribution centered at ±100ms with σ=25ms
            for (int i = 0; i < 10; i++)
            {
                // Positive side [100, 150]
                double offset = GenerateNormalOffset(random, 100, 25);
                offset = Math.Max(100, Math.Min(150, offset));

                HitResult result = hitWindows.ResultFor(offset);
                if (result == HitResult.None)
                    result = HitResult.Miss;

                allOffsetsWithResults.Add((offset, result));
            }

            for (int i = 0; i < 10; i++)
            {
                // Negative side [-150, -100]
                double offset = GenerateNormalOffset(random, -100, 25);
                offset = Math.Max(-150, Math.Min(-100, offset));

                HitResult result = hitWindows.ResultFor(offset);
                if (result == HitResult.None)
                    result = HitResult.Miss;

                allOffsetsWithResults.Add((offset, result));
            }

            // Segment 4: [150, 200]ms and [-200, -150]ms with 10 hits
            // Normal distribution centered at ±150ms with σ=25ms
            for (int i = 0; i < 5; i++)
            {
                // Positive side [150, 200]
                double offset = GenerateNormalOffset(random, 150, 25);
                offset = Math.Max(150, Math.Min(200, offset));

                HitResult result = hitWindows.ResultFor(offset);
                if (result == HitResult.None)
                    result = HitResult.Miss;

                allOffsetsWithResults.Add((offset, result));
            }

            for (int i = 0; i < 5; i++)
            {
                // Negative side [-200, -150]
                double offset = GenerateNormalOffset(random, -150, 25);
                offset = Math.Max(-200, Math.Min(-150, offset));

                HitResult result = hitWindows.ResultFor(offset);
                if (result == HitResult.None)
                    result = HitResult.Miss;

                allOffsetsWithResults.Add((offset, result));
            }

            // Now we have 280 hit events, need 70 more to reach 350
            // Fill remaining with random distribution across all segments
            while (allOffsetsWithResults.Count < beatmap.HitObjects.Count)
            {
                int segment = random.Next(4);
                double offset = 0;
                bool isNegative = random.Next(2) == 0; // Randomly choose positive or negative side

                switch (segment)
                {
                    case 0: // [-40, 40]ms
                        offset = GenerateNormalOffset(random, 0, 20);
                        offset = Math.Max(-40, Math.Min(40, offset));
                        break;

                    case 1: // [±40, ±100]ms
                        offset = GenerateNormalOffset(random, isNegative ? -40 : 40, 30);
                        if (isNegative)
                            offset = Math.Max(-100, Math.Min(-40, offset));
                        else
                            offset = Math.Max(40, Math.Min(100, offset));
                        break;

                    case 2: // [±100, ±150]ms
                        offset = GenerateNormalOffset(random, isNegative ? -100 : 100, 25);
                        if (isNegative)
                            offset = Math.Max(-150, Math.Min(-100, offset));
                        else
                            offset = Math.Max(100, Math.Min(150, offset));
                        break;

                    case 3: // [±150, ±200]ms
                        offset = GenerateNormalOffset(random, isNegative ? -150 : 150, 25);
                        if (isNegative)
                            offset = Math.Max(-200, Math.Min(-150, offset));
                        else
                            offset = Math.Max(150, Math.Min(200, offset));
                        break;
                }

                HitResult result = hitWindows.ResultFor(offset);
                if (result == HitResult.None)
                    result = HitResult.Miss;

                allOffsetsWithResults.Add((offset, result));
            }

            // Trim to exact count
            allOffsetsWithResults = allOffsetsWithResults.Take(beatmap.HitObjects.Count).ToList();

            // Shuffle to randomize distribution while maintaining segments
            allOffsetsWithResults = allOffsetsWithResults.OrderBy(_ => random.Next()).ToList();

            // Create HitEvents
            for (int i = 0; i < allOffsetsWithResults.Count && i < beatmap.HitObjects.Count; i++)
            {
                double timeOffset = allOffsetsWithResults[i].offset;
                HitResult result = allOffsetsWithResults[i].result;

                hitEvents.Add(new HitEvent(timeOffset, null, result, beatmap.HitObjects[i], null, null));
            }

            double accuracy = CalculateAccuracy(hitEvents);

            return new ScoreInfo
            {
                BeatmapInfo = beatmap.BeatmapInfo,
                Ruleset = beatmap.BeatmapInfo.Ruleset,
                HitEvents = hitEvents,
                Accuracy = accuracy,
                TotalScore = 424000,
                MaxCombo = hitEvents.Count,
                Mods = Array.Empty<Mod>()
            };
        }

        /// <summary>
        /// Generate a random value from normal distribution using Box-Muller transform.
        /// </summary>
        private double GenerateNormalOffset(Random random, double mean, double stdDev)
        {
            double u1 = random.NextDouble();
            double u2 = random.NextDouble();
            double z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return mean + z0 * stdDev;
        }

        /// <summary>
        /// Calculate accuracy based on hit event results.
        /// </summary>
        private double CalculateAccuracy(List<HitEvent> hitEvents)
        {
            double totalPoints = 0;
            double maxPoints = 0;

            foreach (var hitEvent in hitEvents)
            {
                maxPoints += 305; // Maximum points per note in Mania

                switch (hitEvent.Result)
                {
                    case HitResult.Perfect:
                        totalPoints += 305;
                        break;

                    case HitResult.Great:
                        totalPoints += 300;
                        break;

                    case HitResult.Good:
                        totalPoints += 200;
                        break;

                    case HitResult.Ok:
                        totalPoints += 100;
                        break;

                    case HitResult.Meh:
                        totalPoints += 50;
                        break;

                    default:
                        totalPoints += 0;
                        break;
                }
            }

            return maxPoints > 0 ? totalPoints / maxPoints : 0;
        }

        private ScoreInfo createPerfectScore(IBeatmap beatmap)
        {
            var hitEvents = new List<HitEvent>();

            // Initialize hit windows based on beatmap difficulty
            var hitWindows = new ManiaHitWindows();
            hitWindows.SetDifficulty(beatmap.Difficulty.OverallDifficulty);

            foreach (var hitObject in beatmap.HitObjects)
            {
                // Perfect timing offset (0ms)
                double timeOffset = 0;

                // Calculate the correct hit result based on hit windows at perfect timing
                HitResult result = hitWindows.ResultFor(timeOffset);
                if (result == HitResult.None)
                    result = HitResult.Miss;

                hitEvents.Add(new HitEvent(timeOffset, null, result, hitObject, null, null));
            }

            return new ScoreInfo
            {
                BeatmapInfo = beatmap.BeatmapInfo,
                Ruleset = beatmap.BeatmapInfo.Ruleset,
                HitEvents = hitEvents,
                Accuracy = 1.0,
                TotalScore = 1000000,
                MaxCombo = hitEvents.Count,
                Mods = Array.Empty<Mod>()
            };
        }
    }
}
