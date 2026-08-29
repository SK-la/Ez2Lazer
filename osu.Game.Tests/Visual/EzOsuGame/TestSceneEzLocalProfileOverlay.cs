// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.Scoring;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Visual.EzOsuGame
{
    public partial class TestSceneEzLocalProfileOverlay : OsuTestScene
    {
        private EzLocalProfileOverlay overlay = null!;
        private TemporaryNativeStorage profileStorage = null!;
        private EzLocalProfileService profileService = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private EzAnalysisPersistentStore analysisStore { get; set; } = null!;

        [Test]
        public void TestFullProfile()
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            profileStorage = new TemporaryNativeStorage($"ez-local-profile-{Guid.NewGuid():N}");

            using (var seedStore = new EzLocalProfileStore(profileStorage))
                seedStore.ReplaceAll(EzLocalProfileOverlayTestData.CreateMockResult());

            profileService = new EzLocalProfileService(profileStorage, realm, analysisStore, beatmapManager);
            overlay = new EzLocalProfileOverlay();

            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(EzLocalProfileService), profileService),
                },
                Child = overlay,
            };

            overlay.Show();
        }

        protected override void Dispose(bool isDisposing)
        {
            profileService?.Dispose();
            profileStorage?.Dispose();
            base.Dispose(isDisposing);
        }
    }

    internal static class EzLocalProfileOverlayTestData
    {
        public static EzLocalProfileAggregationResult CreateMockResult()
        {
            var result = new EzLocalProfileAggregationResult
            {
                IncludedUsernames = new[] { "peppy" },
                ComputedAt = DateTimeOffset.UtcNow,
            };

            result.RulesetStats[EzLocalProfileConstants.OSU_RULESET_ID] = new EzLocalProfileAggregationResult.MutableRulesetStats
            {
                ScoreCount = 42,
                TotalPp = 5123.4,
                TotalKeys = 18_000,
                KpsSampleCount = 40,
                KpsSum = 320,
                MaxKps = 12.5,
                TotalDurationMs = 3_600_000,
            };

            result.GradeCounts[(EzLocalProfileConstants.OSU_RULESET_ID, ScoreRank.S)] = 18;
            result.GradeCounts[(EzLocalProfileConstants.OSU_RULESET_ID, ScoreRank.A)] = 12;
            result.GradeCounts[(EzLocalProfileConstants.OSU_RULESET_ID, ScoreRank.B)] = 8;

            foreach (int bucket in new[] { 3, 4, 5, 6, 7 })
                result.StarPlayCounts[(EzLocalProfileConstants.OSU_RULESET_ID, bucket)] = bucket * 4;

            result.StdAttrAffinities[(EzLocalProfileStdAttr.ApproachRate, 8.0)] = new EzLocalProfileAggregationResult.MutableStdAttr
            {
                PlayCount = 24,
                HighGradeCount = 12,
            };
            result.StdAttrAffinities[(EzLocalProfileStdAttr.CircleSize, 4.0)] = new EzLocalProfileAggregationResult.MutableStdAttr
            {
                PlayCount = 18,
                HighGradeCount = 9,
            };

            result.DrillScores.AddRange(createDrillScores());

            return result;
        }

        private static IEnumerable<EzLocalProfileDrillScoreRow> createDrillScores()
        {
            var beatmapId = Guid.NewGuid();
            const string beatmapHash = "mock-beatmap-hash";

            yield return createDrillRow(beatmapId, beatmapHash, "Visual Test Song With A Long Title", 234.56, withMods: true);
            yield return createDrillRow(beatmapId, beatmapHash, "Second Score On Same Map", 210.0, withMods: false);
            yield return createDrillRow(Guid.NewGuid(), "other-hash", "Another Beatmap", 180.0, withMods: false);
        }

        private static EzLocalProfileDrillScoreRow createDrillRow(Guid beatmapId, string beatmapHash, string title, double pp, bool withMods)
        {
            return new EzLocalProfileDrillScoreRow
            {
                ScoreId = Guid.NewGuid(),
                Username = "peppy",
                RulesetId = EzLocalProfileConstants.OSU_RULESET_ID,
                Rank = ScoreRank.S,
                PpResolved = pp,
                Accuracy = 0.9876,
                MaxCombo = 512,
                MaxAchievableCombo = 512,
                TotalScore = 1_500_000,
                ModsJson = withMods ? "[{\"Acronym\":\"HD\"},{\"Acronym\":\"DT\"}]" : "[]",
                TotalKeys = 1200,
                BeatmapHash = beatmapHash,
                BeatmapId = beatmapId,
                Title = title,
                Artist = "Test Artist",
                DifficultyName = "Insane",
                MapperUsername = "Mapper",
                BeatmapStatus = BeatmapOnlineStatus.Ranked,
                StarRating = 5.42,
                XxyStarRating = -1,
                MapPerformancePoints = 120,
                KpsAvg = 8.75,
                KpsMax = 12.3,
                AvgAbsOffsetMs = 4.5,
                Date = DateTimeOffset.UtcNow,
            };
        }
    }
}
