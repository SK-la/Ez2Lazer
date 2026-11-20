// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.Utils;

namespace osu.Game.Tests.Utils
{
    [TestFixture]
    public class FilesystemSanityCheckHelpersTest
    {
        [TestCase("audio.mp3")]
        [TestCase("sb/bg.jpg")]
        [TestCase("한글곡.mp3")]
        [TestCase("my..song.mp3")]
        public void TestSafeArchiveEntryNameUnchanged(string filename)
        {
            ClassicAssert.AreEqual(filename, FilesystemSanityCheckHelpers.GetSafeArchiveEntryName(filename));
        }

        [Test]
        public void TestSafeArchiveEntryNameStripsRelativeEscape()
        {
            ClassicAssert.AreEqual("tmp/pwned.mp3", FilesystemSanityCheckHelpers.GetSafeArchiveEntryName("../../../../../../tmp/pwned.mp3"));
            ClassicAssert.AreEqual("tmp/pwned.mp3", FilesystemSanityCheckHelpers.GetSafeArchiveEntryName(@"..\..\..\..\..\..\tmp\pwned.mp3"));
        }

        [Test]
        public void TestSafeArchiveEntryNameStripsAbsoluteEscape()
        {
            ClassicAssert.AreEqual("tmp/pwned.mp3", FilesystemSanityCheckHelpers.GetSafeArchiveEntryName("/tmp/pwned.mp3"));
            ClassicAssert.AreEqual("C/tmp/pwned.mp3", FilesystemSanityCheckHelpers.GetSafeArchiveEntryName(@"C:\tmp\pwned.mp3"));
            ClassicAssert.AreEqual("foo", FilesystemSanityCheckHelpers.GetSafeArchiveEntryName("foo/.."));
            ClassicAssert.AreEqual("file", FilesystemSanityCheckHelpers.GetSafeArchiveEntryName(".."));
        }
    }
}
