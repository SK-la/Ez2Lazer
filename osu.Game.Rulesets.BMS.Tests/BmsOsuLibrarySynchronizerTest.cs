// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ExternalLibraries;
using osu.Game.Models;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BmsOsuLibrarySynchronizerTest
    {
        private const string content_root = @"E:\bms\song-folder";

        [Test]
        public void TestComputeRelativeChartFilename_uses_subdirectory()
        {
            string chartPath = Path.Combine(content_root, "charts", "chart.bme");
            Assert.That(BMSOsuLibrarySynchronizer.ComputeRelativeChartFilename(chartPath, content_root), Is.EqualTo("charts/chart.bme"));
        }

        [Test]
        public void TestSetMatches_false_when_file_entry_is_bare_filename_only()
        {
            var ruleset = new RulesetInfo { ShortName = "bms", Name = "BMS" };
            Guid beatmapId = Guid.NewGuid();
            string chartPath = Path.Combine(content_root, "charts", "chart.bme");
            var sourceMap = new Dictionary<Guid, BMSSourceReference>
            {
                [beatmapId] = new BMSSourceReference
                {
                    BeatmapId = beatmapId,
                    FolderPath = content_root,
                    ChartPath = chartPath,
                    Md5Hash = BmsPathKeys.ComputeChartPathKey(chartPath),
                },
            };

            var targetSet = buildVirtualSet(ruleset, beatmapId, chartPath);
            var existingSet = buildSyncedSet(ruleset, beatmapId, chartPath, relativeFilename: "chart.bme");

            Assert.That(BMSOsuLibrarySynchronizer.SetMatchesForTesting(existingSet, targetSet, sourceMap), Is.False);
        }

        [Test]
        public void TestSetMatches_true_when_file_mapping_matches_relative_path()
        {
            var ruleset = new RulesetInfo { ShortName = "bms", Name = "BMS" };
            Guid beatmapId = Guid.NewGuid();
            string chartPath = Path.Combine(content_root, "charts", "chart.bme");
            var sourceMap = new Dictionary<Guid, BMSSourceReference>
            {
                [beatmapId] = new BMSSourceReference
                {
                    BeatmapId = beatmapId,
                    FolderPath = content_root,
                    ChartPath = chartPath,
                    Md5Hash = BmsPathKeys.ComputeChartPathKey(chartPath),
                },
            };

            var targetSet = buildVirtualSet(ruleset, beatmapId, chartPath);
            var existingSet = buildSyncedSet(ruleset, beatmapId, chartPath, relativeFilename: "charts/chart.bme");

            Assert.That(BMSOsuLibrarySynchronizer.SetMatchesForTesting(existingSet, targetSet, sourceMap), Is.True);
        }

        [Test]
        public void TestSetMatches_false_when_hash_is_legacy_prefix()
        {
            var ruleset = new RulesetInfo { ShortName = "bms", Name = "BMS" };
            Guid beatmapId = Guid.NewGuid();
            string chartPath = Path.Combine(content_root, "chart.bms");
            var sourceMap = new Dictionary<Guid, BMSSourceReference>
            {
                [beatmapId] = new BMSSourceReference
                {
                    BeatmapId = beatmapId,
                    FolderPath = content_root,
                    ChartPath = chartPath,
                    Md5Hash = BmsPathKeys.ComputeChartPathKey(chartPath),
                },
            };

            var targetSet = buildVirtualSet(ruleset, beatmapId, chartPath);
            var existingSet = buildSyncedSet(ruleset, beatmapId, chartPath, relativeFilename: "chart.bms");
            string legacyEncoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(Path.GetFullPath(content_root)));
            existingSet.Hash = BMSExternalPath.LEGACY_HASH_PREFIX + legacyEncoded;

            Assert.That(BMSOsuLibrarySynchronizer.SetMatchesForTesting(existingSet, targetSet, sourceMap), Is.False);
        }

        private static BeatmapSetInfo buildVirtualSet(RulesetInfo ruleset, Guid beatmapId, string chartPath)
        {
            string realmHash = BmsPathKeys.ComputeRealmFileHash(chartPath);
            string pathKey = BmsPathKeys.ComputeChartPathKey(chartPath);

            var set = new BeatmapSetInfo
            {
                ID = Guid.NewGuid(),
                Hash = content_root,
            };

            set.Beatmaps.Add(new BeatmapInfo(ruleset, new BeatmapDifficulty(), new BeatmapMetadata())
            {
                ID = beatmapId,
                Hash = realmHash,
                MD5Hash = pathKey,
                DifficultyName = "Normal",
                BeatmapSet = set,
            });

            return set;
        }

        private static BeatmapSetInfo buildSyncedSet(RulesetInfo ruleset, Guid beatmapId, string chartPath, string relativeFilename)
        {
            string realmHash = BmsPathKeys.ComputeRealmFileHash(chartPath);
            string pathKey = BmsPathKeys.ComputeChartPathKey(chartPath);

            var set = new BeatmapSetInfo
            {
                ID = Guid.NewGuid(),
                Hash = ExternalBeatmapPathEncoding.Encode(content_root),
                ExternalContentRoot = content_root,
                HostingKind = BeatmapSetHostingKind.External,
            };

            set.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = realmHash }, relativeFilename));

            set.Beatmaps.Add(new BeatmapInfo(ruleset, new BeatmapDifficulty(), new BeatmapMetadata())
            {
                ID = beatmapId,
                Hash = realmHash,
                MD5Hash = pathKey,
                DifficultyName = "Normal",
                BeatmapSet = set,
            });

            return set;
        }
    }
}
