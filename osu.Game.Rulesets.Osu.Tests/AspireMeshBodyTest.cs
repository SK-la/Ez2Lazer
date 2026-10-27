// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.IO;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class AspireMeshBodyTest
    {
        [Test]
        public void TestTinyBeatLengthStaysBelowTheNormalFloor()
        {
            Slider clamped = loadSlider("0,0,1000,2,0,L|100:0,1,100", "0,0.000001,4,2,0,60,1,0", applyAspire: false);
            Slider restored = loadSlider("0,0,1000,2,0,L|100:0,1,100", "0,0.000001,4,2,0,60,1,0", applyAspire: true);

            Assert.That(clamped.Velocity, Is.EqualTo(100 * 1.4 / 0.6).Within(0.01));
            Assert.That(restored.Velocity, Is.EqualTo(100 * 1.4 / 0.000001).Within(1));
        }

        [Test]
        public void TestGreenLineVelocityIsUnchangedWithoutAspire()
        {
            // 1.5x 对应拍长 -100/1.5。解码器必须存该比值；10–1000 钳制稍后发生。
            // 此处预钳制会改变每条带绿线的 Lazer 判定滑条。
            Slider plain = loadSlider("0,0,1000,2,0,L|100:0,1,100", "0,500,4,2,0,60,1,0", applyAspire: false);
            Slider sped = loadSlider("0,0,1000,2,0,L|100:0,1,100", "0,500,4,2,0,60,1,0\n0,-66.66666666666667,4,2,0,60,0,0", applyAspire: false);

            Assert.That(sped.SliderVelocityMultiplier, Is.EqualTo(1.5).Within(0.001));
            Assert.That(sped.Velocity, Is.EqualTo(plain.Velocity * 1.5).Within(0.001));
        }

        [Test]
        public void TestNanInheritedTimingDoesNotReplaceBeatLength()
        {
            // stable 仅在继承值为负时相乘。NaN 不满足，因此红线仍生效，NaN 点只关闭 tick 生成。
            Slider slider = loadSlider("0,0,1000,2,0,L|100:0,1,100", "0,400,4,2,0,60,1,0\n0,NaN,4,2,0,60,0,0");

            Assert.That(slider.SliderVelocityMultiplier, Is.EqualTo(1));
            Assert.That(slider.GenerateTicks, Is.False);
            Assert.That(double.IsNaN(slider.Velocity), Is.False);
            Assert.That(slider.Velocity, Is.EqualTo(100 * 1.4 / 400).Within(0.0001));
        }

        [Test]
        public void TestDuplicateAnchorBezierIsASpike()
        {
            // 与 stable 保留的接合相同：重复锚点开始下一段二次曲线，离开再回来。
            Slider slider = loadSlider("256,192,65974,2,0,B|256:180|256:192|256:192|256:180|256:192|256:192,1,1000");
            var path = slider.Path.CalculatedPath;

            Assert.That(path, Is.Not.Empty);
            Assert.That(path.Min(p => p.Y), Is.LessThan(-3f));
            Assert.That(path[0].Y, Is.EqualTo(0).Within(0.5f));
            Assert.That(path[^1].Y, Is.EqualTo(0).Within(1f));
        }

        [Test]
        public void TestOutOfRangeEndTimeIsAlreadyOver()
        {
            Slider slider = loadSlider("0,0,66175,2,0,L|1000000000:0,1,2000000000");

            Assert.That(slider.StableRevealEndTime, Is.EqualTo(int.MinValue));
            Assert.That(slider.RevealEndTimeWrapsUnderStableRules, Is.True);
            Assert.That(slider.EndTime, Is.GreaterThan(slider.StartTime));
        }

        [Test]
        public void TestRasterKeepsALongSpikeAndDropsAShortOne()
        {
            var longSpike = new SliderPath(PathType.BEZIER, new[] { Vector2.Zero, new Vector2(0, 80), Vector2.Zero });
            var longRaster = new List<Vector2>();
            longSpike.GetStableRasterPath(longRaster, 0, 1);

            Assert.That(longRaster, Is.Not.Empty);
            Assert.That(longRaster.Count, Is.LessThan(longSpike.CalculatedPath.Count));
            Assert.That(longRaster.Max(p => p.Y), Is.GreaterThan(6f));

            var shortSpike = new SliderPath(PathType.BEZIER, new[] { Vector2.Zero, new Vector2(0, 4), Vector2.Zero });
            var shortRaster = new List<Vector2>();
            shortSpike.GetStableRasterPath(shortRaster, 0, 1);

            Assert.That(shortRaster.Max(p => Math.Abs(p.Y)), Is.LessThan(1f));
        }

        [Test]
        public void TestRasterAfterShorteningStaysOnTheRemainingPath()
        {
            var path = new SliderPath(PathType.BEZIER, new[] { Vector2.Zero, new Vector2(200, 0), new Vector2(200, 200), Vector2.Zero }, 50);
            var raster = new List<Vector2>();

            path.GetStableRasterPath(raster, 0, 1);

            Assert.That(path.Distance, Is.EqualTo(50).Within(0.1));
            Assert.That(raster, Is.Not.Empty);
            Assert.That(raster.Max(p => p.X), Is.LessThan(60f));
        }

        [Test]
        public void TestStraightSegmentIsNotJoinedAway()
        {
            var path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(10, 0) });
            var raster = new List<Vector2>();

            path.GetStableRasterPath(raster, 0, 1);

            Assert.That(raster[^1].X, Is.EqualTo(10).Within(0.01f));
        }

        [Test]
        public void TestFmSynthesisMeshes()
        {
            string? path = findArtifact();

            if (path == null)
                Assert.Ignore("FM Synthesis Experiment is not in the workspace.");

            OsuBeatmap beatmap = loadFile(path);
            Slider first = beatmap.HitObjects.OfType<Slider>().Single(s => Math.Abs(s.StartTime - 65974) < 1);
            Slider second = beatmap.HitObjects.OfType<Slider>().Single(s => Math.Abs(s.StartTime - 69175) < 1);
            Slider linear = beatmap.HitObjects.OfType<Slider>().Single(s => Math.Abs(s.StartTime - 66175) < 1);

            Assert.That(first.Path.CalculatedDistance, Is.EqualTo(first.Path.ExpectedDistance.Value!.Value).Within(first.Path.CalculatedDistance * 0.05));
            Assert.That(second.Path.CalculatedDistance, Is.EqualTo(second.Path.ExpectedDistance.Value!.Value).Within(second.Path.CalculatedDistance * 0.05));
            Assert.That(first.Duration, Is.GreaterThan(4000));
            Assert.That(second.Duration, Is.GreaterThan(2000));
            Assert.That(first.Path.CalculatedPath.Count, Is.GreaterThan(1000));
            // 此处红线约 1e-6 ms。还原后这条 2e11 路径大约持续一秒，而非拍长被地板到 0.6 时的数天。
            // 边界仍不能成为视口。
            Vector2 linearSize = bounds(linear.Path.CalculatedPath);

            Assert.That(linear.Duration, Is.GreaterThan(0).And.LessThan(5000));
            Assert.That(linear.RevealEndTimeWrapsUnderStableRules, Is.False);
            Assert.That(StableViewportBody.IsRejected(linearSize, pixelsPerUnit: 1f), Is.True);

            var raster = new List<Vector2>();
            first.Path.GetStableRasterPath(raster, 0, 1, pixelsPerUnit: 2);

            Assert.That(raster.Count, Is.GreaterThan(1));
            Assert.That(raster.Count, Is.LessThan(first.Path.CalculatedPath.Count));

            // 覆盖网格同为 12 单位线段。必须挺过显示像素接合并装进窗口，
            // 否则身会在出现后的下一帧被清空。
            var secondRaster = new List<Vector2>();
            second.Path.GetStableRasterPath(secondRaster, 0, 1, pixelsPerUnit: 2);
            Vector2 secondSize = bounds(second.Path.CalculatedPath);
            Vector2 secondRasterSize = bounds(secondRaster);

            Assert.That(second.RevealEndTimeWrapsUnderStableRules, Is.False);
            Assert.That(StableViewportBody.IsRejected(secondSize, pixelsPerUnit: 2f), Is.False);
            Assert.That(secondRasterSize.X, Is.GreaterThan(secondSize.X * 0.5f));
            Assert.That(secondRasterSize.Y, Is.GreaterThan(secondSize.Y * 0.5f));
        }

        [Test]
        public void TestFastRepeatCheckpointIsSampledOnTheCurve()
        {
            OsuBeatmap beatmap = load("256,192,1000,2,0,L|200:0,20,200", "0,8,4,2,0,60,1,0");
            Slider slider = (Slider)beatmap.HitObjects[0];
            OsuHitObject checkpoint = slider.NestedHitObjects.OfType<OsuHitObject>().First(nested => nested is SliderRepeat or SliderTick);
            var replay = new OsuAspireAutoGenerator(beatmap, Array.Empty<Mod>()).Generate();
            var nearest = replay.Frames.Cast<OsuReplayFrame>().OrderBy(frame => Math.Abs(frame.Time - checkpoint.StartTime)).First();

            Assert.That(Math.Abs(nearest.Time - checkpoint.StartTime), Is.LessThan(1));
            Assert.That(Vector2.Distance(nearest.Position, checkpoint.Position), Is.LessThan(30));
        }

        [Test]
        public void TestFieldBoundsPastTheViewportAreRejectedAtAnyScale()
        {
            // 测得接近零的缩放不得放行真实游玩区缩放无法视口化的身。
            Assert.That(StableViewportBody.IsRejected(new Vector2(20000, 10), pixelsPerUnit: 0.01f), Is.True);
            Assert.That(StableViewportBody.CanPlaceFieldPosition(new Vector2(30_000_000, 0)), Is.False);
            Assert.That(StableViewportBody.CanPlaceFieldPosition(new Vector2(256, 192)), Is.True);
        }

        [Test]
        public void TestDisplayPixelJoinKeepsASpikeTheFieldUnitJoinDrops()
        {
            // 二次曲线可达距离是控制距的一半。五场地单位在一像素每单位时落在 6px 接合内，
            // 两像素时在外，正是 FieldToDisplay 做的比较。
            var spike = new SliderPath(PathType.BEZIER, new[] { Vector2.Zero, new Vector2(0, 10), Vector2.Zero });
            var coarse = new List<Vector2>();
            var fine = new List<Vector2>();

            spike.GetStableRasterPath(coarse, 0, 1, pixelsPerUnit: 1);
            spike.GetStableRasterPath(fine, 0, 1, pixelsPerUnit: 2);

            Assert.That(fine.Max(p => p.Y), Is.GreaterThan(3f));
            Assert.That(coarse.Max(p => Math.Abs(p.Y)), Is.LessThan(fine.Max(p => p.Y)));
        }

        private static Slider loadSlider(string hitObjectLine, string timingPoints = "0,500,4,2,0,60,1,0", bool applyAspire = true)
            => (Slider)load(hitObjectLine, timingPoints, applyAspire).HitObjects[0];

        private static OsuBeatmap load(string hitObjectLine, string timingPoints = "0,500,4,2,0,60,1,0", bool applyAspire = true)
        {
            string contents = $@"osu file format v14

[General]
Mode: 0

[Metadata]
Title:mesh
Artist:mesh
Creator:mesh
Version:mesh

[Difficulty]
HPDrainRate:5
CircleSize:4
OverallDifficulty:5
ApproachRate:5
SliderMultiplier:1.4
SliderTickRate:1

[TimingPoints]
{timingPoints}

[HitObjects]
{hitObjectLine}
";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(contents));
            using var reader = new LineBufferedReader(stream);
            Beatmap decoded = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
            var converted = (OsuBeatmap)new OsuBeatmapConverter(decoded, new OsuRuleset()).Convert();

            if (applyAspire)
                new OsuModAspire().ApplyToBeatmap(converted);

            foreach (var hitObject in converted.HitObjects)
                hitObject.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);

            return converted;
        }

        private static OsuBeatmap loadFile(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new LineBufferedReader(stream);
            Beatmap decoded = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
            var converted = (OsuBeatmap)new OsuBeatmapConverter(decoded, new OsuRuleset()).Convert();

            new OsuModAspire().ApplyToBeatmap(converted);

            foreach (var hitObject in converted.HitObjects)
                hitObject.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);

            return converted;
        }

        private static Vector2 bounds(IReadOnlyList<Vector2> path)
        {
            Vector2 min = path[0];
            Vector2 max = path[0];

            for (int i = 1; i < path.Count; i++)
            {
                min = Vector2.ComponentMin(min, path[i]);
                max = Vector2.ComponentMax(max, path[i]);
            }

            return max - min;
        }

        private static string? findArtifact()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                DirectoryInfo? dir = new DirectoryInfo(start);

                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, "artifacts", "aspire");

                    if (Directory.Exists(candidate))
                    {
                        string[] files = Directory.GetFiles(candidate, "*.osu", SearchOption.AllDirectories);

                        if (files.Length > 0)
                            return files[0];
                    }

                    dir = dir.Parent;
                }
            }

            return null;
        }
    }
}
