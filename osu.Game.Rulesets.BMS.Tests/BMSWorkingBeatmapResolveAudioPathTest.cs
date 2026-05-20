// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSWorkingBeatmapResolveAudioPathTest
    {
        [Test]
        public void TestResolveAudioPathPrefersExplicitRelativePath()
        {
            string tempDir = createTempDir();

            try
            {
                string explicitFile = Path.Combine(tempDir, "sub", "main.ogg");
                Directory.CreateDirectory(Path.GetDirectoryName(explicitFile)!);
                File.WriteAllText(explicitFile, "x");

                string? resolved = resolveAudioPath(tempDir, @"sub/main.ogg");
                Assert.That(resolved, Is.EqualTo(explicitFile));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void TestResolveAudioPathFallsBackToExtensionSearch()
        {
            string tempDir = createTempDir();

            try
            {
                string fallback = Path.Combine(tempDir, "song.mp3");
                File.WriteAllText(fallback, "x");

                string? resolved = resolveAudioPath(tempDir, "song.wav");
                Assert.That(resolved, Is.EqualTo(fallback));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void TestResolveAudioPathUsesTopLevelFileWhenRelativePathMissing()
        {
            string tempDir = createTempDir();

            try
            {
                string topLevel = Path.Combine(tempDir, "aaa.ogg");
                File.WriteAllText(topLevel, "x");

                string? resolved = resolveAudioPath(tempDir, null);
                Assert.That(resolved, Is.EqualTo(topLevel));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        private static string? resolveAudioPath(string folderPath, string? relativePath)
            => (string?)typeof(BMSWorkingBeatmap).GetMethod("ResolveAudioPath", BindingFlags.Static | BindingFlags.NonPublic)!
                                                  .Invoke(null, new object?[] { folderPath, relativePath });

        private static string createTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"bms-audio-path-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
