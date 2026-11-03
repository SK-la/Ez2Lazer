// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Lists;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mania;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.SongSelect
{
    /// <summary>
    /// Stress toggling expensive Mania conversion mods (PS/SB) on a song-select panel.
    /// Only exercises mod on/off; mod settings are never changed.
    /// </summary>
    [TestFixture]
    public partial class TestScenePanelBeatmapModStress : ThemeComparisonTestScene
    {
        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private EzAnalysisCache ezAnalysisCache { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        private FillFlowContainer scenarioHost = null!;
        private OsuSpriteText summaryText = null!;

        private List<BeatmapInfo> beatmapsToCycle = new List<BeatmapInfo>();

        private WeakList<PanelBeatmapStandalone> panelWeakReferences = new WeakList<PanelBeatmapStandalone>();
        private WeakList<CarouselItem> panelItemWeakReferences = new WeakList<CarouselItem>();

        private PanelBeatmapStandalone? reusablePanel;
        private int beatmapIndex;
        private long baselineManagedMemory;
        private int baselineEzAnalysisTrackedBindables;
        private int baselineStarDifficultyTrackedBindables;

        public TestScenePanelBeatmapModStress()
            : base(false)
        {
        }

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            populateBeatmaps();
            Ruleset.Value = new ManiaRuleset().RulesetInfo;
            SelectedMods.SetDefault();
            CreateThemedContent(OverlayColourScheme.Aquamarine);
            resetDiagnostics();
        });

        [Test]
        public void TestManiaModToggleCycleDoesNotLeakReferences()
        {
            runModToggleStress(createReusablePanel, performFullModCycle);
        }

        [Test]
        public void TestManiaModToggleWithBeatmapSwapDoesNotLeakReferences()
        {
            runModToggleStress(createReusablePanel, swapBeatmapAndCycleMods);
        }

        [Test]
        public void TestManiaModToggleWithPanelRecreationDoesNotLeakReferences()
        {
            runModToggleStress(resetForRecreation, recreateSelectedPanelWithModCycle);
        }

        [Test]
        [Explicit]
        public void TestManiaModToggleMemoryDiagnostics()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddStep("create reusable panel", createReusablePanel);
            AddRepeatStep("cycle PS/SB mod toggles", performFullModCycle, ManiaModStressSteps.default_cycle_count);
            finishModStressAndSummarise("Mod toggle");
        }

        [Test]
        [Explicit]
        public void TestManiaModToggleWithBeatmapSwapMemoryDiagnostics()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddStep("create reusable panel", createReusablePanel);
            AddRepeatStep("swap beatmap and cycle PS/SB mods", swapBeatmapAndCycleMods, ManiaModStressSteps.default_cycle_count);
            finishModStressAndSummarise("Mod toggle + beatmap swap");
        }

        protected override Drawable CreateContent()
        {
            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 12),
                Padding = new MarginPadding(20),
                Children = new Drawable[]
                {
                    summaryText = new OsuSpriteText
                    {
                        RelativeSizeAxes = Axes.X,
                        Font = OsuFont.Style.Body,
                        Colour = Color4.White,
                        Text = "Run one of the mod toggle stress tests.",
                    },
                    scenarioHost = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 8),
                    }
                }
            };
        }

        private void runModToggleStress(Action setupStep, Action stressStep)
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddStep("setup panel scenario", setupStep);
            AddRepeatStep("cycle PS/SB mod toggles", stressStep, ManiaModStressSteps.default_cycle_count);
            AddWaitStep("wait for analysis to settle", ManiaModStressSteps.analysis_settle_wait_steps);
            AddStep("clear mods", () => SelectedMods.SetDefault());
            AddWaitStep("wait after clearing mods", ManiaModStressSteps.analysis_settle_wait_steps);
            AddStep("clear host", clearScenarioHost);
            AddUntilStep("no leaked panels", () => ManiaModStressSteps.collectAndCount(panelWeakReferences) == 0);
            AddUntilStep("no leaked items", () => ManiaModStressSteps.collectAndCount(panelItemWeakReferences) == 0);
            AddUntilStep("ez analysis tracked bindables return to baseline",
                () => ezAnalysisCache.GetTrackedBindableCount() <= baselineEzAnalysisTrackedBindables);
            AddUntilStep("star difficulty tracked bindables return to baseline",
                () => difficultyCache.GetTrackedBindableCount() <= baselineStarDifficultyTrackedBindables);
        }

        private void finishModStressAndSummarise(string scenario)
        {
            AddWaitStep("wait for analysis to settle", ManiaModStressSteps.analysis_settle_wait_steps);
            AddStep("clear mods", () => SelectedMods.SetDefault());
            AddWaitStep("wait after clearing mods", ManiaModStressSteps.analysis_settle_wait_steps);
            AddStep("clear host", clearScenarioHost);
            AddStep("collect and summarise", () => updateSummary(createModToggleSummary(scenario)));
            AddAssert("mod toggle summary available", () => summaryText.Text.ToString().Length, () => Is.GreaterThan(0));
        }

        private void populateBeatmaps()
        {
            beatmapsToCycle = beatmaps.GetAllUsableBeatmapSets()
                                      .SelectMany(s => s.Beatmaps)
                                      .Where(b => b.Ruleset.OnlineID == 3)
                                      .Take(24)
                                      .ToList();

            if (beatmapsToCycle.Count == 0)
            {
                beatmapsToCycle = TestResources.CreateTestBeatmapSetInfo(difficultyCount: 24, rulesets: new[] { new ManiaRuleset().RulesetInfo })
                                               .Beatmaps
                                               .ToList();
            }

            Assert.That(beatmapsToCycle.Count, Is.GreaterThan(0), "No beatmaps available for mod stress diagnostics.");
        }

        private void resetDiagnostics()
        {
            panelWeakReferences = new WeakList<PanelBeatmapStandalone>();
            panelItemWeakReferences = new WeakList<CarouselItem>();
            reusablePanel = null;
            beatmapIndex = 0;

            clearScenarioHost();
            SelectedMods.SetDefault();
            baselineManagedMemory = ManiaModStressSteps.forceCollectionAndGetManagedMemory();
            baselineEzAnalysisTrackedBindables = ezAnalysisCache.GetTrackedBindableCount();
            baselineStarDifficultyTrackedBindables = difficultyCache.GetTrackedBindableCount();
            updateSummary("Mod stress diagnostics reset. Managed baseline captured.");
        }

        private void resetForRecreation() => clearScenarioHost();

        private void performFullModCycle()
        {
            ManiaModStressSteps.performFullModCycle(SelectedMods);
            updateSummary($"Mod toggle cycle {ManiaModStressSteps.countAlive(panelWeakReferences)} running...");
        }

        private void swapBeatmapAndCycleMods()
        {
            swapReusablePanelBeatmap();
            performFullModCycle();
        }

        private void recreateSelectedPanelWithModCycle()
        {
            clearScenarioHost();

            var panel = new PanelBeatmapStandalone
            {
                Item = createCarouselItem(nextBeatmap()),
            };

            panel.Selected.Value = true;
            panel.KeyboardSelected.Value = true;

            panelWeakReferences.Add(panel);
            panelItemWeakReferences.Add(panel.Item);
            scenarioHost.Add(panel);

            performFullModCycle();
        }

        private void createReusablePanel()
        {
            clearScenarioHost();

            reusablePanel = new PanelBeatmapStandalone();
            panelWeakReferences.Add(reusablePanel);
            scenarioHost.Add(reusablePanel);

            swapReusablePanelBeatmap();
        }

        private void swapReusablePanelBeatmap()
        {
            if (reusablePanel == null)
                createReusablePanel();

            var item = createCarouselItem(nextBeatmap());
            panelItemWeakReferences.Add(item);

            reusablePanel!.Item = item;
            reusablePanel.Selected.Value = true;
            reusablePanel.KeyboardSelected.Value = true;

            updateSummary($"Panel swap iteration {ManiaModStressSteps.countAlive(panelItemWeakReferences)} running...");
        }

        private void clearScenarioHost()
        {
            reusablePanel = null;
            scenarioHost.Clear();
        }

        private CarouselItem createCarouselItem(BeatmapInfo beatmap) => new CarouselItem(new GroupedBeatmap(null, beatmap));

        private BeatmapInfo nextBeatmap()
        {
            BeatmapInfo beatmap = beatmapsToCycle[beatmapIndex % beatmapsToCycle.Count];
            beatmapIndex++;
            return beatmap;
        }

        private string createModToggleSummary(string scenario)
        {
            long managed = ManiaModStressSteps.forceCollectionAndGetManagedMemory();

            return $"{scenario} diagnostics | panel survivors: {ManiaModStressSteps.countAlive(panelWeakReferences)} | item survivors: {ManiaModStressSteps.countAlive(panelItemWeakReferences)} | ez tracked: {ezAnalysisCache.GetTrackedBindableCount()} (baseline {baselineEzAnalysisTrackedBindables}) | star tracked: {difficultyCache.GetTrackedBindableCount()} (baseline {baselineStarDifficultyTrackedBindables}) | managed delta: {ManiaModStressSteps.formatMemoryDelta(managed - baselineManagedMemory)}";
        }

        private void updateSummary(string summary)
        {
            summaryText.Text = summary;
            summaryText.Colour = summary.Contains("survivors: 0") || summary.Contains("reset")
                ? Color4.White
                : Color4.OrangeRed;
        }
    }
}
