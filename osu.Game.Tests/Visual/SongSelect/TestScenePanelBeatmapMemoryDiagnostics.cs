// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Lists;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.UserInterface;
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
    [TestFixture]
    public partial class TestScenePanelBeatmapMemoryDiagnostics : ThemeComparisonTestScene
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
        private WeakList<EzDisplayKpsGraph> graphWeakReferences = new WeakList<EzDisplayKpsGraph>();
        private WeakList<EzDisplayKps> kpsWeakReferences = new WeakList<EzDisplayKps>();
        private WeakList<EzDisplayKpc> kpcWeakReferences = new WeakList<EzDisplayKpc>();
        private WeakList<EzDisplaySR> xxyWeakReferences = new WeakList<EzDisplaySR>();
        private WeakList<EzDisplayTag> tagWeakReferences = new WeakList<EzDisplayTag>();
        private WeakList<CompositeDrawable> componentHostWeakReferences = new WeakList<CompositeDrawable>();

        private PanelBeatmapStandalone? reusablePanel;
        private int beatmapIndex;
        private long baselineManagedMemory;
        private int baselineEzAnalysisTrackedBindables;
        private int baselineStarDifficultyTrackedBindables;

        public TestScenePanelBeatmapMemoryDiagnostics()
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
        public void TestPanelRecreationDoesNotLeakReferences()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddRepeatStep("recreate selected panel", recreateSelectedPanel, 40);
            AddStep("clear host", clearScenarioHost);
            AddUntilStep("no leaked panels", () => ManiaModStressSteps.collectAndCount(panelWeakReferences) == 0);
            AddUntilStep("no leaked items", () => ManiaModStressSteps.collectAndCount(panelItemWeakReferences) == 0);
        }

        [Test]
        public void TestPanelBeatmapSwapDoesNotLeakReferences()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddStep("create reusable panel", createReusablePanel);
            AddRepeatStep("swap beatmap item", swapReusablePanelBeatmap, 60);
            AddStep("clear host", clearScenarioHost);
            AddUntilStep("no leaked panels", () => ManiaModStressSteps.collectAndCount(panelWeakReferences) == 0);
            AddUntilStep("no leaked items", () => ManiaModStressSteps.collectAndCount(panelItemWeakReferences) == 0);
        }

        [Test]
        public void TestPanelRecreationWithManiaModToggleDoesNotLeakReferences()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddRepeatStep("recreate panel and cycle PS/SB mods", recreateSelectedPanelWithModCycle, ManiaModStressSteps.default_cycle_count);
            AddWaitStep("wait for analysis to settle", ManiaModStressSteps.analysis_settle_wait_steps);
            AddStep("clear mods", () => SelectedMods.SetDefault());
            AddStep("clear host", clearScenarioHost);
            assertNoReferenceLeaks();
            assertCacheBindablesReturnToBaseline();
        }

        [Test]
        public void TestPanelBeatmapSwapWithManiaModToggleDoesNotLeakReferences()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddStep("create reusable panel", createReusablePanel);
            AddRepeatStep("swap beatmap and cycle PS/SB mods", swapBeatmapAndCycleMods, ManiaModStressSteps.default_cycle_count);
            AddWaitStep("wait for analysis to settle", ManiaModStressSteps.analysis_settle_wait_steps);
            AddStep("clear mods", () => SelectedMods.SetDefault());
            AddStep("clear host", clearScenarioHost);
            assertNoReferenceLeaks();
            assertCacheBindablesReturnToBaseline();
        }

        [Test]
        public void TestEzComponentsDoNotLeakReferences()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddStep("create reusable panel", createReusablePanel);
            AddRepeatStep("swap beatmap item", swapReusablePanelBeatmap, 20);
            AddRepeatStep("cycle PS/SB mod toggles", performFullModCycle, 8);
            AddWaitStep("wait for analysis to settle", ManiaModStressSteps.analysis_settle_wait_steps);
            AddStep("clear mods", () => SelectedMods.SetDefault());
            AddStep("clear host", clearScenarioHost);
            AddUntilStep("no leaked component hosts", () => ManiaModStressSteps.collectAndCount(componentHostWeakReferences) == 0);
            AddUntilStep("no leaked graphs", () => ManiaModStressSteps.collectAndCount(graphWeakReferences) == 0);
            AddUntilStep("no leaked kps displays", () => ManiaModStressSteps.collectAndCount(kpsWeakReferences) == 0);
            AddUntilStep("no leaked kpc displays", () => ManiaModStressSteps.collectAndCount(kpcWeakReferences) == 0);
            AddUntilStep("no leaked xxySR displays", () => ManiaModStressSteps.collectAndCount(xxyWeakReferences) == 0);
            AddUntilStep("no leaked tag displays", () => ManiaModStressSteps.collectAndCount(tagWeakReferences) == 0);
            assertCacheBindablesReturnToBaseline();
        }

        [Test]
        [Explicit]
        public void TestPanelRecreationMemoryDiagnostics()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddRepeatStep("recreate selected panel", recreateSelectedPanel, 40);
            AddStep("clear host", clearScenarioHost);
            AddStep("collect and summarise", () => updateSummary(createPanelRecreationSummary()));
            AddAssert("panel summary available", () => summaryText.Text.ToString().Length, () => Is.GreaterThan(0));
        }

        [Test]
        [Explicit]
        public void TestPanelBeatmapSwapMemoryDiagnostics()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddStep("create reusable panel", createReusablePanel);
            AddRepeatStep("swap beatmap item", swapReusablePanelBeatmap, 60);
            AddStep("clear host", clearScenarioHost);
            AddStep("collect and summarise", () => updateSummary(createPanelSwapSummary()));
            AddAssert("swap summary available", () => summaryText.Text.ToString().Length, () => Is.GreaterThan(0));
        }

        [Test]
        [Explicit]
        public void TestPanelRecreationWithModToggleMemoryDiagnostics()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddRepeatStep("recreate panel and cycle PS/SB mods", recreateSelectedPanelWithModCycle, ManiaModStressSteps.default_cycle_count);
            AddWaitStep("wait for analysis to settle", ManiaModStressSteps.analysis_settle_wait_steps);
            AddStep("clear mods", () => SelectedMods.SetDefault());
            AddStep("clear host", clearScenarioHost);
            AddStep("collect and summarise", () => updateSummary(createModStressSummary("Panel recreation + mod toggle")));
            AddAssert("summary available", () => summaryText.Text.ToString().Length, () => Is.GreaterThan(0));
        }

        [Test]
        [Explicit]
        public void TestPanelBeatmapSwapWithModToggleMemoryDiagnostics()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddStep("create reusable panel", createReusablePanel);
            AddRepeatStep("swap beatmap and cycle PS/SB mods", swapBeatmapAndCycleMods, ManiaModStressSteps.default_cycle_count);
            AddWaitStep("wait for analysis to settle", ManiaModStressSteps.analysis_settle_wait_steps);
            AddStep("clear mods", () => SelectedMods.SetDefault());
            AddStep("clear host", clearScenarioHost);
            AddStep("collect and summarise", () => updateSummary(createModStressSummary("Panel swap + mod toggle")));
            AddAssert("summary available", () => summaryText.Text.ToString().Length, () => Is.GreaterThan(0));
        }

        [Test]
        [Explicit]
        public void TestEzComponentMemoryDiagnostics()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddStep("create reusable panel", createReusablePanel);
            AddRepeatStep("swap beatmap item", swapReusablePanelBeatmap, 20);
            AddRepeatStep("cycle PS/SB mod toggles", performFullModCycle, 8);
            AddWaitStep("wait for analysis to settle", ManiaModStressSteps.analysis_settle_wait_steps);
            AddStep("clear mods", () => SelectedMods.SetDefault());
            AddStep("clear host", clearScenarioHost);
            AddStep("collect and summarise", () => updateSummary(createEzComponentSummary()));
            AddAssert("component summary available", () => summaryText.Text.ToString().Length, () => Is.GreaterThan(0));
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
                        Text = "Run one of the Explicit diagnostics to populate memory results.",
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

        private void assertNoReferenceLeaks()
        {
            AddUntilStep("no leaked panels", () => ManiaModStressSteps.collectAndCount(panelWeakReferences) == 0);
            AddUntilStep("no leaked items", () => ManiaModStressSteps.collectAndCount(panelItemWeakReferences) == 0);
        }

        private void assertCacheBindablesReturnToBaseline()
        {
            AddUntilStep("ez analysis tracked bindables return to baseline",
                () => ezAnalysisCache.GetTrackedBindableCount() <= baselineEzAnalysisTrackedBindables);
            AddUntilStep("star difficulty tracked bindables return to baseline",
                () => difficultyCache.GetTrackedBindableCount() <= baselineStarDifficultyTrackedBindables);
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

            Assert.That(beatmapsToCycle.Count, Is.GreaterThan(0), "No beatmaps available for diagnostics.");
        }

        private void resetDiagnostics()
        {
            panelWeakReferences = new WeakList<PanelBeatmapStandalone>();
            panelItemWeakReferences = new WeakList<CarouselItem>();
            graphWeakReferences = new WeakList<EzDisplayKpsGraph>();
            kpsWeakReferences = new WeakList<EzDisplayKps>();
            kpcWeakReferences = new WeakList<EzDisplayKpc>();
            xxyWeakReferences = new WeakList<EzDisplaySR>();
            tagWeakReferences = new WeakList<EzDisplayTag>();
            componentHostWeakReferences = new WeakList<CompositeDrawable>();
            reusablePanel = null;
            beatmapIndex = 0;

            clearScenarioHost();
            SelectedMods.SetDefault();
            baselineManagedMemory = ManiaModStressSteps.forceCollectionAndGetManagedMemory();
            captureCacheBaselines();
            updateSummary("Diagnostics reset. Managed baseline captured.");
        }

        private void captureCacheBaselines()
        {
            ManiaModStressSteps.forceCollectionAndGetManagedMemory();
            baselineEzAnalysisTrackedBindables = ezAnalysisCache.GetTrackedBindableCount();
            baselineStarDifficultyTrackedBindables = difficultyCache.GetTrackedBindableCount();
        }

        private void performFullModCycle() => ManiaModStressSteps.performFullModCycle(SelectedMods);

        private void recreateSelectedPanelWithModCycle()
        {
            recreateSelectedPanel();
            performFullModCycle();
        }

        private void swapBeatmapAndCycleMods()
        {
            swapReusablePanelBeatmap();
            performFullModCycle();
        }

        private void recreateSelectedPanel()
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
            trackEzComponents(panel);
            scenarioHost.Add(panel);

            updateSummary($"Panel recreation iteration {ManiaModStressSteps.countAlive(panelWeakReferences)} running...");
        }

        private void createReusablePanel()
        {
            clearScenarioHost();

            reusablePanel = new PanelBeatmapStandalone();
            panelWeakReferences.Add(reusablePanel);
            trackEzComponents(reusablePanel);
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

        private void trackEzComponents(PanelBeatmapStandalone panel)
        {
            componentHostWeakReferences.Add(panel);

            foreach (var graph in panel.ChildrenOfType<EzDisplayKpsGraph>())
                graphWeakReferences.Add(graph);

            foreach (var kps in panel.ChildrenOfType<EzDisplayKps>())
                kpsWeakReferences.Add(kps);

            foreach (var kpc in panel.ChildrenOfType<EzDisplayKpc>())
                kpcWeakReferences.Add(kpc);

            foreach (var xxy in panel.ChildrenOfType<EzDisplaySR>())
                xxyWeakReferences.Add(xxy);

            foreach (var tag in panel.ChildrenOfType<EzDisplayTag>())
                tagWeakReferences.Add(tag);
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

        private string createPanelRecreationSummary()
        {
            long managed = ManiaModStressSteps.forceCollectionAndGetManagedMemory();

            return $"Panel recreation diagnostics | panel survivors: {ManiaModStressSteps.countAlive(panelWeakReferences)} | item survivors: {ManiaModStressSteps.countAlive(panelItemWeakReferences)} | managed delta: {ManiaModStressSteps.formatMemoryDelta(managed - baselineManagedMemory)}";
        }

        private string createPanelSwapSummary()
        {
            long managed = ManiaModStressSteps.forceCollectionAndGetManagedMemory();

            return $"Panel swap diagnostics | panel survivors: {ManiaModStressSteps.countAlive(panelWeakReferences)} | item survivors: {ManiaModStressSteps.countAlive(panelItemWeakReferences)} | managed delta: {ManiaModStressSteps.formatMemoryDelta(managed - baselineManagedMemory)}";
        }

        private string createModStressSummary(string scenario)
        {
            long managed = ManiaModStressSteps.forceCollectionAndGetManagedMemory();

            return $"{scenario} diagnostics | panel survivors: {ManiaModStressSteps.countAlive(panelWeakReferences)} | item survivors: {ManiaModStressSteps.countAlive(panelItemWeakReferences)} | ez tracked: {ezAnalysisCache.GetTrackedBindableCount()} (baseline {baselineEzAnalysisTrackedBindables}) | star tracked: {difficultyCache.GetTrackedBindableCount()} (baseline {baselineStarDifficultyTrackedBindables}) | managed delta: {ManiaModStressSteps.formatMemoryDelta(managed - baselineManagedMemory)}";
        }

        private string createEzComponentSummary()
        {
            long managed = ManiaModStressSteps.forceCollectionAndGetManagedMemory();

            return $"Ez component diagnostics | hosts: {ManiaModStressSteps.countAlive(componentHostWeakReferences)} | graphs: {ManiaModStressSteps.countAlive(graphWeakReferences)} | kps displays: {ManiaModStressSteps.countAlive(kpsWeakReferences)} | kpc displays: {ManiaModStressSteps.countAlive(kpcWeakReferences)} | xxySR displays: {ManiaModStressSteps.countAlive(xxyWeakReferences)} | tag displays: {ManiaModStressSteps.countAlive(tagWeakReferences)} | ez tracked: {ezAnalysisCache.GetTrackedBindableCount()} | star tracked: {difficultyCache.GetTrackedBindableCount()} | managed delta: {ManiaModStressSteps.formatMemoryDelta(managed - baselineManagedMemory)}";
        }

        private void updateSummary(string summary)
        {
            summaryText.Text = summary;
            summaryText.Colour = summary.Contains("survivors: 0") || summary.Contains("Diagnostics reset")
                ? Color4.White
                : Color4.OrangeRed;
        }
    }
}
