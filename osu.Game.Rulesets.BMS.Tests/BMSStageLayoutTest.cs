// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Mania;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSStageLayoutTest
    {
        [Test]
        public void TestFromBeatmapBuildsTotalColumnsAndScratchList()
        {
            var beatmap = new Beatmap();
            beatmap.HitObjects.Add(new BMSNote { Column = 1 });
            beatmap.HitObjects.Add(new BMSNote { Column = 7 });
            beatmap.HitObjects.Add(new BMSNote { Column = 0, IsScratch = true });
            beatmap.HitObjects.Add(new BMSNote { Column = 8, IsScratch = true });

            var layout = BMSStageLayout.FromBeatmap(beatmap);

            Assert.That(layout.TotalColumns, Is.EqualTo(9));
            Assert.That(layout.ScratchColumnIndices, Is.EqualTo(new[] { 0, 8 }));
        }

        [Test]
        public void TestActionForMapsScratchAndKeys()
        {
            var layout = new BMSStageLayout(9, new[] { 0, 8 });

            Assert.That(layout.ActionFor(new BMSNote { Column = 0, IsScratch = true }), Is.EqualTo(BMSAction.Scratch1));
            Assert.That(layout.ActionFor(new BMSNote { Column = 8, IsScratch = true }), Is.EqualTo(BMSAction.Scratch2));
            Assert.That(layout.ActionFor(new BMSNote { Column = 1 }), Is.EqualTo(BMSAction.Key1));
            Assert.That(layout.ActionFor(new BMSNote { Column = 7 }), Is.EqualTo(BMSAction.Key7));
        }

        [Test]
        public void TestManiaSkinActionForColumnClamp()
        {
            Assert.That(BMSStageLayout.ManiaSkinActionForColumn(-1), Is.EqualTo(ManiaAction.Key1));
            Assert.That(BMSStageLayout.ManiaSkinActionForColumn(0), Is.EqualTo(ManiaAction.Key1));
            Assert.That(BMSStageLayout.ManiaSkinActionForColumn(5), Is.EqualTo(ManiaAction.Key6));
            Assert.That(BMSStageLayout.ManiaSkinActionForColumn(999), Is.EqualTo(ManiaAction.Key20));
        }
    }
}
