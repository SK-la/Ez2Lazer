// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public sealed class EzQuickRotationSession
    {
        public bool IsActive { get; private set; }

        public EzQuickRotationPoolConstraints PoolConstraints { get; private set; } = null!;

        public IReadOnlyList<BeatmapInfo> CachedPool { get; private set; } = Array.Empty<BeatmapInfo>();

        public HashSet<Guid> PlayedBeatmapIds { get; } = new HashSet<Guid>();

        public double BaselineDifficulty { get; private set; }

        public RulesetInfo Ruleset { get; private set; } = null!;

        public IReadOnlyList<Mod> BaseMods { get; private set; } = Array.Empty<Mod>();

        public void Begin(BeatmapManager beatmapManager,
                          FilterCriteria filterCriteria,
                          BeatmapInfo firstBeatmap,
                          RulesetInfo ruleset,
                          IReadOnlyList<Mod> baseMods,
                          double baselineDifficulty)
        {
            bool crossKeyMode = GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.QuickRotationCrossKeyMode);

            PoolConstraints = EzQuickRotationPoolBuilder.CreateConstraintsFromFilter(filterCriteria, firstBeatmap, crossKeyMode);
            CachedPool = EzQuickRotationPoolBuilder.BuildPool(beatmapManager, PoolConstraints);
            BaselineDifficulty = baselineDifficulty;
            Ruleset = ruleset;
            BaseMods = baseMods.Select(m => m.DeepClone()).ToArray();
            PlayedBeatmapIds.Clear();
            PlayedBeatmapIds.Add(firstBeatmap.ID);
            IsActive = true;
        }

        public void MarkPlayed(BeatmapInfo beatmap) => PlayedBeatmapIds.Add(beatmap.ID);

        public void End() => IsActive = false;

        public static bool IsEnabled => GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.QuickRotationEnabled);

        public static double DifficultyTolerance => GlobalConfigStore.EzConfig.Get<double>(Ez2Setting.QuickRotationDifficultyTolerance);

        public static int CandidateCount => GlobalConfigStore.EzConfig.Get<int>(Ez2Setting.QuickRotationCandidateCount);
    }
}
