// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Mods
{
    /// <summary>
    /// SR 回归：越界列号 / 空谱面不得让 <see cref="ManiaModNtoMAnother"/> 抛
    /// <see cref="System.ArgumentOutOfRangeException"/>（表现为整张图 "Failed to calculate beatmap difficulty"）。
    /// <para>
    /// 以下用例统一用 CircleSize=7、Key=8 的转谱：源谱面的实际列号可能落在 <c>[0, Key]</c> 之外，
    /// 旧实现直接索引 <c>confirmNull[column]</c> 就会崩。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ManiaModNtoMAnotherTest
    {
        private const int source_keys = 7;
        private const int target_keys = 8;

        [Test]
        public void TestColumnBeyondKeyCountDoesNotThrow()
        {
            var beatmap = createBeatmap();
            beatmap.HitObjects.Add(new Note { StartTime = 1000, Column = target_keys + 4 });

            Assert.DoesNotThrow(() => createMod().ApplyToBeatmap(beatmap));
        }

        [Test]
        public void TestHoldNoteBeyondKeyCountDoesNotThrow()
        {
            var beatmap = createBeatmap();
            beatmap.HitObjects.Add(new HoldNote { StartTime = 1000, Duration = 500, Column = target_keys + 4 });

            Assert.DoesNotThrow(() => createMod().ApplyToBeatmap(beatmap));
        }

        [Test]
        public void TestEmptyBeatmapDoesNotThrow()
        {
            Assert.DoesNotThrow(() => createMod().ApplyToBeatmap(createBeatmap()));
        }

        [Test]
        public void TestZeroCircleSizeDoesNotThrow()
        {
            var beatmap = createBeatmap();
            beatmap.Difficulty.CircleSize = 0;
            beatmap.HitObjects.Add(new Note { StartTime = 1000, Column = 0 });

            // Key(8) > 0：转谱本来应当生效，但源列数为 0 时没有任何合法列可映射。
            Assert.DoesNotThrow(() => createMod().ApplyToBeatmap(beatmap));
        }

        [Test]
        public void TestInRangeColumnsStillConvert()
        {
            var beatmap = createBeatmap();

            for (int column = 0; column < source_keys; column++)
                beatmap.HitObjects.Add(new Note { StartTime = 1000 + column * 300, Column = column });

            createMod().ApplyToBeatmap(beatmap);

            Assert.That(beatmap.HitObjects, Is.Not.Empty, "正常谱面必须仍有输出");
            Assert.That(beatmap.HitObjects, Has.All.Matches<ManiaHitObject>(h => h.Column >= 0 && h.Column < target_keys));
        }

        private static ManiaBeatmap createBeatmap()
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(source_keys));
            beatmap.Difficulty.CircleSize = source_keys;
            return beatmap;
        }

        private static ManiaModNtoMAnother createMod() => new ManiaModNtoMAnother
        {
            Key = { Value = target_keys },
            BlankColumn = { Value = 0 },
            Gap = { Value = 0 },
            Clean = { Value = true },
        };
    }
}
