// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Mods
{
    /// <summary>
    /// <see cref="ManiaObjectColumnIndex"/> 的判定必须与它替代的全表扫描逐次等价：这是「用索引换掉 O(n²) 扫描」
    /// 唯一的安全依据，随机输入下与
    /// <see cref="ManiaModYuModHelper.FindOverlapInList(List{ManiaHitObject}, int, double, double)"/> 逐题对比。
    /// </summary>
    [TestFixture]
    public class ManiaObjectColumnIndexTest
    {
        private const int column_count = 8;

        [Test]
        public void TestOverlapsMatchesLinearScan()
        {
            var rng = new System.Random(20240923);

            for (int iteration = 0; iteration < 300; iteration++)
            {
                List<ManiaHitObject> list = createRandomList(rng);
                var index = new ManiaObjectColumnIndex(column_count);

                foreach (var hitObject in list)
                    index.Add(hitObject);

                for (int query = 0; query < 40; query++)
                {
                    // 列号故意越界（调用点会在边界判定之前查询），时间也故意取到边界外。
                    int column = rng.Next(-2, column_count + 2);
                    double startTime = rng.Next(0, 8) * 250 + rng.Next(-2, 3);
                    double endTime = rng.Next(0, 8) * 250 + rng.Next(-2, 3);

                    bool expected = ManiaModYuModHelper.FindOverlapInList(list, column, startTime, endTime);
                    bool actual = index.Overlaps(column, startTime);

                    Assert.That(actual, Is.EqualTo(expected),
                                $"iteration {iteration} query {query}: column={column} startTime={startTime} endTime={endTime}");
                }
            }
        }

        /// <summary>
        /// NtoM 第二个循环里的 <c>FindOverlapInList(obj, newColumnObjects.Where(h =&gt; h.Column == c).ToList())</c>
        /// 取值与对象列表无关：判定内部是按 <c>obj.Column</c> 比较的，而列表已按 <c>c</c> 过滤过，
        /// 于是 <c>c != obj.Column</c> 恒为 false、<c>c == obj.Column</c> 恒为 true。
        /// </summary>
        /// <remarks>
        /// 这正是把该调用整段删掉（省掉每轮 keyValue 次「过滤 + 全表扫描 + 临时列表」）的依据，所以把它钉在这里；
        /// 若哪天 helper 改成按查询列判定，这条测试会失败，那时必须重新评估 NtoM 的结果是否改变。
        /// </remarks>
        [Test]
        public void TestFilteredListVariantIsConstantOffTheObjectOwnColumn()
        {
            var rng = new System.Random(2024);

            for (int iteration = 0; iteration < 100; iteration++)
            {
                List<ManiaHitObject> list = createRandomList(rng);

                foreach (var hitObject in list)
                {
                    for (int column = 0; column < column_count; column++)
                    {
                        bool viaFilteredList = ManiaModYuModHelper.FindOverlapInList(hitObject, list.Where(h => h.Column == column).ToList());

                        Assert.That(viaFilteredList, Is.EqualTo(column == hitObject.Column),
                                    $"iteration {iteration}: column={column} obj.Column={hitObject.Column}");
                    }
                }
            }
        }

        [Test]
        public void TestClearResetsToEmptyIndex()
        {
            var rng = new System.Random(5150);
            var index = new ManiaObjectColumnIndex(column_count);
            List<ManiaHitObject> list = createRandomList(rng);

            foreach (var hitObject in list)
                index.Add(hitObject);

            index.Clear();

            foreach (var hitObject in list)
                Assert.That(index.Overlaps(hitObject.Column, hitObject.StartTime), Is.False, "清空后不应再有任何命中");

            // 复用：清空后重建的结果与新建索引一致。
            var reused = index;
            var fresh = new ManiaObjectColumnIndex(column_count);

            foreach (var hitObject in list)
            {
                reused.Add(hitObject);
                fresh.Add(hitObject);
            }

            for (int column = 0; column < column_count; column++)
            {
                for (int step = 0; step < 10; step++)
                {
                    double startTime = rng.Next(0, 8) * 250;

                    Assert.That(reused.Overlaps(column, startTime), Is.EqualTo(fresh.Overlaps(column, startTime)));
                }
            }
        }

        /// <summary>
        /// 随机清单：note / 长条 / 零长与负长长条混在一起，同列同刻还有重复，覆盖判定里所有分支。
        /// </summary>
        private static List<ManiaHitObject> createRandomList(System.Random rng)
        {
            int count = rng.Next(0, 12);
            var list = new List<ManiaHitObject>(count);

            for (int i = 0; i < count; i++)
            {
                double startTime = rng.Next(0, 8) * 250 + rng.Next(-2, 3);
                int column = rng.Next(0, column_count);

                if (rng.Next(2) == 0)
                {
                    list.Add(new Note { StartTime = startTime, Column = column });
                }
                else
                {
                    double duration = rng.Next(-1, 4) * 250;
                    list.Add(new HoldNote { StartTime = startTime, Duration = duration, Column = column });
                }
            }

            return list;
        }
    }
}
