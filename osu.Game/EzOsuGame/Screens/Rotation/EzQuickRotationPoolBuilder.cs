// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public static class EzQuickRotationPoolBuilder
    {
        public static EzQuickRotationPoolConstraints CreateConstraintsFromFilter(FilterCriteria criteria, BeatmapInfo firstBeatmap, bool crossKeyMode)
        {
            int? lockedKeyCount = null;

            if (!crossKeyMode && criteria.Ruleset?.ShortName == "mania")
                lockedKeyCount = (int)firstBeatmap.Difficulty.CircleSize;

            HashSet<string>? collectionHashes = criteria.CollectionBeatmapMD5Hashes?.ToHashSet();

            return new EzQuickRotationPoolConstraints(
                criteria.UserStarDifficulty.Min,
                collectionHashes,
                criteria.Ruleset!,
                lockedKeyCount,
                crossKeyMode);
        }

        public static IReadOnlyList<BeatmapInfo> BuildPool(BeatmapManager beatmapManager, EzQuickRotationPoolConstraints constraints)
        {
            if (constraints.Ruleset == null)
                return Array.Empty<BeatmapInfo>();

            return beatmapManager.GetAllUsableBeatmapSets()
                                 .SelectMany(set => set.Beatmaps)
                                 .Where(b => !b.Hidden)
                                 .Where(b => b.AllowGameplayWithRuleset(constraints.Ruleset, allowConversion: false))
                                 .Where(b => matchesCollection(b, constraints.CollectionMd5Hashes))
                                 .Where(b => matchesStarMin(b, constraints))
                                 .Where(b => matchesKeyCount(b, constraints))
                                 .Distinct()
                                 .ToList();
        }

        public static IReadOnlyList<BeatmapInfo> DrawCandidates(IReadOnlyList<BeatmapInfo> pool, IReadOnlySet<Guid> playedBeatmapIds, int count, Random? random = null)
        {
            random ??= Random.Shared;

            var available = pool.Where(b => !playedBeatmapIds.Contains(b.ID)).ToList();

            if (available.Count == 0)
                return Array.Empty<BeatmapInfo>();

            if (available.Count <= count)
                return available.OrderBy(_ => random.Next()).ToList();

            return available.OrderBy(_ => random.Next()).Take(count).ToList();
        }

        private static bool matchesCollection(BeatmapInfo beatmap, HashSet<string>? collectionMd5Hashes) => collectionMd5Hashes?.Contains(beatmap.MD5Hash) ?? true;

        private static bool matchesStarMin(BeatmapInfo beatmap, EzQuickRotationPoolConstraints constraints)
        {
            if (constraints.StarRatingMin is not double min)
                return true;

            double star = EzQuickRotationDifficultyHelper.UsesXxyStarRating(constraints.Ruleset)
                ? beatmap.GetPersistedXxyStarRating() ?? beatmap.StarRating
                : beatmap.StarRating;

            return star >= min;
        }

        internal static bool matchesKeyCount(BeatmapInfo beatmap, EzQuickRotationPoolConstraints constraints)
        {
            if (constraints.Ruleset.ShortName != "mania")
                return true;

            int keyCount = (int)beatmap.Difficulty.CircleSize;

            if (constraints.CrossKeyMode)
                return keyCount >= EzQuickRotationPoolConstraints.CrossKeyMin && keyCount <= EzQuickRotationPoolConstraints.CrossKeyMax;

            if (constraints.LockedKeyCount is int lockedKeyCount)
                return keyCount == lockedKeyCount;

            return true;
        }
    }
}
