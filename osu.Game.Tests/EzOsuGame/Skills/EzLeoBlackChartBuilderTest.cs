// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills.LeoBlack;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    /// <summary>
    /// Hold bodies between rows are filled by a per-column sweep; they must still mark exactly the rows a long note
    /// covers, and must not overwrite a head or tail that another note put on the same cell.
    /// </summary>
    [TestFixture]
    public class EzLeoBlackChartBuilderTest
    {
        [Test]
        public void HoldBodiesFillOnlyCoveredRows()
        {
            var input = new EzManiaChartInput
            {
                KeyCount = 4,
                Bpm = 120,
                TotalLengthMs = 2000,
                Notes = new List<EzManiaNote>
                {
                    new EzManiaNote(0, 0, true, 1000),
                    new EzManiaNote(250, 1, false, 250),
                    new EzManiaNote(500, 0, false, 500),
                    new EzManiaNote(500, 2, false, 500),
                    new EzManiaNote(1000, 0, false, 1000),
                },
                TimingPoints = new[] { new EzManiaTimingPoint(0, 500) },
            };

            var chart = EzLeoBlackChartBuilder.TryBuild(input);

            Assert.That(chart, Is.Not.Null);
            Assert.That(chart!.Keys, Is.EqualTo(4));
            Assert.That(chart.Notes.Count, Is.EqualTo(4));

            AssertCell(chart, 0, 0, EzLeoBlackNoteType.HOLDHEAD);
            AssertCell(chart, 250, 1, EzLeoBlackNoteType.NORMAL);
            AssertCell(chart, 250, 0, EzLeoBlackNoteType.HOLDBODY);
            AssertCell(chart, 500, 0, EzLeoBlackNoteType.NORMAL);
            AssertCell(chart, 500, 2, EzLeoBlackNoteType.NORMAL);
            AssertCell(chart, 1000, 0, EzLeoBlackNoteType.HOLDTAIL);
        }

        [Test]
        public void OverlappingHoldsKeepTheBodyFilled()
        {
            var input = new EzManiaChartInput
            {
                KeyCount = 4,
                Bpm = 120,
                TotalLengthMs = 2000,
                Notes = new List<EzManiaNote>
                {
                    new EzManiaNote(0, 0, true, 900),
                    new EzManiaNote(200, 0, true, 700),
                    new EzManiaNote(400, 1, false, 400),
                    new EzManiaNote(900, 0, false, 900),
                    new EzManiaNote(1000, 2, false, 1000),
                },
                TimingPoints = new[] { new EzManiaTimingPoint(0, 500) },
            };

            var chart = EzLeoBlackChartBuilder.TryBuild(input);

            Assert.That(chart, Is.Not.Null);
            AssertCell(chart!, 400, 0, EzLeoBlackNoteType.HOLDBODY);
            AssertCell(chart!, 900, 0, EzLeoBlackNoteType.HOLDTAIL);
        }

        private static void AssertCell(EzLeoBlackChart chart, double time, int column, int expected)
        {
            for (int i = 0; i < chart.Notes.Count; i++)
            {
                if (chart.Notes[i].Time != time)
                    continue;

                TestContext.WriteLine($"row {time} column {column} = {chart.Notes[i].Data[column]}");
                Assert.That(chart.Notes[i].Data[column], Is.EqualTo(expected), $"cell ({time}, {column})");
                return;
            }

            Assert.Fail($"row {time} not present");
        }
    }
}
