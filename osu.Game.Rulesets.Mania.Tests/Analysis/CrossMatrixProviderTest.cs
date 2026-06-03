// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Analysis;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Tests.Analysis
{
    [TestFixture]
    public class CrossMatrixProviderTest
    {
        [SetUp]
        public void SetUp() => CrossMatrixProvider.ResetStateForTests();

        [TearDown]
        public void TearDown() => CrossMatrixProvider.ResetStateForTests();

        #region 默认矩阵（K=1..18）

        [Test]
        public void GetMatrix_AllSupportedKeyCounts_ReturnValidMatrix()
        {
            for (int keyCount = 1; keyCount <= CrossMatrixProvider.MAX_SUPPORTED_KEY_COUNT; keyCount++)
                assertValidMatrix(keyCount, CrossMatrixProvider.GetMatrix(keyCount));
        }

        [TestCase(1)]
        [TestCase(4)]
        [TestCase(10)]
        [TestCase(12)]
        [TestCase(18)]
        public void GetMatrix_EvenKeys_ReturnsExpectedLength(int keyCount)
        {
            assertValidMatrix(keyCount, CrossMatrixProvider.GetMatrix(keyCount));
        }

        [TestCase(11)]
        [TestCase(13)]
        [TestCase(15)]
        [TestCase(17)]
        public void GetMatrix_OddKeys_ReturnsValidMatrix(int keyCount)
        {
            assertValidMatrix(keyCount, CrossMatrixProvider.GetMatrix(keyCount));
        }

        [TestCase(11)]
        [TestCase(13)]
        [TestCase(15)]
        [TestCase(17)]
        public void InferMatrixFromNeighbors_MatchesGetMatrix(int keyCount)
        {
            double[] inferred = CrossMatrixProvider.InferMatrixFromNeighbors(keyCount);
            double[]? fromProvider = CrossMatrixProvider.GetMatrix(keyCount);

            Assert.That(fromProvider, Is.Not.Null);
            Assert.That(fromProvider, Is.EqualTo(inferred).Within(1e-9));
        }

        [Test]
        public void GetMatrix_CalledTwiceForOddKey_ReturnsCachedInstance()
        {
            double[]? first = CrossMatrixProvider.GetMatrix(11);
            double[]? second = CrossMatrixProvider.GetMatrix(11);

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.SameAs(first));
        }

        #endregion

        #region 边界与不支持键数

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(19)]
        [TestCase(100)]
        public void GetMatrix_OutOfRange_ReturnsNull(int keyCount)
        {
            Assert.That(CrossMatrixProvider.GetMatrix(keyCount), Is.Null);
        }

        [Test]
        public void IsPatternSupported_KeyCountAboveMax_ReturnsFalse()
        {
            const int key_count = CrossMatrixProvider.MAX_SUPPORTED_KEY_COUNT + 1;
            var beatmap = createBeatmap(key_count);

            Assert.That(EzManiaXxyStarRating.IsPatternSupported(beatmap), Is.False);
        }

        [Test]
        public void InferMatrixFromNeighbors_WhenNeighbourInvalid_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => CrossMatrixProvider.InferMatrixFromNeighbors(1));
        }

        #endregion

        #region 自定义矩阵（未来玩家配置 API）

        [Test]
        public void SetCustomMatrix_OverridesDefault_GetMatrixReturnsCustom()
        {
            const int key_count = 7;
            double[] custom = Enumerable.Repeat(0.42, key_count + 1).ToArray();

            CrossMatrixProvider.SetCustomMatrix(key_count, custom);

            Assert.That(CrossMatrixProvider.GetMatrix(key_count), Is.SameAs(custom));
        }

        [Test]
        public void SetCustomMatrix_ClearWithNull_RevertsToDefault()
        {
            const int key_count = 7;
            double[]? defaultMatrix = CrossMatrixProvider.GetMatrix(key_count);
            double[] custom = Enumerable.Repeat(0.99, key_count + 1).ToArray();

            CrossMatrixProvider.SetCustomMatrix(key_count, custom);
            Assert.That(CrossMatrixProvider.GetMatrix(key_count), Is.SameAs(custom));

            CrossMatrixProvider.SetCustomMatrix(key_count, null);

            double[]? restored = CrossMatrixProvider.GetMatrix(key_count);
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored, Is.Not.SameAs(custom));
            Assert.That(restored, Is.EqualTo(defaultMatrix).Within(1e-9));
        }

        [Test]
        public void SetCustomMatrix_OnOddKey_OverridesInferredMatrix()
        {
            const int key_count = 11;
            double[] custom = Enumerable.Repeat(0.33, key_count + 1).ToArray();

            CrossMatrixProvider.SetCustomMatrix(key_count, custom);

            Assert.That(CrossMatrixProvider.GetMatrix(key_count), Is.SameAs(custom));
        }

        [Test]
        public void ValidateMatrix_AcceptsZeroCoefficients()
        {
            double[] matrix = new double[5];
            Assert.DoesNotThrow(() => CrossMatrixProvider.ValidateMatrix(4, matrix));
        }

        [TestCase(0)]
        [TestCase(19)]
        [TestCase(-1)]
        public void SetCustomMatrix_InvalidKeyCount_Throws(int keyCount)
        {
            double[] matrix = new double[Math.Max(keyCount, 1) + 1];
            Assert.Throws<ArgumentOutOfRangeException>(() => CrossMatrixProvider.SetCustomMatrix(keyCount, matrix));
        }

        [Test]
        public void SetCustomMatrix_WrongLength_Throws()
        {
            Assert.Throws<ArgumentException>(() => CrossMatrixProvider.SetCustomMatrix(4, new double[4]));
            Assert.Throws<ArgumentException>(() => CrossMatrixProvider.SetCustomMatrix(4, new double[6]));
        }

        [Test]
        public void SetCustomMatrix_NegativeCoefficient_Throws()
        {
            double[] matrix = new[] { 0.1, -0.01, 0.1, 0.1, 0.1 };
            Assert.Throws<ArgumentOutOfRangeException>(() => CrossMatrixProvider.SetCustomMatrix(4, matrix));
        }

        [Test]
        public void SetCustomMatrix_NaNOrInfinity_Throws()
        {
            double[] nanMatrix = new[] { 0.1, double.NaN, 0.1, 0.1, 0.1 };
            double[] infMatrix = new[] { 0.1, double.PositiveInfinity, 0.1, 0.1, 0.1 };

            Assert.Throws<ArgumentOutOfRangeException>(() => CrossMatrixProvider.SetCustomMatrix(4, nanMatrix));
            Assert.Throws<ArgumentOutOfRangeException>(() => CrossMatrixProvider.SetCustomMatrix(4, infMatrix));
        }

        [Test]
        public void ValidateMatrix_NullMatrix_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CrossMatrixProvider.ValidateMatrix(4, null!));
        }

        [Test]
        public void CalculateSR_UsesCustomMatrix_ChangesResult()
        {
            const int key_count = 4;
            var beatmap = createBeatmap(key_count);
            beatmap.HitObjects.Add(new Note { StartTime = 0, Column = 0 });
            beatmap.HitObjects.Add(new Note { StartTime = 100, Column = 1 });

            double baseline = SRCalculator.CalculateSR(beatmap);

            double[] heavyCross = Enumerable.Repeat(0.9, key_count + 1).ToArray();
            CrossMatrixProvider.SetCustomMatrix(key_count, heavyCross);

            double withCustom = SRCalculator.CalculateSR(beatmap);

            Assert.That(withCustom, Is.Not.EqualTo(baseline).Within(1e-6));
        }

        #endregion

        #region xxy SR 集成

        [Test]
        public void IsPatternSupported_AllSupportedKeyCounts_ReturnsTrue()
        {
            for (int keyCount = 1; keyCount <= CrossMatrixProvider.MAX_SUPPORTED_KEY_COUNT; keyCount++)
                Assert.That(EzManiaXxyStarRating.IsPatternSupported(createBeatmap(keyCount)), Is.True, $"K={keyCount}");
        }

        [TestCase(11)]
        [TestCase(13)]
        [TestCase(15)]
        [TestCase(17)]
        public void CalculateSR_OddKeyBeatmap_DoesNotThrow(int keyCount)
        {
            var beatmap = createBeatmap(keyCount);
            beatmap.HitObjects.Add(new Note { StartTime = 0, Column = 0 });
            beatmap.HitObjects.Add(new Note { StartTime = 150, Column = keyCount - 1 });

            Assert.DoesNotThrow(() => SRCalculator.CalculateSR(beatmap));
            Assert.That(SRCalculator.CalculateSR(beatmap), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void CalculateSR_EmptyBeatmap_ReturnsZero()
        {
            var beatmap = createBeatmap(4);

            Assert.That(SRCalculator.CalculateSR(beatmap), Is.EqualTo(0).Within(1e-6));
        }

        [TestCase(1)]
        [TestCase(18)]
        public void CalculateSR_EmptyBeatmap_AllKeyCounts_ReturnsZero(int keyCount)
        {
            Assert.That(SRCalculator.CalculateSR(createBeatmap(keyCount)), Is.EqualTo(0).Within(1e-6));
        }

        [Test]
        public void CalculateSR_UnsupportedKeyCount_Throws()
        {
            var beatmap = createBeatmap(19);

            Assert.Throws<NotSupportedException>(() => SRCalculator.CalculateSR(beatmap));
        }

        #endregion

        private static void assertValidMatrix(int keyCount, double[]? matrix)
        {
            Assert.That(matrix, Is.Not.Null, $"K={keyCount}");
            Assert.That(matrix!.Length, Is.EqualTo(keyCount + 1));

            foreach (double value in matrix)
            {
                Assert.That(value, Is.GreaterThanOrEqualTo(0));
                Assert.That(double.IsNaN(value), Is.False);
                Assert.That(double.IsInfinity(value), Is.False);
            }
        }

        private static ManiaBeatmap createBeatmap(int keyCount)
        {
            return new ManiaBeatmap(new StageDefinition(keyCount))
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Ruleset = new ManiaRuleset().RulesetInfo,
                    Difficulty = new BeatmapDifficulty
                    {
                        CircleSize = keyCount,
                        OverallDifficulty = 8,
                    },
                },
            };
        }
    }
}
