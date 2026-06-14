// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.Tests.Visual.SongSelect
{
    /// <summary>
    /// Full song-select PS/SB mod on/off + beatmap change stress.
    /// Mod settings are never changed.
    /// </summary>
    [TestFixture]
    public partial class TestSceneSongSelectModStress : SongSelectTestScene
    {
        [Resolved]
        private EzAnalysisCache ezAnalysisCache { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        private int loadedEzAnalysisTrackedBindables;
        private int loadedStarDifficultyTrackedBindables;
        private int peakEzAnalysisTrackedBindables;
        private int peakStarDifficultyTrackedBindables;
        private long baselineManagedMemory;
        private int beatmapIndex;

        [Test]
        public void TestSongSelectManiaModToggleAndBeatmapChangeDoesNotGrowCacheBindables()
        {
            prepareSongSelect();

            AddStep("capture loaded baselines", () =>
            {
                ManiaModStressSteps.forceCollectionAndGetManagedMemory();
                loadedEzAnalysisTrackedBindables = ezAnalysisCache.GetTrackedBindableCount();
                loadedStarDifficultyTrackedBindables = difficultyCache.GetTrackedBindableCount();
                peakEzAnalysisTrackedBindables = loadedEzAnalysisTrackedBindables;
                peakStarDifficultyTrackedBindables = loadedStarDifficultyTrackedBindables;
            });

            AddRepeatStep("change beatmap and cycle PS/SB mods", changeBeatmapAndCycleMods, ManiaModStressSteps.default_cycle_count);
            AddWaitStep("wait for analysis to settle", ManiaModStressSteps.analysis_settle_wait_steps);
            AddStep("clear mods", () => SelectedMods.SetDefault());
            AddWaitStep("wait after clearing mods", ManiaModStressSteps.analysis_settle_wait_steps);

            AddAssert("peak ez analysis tracked bindables remain bounded",
                () => peakEzAnalysisTrackedBindables,
                () => Is.LessThanOrEqualTo(loadedEzAnalysisTrackedBindables + ManiaModStressSteps.default_cycle_count * ManiaModStressSteps.song_select_bindable_slack_per_cycle));
            AddAssert("peak star difficulty tracked bindables remain bounded",
                () => peakStarDifficultyTrackedBindables,
                () => Is.LessThanOrEqualTo(loadedStarDifficultyTrackedBindables + ManiaModStressSteps.default_cycle_count * ManiaModStressSteps.song_select_bindable_slack_per_cycle));
        }

        [Test]
        [Explicit]
        public void TestSongSelectManiaModStressMemoryDiagnostics()
        {
            int preLoadEzAnalysisTrackedBindables = 0;
            int preLoadStarDifficultyTrackedBindables = 0;

            AddStep("capture pre-load baselines", () =>
            {
                baselineManagedMemory = ManiaModStressSteps.forceCollectionAndGetManagedMemory();
                preLoadEzAnalysisTrackedBindables = ezAnalysisCache.GetTrackedBindableCount();
                preLoadStarDifficultyTrackedBindables = difficultyCache.GetTrackedBindableCount();
            });

            prepareSongSelect();

            AddRepeatStep("change beatmap and cycle PS/SB mods", changeBeatmapAndCycleMods, ManiaModStressSteps.default_cycle_count * 2);
            AddWaitStep("wait for analysis to settle", ManiaModStressSteps.analysis_settle_wait_steps);
            AddStep("clear mods", () => SelectedMods.SetDefault());
            AddWaitStep("wait after clearing mods", ManiaModStressSteps.analysis_settle_wait_steps);

            AddStep("exit song select", () =>
            {
                while (Stack.CurrentScreen != null)
                    Stack.Exit();
            });

            WaitForSuspension();
            AddStep("collect and log", () =>
            {
                ManiaModStressSteps.forceCollectionAndGetManagedMemory();
                long managed = GC.GetTotalMemory(true);
                Logger.Log(
                    $"SongSelect mod stress | peak ez tracked: {peakEzAnalysisTrackedBindables} (loaded {loadedEzAnalysisTrackedBindables}, pre-load {preLoadEzAnalysisTrackedBindables}) | peak star tracked: {peakStarDifficultyTrackedBindables} (loaded {loadedStarDifficultyTrackedBindables}, pre-load {preLoadStarDifficultyTrackedBindables}) | post-exit ez: {ezAnalysisCache.GetTrackedBindableCount()} | post-exit star: {difficultyCache.GetTrackedBindableCount()} | managed delta: {ManiaModStressSteps.formatMemoryDelta(managed - baselineManagedMemory)}");
            });
        }

        private void prepareSongSelect()
        {
            ImportBeatmapForRuleset(set => { }, 6, 3);
            ImportBeatmapForRuleset(set => { }, 6, 3);
            LoadSongSelect();
            ChangeRuleset(3);
            WaitForFiltering();
            beatmapIndex = 0;
        }

        private void changeBeatmapAndCycleMods()
        {
            selectNextManiaBeatmap();
            ManiaModStressSteps.performFullModCycle(SelectedMods);

            peakEzAnalysisTrackedBindables = Math.Max(peakEzAnalysisTrackedBindables, ezAnalysisCache.GetTrackedBindableCount());
            peakStarDifficultyTrackedBindables = Math.Max(peakStarDifficultyTrackedBindables, difficultyCache.GetTrackedBindableCount());
        }

        private void selectNextManiaBeatmap()
        {
            var beatmaps = Beatmaps.GetAllUsableBeatmapSets()
                                   .SelectMany(s => s.Beatmaps)
                                   .Where(b => b.Ruleset.OnlineID == 3)
                                   .ToList();

            Beatmap.Value = Beatmaps.GetWorkingBeatmap(beatmaps[beatmapIndex++ % beatmaps.Count]);
        }
    }
}
