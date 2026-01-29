// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Collections.Generic;
using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Mods.LAsMods;
using osu.Game.Audio;

namespace osu.Game.Rulesets.Mania.Tests.Mods
{
    [TestFixture]
    public class ManiaModCleanColumnTest
    {
        [Test]
        public void ExpansionNumericReorder_CopiesSourceColumnsAndUpdatesColumnCount()
        {
            // 构造一个 5k 源谱面，列上的 note 数分别为 1,2,3,4,5（用于简化断言）
            var beatmap = new ManiaBeatmap(new StageDefinition(5));
            beatmap.Difficulty = new BeatmapDifficulty { CircleSize = 5 };

            var hitObjects = new List<ManiaHitObject>();
            int time = 0;
            int[] sourceCounts = { 1, 2, 3, 4, 5 };

            for (int col = 0; col < sourceCounts.Length; col++)
            {
                for (int k = 0; k < sourceCounts[col]; k++)
                {
                    hitObjects.Add(new Note
                    {
                        Column = col,
                        StartTime = time,
                        Samples = new List<HitSampleInfo>()
                    });

                    time += 10;
                }
            }

            beatmap.HitObjects = hitObjects.Cast<ManiaHitObject>().ToList();

            // 应用 mod：规则为 "123123123"（将生成 9 列，按 1,2,3,1,2,3,1,2,3 映射）
            var mod = new ManiaModCleanColumn();
            mod.EnableCustomReorder.Value = true;
            mod.CustomReorderColumn.Value = "12-112233";

            mod.ApplyToBeatmap(beatmap);

            // 验证列数已更新为 9
            Assert.AreEqual(9, beatmap.Difficulty.CircleSize);

            // 计算每列 note 数量并验证
            var counts = Enumerable.Range(0, 9).Select(t => beatmap.HitObjects.Count(h => h.Column == t)).ToArray();
            int[] expected = { 1, 2, 0, 1, 1, 2, 2, 3, 3 };

            Assert.AreEqual(expected.Length, counts.Length);
            CollectionAssert.AreEqual(expected, counts);
        }

        [Test]
        public void ReductionNumericReorder_TruncatesToSpecifiedColumns()
        {
            // 构造一个 9k 源谱面，列上的 note 数分别为 1..9
            var beatmap = new ManiaBeatmap(new StageDefinition(9));
            beatmap.Difficulty = new BeatmapDifficulty { CircleSize = 9 };

            var hitObjects = new List<ManiaHitObject>();
            int time = 0;
            int[] sourceCounts = { 1, 1, 1, 1, 1, 1, 1, 1, 1 };

            for (int col = 0; col < sourceCounts.Length; col++)
            {
                for (int k = 0; k < sourceCounts[col]; k++)
                {
                    hitObjects.Add(new Note
                    {
                        Column = col,
                        StartTime = time,
                        Samples = new List<HitSampleInfo>()
                    });
                    time += 10;
                }
            }

            beatmap.HitObjects = hitObjects.Cast<ManiaHitObject>().ToList();

            // 规则 "12345" 将把前 5 列复制到 5 列谱面
            var mod = new ManiaModCleanColumn();
            mod.EnableCustomReorder.Value = true;
            mod.CustomReorderColumn.Value = "12345";

            mod.ApplyToBeatmap(beatmap);

            Assert.AreEqual(5, beatmap.Difficulty.CircleSize);
            var counts = Enumerable.Range(0, 5).Select(t => beatmap.HitObjects.Count(h => h.Column == t)).ToArray();
            int[] expected = { 1, 1, 1, 1, 1 };
            CollectionAssert.AreEqual(expected, counts);
        }

        [Test]
        public void Operators_HoldAndClear_WorkCorrectly()
        {
            // 源谱面 4 列，每列 1 个 note
            var beatmap = new ManiaBeatmap(new StageDefinition(4));
            beatmap.Difficulty = new BeatmapDifficulty { CircleSize = 4 };

            var hitObjects = new List<ManiaHitObject>();
            int time = 0;
            for (int col = 0; col < 4; col++)
            {
                hitObjects.Add(new Note { Column = col, StartTime = time, Samples = new List<HitSampleInfo>() });
                time += 10;
            }

            beatmap.HitObjects = hitObjects.Cast<ManiaHitObject>().ToList();

            // 规则："12-|" → target0 from src1, target1 from src2, target2 cleared, target3 hold
            var mod = new ManiaModCleanColumn();
            mod.EnableCustomReorder.Value = true;
            mod.CustomReorderColumn.Value = "12-|";

            mod.ApplyToBeatmap(beatmap);

            Assert.AreEqual(4, beatmap.Difficulty.CircleSize);

            // target2 should have 0 notes
            Assert.AreEqual(0, beatmap.HitObjects.Count(h => h.Column == 2));

            // target3 should contain a PunishmentHoldNote
            Assert.IsTrue(beatmap.HitObjects.Any(h => h is PunishmentHoldNote && h.Column == 3));
        }

        [Test]
        public void QuestionMarkOperator_DoesNotThrow_ProducesExpectedColumnCount()
        {
            // 源谱面 3 列，分别有 1、2、3 个 note
            var beatmap = new ManiaBeatmap(new StageDefinition(3));
            beatmap.Difficulty = new BeatmapDifficulty { CircleSize = 3 };

            var hitObjects = new List<ManiaHitObject>();
            int time = 0;
            int[] sourceCounts = { 1, 2, 3 };
            for (int col = 0; col < sourceCounts.Length; col++)
            {
                for (int k = 0; k < sourceCounts[col]; k++)
                {
                    hitObjects.Add(new Note { Column = col, StartTime = time, Samples = new List<HitSampleInfo>() });
                    time += 10;
                }
            }

            beatmap.HitObjects = hitObjects.Cast<ManiaHitObject>().ToList();

            var mod = new ManiaModCleanColumn();
            mod.EnableCustomReorder.Value = true;
            mod.CustomReorderColumn.Value = "1?3?"; // includes two '?' positions

            // should not throw
            Assert.DoesNotThrow(() => mod.ApplyToBeatmap(beatmap));

            // column count should equal rule length
            Assert.AreEqual(4, beatmap.Difficulty.CircleSize);

            // all hit objects columns must be within [0,3]
            Assert.IsTrue(beatmap.HitObjects.All(h => h.Column >= 0 && h.Column < 4));
        }
    }
}
