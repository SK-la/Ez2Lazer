// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    /// <summary>
    /// The linear row builder is only used when the source is already in time order; it must produce exactly the
    /// rows the sorted-dictionary path produces, including the negative-time shift and chord aggregation.
    /// </summary>
    [TestFixture]
    public class EzMinaNoteConverterOrderTest
    {
        [Test]
        public void SortedAndUnsortedInputsProduceIdenticalRows()
        {
            var notes = createNotes();

            var sorted = EzMinaNoteConverter.Convert(notes);

            var shuffled = notes.ToArray();
            var random = new Random(7);

            for (int i = shuffled.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            var unsorted = EzMinaNoteConverter.Convert(shuffled);

            Assert.That(unsorted.Length, Is.EqualTo(sorted.Length));

            for (int i = 0; i < sorted.Length; i++)
            {
                Assert.That(unsorted[i].Notes, Is.EqualTo(sorted[i].Notes), $"row {i} mask");
                Assert.That(BitConverter.SingleToInt32Bits(unsorted[i].RowTime), Is.EqualTo(BitConverter.SingleToInt32Bits(sorted[i].RowTime)), $"row {i} time");
            }
        }

        [Test]
        public void NegativeStartShiftsRowsToZero()
        {
            var notes = new List<EzManiaNote>
            {
                new EzManiaNote(-500, 0, false, -500),
                new EzManiaNote(-400, 1, false, -400),
                new EzManiaNote(250, 2, false, 250),
            };

            var rows = EzMinaNoteConverter.Convert(notes);

            Assert.That(rows.Length, Is.EqualTo(3));
            Assert.That(rows[0].RowTime, Is.EqualTo(0f));
            Assert.That(rows[1].RowTime, Is.EqualTo(0.1f));
            Assert.That(rows[2].RowTime, Is.EqualTo(0.75f));
        }

        [Test]
        public void ChordSharesOneRow()
        {
            var notes = new List<EzManiaNote>
            {
                new EzManiaNote(100, 0, false, 100),
                new EzManiaNote(100, 3, false, 100),
                new EzManiaNote(250, 1, false, 250),
            };

            var rows = EzMinaNoteConverter.Convert(notes);

            Assert.That(rows.Length, Is.EqualTo(2));
            Assert.That(rows[0].Notes, Is.EqualTo((1u << 0) | (1u << 3)));
            Assert.That(rows[0].RowTime, Is.EqualTo(0.1f));
        }

        private static List<EzManiaNote> createNotes()
        {
            var notes = new List<EzManiaNote>();
            var random = new Random(17);
            double time = -300;

            for (int row = 0; row < 400; row++)
            {
                int size = 1 + random.Next(4);

                for (int note = 0; note < size; note++)
                {
                    int column = random.Next(4);
                    notes.Add(new EzManiaNote(time, column, note == 0, time));
                }

                time += 25 + random.Next(150);
            }

            return notes;
        }
    }
}
