// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Lists;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mania;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.SongSelect
{
    /// <summary>
    /// PS/SB mod on/off stress for <see cref="BeatmapTitleWedge"/> Ez analysis bindables.
    /// Mod settings are never changed.
    /// </summary>
    [TestFixture]
    public partial class TestSceneBeatmapTitleWedgeModStress : SongSelectComponentsTestScene
    {
        [Cached(typeof(IBindable<Screens.Select.SongSelect.BeatmapSetLookupResult?>))]
        private Bindable<Screens.Select.SongSelect.BeatmapSetLookupResult?> onlineLookupResult = new Bindable<Screens.Select.SongSelect.BeatmapSetLookupResult?>();

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private EzAnalysisCache ezAnalysisCache { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        private BeatmapTitleWedge? titleWedge;
        private Container wedgeHost = null!;
        private OsuSpriteText summaryText = null!;

        private List<BeatmapInfo> beatmapsToCycle = new List<BeatmapInfo>();
        private WeakList<BeatmapTitleWedge> wedgeWeakReferences = new WeakList<BeatmapTitleWedge>();

        private int beatmapIndex;
        private long baselineManagedMemory;
        private int baselineEzAnalysisTrackedBindables;
        private int baselineStarDifficultyTrackedBindables;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            populateBeatmaps();
            Ruleset.Value = new ManiaRuleset().RulesetInfo;

            AddRange(new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 12),
                    Children = new Drawable[]
                    {
                        summaryText = new OsuSpriteText
                        {
                            RelativeSizeAxes = Axes.X,
                            Font = OsuFont.Style.Body,
                            Colour = Color4.White,
                            Text = "Wedge mod stress tests.",
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Shear = OsuGame.SHEAR,
                            Child = wedgeHost = new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                            },
                        }
                    }
                }
            });

            resetDiagnostics();
        }

        [Test]
        public void TestWedgeManiaModToggleDoesNotLeakReferences()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddRepeatStep("cycle PS/SB mod toggles", performFullModCycle, ManiaModStressSteps.default_cycle_count);
            finishWedgeStress();
            AddUntilStep("no leaked wedges", () => ManiaModStressSteps.collectAndCount(wedgeWeakReferences) == 0);
        }

        [Test]
        public void TestWedgeManiaModToggleWithBeatmapChangeDoesNotLeakReferences()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddRepeatStep("change beatmap and cycle PS/SB mods", changeBeatmapAndCycleMods, ManiaModStressSteps.default_cycle_count);
            finishWedgeStress();
            AddUntilStep("no leaked wedges", () => ManiaModStressSteps.collectAndCount(wedgeWeakReferences) == 0);
        }

        [Test]
        public void TestWedgeRecreationWithManiaModToggleDoesNotLeakReferences()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddRepeatStep("recreate wedge and cycle PS/SB mods", recreateWedgeWithModCycle, ManiaModStressSteps.default_cycle_count);
            finishWedgeStress();
            AddUntilStep("no leaked wedges", () => ManiaModStressSteps.collectAndCount(wedgeWeakReferences) == 0);
        }

        [Test]
        [Explicit]
        public void TestWedgeManiaModToggleMemoryDiagnostics()
        {
            AddStep("reset diagnostics", resetDiagnostics);
            AddRepeatStep("change beatmap and cycle PS/SB mods", changeBeatmapAndCycleMods, ManiaModStressSteps.default_cycle_count);
            finishWedgeStress();
            AddStep("collect and summarise", () => updateSummary(createWedgeSummary("Wedge mod toggle + beatmap change")));
            AddAssert("summary available", () => summaryText.Text.ToString().Length, () => Is.GreaterThan(0));
        }

        private void finishWedgeStress()
        {
            AddWaitStep("wait for analysis to settle", ManiaModStressSteps.analysis_settle_wait_steps);
            AddStep("clear mods", () => SelectedMods.SetDefault());
            AddWaitStep("wait after clearing mods", ManiaModStressSteps.analysis_settle_wait_steps);
            AddStep("destroy wedge", destroyWedge);
            AddStep("collect", () => ManiaModStressSteps.forceCollectionAndGetManagedMemory());
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

            Assert.That(beatmapsToCycle.Count, Is.GreaterThan(0), "No beatmaps available for wedge mod stress.");
        }

        private void resetDiagnostics()
        {
            destroyWedge();
            wedgeWeakReferences = new WeakList<BeatmapTitleWedge>();
            beatmapIndex = 0;
            SelectedMods.SetDefault();
            ManiaModStressSteps.forceCollectionAndGetManagedMemory();
            baselineManagedMemory = ManiaModStressSteps.forceCollectionAndGetManagedMemory();
            baselineEzAnalysisTrackedBindables = ezAnalysisCache.GetTrackedBindableCount();
            baselineStarDifficultyTrackedBindables = difficultyCache.GetTrackedBindableCount();

            titleWedge = createTrackedWedge();
            wedgeHost.Add(titleWedge);
            selectNextManiaBeatmap();

            updateSummary("Wedge diagnostics reset.");
        }

        private BeatmapTitleWedge createTrackedWedge()
        {
            var wedge = new BeatmapTitleWedge
            {
                State = { Value = Visibility.Visible },
            };

            wedgeWeakReferences.Add(wedge);
            return wedge;
        }

        private void performFullModCycle() => ManiaModStressSteps.performFullModCycle(SelectedMods);

        private void changeBeatmapAndCycleMods()
        {
            selectNextManiaBeatmap();
            performFullModCycle();
        }

        private void recreateWedgeWithModCycle()
        {
            destroyWedge();
            titleWedge = createTrackedWedge();
            wedgeHost.Add(titleWedge);
            selectNextManiaBeatmap();
            performFullModCycle();
        }

        private void selectNextManiaBeatmap()
        {
            BeatmapInfo beatmap = beatmapsToCycle[beatmapIndex % beatmapsToCycle.Count];
            beatmapIndex++;
            Beatmap.Value = beatmaps.GetWorkingBeatmap(beatmap);
        }

        private void destroyWedge()
        {
            if (titleWedge == null)
                return;

            titleWedge.Expire();
            titleWedge = null;
        }

        private string createWedgeSummary(string scenario)
        {
            long managed = ManiaModStressSteps.forceCollectionAndGetManagedMemory();

            return $"{scenario} | wedge survivors: {ManiaModStressSteps.countAlive(wedgeWeakReferences)} | ez tracked: {ezAnalysisCache.GetTrackedBindableCount()} (baseline {baselineEzAnalysisTrackedBindables}) | star tracked: {difficultyCache.GetTrackedBindableCount()} (baseline {baselineStarDifficultyTrackedBindables}) | managed delta: {ManiaModStressSteps.formatMemoryDelta(managed - baselineManagedMemory)}";
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
