// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Mods
{
    /// <summary>
    /// <see cref="ManiaModYuModHelper.SelectRandomColumns"/> 必须与它替代的
    /// <c>SelectRandom(Enumerable.Range(0, columns), rng, times)</c> 逐次等价：取到的列号序列相同，
    /// 而且消耗的随机数流位置也相同（同种子下后续抽样才会一致）。
    /// </summary>
    [TestFixture]
    public class ManiaModYuModHelperRandomColumnsTest
    {
        [Test]
        public void TestMatchesOldSamplerAndRngStream()
        {
            var seedRng = new Random(1234567);

            for (int iteration = 0; iteration < 500; iteration++)
            {
                int columns = seedRng.Next(1, 25);
                int times = seedRng.Next(0, columns + 1);
                int seed = seedRng.Next();

                List<int> expected = ManiaModYuModHelper.SelectRandom(Enumerable.Range(0, columns), new Random(seed), times).ToList();

                var destination = new List<int> { -1, -1 }; // 调用前的内容必须被清掉
                var rng = new Random(seed);
                ManiaModYuModHelper.SelectRandomColumns(rng, columns, times, destination);

                Assert.That(destination, Is.EqualTo(expected), $"columns={columns} times={times} seed={seed}");

                // 随机数流位置也必须一致。
                var expectedRng = new Random(seed);

                ManiaModYuModHelper.SelectRandom(Enumerable.Range(0, columns), expectedRng, times).ToList();

                Assert.That(rng.Next(), Is.EqualTo(expectedRng.Next()), $"columns={columns} times={times} seed={seed}：随机数流位置不同");
            }
        }

        [Test]
        public void TestNonPositiveTimesLeavesDestinationEmptyWithoutConsumingRandomness()
        {
            foreach (int times in new[] { 0, -1, -5 })
            {
                var destination = new List<int> { 7 };
                var rng = new Random(42);

                ManiaModYuModHelper.SelectRandomColumns(rng, 8, times, destination);

                Assert.That(destination, Is.Empty);

                // 原实现在 times <= 0 时直接返回空序列，一次 Next 都不调用。
                Assert.That(rng.Next(), Is.EqualTo(new Random(42).Next()), $"times={times} 不应消耗随机数");
            }
        }

        [Test]
        public void TestTooManyTimesThrowsLikeOldSampler()
        {
            // 原实现在池子抽空后会 rng.Next(0) 抛异常，新实现必须同样抛出而不是静默少给几个列号。
            Assert.Throws<ArgumentOutOfRangeException>(() => ManiaModYuModHelper.SelectRandomColumns(new Random(1), 3, 4, new List<int>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => ManiaModYuModHelper.SelectRandom(Enumerable.Range(0, 3), new Random(1), 4).ToList());
        }

        [Test]
        public void TestLargeColumnCountMatchesOldSampler()
        {
            // 超过栈缓冲上限时走堆数组分支，结果同样必须一致。
            var seedRng = new Random(99);

            for (int iteration = 0; iteration < 50; iteration++)
            {
                int columns = seedRng.Next(65, 200);
                int times = seedRng.Next(0, columns + 1);
                int seed = seedRng.Next();

                List<int> expected = ManiaModYuModHelper.SelectRandom(Enumerable.Range(0, columns), new Random(seed), times).ToList();

                var destination = new List<int>();
                ManiaModYuModHelper.SelectRandomColumns(new Random(seed), columns, times, destination);

                Assert.That(destination, Is.EqualTo(expected), $"columns={columns} times={times} seed={seed}");
            }
        }
    }
}
