// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Tests.Beatmaps
{
    public class BeatmapExtensionsTest
    {
        [Test]
        public void TestLengthCalculations()
        {
            var beatmap = new Beatmap
            {
                HitObjects =
                {
                    new HitCircle { StartTime = 5_000 },
                    new HitCircle { StartTime = 300_000 },
                    new Spinner { StartTime = 280_000, Duration = 40_000 }
                },
                Breaks =
                {
                    new BreakPeriod(50_000, 75_000),
                    new BreakPeriod(100_000, 150_000),
                }
            };

            Assert.That(beatmap.CalculatePlayableBounds(), Is.EqualTo((5_000, 320_000)));
            Assert.That(beatmap.CalculatePlayableLength(), Is.EqualTo(315_000)); // 320_000 - 5_000
            Assert.That(beatmap.CalculateDrainLength(), Is.EqualTo(240_000)); // 315_000 - (25_000 + 50_000) = 315_000 - 75_000
        }

        [Test]
        public void TestDrainLengthCannotGoNegative()
        {
            var beatmap = new Beatmap
            {
                HitObjects =
                {
                    new HitCircle { StartTime = 5_000 },
                    new HitCircle { StartTime = 300_000 },
                    new Spinner { StartTime = 280_000, Duration = 40_000 }
                },
                Breaks =
                {
                    new BreakPeriod(0, 350_000),
                }
            };

            Assert.That(beatmap.CalculatePlayableBounds(), Is.EqualTo((5_000, 320_000)));
            Assert.That(beatmap.CalculatePlayableLength(), Is.EqualTo(315_000)); // 320_000 - 5_000
            Assert.That(beatmap.CalculateDrainLength(), Is.EqualTo(0)); // break period encompasses entire beatmap
        }

        [Test]
        public void TestExcludedEndTimeDoesNotExtendBounds()
        {
            var slider = createSlider();
            var beatmap = new Beatmap { HitObjects = { slider } };

            Assert.That(beatmap.CalculatePlayableBounds(), Is.EqualTo((1_000, 1_000 + slider.Duration)));

            slider.ExcludeEndTimeFromPlayableLength = true;

            Assert.That(beatmap.CalculatePlayableBounds(), Is.EqualTo((1_000, 1_000)));
            Assert.That(beatmap.CalculatePlayableLength(), Is.EqualTo(0));
        }

        [Test]
        public void TestEveryEndTimeFallsWithinBounds()
        {
            var excluded = createSlider();
            excluded.ExcludeEndTimeFromPlayableLength = true;

            var beatmap = new Beatmap
            {
                HitObjects =
                {
                    excluded,
                    new HitCircle { StartTime = 5_000 },
                    new HitCircle { StartTime = 300_000 },
                }
            };

            (double start, double end) = beatmap.CalculatePlayableBounds();

            // 若物件报告的结束时间超出边界，按边界排布会冲出远端。进度图正是用该偏移给数组下标。
            Assert.That(beatmap.HitObjects, Has.All.Matches<HitObject>(o => o.GetPlayableEndTime() >= start && o.GetPlayableEndTime() <= end));
        }

        private static Slider createSlider()
        {
            var slider = new Slider
            {
                StartTime = 1_000,
                Path = new SliderPath(PathType.LINEAR, new[]
                {
                    Vector2.Zero,
                    new Vector2(100, 0),
                }, 100),
            };

            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return slider;
        }
    }
}
