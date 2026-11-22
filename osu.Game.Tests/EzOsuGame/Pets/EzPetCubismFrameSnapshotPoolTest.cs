// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Pets;

namespace osu.Game.Tests.EzOsuGame.Pets
{
    [TestFixture]
    public class EzPetCubismFrameSnapshotPoolTest
    {
        [Test]
        public void SnapshotIsNotReusedUntilAllReadersReleaseIt()
        {
            var pool = new EzPetCubismFrameSnapshotPool();

            try
            {
                var held = pool.Rent(100, 100);
                held.Acquire();
                held.Release();

                var other = pool.Rent(100, 100);
                Assert.That(other, Is.Not.SameAs(held));

                other.Release();
                held.Release();

                var firstReused = pool.Rent(100, 100);
                var secondReused = pool.Rent(100, 100);

                Assert.That(new[] { firstReused, secondReused }, Does.Contain(held));
                Assert.That(new[] { firstReused, secondReused }, Does.Contain(other));

                firstReused.Release();
                secondReused.Release();
            }
            finally
            {
                pool.Dispose();
            }
        }

        [Test]
        public void ReusedSnapshotRetainsMeshBuffers()
        {
            var pool = new EzPetCubismFrameSnapshotPool();

            try
            {
                var first = pool.Rent(100, 100);
                var part = first.AddPart(0, 1, default, 3, 3);
                var positions = part.Positions;
                var uvs = part.UVs;
                ushort[] indices = part.Indices;
                first.Release();

                var reused = pool.Rent(100, 100);
                var reusedPart = reused.AddPart(0, 1, default, 3, 3);

                Assert.That(reused, Is.SameAs(first));
                Assert.That(reusedPart, Is.SameAs(part));
                Assert.That(reusedPart.Positions, Is.SameAs(positions));
                Assert.That(reusedPart.UVs, Is.SameAs(uvs));
                Assert.That(reusedPart.Indices, Is.SameAs(indices));

                reused.Release();
            }
            finally
            {
                pool.Dispose();
            }
        }
    }
}
