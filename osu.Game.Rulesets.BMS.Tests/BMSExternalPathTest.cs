// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ExternalLibraries;
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

            Assert.That(encoded, Does.StartWith(ExternalBeatmapPathEncoding.HASH_PREFIX));
        }

        [Test]
        public void TestEncodeDecodeRoundtrip()
        {
            const string folder = "F:/MUG BMS/MUG.MAP.BMS/Some Folder";

            string encoded = BMSExternalPath.Encode(folder);

            Assert.That(BMSExternalPath.TryDecodeRaw(encoded, out string decoded), Is.True);
            Assert.That(decoded, Is.EqualTo(Path.GetFullPath(folder)));
        }

        [Test]
        public void TestTryDecodeLegacyHashRoundtrip()
        {
            const string folder = "F:/legacy-bms/folder";

            string legacyHash = BMSExternalPath.LEGACY_HASH_PREFIX + Convert.ToBase64String(Encoding.UTF8.GetBytes(folder));

            Assert.That(BMSExternalPath.TryDecodeRaw(legacyHash, out string decoded), Is.True);
            Assert.That(decoded, Is.EqualTo(Path.GetFullPath(folder)));
        }

        [Test]
        public void TestTryGetContentRootPrefersExternalContentRootField()
        {
            string folder = Path.Combine(Path.GetTempPath(), "bms-content-root-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                var set = new BeatmapSetInfo
                {
                    Hash = "unrelated-hash",
                    ExternalContentRoot = folder,
                    HostingKind = BeatmapSetHostingKind.External,
                };

                Assert.That(BMSExternalPath.TryGetContentRoot(set, out string resolved), Is.True);
                Assert.That(resolved, Is.EqualTo(folder));
            }
            finally
            {
                Directory.Delete(folder);
            }
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
            string malformed = ExternalBeatmapPathEncoding.HASH_PREFIX + "@@@@@";

            Assert.That(BMSExternalPath.TryDecode(malformed, out string folderPath), Is.False);
            Assert.That(folderPath, Is.EqualTo(string.Empty));
            Assert.That(BMSExternalPath.TryDecodeRaw(malformed, out folderPath), Is.False);
            Assert.That(folderPath, Is.EqualTo(string.Empty));
        }

        [Test]
        public void TestTryDecodeReturnsTrueOnlyWhenFolderExists()
        {
            string nonExistentFolder = Path.Combine(Path.GetTempPath(), "bms-ext-nonexistent-" + Guid.NewGuid().ToString("N"));
            string encoded = BMSExternalPath.Encode(nonExistentFolder);

            Assert.That(BMSExternalPath.TryDecode(encoded, out _), Is.False);
            Assert.That(BMSExternalPath.TryDecodeRaw(encoded, out string decoded), Is.True);
            Assert.That(decoded, Is.EqualTo(nonExistentFolder));

            string existentFolder = Path.Combine(Path.GetTempPath(), "bms-ext-existent-" + Guid.NewGuid().ToString("N"));
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
