// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Statistics;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.UI;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Tests.Resources;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.SongSelect
{
    /// <summary>
    /// 选歌 carousel 展开态 draw 消融。HUD / runtime 每秒打 <c>[SS-DRAW]</c>。
    /// </summary>
    [TestFixture]
    [System.ComponentModel.Description("选歌展开态 draw 消融：难度分组+液态玻璃")]
    public partial class TestSceneSongSelectDrawCost : SongSelectTestScene
    {
        private const int fill_star = 5;
        private const int fill_map_count = 30;
        private const double log_interval_ms = 1000;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        private OsuSpriteText summaryText = null!;

        private EzMaterialUiMode? restoreMaterialMode;
        private bool disablePanelGlass;
        private bool hideKpsGraphs;
        private bool disableEdgeGlow;
        private double nextLogAt;
        private string lastLoggedVariant = string.Empty;
        private long lastConstructed;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Add(new Container
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Margin = new MarginPadding { Horizontal = 12, Top = 52 },
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.7f,
                    },
                    summaryText = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 14),
                        Padding = new MarginPadding(8),
                        Text = "waiting for song select…",
                    },
                }
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            if (restoreMaterialMode is EzMaterialUiMode mode)
                ezConfig.SetValue(Ez2Setting.MaterialUiMode, mode);

            base.Dispose(isDisposing);
        }

        protected override void Update()
        {
            base.Update();

            applyAblation();

            if (Time.Current < nextLogAt)
                return;

            nextLogAt = Time.Current + log_interval_ms;
            logSample("idle");
        }

        [Test]
        public void TestIdleDrawableConstruction()
        {
            prepareScene();
            AddWaitStep("settle", 10);

            measureConstruction("all");
            AddStep("hide carousel", () => tryGetCarousel()?.Hide());
            AddWaitStep("settle no carousel", 5);
            measureConstruction("no-carousel");

            AddStep("show carousel", () => tryGetCarousel()?.Show());
            AddStep("hide wedges", () =>
            {
                foreach (var wedge in SongSelect.ChildrenOfType<BeatmapTitleWedge>())
                    wedge.Hide();
                foreach (var wedge in SongSelect.ChildrenOfType<BeatmapMetadataWedge>())
                    wedge.Hide();
            });
            AddWaitStep("settle no wedges", 5);
            measureConstruction("no-wedges");
        }

        private void measureConstruction(string variant)
        {
            long before = 0;
            double start = 0;

            AddStep($"start measure {variant}", () =>
            {
                before = constructed;
                start = Time.Current;
            });
            AddUntilStep($"measure {variant} window", () => Time.Current - start >= 2000);
            AddStep($"log measure {variant}", () =>
            {
                double seconds = Math.Max(0.001, (Time.Current - start) / 1000);
                long delta = constructed - before;
                Logger.Log($"[SS-CONSTRUCT] variant={variant} delta={delta} perSecond={delta / seconds:F1}", LoggingTarget.Runtime, LogLevel.Important);
            });
        }

        private static long constructed => GlobalStatistics.Get<int>(nameof(Drawable), "Total constructed").Value;

        [Test]
        public void TestCollapsedExpandedAblationLogs()
        {
            prepareScene();

            AddStep("collapse fill group", () => setFillGroupExpanded(false));
            AddUntilStep("standalone hidden", () => countStandalone() == 0);
            AddUntilStep("group headers visible", () => countGroups() > 0);
            AddWaitStep("settle collapsed", 10);
            AddStep("log collapsed", () => logSample("collapsed"));

            AddStep("expand fill group", () => setFillGroupExpanded(true));
            AddUntilStep("standalone visible", () => countStandalone() > 0);
            AddWaitStep("settle expanded", 10);
            AddStep("log expanded", () => logSample("expanded"));

            AddStep("disable panel glass", () => disablePanelGlass = true);
            AddWaitStep("settle expanded-no-glass", 5);
            AddStep("log expanded-no-glass", () => logSample("expanded-no-glass"));

            AddStep("collapse fill group", () => setFillGroupExpanded(false));
            AddUntilStep("standalone hidden again", () => countStandalone() == 0);
            AddWaitStep("settle collapsed-no-glass", 5);
            AddStep("log collapsed-no-glass", () => logSample("collapsed-no-glass"));

            AddStep("enable panel glass", () => disablePanelGlass = false);
            AddStep("expand fill group", () => setFillGroupExpanded(true));
            AddUntilStep("standalone visible again", () => countStandalone() > 0);
            AddWaitStep("settle restored", 5);

            AddStep("hide KPS graphs", () => hideKpsGraphs = true);
            AddWaitStep("settle no-kps", 5);
            AddStep("log expanded-no-kps", () => logSample("expanded-no-kps"));
            AddStep("show KPS graphs", () => hideKpsGraphs = false);

            AddStep("disable edge glow", () => disableEdgeGlow = true);
            AddWaitStep("settle no-glow", 5);
            AddStep("log expanded-no-glow", () => logSample("expanded-no-glow"));
            AddStep("enable edge glow", () => disableEdgeGlow = false);

            AddStep("material Acrylic", () => ezConfig.SetValue(Ez2Setting.MaterialUiMode, EzMaterialUiMode.Acrylic));
            AddWaitStep("settle acrylic", 5);
            AddStep("log expanded-acrylic", () => logSample("expanded-acrylic"));

            AddStep("material Default", () => ezConfig.SetValue(Ez2Setting.MaterialUiMode, EzMaterialUiMode.Default));
            AddWaitStep("settle classic", 5);
            AddStep("log expanded-classic", () => logSample("expanded-classic"));

            AddStep("material LiquidGlass", () => ezConfig.SetValue(Ez2Setting.MaterialUiMode, EzMaterialUiMode.LiquidGlass));
        }

        [Test]
        [Explicit("Visual TestBrowser ablation; headless GPU counters are dummy.")]
        public void TestInteractiveAblation()
        {
            prepareScene();

            AddStep("collapse fill group", () => setFillGroupExpanded(false));
            AddStep("expand fill group", () => setFillGroupExpanded(true));
            AddToggleStep("disable panel glass", v => disablePanelGlass = v);
            AddToggleStep("hide KPS graphs", v => hideKpsGraphs = v);
            AddToggleStep("disable edge glow", v => disableEdgeGlow = v);
            AddStep("material LiquidGlass", () => ezConfig.SetValue(Ez2Setting.MaterialUiMode, EzMaterialUiMode.LiquidGlass));
            AddStep("material Acrylic", () => ezConfig.SetValue(Ez2Setting.MaterialUiMode, EzMaterialUiMode.Acrylic));
            AddStep("material Default", () => ezConfig.SetValue(Ez2Setting.MaterialUiMode, EzMaterialUiMode.Default));
            AddStep("log sample now", () => logSample("manual"));
        }

        private void prepareScene()
        {
            AddStep("liquid glass + difficulty group/sort", () =>
            {
                restoreMaterialMode ??= ezConfig.Get<EzMaterialUiMode>(Ez2Setting.MaterialUiMode);
                ezConfig.SetValue(Ez2Setting.MaterialUiMode, EzMaterialUiMode.LiquidGlass);
                Config.SetValue(OsuSetting.SongSelectSortingMode, SortMode.Difficulty);
                Config.SetValue(OsuSetting.SongSelectGroupMode, GroupMode.Difficulty);
                disablePanelGlass = false;
                hideKpsGraphs = false;
                disableEdgeGlow = false;
                nextLogAt = Time.Current + log_interval_ms;
            });

            AddStep("import fill maps", importFillMaps);
            AddUntilStep("maps in manager", () => Beatmaps.GetAllUsableBeatmapSets().Count >= fill_map_count + 4);
            LoadSongSelect();
            SortAndGroupBy(SortMode.Difficulty, GroupMode.Difficulty);
            AddUntilStep("difficulty groups in filter", difficultyGroupsReady);
            AddUntilStep("fill star group exists", () => findFillGroupItem(tryGetCarousel()) != null);
        }

        private bool difficultyGroupsReady()
        {
            var carousel = tryGetCarousel();

            if (carousel == null || carousel.IsFiltering)
                return false;

            return carousel.Filters.OfType<BeatmapCarouselFilterGrouping>()
                           .FirstOrDefault()
                           ?.GroupItems.Keys.OfType<StarDifficultyGroupDefinition>()
                           .Any() == true;
        }

        private void importFillMaps()
        {
            var osu = Rulesets.AvailableRulesets.First(r => r.OnlineID == 0);

            importStarGroup(osu, fill_star + 0.5, fill_map_count);
            importStarGroup(osu, 1.5, 2);
            importStarGroup(osu, 9.5, 2);
        }

        private void importStarGroup(RulesetInfo ruleset, double starRating, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var set = TestResources.CreateTestBeatmapSetInfo(1, new[] { ruleset });

                foreach (var beatmap in set.Beatmaps)
                    beatmap.StarRating = starRating;

                Beatmaps.Import(set);
            }
        }

        private void setFillGroupExpanded(bool expanded)
        {
            var carousel = tryGetCarousel();
            var item = findFillGroupItem(carousel);

            if (item == null)
                return;

            if (item.IsExpanded == expanded)
                return;

            carousel!.Activate(item);
        }

        private static CarouselItem? findFillGroupItem(BeatmapCarousel? carousel)
            => carousel?.GetCarouselItems()?.FirstOrDefault(i =>
                i.Model is StarDifficultyGroupDefinition group && (int)group.Difficulty.Stars == fill_star);

        private void applyAblation()
        {
            var carousel = tryGetCarousel();

            if (carousel == null)
                return;

            foreach (var panel in carousel.ChildrenOfType<Panel>())
            {
                if (panel.Item?.IsVisible != true)
                    continue;

                var glass = panel.ChildrenOfType<EzMaterialPanelBackground>().FirstOrDefault();

                if (glass != null)
                {
                    bool wantGlass = !disablePanelGlass;

                    if (glass.MaterialCaptureVisible != wantGlass)
                    {
                        glass.MaterialCaptureVisible = wantGlass;
                        glass.SyncMaterialCaptureState();
                    }
                }

                if (disableEdgeGlow)
                {
                    panel.TopLevelContent.ClearTransforms();
                    panel.TopLevelContent.EdgeEffect = new EdgeEffectParameters();
                }
            }

            foreach (var graph in carousel.ChildrenOfType<EzDisplayKpsGraph>())
            {
                if (hideKpsGraphs)
                    graph.Hide();
                else if (graph.Alpha == 0)
                    graph.Show();
            }
        }

        private void logSample(string variant)
        {
            var carousel = tryGetCarousel();

            if (carousel == null)
                return;

            int groups = countGroups();
            int standalone = countStandalone();
            int glassOn = carousel.ChildrenOfType<LiquidGlassDrawable>().Count(g => g.EffectEnabled);
            int glassTotal = carousel.ChildrenOfType<LiquidGlassDrawable>().Count();
            int allGlassOn = this.ChildrenOfType<LiquidGlassDrawable>().Count(g => g.EffectEnabled);
            bool fillExpanded = findFillGroupItem(carousel)?.IsExpanded == true;
            bool copy = host.Renderer.SupportsBackbufferRegionCopy;
            bool parentFbo = carousel.FindClosestParent<BufferedContainer>() != null;
            float glassScale = carousel.ChildrenOfType<LiquidGlassDrawable>().FirstOrDefault()?.FrameBufferScale.X ?? 0;
            string surface = host.ResolvedRenderer.ToString();
            string path = !copy || parentFbo ? "per-card-likely" : "shared-likely";

            double updateMs = host.UpdateThread.Clock.ElapsedFrameTime;
            double drawMs = host.DrawThread.Clock.ElapsedFrameTime;
            double updateHz = host.UpdateThread.Clock.FramesPerSecond;
            double drawHz = host.DrawThread.Clock.FramesPerSecond;

            string line =
                $"variant={variant} material={ezConfig.Get<EzMaterialUiMode>(Ez2Setting.MaterialUiMode)} "
                + $"expanded={fillExpanded} groups={groups} standalone={standalone} visible={carousel.VisibleItems} "
                + $"glass={glassOn}/{glassTotal} allGlass={allGlassOn} "
                + $"masking={carousel.ChildrenOfType<CompositeDrawable>().Count(d => d.Masking && d.Alpha > 0)} "
                + $"texts={carousel.ChildrenOfType<SpriteText>().Count(d => d.Alpha > 0)} "
                + $"constructed={constructed} constructedDelta={constructed - lastConstructed} "
                + $"surface={surface} renderer={host.Renderer.GetType().Name} copy={copy} parentFbo={parentFbo} "
                + $"glassScale={glassScale:0.##} path={path} "
                + $"ablate(glass/kps/glow)={(disablePanelGlass ? "off" : "on")}/{(hideKpsGraphs ? "off" : "on")}/{(disableEdgeGlow ? "off" : "on")} "
                + $"updMs={updateMs:F2} updHz={updateHz:F0} drawMs={drawMs:F2} drawHz={drawHz:F0} "
                + $"FBORedraw={drawStat("FBORedraw")} DrawCalls={drawStat("DrawCalls")} "
                + $"ShaderBinds={drawStat("ShaderBinds")} Pixels={drawStat("Pixels")}";

            summaryText.Text = line.Replace(" ", "\n", StringComparison.Ordinal);

            if (variant != lastLoggedVariant || variant == "idle")
                Logger.Log($"[SS-DRAW] {line}", LoggingTarget.Runtime, LogLevel.Important);

            lastConstructed = constructed;
            lastLoggedVariant = variant;
        }

        private BeatmapCarousel? tryGetCarousel()
        {
            if (SongSelect is not { IsLoaded: true })
                return null;

            return SongSelect.ChildrenOfType<BeatmapCarousel>().FirstOrDefault();
        }

        private int countGroups()
            => tryGetCarousel()?.ChildrenOfType<PanelGroupStarDifficulty>().Count(p => p.Alpha > 0) ?? 0;

        private int countStandalone()
            => tryGetCarousel()?.ChildrenOfType<PanelBeatmapStandalone>().Count(p => p.Alpha > 0) ?? 0;

        private static long drawStat(string name) => GlobalStatistics.Get<long>("Draw", name).Value;
    }
}
