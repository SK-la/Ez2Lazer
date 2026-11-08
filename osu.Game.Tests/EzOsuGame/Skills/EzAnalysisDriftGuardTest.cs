// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    /// <summary>
    /// Guards the numbers, not the structure: sharing a conversion or an extraction must not move any published
    /// value by more than <see cref="tolerance"/>. Radar axes round to two decimals, so they are compared exactly.
    /// </summary>
    [TestFixture]
    public class EzAnalysisDriftGuardTest
    {
        /// <summary>Largest accepted drift for a continuous value (star, Mina axis, dan).</summary>
        private const double tolerance = 0.002;

        [TearDown]
        public void TearDown() => EzMinaEnginePool.Release();

        /// <summary>
        /// The PPP stage reads the shared converted beatmap. It must see exactly what its own conversion produced.
        /// </summary>
        [Test]
        public void SharedConversionMatchesDirectConversionForPpp()
        {
            ManiaBeatmap beatmap = createChart(4);
            var working = new TestWorkingBeatmap(beatmap);
            var engine = new EzPPPEngine();

            var shared = EzPlayableBeatmapCache.GetShared(working, beatmap.BeatmapInfo.Ruleset, Array.Empty<Mod>());
            var direct = working.GetPlayableBeatmap(beatmap.BeatmapInfo.Ruleset, Array.Empty<Mod>());

            var sharedSkills = engine.CalculateChart(shared, Array.Empty<Mod>());
            var directSkills = engine.CalculateChart(direct, Array.Empty<Mod>());

            assertSameAxes(sharedSkills, directSkills, "PPP chart skills");
        }

        /// <summary>
        /// The structure pass and the dan derivation are pure functions of the chart input; two runs must agree.
        /// </summary>
        [Test]
        public void StructureAndDanAreDeterministic()
        {
            ManiaBeatmap beatmap = createChart(4);
            var input = EzCSIEngine.FromPlayable(beatmap);

            var firstInfo = EzCSIEngine.Compute(input);
            var secondInfo = EzCSIEngine.Compute(input);

            Assert.That(secondInfo.TechScore, Is.EqualTo(firstInfo.TechScore).Within(tolerance));
            Assert.That(secondInfo.ChordjackScore, Is.EqualTo(firstInfo.ChordjackScore).Within(tolerance));
            Assert.That(secondInfo.LnRatio, Is.EqualTo(firstInfo.LnRatio).Within(tolerance));
            Assert.That(secondInfo.Patterns, Is.EqualTo(firstInfo.Patterns));

            var msd = computeMsd(input, 4);
            int holdCount = countHolds(input);

            var firstSet = EzDanSet.Create(msd, 4, 0.2, 12.5, firstInfo, holdCount);
            var secondSet = EzDanSet.Create(msd, 4, 0.2, 12.5, secondInfo, holdCount);

            Assert.That(firstSet, Is.Not.Null);
            Assert.That(secondSet, Is.Not.Null);

            foreach (var side in EzDanSideExtensions.All)
            {
                var first = firstSet!.For(side);
                var second = secondSet!.For(side);

                Assert.That(second?.RawDan ?? -1, Is.EqualTo(first?.RawDan ?? -1).Within(tolerance), $"dan side {side}");
            }
        }

        /// <summary>
        /// The structure input the dan row reads must be stable across passes; two runs may not disagree.
        /// </summary>
        [Test]
        public void StructureInputIsStableAcrossPasses()
        {
            ManiaBeatmap beatmap = createChart(4);
            var input = EzCSIEngine.FromPlayable(beatmap);

            var first = EzCSIEngine.Compute(input);
            var second = EzCSIEngine.Compute(input);

            Assert.That(second.TechScore, Is.EqualTo(first.TechScore).Within(tolerance));
            Assert.That(second.JackShare ?? 0, Is.EqualTo(first.JackShare ?? 0).Within(tolerance));
            Assert.That(second.Patterns, Is.EqualTo(first.Patterns));
        }

        private static void assertSameAxes(IReadOnlyDictionary<string, double> expected, IReadOnlyDictionary<string, double> actual, string label)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count), $"{label} axis count");

            foreach (var (id, value) in expected)
            {
                Assert.That(actual.TryGetValue(id, out double other), Is.True, $"{label} missing axis {id}");
                Assert.That(Math.Abs(other - value), Is.LessThanOrEqualTo(tolerance), $"{label} axis {id}");
            }
        }

        private static IReadOnlyDictionary<string, double> computeMsd(EzManiaChartInput chart, int keyCount)
        {
            var vector = EzMinaEnginePool.Rent().CalculateMsd(EzMinaNoteConverter.Convert(chart.Notes), keyCount, 1f);
            var msd = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var (axis, value) in vector.Enumerate())
                msd[axis.ToMsdSkillId()] = value;

            return msd;
        }

        private static int countHolds(EzManiaChartInput chart)
        {
            int holds = 0;

            for (int i = 0; i < chart.Notes.Count; i++)
            {
                if (chart.Notes[i].IsHold)
                    holds++;
            }

            return holds;
        }

        private static ManiaBeatmap createChart(int keyCount)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(keyCount))
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Metadata = new BeatmapMetadata { Title = "drift", Artist = "drift" },
                    Ruleset = new ManiaRuleset().RulesetInfo,
                    Difficulty = new BeatmapDifficulty { CircleSize = keyCount, OverallDifficulty = 8 },
                },
                Difficulty = { OverallDifficulty = 8 },
            };

            int seed = 987;

            for (int row = 0; row < 400; row++)
            {
                seed = seed * 1103515245 + 12345;
                int size = 1 + Math.Abs(seed >> 16) % Math.Min(3, keyCount);
                double time = 500 + row * 125.0;

                for (int note = 0; note < size; note++)
                {
                    seed = seed * 1103515245 + 12345;
                    int column = Math.Abs(seed >> 16) % keyCount;

                    if ((seed & 3) == 0)
                    {
                        beatmap.HitObjects.Add(new HoldNote
                        {
                            StartTime = time,
                            Column = column,
                            Duration = 150 + Math.Abs(seed >> 8) % 400,
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
