// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mania.Edit.Extension;

namespace osu.Game.Rulesets.Mania.Tests.Editor
{
    /// <summary>
    /// Locks the dock's undo block to a self-contained round trip, so an editor snapshot can carry it without touching the
    /// beatmap side of the state.
    /// </summary>
    [TestFixture]
    public class TestExtensionColumnSetStateContributor
    {
        [Test]
        public void TestRestoreRebuildsColumnsAndNotes()
        {
            var original = new ExtensionColumnSet();
            original.Append();
            original.Append();
            original.Append();

            original.AddNote(original.Columns[0], 1000);
            original.AddNote(original.Columns[2], 2000, 500);

            var state = write(original);

            var restored = new ExtensionColumnSet();
            new ExtensionColumnSetStateContributor(restored).Restore(state, state);

            Assert.That(restored.Columns.Count, Is.EqualTo(3));
            Assert.That(restored.Columns[0].Notes.Single().Time, Is.EqualTo(1000));
            Assert.That(restored.Columns[1].Notes, Is.Empty);

            var hold = restored.Columns[2].Notes.Single();
            Assert.That(hold.Time, Is.EqualTo(2000));
            Assert.That(hold.Duration, Is.EqualTo(500));
        }

        [Test]
        public void TestRestoreReplacesCurrentState()
        {
            var source = new ExtensionColumnSet();
            source.Append();
            source.AddNote(source.Columns[0], 4000);

            var state = write(source);

            // The target starts from a different shape, so restoring has to drop columns and notes rather than append.
            var target = new ExtensionColumnSet();
            target.Append();
            target.Append();
            target.AddNote(target.Columns[0], 1000);
            target.AddNote(target.Columns[1], 2000, 100);

            new ExtensionColumnSetStateContributor(target).Restore(state, state);

            Assert.That(target.Columns.Count, Is.EqualTo(1));
            Assert.That(target.Columns[0].Notes.Single().Time, Is.EqualTo(4000));
            Assert.That(target.Columns[0].Notes.Single().Duration, Is.EqualTo(0));
        }

        private static byte[] write(ExtensionColumnSet set)
        {
            using (var stream = new MemoryStream())
            {
                new ExtensionColumnSetStateContributor(set).Write(stream);
                return stream.ToArray();
            }
        }
    }
}
