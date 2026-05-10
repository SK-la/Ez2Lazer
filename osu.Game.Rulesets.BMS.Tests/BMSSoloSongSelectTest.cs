// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.UI.SongSelect;
using osu.Game.Screens.Select;

namespace osu.Game.Rulesets.BMS.Tests
{
    /// <summary>
    /// Validates the chart-path resolution helper used by <see cref="BMSSoloSongSelect"/> when reconstructing
    /// a <see cref="BMSWorkingBeatmap"/> for an external BMS entry. Also asserts the screen sits on the
    /// standard <see cref="SoloSongSelect"/> stack so it inherits carousel / preview / footer behaviour.
    /// </summary>
    [TestFixture]
    public class BMSSoloSongSelectTest
    {
        [Test]
        public void TestBmsSoloSongSelectInheritsSoloSongSelect()
        {
            Assert.That(typeof(SoloSongSelect).IsAssignableFrom(typeof(BMSSoloSongSelect)),
                "BMSSoloSongSelect must derive from SoloSongSelect to reuse the standard carousel/preview/footer.");
        }

        [Test]
        public void TestResolveExternalChartPath_FailsForNonBmsHash()
        {
            bool resolved = BMSSoloSongSelect.TryResolveExternalChartPath(
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
                bool resolved = BMSSoloSongSelect.TryResolveExternalChartPath(
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
                bool resolved = BMSSoloSongSelect.TryResolveExternalChartPath(
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
                bool resolved = BMSSoloSongSelect.TryResolveExternalChartPath(
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
                    bool resolved = BMSSoloSongSelect.TryResolveExternalChartPath(
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
