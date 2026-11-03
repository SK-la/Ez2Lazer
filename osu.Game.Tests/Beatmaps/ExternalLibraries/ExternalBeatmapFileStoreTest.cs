// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.Beatmaps.ExternalLibraries;

namespace osu.Game.Tests.Beatmaps.ExternalLibraries
{
    [TestFixture]
    public class ExternalBeatmapFileStoreTest
    {
        private string tempRoot = null!;
        private string externalRoot = null!;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "osu-external-beatmap-store-" + Guid.NewGuid());
            externalRoot = Path.Combine(tempRoot, "external");
            Directory.CreateDirectory(externalRoot);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
            catch (IOException)
            {
                // Windows may still hold file handles briefly after stream disposal.
            }
        }

        [Test]
        public void TestResolvesMappedStoragePathFromExternalRootOnly()
        {
            const string storage_path = "abc123/chart.bms";
            const string relative_filename = "folder/chart.bms";
            const string content = "external chart";

            Directory.CreateDirectory(Path.Combine(externalRoot, "folder"));
            File.WriteAllText(Path.Combine(externalRoot, relative_filename), content);

            using var store = new ExternalBeatmapFileStore(externalRoot, new[] { (storage_path, relative_filename) });

            using var stream = store.GetStream(storage_path);

            ClassicAssert.NotNull(stream);
            ClassicAssert.AreEqual(content, new StreamReader(stream!).ReadToEnd());
        }

        [Test]
        public void TestReturnsNullWhenFileMissingFromExternalRoot()
        {
            const string storage_path = "internal-only.txt";

            using var store = new ExternalBeatmapFileStore(externalRoot, new[] { (storage_path, storage_path) });

            ClassicAssert.IsNull(store.GetStream(storage_path));
        }

        [Test]
        public void TestResolvesByRelativeFilenameWhenStoragePathUnmapped()
        {
            const string filename = "audio.wav";
            File.WriteAllText(Path.Combine(externalRoot, filename), "wav");

            using var store = new ExternalBeatmapFileStore(externalRoot, Array.Empty<(string, string)>());

            using var stream = store.GetStream(filename);
            ClassicAssert.NotNull(stream);
        }
    }
}
