// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    /// <summary>
    /// Mania 列统计 dense 化（末尾空列补 0 到真实 N）与 scratch 标签 / 显示列数的联动。
    /// </summary>
    [TestFixture]
    public class EzManiaColumnLayoutTest
    {
        private Ez2ConfigManager config = null!;

        [SetUp]
        public void SetUp()
        {
            // GlobalConfigStore.EzConfig 的 getter 在未显式赋值时每次都会新建实例，这里固定一份供测试写入。
            config = GlobalConfigStore.EzConfig;
            GlobalConfigStore.EzConfig = config;
            config.SetValue(Ez2Setting.ManiaSkipEmptyEdgeColumns, false);
        }

        [TearDown]
        public void TearDown()
        {
            config.SetValue(Ez2Setting.ManiaSkipEmptyEdgeColumns, false);
        }

        #region 补列

        [Test]
        public void TestPadPadsTrailingGapOnly()
        {
            var counts = dense(5, 5);
            var holds = dense(2, 0);

            EzManiaColumnLayout.PadToRealColumnCount(counts, holds, 5);

            Assert.That(counts, Is.EqualTo(new Dictionary<int, int> { [0] = 5, [1] = 5, [2] = 0, [3] = 0, [4] = 0 }));
            Assert.That(holds, Is.EqualTo(new Dictionary<int, int> { [0] = 2, [1] = 0, [2] = 0, [3] = 0, [4] = 0 }));
        }

        [Test]
        public void TestPadLeavesEmptyCountsUntouched()
        {
            // 0 note 谱面：保留"无列数据"语义，不补列。
            var counts = new Dictionary<int, int>();
            var holds = new Dictionary<int, int>();

            EzManiaColumnLayout.PadToRealColumnCount(counts, holds, 7);

            Assert.That(counts, Is.Empty);
            Assert.That(holds, Is.Empty);
        }

        [Test]
        public void TestPadDoesNotShrinkOrOverwriteExistingColumns()
        {
            var counts = dense(5, 0, 5, 0);

            EzManiaColumnLayout.PadToRealColumnCount(counts, null, 3);

            // N=3 但已有索引 3：不裁剪、不改写已存在的 0。
            Assert.That(counts, Is.EqualTo(new Dictionary<int, int> { [0] = 5, [1] = 0, [2] = 5, [3] = 0 }));
        }

        #endregion

        #region 显示列数

        [TestCase(14, false, 14)]
        [TestCase(14, true, 13)]
        [TestCase(13, true, 13)]
        [TestCase(7, true, 7)]
        [TestCase(0, true, 0)]
        public void TestDisplayColumnCount(int realColumnCount, bool skipEmptyEdgeColumns, int expected)
        {
            config.SetValue(Ez2Setting.ManiaSkipEmptyEdgeColumns, skipEmptyEdgeColumns);

            Assert.That(EzManiaColumnLayout.GetDisplayColumnCount(realColumnCount), Is.EqualTo(expected));
        }

        [Test]
        public void TestResolveRealColumnCountFallsBackToCircleSize()
        {
            var beatmap = new BeatmapInfo(ruleset: null, difficulty: new BeatmapDifficulty { CircleSize = 14 });

            Assert.That(EzManiaColumnLayout.ResolveRealColumnCount(null, beatmap, null), Is.EqualTo(14));
        }

        #endregion

        #region scratch 标签：尾部空列

        [Test]
        public void TestTrailingEmptyColumnFallsBackToRealKeyCount()
        {
            // dense 后 N = 真实列数；末列为 0 -> 不显著 -> 落回 [Nk]。
            Assert.That(scratch(dense(5, 5, 5, 5, 5, 0), 10), Is.EqualTo("[6k] "));
            Assert.That(scratch(dense(5, 5, 5, 5, 5, 5, 0), 10), Is.EqualTo("[7k] "));
            Assert.That(scratch(dense(5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 0), 10), Is.EqualTo("[12k] "));
            Assert.That(scratch(dense(5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 0), 10), Is.EqualTo("[16k] "));
        }

        [Test]
        public void TestSparseTrailingGapUnderCountsBeforePadding()
        {
            // 反例（旧 sparse 输入）：16k 但末两位空，maxKey+1 只得 14 -> 会被误判为 14k。
            // 说明补列就是修复点本身，而不是靠 UI 层兜底。
            var unpadded = dense(5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5);

            Assert.That(scratch(unpadded, 10), Is.EqualTo("[14k] "));
        }

        [Test]
        public void TestDenseAndSparseAgreeWhenNoTrailingGap()
        {
            // 末列有音符时没有补列缺口，dense 与 sparse 输入结果一致。
            var sparse = dense(5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5);
            var denseCounts = dense(5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5);

            Assert.That(scratch(sparse, 10), Is.EqualTo(scratch(denseCounts, 10)));
            Assert.That(scratch(denseCounts, 10), Is.EqualTo("[16k] "));
        }

        [Test]
        public void TestNoKpsStillUsesRealKeyCount()
        {
            Assert.That(scratch(dense(5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 0), 0), Is.EqualTo("[16k] "));
        }

        [Test]
        public void TestZeroNoteBeatmapHasNoScratch()
        {
            Assert.That(EzBeatmapCalculator.GetScratchFromPrecomputed(null, 10), Is.Null);
            Assert.That(EzBeatmapCalculator.GetScratchFromPrecomputed(new Dictionary<int, int>(), 10), Is.Null);
        }

        #endregion

        #region scratch 标签：13k / 14k

        [Test]
        public void TestThirteenKFollowsSwitch()
        {
            var counts = dense(5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5);

            config.SetValue(Ez2Setting.ManiaSkipEmptyEdgeColumns, false);
            Assert.That(scratch(counts, 10), Is.EqualTo("[13k] "));

            config.SetValue(Ez2Setting.ManiaSkipEmptyEdgeColumns, true);
            Assert.That(scratch(counts, 10), Is.EqualTo("[10k2s1p] "));
        }

        [Test]
        public void TestFourteenKWithoutSwitchIsAlwaysFourteenK()
        {
            config.SetValue(Ez2Setting.ManiaSkipEmptyEdgeColumns, false);

            Assert.That(scratch(dense(5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 0), 10), Is.EqualTo("[14k] "));
            Assert.That(scratch(dense(5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5), 10), Is.EqualTo("[14k] "));
        }

        [Test]
        public void TestFourteenKWithSwitchDependsOnAlphaColumn()
        {
            config.SetValue(Ez2Setting.ManiaSkipEmptyEdgeColumns, true);

            // 第 14 列（索引 13，ez2ac alpha 列）为空 -> 10k2s1p
            Assert.That(scratch(dense(5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 0), 10), Is.EqualTo("[10k2s1p] "));

            // 第 14 列有音符 -> 仍按真实 14k
            Assert.That(scratch(dense(5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5), 10), Is.EqualTo("[14k] "));

            // 真实 14k 且 alpha 列为 0 时，才触达显示列数 13。
            Assert.That(EzManiaColumnLayout.GetDisplayColumnCount(14), Is.EqualTo(13));
        }

        #endregion

        #region scratch 标签：其余固定列数

        [Test]
        public void TestNineKMixedAndSwitch()
        {
            // 一高一低：6k/8k/10k 走单高，7k/9k/16k 按双高；9k 开关开时统一 5k1s2e1p。
            var mixed = dense(30, 5, 5, 5, 5, 5, 5, 5, 1);

            config.SetValue(Ez2Setting.ManiaSkipEmptyEdgeColumns, false);
            Assert.That(scratch(mixed, 10), Is.EqualTo("[7k2s] "));

            config.SetValue(Ez2Setting.ManiaSkipEmptyEdgeColumns, true);
            Assert.That(scratch(mixed, 10), Is.EqualTo("[5k1s2e1p] "));

            // 双低 + 开关开
            Assert.That(scratch(dense(1, 5, 5, 5, 5, 5, 5, 5, 1), 10), Is.EqualTo("[5k1s2e1p] "));

            // 无显著列 -> [9k]
            Assert.That(scratch(dense(5, 5, 5, 5, 5, 5, 5, 5, 5), 10), Is.EqualTo("[9k] "));
        }

        [Test]
        public void TestEighteenKLargeKeysLayout()
        {
            int[] counts = new int[18];
            for (int i = 0; i < counts.Length; i++)
                counts[i] = 5;

            // 无显著 -> [18k]
            Assert.That(scratch(dense(counts), 10), Is.EqualTo("[18k] "));

            // 首列显著 -> 16k2s
            counts[0] = 30;
            Assert.That(scratch(dense(counts), 10), Is.EqualTo("[16k2s] "));
        }

        [Test]
        public void TestSixteenKSwitchLayout()
        {
            config.SetValue(Ez2Setting.ManiaSkipEmptyEdgeColumns, true);

            // 双显著 + 开关开 -> 10k2s4e
            Assert.That(scratch(dense(30, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 30), 10), Is.EqualTo("[10k2s4e] "));

            config.SetValue(Ez2Setting.ManiaSkipEmptyEdgeColumns, false);
            Assert.That(scratch(dense(30, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 30), 10), Is.EqualTo("[14k2s] "));
        }

        #endregion

        private static string? scratch(Dictionary<int, int> counts, double maxKps)
            => EzBeatmapCalculator.GetScratchFromPrecomputed(counts, maxKps);

        private static Dictionary<int, int> dense(params int[] counts)
        {
            var result = new Dictionary<int, int>(counts.Length);

            for (int i = 0; i < counts.Length; i++)
                result[i] = counts[i];

            return result;
        }
    }
}
