// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mania.Edit.Extension;

namespace osu.Game.Rulesets.Mania.Tests.Editor
{
    /// <summary>
    /// Locks the extension columns' numbering to left-to-right position, independent of insertion history and note times.
    /// </summary>
    [TestFixture]
    public class TestExtensionColumnNumbering
    {
        [Test]
        public void TestAppendNumbersLeftToRight()
        {
            var set = new ExtensionColumnSet();

            set.Append();
            set.Append();
            set.Append();

            Assert.That(numbers(set), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void TestInsertInMiddleRenumbersByPosition()
        {
            var set = new ExtensionColumnSet();

            for (int i = 0; i < 3; i++)
                set.Append();

            var inserted = set.Insert(2);

            Assert.That(inserted, Is.Not.Null);
            Assert.That(numbers(set), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(set.Columns[2], Is.SameAs(inserted));
        }

        [Test]
        public void TestRepeatedInsertsStayContiguous()
        {
            var set = new ExtensionColumnSet();

            for (int i = 0; i < 5; i++)
                set.Append();

            // Every insert to the right of #2 must shift the columns after it, never allocate a fresh number.
            set.Insert(2);
            set.Insert(3);
            set.Insert(2);

            Assert.That(numbers(set), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
        }

        [Test]
        public void TestRemoveRenumbersByPosition()
        {
            var set = new ExtensionColumnSet();

            for (int i = 0; i < 5; i++)
                set.Append();

            set.Remove(set.Columns[1]);

            Assert.That(numbers(set), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void TestNotesDoNotAffectNumbering()
        {
            var set = new ExtensionColumnSet();

            for (int i = 0; i < 3; i++)
                set.Append();

            // Notes added in an order which disagrees with the columns must never influence the column numbers.
            set.AddNote(set.Columns[2], 5000);
            set.AddNote(set.Columns[0], 1000);
            set.AddNote(set.Columns[1], 3000);

            set.Insert(1);

            Assert.That(numbers(set), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(set.Columns[1].Notes, Is.Empty);
            Assert.That(set.Columns[2].Notes.Single().Time, Is.EqualTo(3000));
            Assert.That(set.Columns[0].Notes.Single().Time, Is.EqualTo(1000));
        }

        private static int[] numbers(ExtensionColumnSet set) => set.Columns.Select(c => c.Number.Value).ToArray();
    }
}
