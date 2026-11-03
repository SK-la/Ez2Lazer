// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.IO.Serialization;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.Tests.Beatmaps
{
    /// <summary>
    /// <see cref="SliderPath"/> 以位置与类型存储控制点，仅在调用方真正需要时构建
    /// <see cref="PathControlPoint"/>。几何不得因此改变。
    /// </summary>
    [TestFixture]
    public class SliderPathLazyControlPointTest
    {
        [Test]
        public void TestGeometryDoesNotNeedMaterialisation()
        {
            var path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) });

            Assert.That(path.Distance, Is.EqualTo(100));
            Assert.That(path.CalculatedPath, Has.Count.EqualTo(2));
        }

        [Test]
        public void TestMaterialisedGeometryMatches()
        {
            var controlPoints = new[]
            {
                new PathControlPoint(Vector2.Zero, PathType.BEZIER),
                new PathControlPoint(new Vector2(100, 50)),
                new PathControlPoint(new Vector2(200, 0)),
                new PathControlPoint(new Vector2(300, 120)),
            };

            var path = new SliderPath(controlPoints, 250);

            // 枚举会物化对象；结果几何必须不变。
            var materialised = path.ControlPoints.ToArray();

            Assert.That(materialised, Has.Length.EqualTo(controlPoints.Length));

            var rebuilt = new SliderPath(materialised, path.ExpectedDistance.Value);

            Assert.That(rebuilt.Distance, Is.EqualTo(path.Distance));
            Assert.That(rebuilt.CalculatedPath, Is.EqualTo(path.CalculatedPath));
        }

        [Test]
        public void TestInPlaceMutationInvalidates()
        {
            var path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) });

            path.ControlPoints[1].Position = new Vector2(200, 0);
            Assert.That(path.Distance, Is.EqualTo(200));

            path.ControlPoints.Add(new PathControlPoint(new Vector2(300, 0)));
            Assert.That(path.Distance, Is.EqualTo(300));

            path.ControlPoints.RemoveAt(2);
            Assert.That(path.Distance, Is.EqualTo(200));
        }

        [Test]
        public void TestCopyFromDoesNotShareStorage()
        {
            var source = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) }, 75);
            var target = new SliderPath();

            target.CopyFrom(source);

            Assert.That(target.ExpectedDistance.Value, Is.EqualTo(75));
            Assert.That(target.Distance, Is.EqualTo(75));

            source.ControlPoints[1].Position = new Vector2(500, 0);

            Assert.That(target.Distance, Is.EqualTo(75));
        }

        [Test]
        public void TestJsonRoundTrip()
        {
            var path = new SliderPath(PathType.BEZIER, new[] { Vector2.Zero, new Vector2(100, 50), new Vector2(200, 0) }, 150);

            var restored = path.Serialize().Deserialize<SliderPath>();

            Assert.That(restored.ExpectedDistance.Value, Is.EqualTo(path.ExpectedDistance.Value));
            Assert.That(restored.ControlPoints, Has.Count.EqualTo(path.ControlPoints.Count));
            Assert.That(restored.Distance, Is.EqualTo(path.Distance));
            Assert.That(restored.CalculatedPath, Is.EqualTo(path.CalculatedPath));
        }
    }
}
