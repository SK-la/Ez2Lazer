// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mania.Edit;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Mania.Tests.Edit
{
    [TestFixture]
    public class ManiaEditorBeatSnapColourIndexTest
    {
        [Test]
        public void TestQuarterStreamUsesDistinctLineDivisors()
        {
            var cpi = new ControlPointInfo();
            cpi.Add(0, new TimingControlPoint { BeatLength = 1000 });

            int beatDivisor = 4;

            int d0 = ManiaEditorBeatSnapColourIndex.GetLineDivisorAtTime(cpi, 0, beatDivisor, fixedGrid: false);
            int d1 = ManiaEditorBeatSnapColourIndex.GetLineDivisorAtTime(cpi, 250, beatDivisor, fixedGrid: false);
            int d2 = ManiaEditorBeatSnapColourIndex.GetLineDivisorAtTime(cpi, 500, beatDivisor, fixedGrid: false);
            int d3 = ManiaEditorBeatSnapColourIndex.GetLineDivisorAtTime(cpi, 750, beatDivisor, fixedGrid: false);

            Assert.That(d0, Is.EqualTo(BindableBeatDivisor.GetDivisorForBeatIndex(0, beatDivisor)));
            Assert.That(d1, Is.EqualTo(BindableBeatDivisor.GetDivisorForBeatIndex(1, beatDivisor)));
            Assert.That(d2, Is.EqualTo(BindableBeatDivisor.GetDivisorForBeatIndex(2, beatDivisor)));
            Assert.That(d3, Is.EqualTo(BindableBeatDivisor.GetDivisorForBeatIndex(3, beatDivisor)));
            Assert.That(new[] { d0, d1, d2, d3 }, Is.Not.All.EqualTo(d0));
        }

        [Test]
        public void TestQuarterPositionsProduceDistinctNoteTints()
        {
            var cpi = new ControlPointInfo();
            cpi.Add(0, new TimingControlPoint { BeatLength = 1000 });

            var beatmap = new osu.Game.Beatmaps.Beatmap { ControlPointInfo = cpi };
            var colours = new OsuColour();

            var c0 = ManiaNoteDisplayTint.GetBeatLineDivisorNoteColour(beatmap, 0, 4, fixedGrid: false, colours);
            var c250 = ManiaNoteDisplayTint.GetBeatLineDivisorNoteColour(beatmap, 250, 4, fixedGrid: false, colours);
            var c500 = ManiaNoteDisplayTint.GetBeatLineDivisorNoteColour(beatmap, 500, 4, fixedGrid: false, colours);

            Assert.That(c250, Is.Not.EqualTo(c0));
            Assert.That(c500, Is.Not.EqualTo(c250));
        }
    }
}
