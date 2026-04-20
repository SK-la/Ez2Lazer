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

        private FillFlowContainer scenarioHost = null!;
        private OsuSpriteText summaryText = null!;

        private List<BeatmapInfo> beatmapsToCycle = new List<BeatmapInfo>();

        private WeakList<PanelBeatmapStandalone> panelWeakReferences = new WeakList<PanelBeatmapStandalone>();
        private WeakList<CarouselItem> panelItemWeakReferences = new WeakList<CarouselItem>();
        private WeakList<EzDisplayKpsGraph> graphWeakReferences = new WeakList<EzDisplayKpsGraph>();
        private WeakList<EzKpsDisplay> kpsWeakReferences = new WeakList<EzKpsDisplay>();
        private WeakList<EzKpcDisplay> kpcWeakReferences = new WeakList<EzKpcDisplay>();
        private WeakList<EzDisplaySR> xxyWeakReferences = new WeakList<EzDisplaySR>();
        private WeakList<EzTagDisplay> tagWeakReferences = new WeakList<EzTagDisplay>();
        private WeakList<CompositeDrawable> componentHostWeakReferences = new WeakList<CompositeDrawable>();

        private PanelBeatmapStandalone? reusablePanel;
        private int beatmapIndex;
        private long baselineManagedMemory;

        public TestScenePanelBeatmapMemoryDiagnostics()
            : base(false)
        {
        }

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            populateBeatmaps();
            Ruleset.Value = new ManiaRuleset().RulesetInfo;
            CreateThemedContent(OverlayColourScheme.Aquamarine);
            resetDiagnostics();
        });

        [Test]
        public void TestPanelRecreationDoesNotLeakReferences()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddRepeatStep("recreate selected panel", recreateSelectedPanel, 40);
            AddStep("clear host", clearScenarioHost);
            AddUntilStep("no leaked panels", () => collectAndCount(panelWeakReferences) == 0);
            AddUntilStep("no leaked items", () => collectAndCount(panelItemWeakReferences) == 0);
        }

        [Test]
        public void TestPanelBeatmapSwapDoesNotLeakReferences()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddStep("create reusable panel", createReusablePanel);
            AddRepeatStep("swap beatmap item", swapReusablePanelBeatmap, 60);
            AddStep("clear host", clearScenarioHost);
            AddUntilStep("no leaked panels", () => collectAndCount(panelWeakReferences) == 0);
            AddUntilStep("no leaked items", () => collectAndCount(panelItemWeakReferences) == 0);
        }

        [Test]
        public void TestEzComponentsDoNotLeakReferences()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddStep("clear host", clearScenarioHost);
            AddUntilStep("no leaked component hosts", () => collectAndCount(componentHostWeakReferences) == 0);
            AddUntilStep("no leaked graphs", () => collectAndCount(graphWeakReferences) == 0);
            AddUntilStep("no leaked kps displays", () => collectAndCount(kpsWeakReferences) == 0);
            AddUntilStep("no leaked kpc displays", () => collectAndCount(kpcWeakReferences) == 0);
            AddUntilStep("no leaked xxySR displays", () => collectAndCount(xxyWeakReferences) == 0);
            AddUntilStep("no leaked tag displays", () => collectAndCount(tagWeakReferences) == 0);
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
        public void TestEzComponentMemoryDiagnostics()
        {
            AddStep("reset diagnostics", resetDiagnostics);
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
            kpsWeakReferences = new WeakList<EzKpsDisplay>();
            kpcWeakReferences = new WeakList<EzKpcDisplay>();
            xxyWeakReferences = new WeakList<EzDisplaySR>();
            tagWeakReferences = new WeakList<EzTagDisplay>();
            componentHostWeakReferences = new WeakList<CompositeDrawable>();
            reusablePanel = null;
            beatmapIndex = 0;

            clearScenarioHost();
            baselineManagedMemory = forceCollectionAndGetManagedMemory();
            updateSummary("Diagnostics reset. Managed baseline captured.");
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
            scenarioHost.Add(panel);

            updateSummary($"Panel recreation iteration {countAlive(panelWeakReferences)} running...");
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

            updateSummary($"Panel swap iteration {countAlive(panelItemWeakReferences)} running...");
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
            long managed = forceCollectionAndGetManagedMemory();

            return $"Panel recreation diagnostics | panel survivors: {countAlive(panelWeakReferences)} | item survivors: {countAlive(panelItemWeakReferences)} | managed delta: {formatMemoryDelta(managed - baselineManagedMemory)}";
        }

        private string createPanelSwapSummary()
        {
            long managed = forceCollectionAndGetManagedMemory();

            return $"Panel swap diagnostics | panel survivors: {countAlive(panelWeakReferences)} | item survivors: {countAlive(panelItemWeakReferences)} | managed delta: {formatMemoryDelta(managed - baselineManagedMemory)}";
        }

        private string createEzComponentSummary()
        {
            long managed = forceCollectionAndGetManagedMemory();

            return $"Ez component diagnostics | hosts: {countAlive(componentHostWeakReferences)} | graphs: {countAlive(graphWeakReferences)} | kps displays: {countAlive(kpsWeakReferences)} | kpc displays: {countAlive(kpcWeakReferences)} | xxySR displays: {countAlive(xxyWeakReferences)} | tag displays: {countAlive(tagWeakReferences)} | managed delta: {formatMemoryDelta(managed - baselineManagedMemory)}";
        }

        private void updateSummary(string summary)
        {
            summaryText.Text = summary;
            summaryText.Colour = summary.Contains("survivors: 0") || summary.Contains("Diagnostics reset")
                ? Color4.White
                : Color4.OrangeRed;
        }

        private static int countAlive<T>(WeakList<T> weakList)
            where T : class
        {
            int count = 0;

            foreach (var _ in weakList)
                count++;

            return count;
        }

        private static int collectAndCount<T>(WeakList<T> weakList)
            where T : class
        {
            forceCollectionAndGetManagedMemory();
            return countAlive(weakList);
        }

        private static string formatMemoryDelta(long delta)
        {
            double kiloBytes = delta / 1024.0;
            return $"{kiloBytes:+0.0;-0.0;0.0} KB";
        }

        private static long forceCollectionAndGetManagedMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return GC.GetTotalMemory(true);
        }
    }
}
