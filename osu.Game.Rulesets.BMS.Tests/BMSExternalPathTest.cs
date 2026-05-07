// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSExternalPathTest
    {
        [Test]
        public void TestEncodeProducesPrefixedBase64()
        {
            string encoded = BMSExternalPath.Encode("F:/MUG BMS/MUG.MAP.BMS/Some Folder");

            Assert.That(encoded, Does.StartWith(BMSExternalPath.HashPrefix));
        }

        [Test]
        public void TestEncodeDecodeRoundtrip()
        {
            const string folder = "F:/MUG BMS/MUG.MAP.BMS/Some Folder";

            string encoded = BMSExternalPath.Encode(folder);

            Assert.That(BMSExternalPath.TryDecodeRaw(encoded, out string decoded), Is.True);
            Assert.That(decoded, Is.EqualTo(folder));
        }

        [Test]
        public void TestTryDecodeRejectsNullOrEmpty()
        {
            Assert.That(BMSExternalPath.TryDecode(null, out _), Is.False);
            Assert.That(BMSExternalPath.TryDecode(string.Empty, out _), Is.False);
            Assert.That(BMSExternalPath.TryDecodeRaw(null, out _), Is.False);
            Assert.That(BMSExternalPath.TryDecodeRaw(string.Empty, out _), Is.False);
        }

        [Test]
        public void TestTryDecodeRejectsUnrelatedHashes()
        {
            Assert.That(BMSExternalPath.TryDecode("abc123", out _), Is.False);
            Assert.That(BMSExternalPath.TryDecodeRaw("abc123", out _), Is.False);
            Assert.That(BMSExternalPath.TryDecode("bms-other:set:abc", out _), Is.False);
        }

        [Test]
        public void TestTryDecodeHandlesMalformedBase64()
        {
            string malformed = BMSExternalPath.HashPrefix + "@@@@@";

            Assert.That(BMSExternalPath.TryDecode(malformed, out string folderPath), Is.False);
            Assert.That(folderPath, Is.EqualTo(string.Empty));
            Assert.That(BMSExternalPath.TryDecodeRaw(malformed, out folderPath), Is.False);
            Assert.That(folderPath, Is.EqualTo(string.Empty));
        }

        [Test]
        public void TestTryDecodeReturnsTrueOnlyWhenFolderExists()
        {
            string nonExistentFolder = Path.Combine(Path.GetTempPath(), "bms-ext-nonexistent-" + System.Guid.NewGuid().ToString("N"));
            string encoded = BMSExternalPath.Encode(nonExistentFolder);

            Assert.That(BMSExternalPath.TryDecode(encoded, out _), Is.False);
            Assert.That(BMSExternalPath.TryDecodeRaw(encoded, out string decoded), Is.True);
            Assert.That(decoded, Is.EqualTo(nonExistentFolder));

            string existentFolder = Path.Combine(Path.GetTempPath(), "bms-ext-existent-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(existentFolder);

            try
            {
                string existentEncoded = BMSExternalPath.Encode(existentFolder);
                Assert.That(BMSExternalPath.TryDecode(existentEncoded, out string existentDecoded), Is.True);
                Assert.That(existentDecoded, Is.EqualTo(existentFolder));
            }
            finally
            {
                Directory.Delete(existentFolder);
            }
        }
    }
}
