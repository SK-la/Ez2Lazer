// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Audio;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BmsFolderSampleIndexTest
    {
        [Test]
        public void TestResolvesCaseInsensitiveExtension()
        {
            string folder = Path.Combine(Path.GetTempPath(), "bms-index-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "KICK.WAV"), "x");

                var index = BmsFolderSampleIndex.TryBuild(folder);
                Assert.That(index, Is.Not.Null);
                Assert.That(index!.TryResolveRelativePath("kick"), Is.EqualTo("KICK.WAV"));
                Assert.That(index.TryResolveRelativePath("kick.wav"), Is.EqualTo("KICK.WAV"));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TestMissingLookupIsCached()
        {
            string folder = Path.Combine(Path.GetTempPath(), "bms-index-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(folder);
                var index = BmsFolderSampleIndex.TryBuild(folder);

                Assert.That(index!.TryResolveRelativePath("missing.wav"), Is.Null);
                Assert.That(index.TryResolveRelativePath("missing.wav"), Is.Null);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
