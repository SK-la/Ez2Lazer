// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.Tests
{
    /// <summary>
    /// Validates the chart-path resolution helper used when reconstructing a
    /// <see cref="BMSWorkingBeatmap"/> for an external BMS entry coming from Realm.
    /// (Previously lived on a now-deleted <c>BMSSoloSongSelect</c> wrapper; the helper itself
    /// has moved to <see cref="BMSExternalPath"/>.)
    /// </summary>
    [TestFixture]
    public class BMSSoloSongSelectTest
    {
        [Test]
        public void TestResolveExternalChartPath_FailsForNonBmsHash()
        {
            bool resolved = BMSExternalPath.TryResolveExternalChartPath(
                setHash: "not-a-bms-hash",
                beatmapPath: "foo.bms",
                setFilenames: Array.Empty<string>(),
                out string chartPath);

            Assert.That(resolved, Is.False);
            Assert.That(chartPath, Is.Empty);
        }

        [Test]
        public void TestResolveExternalChartPath_PrefersBeatmapPath()
        {
            string folder = createTempBmsFolder(out string chartFile, "explicit.bms");

            try
            {
                bool resolved = BMSExternalPath.TryResolveExternalChartPath(
                    setHash: BMSExternalPath.Encode(folder),
                    beatmapPath: "explicit.bms",
                    setFilenames: new[] { "wrong.bms" },
                    out string chartPath);

                Assert.That(resolved, Is.True);
                Assert.That(chartPath, Is.EqualTo(chartFile));
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Test]
        public void TestResolveExternalChartPath_FallsBackToFirstChartFile()
        {
            string folder = createTempBmsFolder(out string chartFile, "fallback.bme");

            try
            {
                bool resolved = BMSExternalPath.TryResolveExternalChartPath(
                    setHash: BMSExternalPath.Encode(folder),
                    beatmapPath: null,
                    setFilenames: new List<string> { "irrelevant.txt", "fallback.bme" },
                    out string chartPath);

                Assert.That(resolved, Is.True);
                Assert.That(chartPath, Is.EqualTo(chartFile));
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Test]
        public void TestResolveExternalChartPath_FailsWhenChartMissing()
        {
            string folder = createTempBmsFolder(out _, "exists.bms");

            try
            {
                bool resolved = BMSExternalPath.TryResolveExternalChartPath(
                    setHash: BMSExternalPath.Encode(folder),
                    beatmapPath: "missing.bms",
                    setFilenames: Array.Empty<string>(),
                    out string chartPath);

                Assert.That(resolved, Is.False);
                Assert.That(chartPath, Is.Empty);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Test]
        public void TestResolveExternalChartPath_RecognisesAllChartExtensions()
        {
            foreach (string extension in new[] { ".bms", ".bme", ".bml", ".pms" })
            {
                string filename = "song" + extension;
                string folder = createTempBmsFolder(out string chartFile, filename);

                try
                {
                    bool resolved = BMSExternalPath.TryResolveExternalChartPath(
                        setHash: BMSExternalPath.Encode(folder),
                        beatmapPath: null,
                        setFilenames: new[] { filename },
                        out string chartPath);

                    Assert.That(resolved, Is.True, $"Failed to resolve chart with extension {extension}");
                    Assert.That(chartPath, Is.EqualTo(chartFile));
                }
                finally
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
        }

        private static string createTempBmsFolder(out string chartFile, string chartFilename)
        {
            string folder = Path.Combine(Path.GetTempPath(), "bms-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            chartFile = Path.Combine(folder, chartFilename);
            File.WriteAllText(chartFile, "#TITLE test\n#ARTIST test\n");
            return folder;
        }
    }
}
