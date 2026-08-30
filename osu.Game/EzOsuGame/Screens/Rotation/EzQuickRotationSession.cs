// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public sealed class EzQuickRotationSession
    {
        private readonly Lock poolLock = new Lock();

        private Task<(IReadOnlyList<BeatmapInfo> Pool, double Baseline)>? poolBuildTask;

        private bool poolApplied;

        public bool IsActive { get; private set; }

        public EzQuickRotationPoolConstraints PoolConstraints { get; private set; } = null!;

        public IReadOnlyList<BeatmapInfo> CachedPool { get; private set; } = Array.Empty<BeatmapInfo>();

        public HashSet<Guid> PlayedBeatmapIds { get; } = new HashSet<Guid>();

        public double BaselineDifficulty { get; private set; }

        public RulesetInfo Ruleset { get; private set; } = null!;

        public IReadOnlyList<Mod> BaseMods { get; private set; } = Array.Empty<Mod>();

        public bool IsPoolReady
        {
            get
            {
                lock (poolLock)
                    return poolApplied;
            }
        }

        public void Begin(BeatmapManager beatmapManager,
                          FilterCriteria filterCriteria,
                          BeatmapInfo firstBeatmap,
                          RulesetInfo ruleset,
                          IReadOnlyList<Mod> baseMods)
        {
            bool crossKeyMode = GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.QuickRotationCrossKeyMode);

            PoolConstraints = EzQuickRotationPoolBuilder.CreateConstraintsFromFilter(filterCriteria, firstBeatmap, crossKeyMode);
            BaselineDifficulty = getProvisionalBaseline(firstBeatmap, ruleset);
            Ruleset = ruleset;
            BaseMods = baseMods.Select(m => m.DeepClone()).ToArray();
            PlayedBeatmapIds.Clear();
            PlayedBeatmapIds.Add(firstBeatmap.ID);
            IsActive = true;

            CachedPool = Array.Empty<BeatmapInfo>();

            lock (poolLock)
                poolApplied = false;

            var capturedFirstBeatmap = firstBeatmap;
            var capturedRuleset = ruleset;
            var capturedMods = BaseMods;

            poolBuildTask = Task.Run(() =>
            {
                var pool = EzQuickRotationPoolBuilder.BuildPool(beatmapManager, PoolConstraints);
                double baseline = EzQuickRotationDifficultyHelper.GetBaselineStarRating(beatmapManager, capturedFirstBeatmap, capturedRuleset, capturedMods);
                return (pool, baseline);
            });
        }

        public void EnsurePoolReady(Action onReady)
        {
            tryApplyPoolResult();

            if (IsPoolReady)
            {
                onReady();
                return;
            }

            if (poolBuildTask == null)
            {
                onReady();
                return;
            }

            poolBuildTask.ContinueWith(_ =>
            {
                tryApplyPoolResult();
                onReady();
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        private void tryApplyPoolResult()
        {
            lock (poolLock)
            {
                if (poolApplied || poolBuildTask is not { IsCompletedSuccessfully: true } task)
                    return;

                (IReadOnlyList<BeatmapInfo> pool, double baseline) = task.GetResultSafely();

                if (!IsActive)
                    return;

                CachedPool = pool;
                BaselineDifficulty = baseline;
                poolApplied = true;
            }
        }

        private static double getProvisionalBaseline(BeatmapInfo beatmap, RulesetInfo ruleset)
        {
            if (EzQuickRotationDifficultyHelper.UsesXxyStarRating(ruleset))
                return beatmap.GetPersistedXxyStarRating() ?? beatmap.StarRating;

            return beatmap.StarRating;
        }

        public void MarkPlayed(BeatmapInfo beatmap) => PlayedBeatmapIds.Add(beatmap.ID);

        public void End()
        {
            IsActive = false;
            poolBuildTask = null;

            lock (poolLock)
                poolApplied = false;
        }

        public static bool IsEnabled => GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.QuickRotationEnabled);

        public static double DifficultyTolerance => GlobalConfigStore.EzConfig.Get<double>(Ez2Setting.QuickRotationDifficultyTolerance);

        public static int CandidateCount => GlobalConfigStore.EzConfig.Get<int>(Ez2Setting.QuickRotationCandidateCount);
    }
}
