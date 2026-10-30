// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.EzOsuGame.Edit.Scenes;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.EzMania.Editor;
using osu.Game.Screens.Edit.Components.Menus;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osu.Game.Tests.Resources;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Edit
{
    public partial class TestSceneEzSkinEditorScreen : ScreenTestScene
    {
        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        private EzSkinEditorScreen editorScreen = null!;

        [Test]
        public void TestSceneRegistryMapsCurrentScreenOrFallsBackToGameplay()
        {
            AddAssert("main menu falls back to gameplay", () => EzSkinEditorSceneRegistry.ResolveCurrentScene(typeof(MainMenu)) == EzSkinEditorSceneType.Gameplay);
            AddAssert("player maps to gameplay", () => EzSkinEditorSceneRegistry.ResolveCurrentScene(typeof(Player)) == EzSkinEditorSceneType.Gameplay);
            AddAssert("song select maps to song select", () => EzSkinEditorSceneRegistry.ResolveCurrentScene(typeof(SoloSongSelect)) == EzSkinEditorSceneType.SongSelect);
        }

        [Test]
        public void TestSceneRegistryIsSingleHudCapabilitySource()
        {
            AddAssert("gameplay supports hud", () => EzSkinEditorSceneRegistry.SupportsHudLayer(EzSkinEditorSceneType.Gameplay));
            AddAssert("song select supports hud", () => EzSkinEditorSceneRegistry.SupportsHudLayer(EzSkinEditorSceneType.SongSelect));
            AddAssert("appearance has no hud", () => !EzSkinEditorSceneRegistry.SupportsHudLayer(EzSkinEditorSceneType.Appearance));
            AddAssert("note has no hud", () => !EzSkinEditorSceneRegistry.SupportsHudLayer(EzSkinEditorSceneType.Note));

            AddAssert("gameplay offers ez hud + ruleset layers", () =>
            {
                var layers = EzSkinEditorLayerOptions.For(EzSkinEditorSceneType.Gameplay);
                return layers.Any(d => d.Layer == EzSkinEditorLayer.EzHud)
                       && layers.Any(d => d.Layer == EzSkinEditorLayer.EzHudRuleset)
                       && layers.Any(d => d.Layer == EzSkinEditorLayer.EzPlayfieldRuleset);
            });
            AddAssert("song select offers only global ez hud", () =>
            {
                var layers = EzSkinEditorLayerOptions.For(EzSkinEditorSceneType.SongSelect);
                return layers.Any(d => d.Layer == EzSkinEditorLayer.EzHud) && !layers.Any(d => d.RulesetScoped);
            });
            AddAssert("appearance offers full layers (shown but not editable)", () =>
            {
                var layers = EzSkinEditorLayerOptions.For(EzSkinEditorSceneType.Appearance);
                return layers.Any(d => d.Layer == EzSkinEditorLayer.EzHud)
                       && layers.Any(d => d.Layer == EzSkinEditorLayer.EzHudRuleset)
                       && layers.Any(d => d.Layer == EzSkinEditorLayer.EzPlayfieldRuleset);
            });

            AddAssert("global ez hud targets main hud without ruleset", () =>
            {
                var lookup = EzSkinEditorLayerOptions.ResolveLookup(EzSkinEditorSceneType.Gameplay, EzSkinEditorLayer.EzHud, new ManiaRuleset().RulesetInfo);
                return lookup is { Ruleset: null, Lookup: GlobalSkinnableContainers.MainHUDComponents };
            });
            AddAssert("ruleset hud targets main hud with ruleset", () =>
            {
                var ruleset = new ManiaRuleset().RulesetInfo;
                var lookup = EzSkinEditorLayerOptions.ResolveLookup(EzSkinEditorSceneType.Gameplay, EzSkinEditorLayer.EzHudRuleset, ruleset);
                return lookup != null && ReferenceEquals(lookup.Ruleset, ruleset) && lookup.Lookup == GlobalSkinnableContainers.MainHUDComponents;
            });
            AddAssert("ruleset playfield targets playfield", () =>
            {
                var lookup = EzSkinEditorLayerOptions.ResolveLookup(EzSkinEditorSceneType.Gameplay, EzSkinEditorLayer.EzPlayfieldRuleset, new ManiaRuleset().RulesetInfo);
                return lookup != null && lookup.Lookup == GlobalSkinnableContainers.Playfield;
            });
            AddAssert("song select resolves its own container", () =>
            {
                var lookup = EzSkinEditorLayerOptions.ResolveLookup(EzSkinEditorSceneType.SongSelect, EzSkinEditorLayer.EzHud, new ManiaRuleset().RulesetInfo);
                return lookup != null && lookup.Lookup == GlobalSkinnableContainers.SongSelect;
            });
            AddAssert("hud off has no editing target", () =>
                EzSkinEditorLayerOptions.ResolveLookup(EzSkinEditorSceneType.Gameplay, EzSkinEditorLayer.HudOff, new ManiaRuleset().RulesetInfo) == null);

            AddAssert("ruleset layer label shows the ruleset name once", () =>
            {
                var ruleset = new ManiaRuleset().RulesetInfo;
                string label = EzSkinEditorLayerOptions.GetDisplayName(EzSkinEditorLayer.EzHudRuleset, ruleset).ToString();
                return label == EzEditorStrings.LAYER_EZ_HUD_RULESET.ToString().Replace("{0}", ruleset.Name)
                       && !label.Contains("模式")
                       && !label.Contains("mode");
            });
        }

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();

            // 测试会导入并选中的皮肤会留在 SkinManager 里，影响后续用例（例如「内置皮肤下导出应禁用」）。
            // 每个用例都从内置皮肤起步。
            AddStep("reset skin", () => skinManager.CurrentSkinInfo.SetDefault());

            // 色板场景的预览列数由该设置驱动，逐个用例重置以免顺序相关。
            AddStep("reset preview key mode", () => ezConfig.SetValue(Ez2Setting.ColumnTypeListSelect, 4));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Ruleset.Value = new ManiaRuleset().RulesetInfo;
            SkinEditorProviderRegistry.Register(3, () => new EzSkinLNEditorProvider());
        }

        protected override void BackButtonPressed()
        {
            if (editorScreen is { IsLoaded: true })
            {
                editorScreen.ShowExitDialog();
                return;
            }

            if (Stack.CurrentScreen != null)
                Stack.Exit();
        }

        private void loadScreen()
        {
            Child = editorScreen = new EzSkinEditorScreen
            {
                RelativeSizeAxes = Framework.Graphics.Axes.Both,
                RequestClose = () => editorScreen.Expire(),
            };
            editorScreen.Activate();
        }

        private void waitForScreenLoaded() => AddUntilStep("wait for workspace load", () => editorScreen.IsLoaded);

        private void switchScene(EzSkinEditorSceneType scene) => AddStep($"switch to {scene}", () => editorScreen.CurrentScene.Value = scene);

        private void selectSidebarGroup(string groupId) =>
            AddStep($"select group {groupId}", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().SelectSidebarGroupById(groupId));

        private void importLegacySkin() => AddStep("import legacy skin", () =>
        {
            var imported = skinManager.Import(new ImportTask(TestResources.OpenResource(@"Archives/modified-ezSkin.osk"), "modified-ezSkin.osk")).GetResultSafely();
            skinManager.CurrentSkinInfo.Value = imported;
        });

        [Test]
        public void TestLayoutShell()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Appearance);
            selectSidebarGroup(AppearanceSceneStrategy.GROUP_ID_TEXTURE);

            AddUntilStep("menu bar visible", () => editorScreen.ChildrenOfType<EditorMenuBar>().Any());
            AddUntilStep("scene bar visible", () => editorScreen.ChildrenOfType<EzSkinEditorSceneBar>().Any());
            AddUntilStep("sidebar visible", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Any());
            AddUntilStep("sidebar group rail visible", () =>
            {
                var rail = editorScreen.ChildrenOfType<EzSkinEditorSidebarGroupRail>().SingleOrDefault();
                return rail is { Alpha: > 0, DrawHeight: > 1, DrawWidth: > 1 };
            });
            AddUntilStep("scene and sidebar are horizontal", () =>
            {
                // 侧栏占位宽随固定态变化，稳妥地按其内部的导航轨宽度判定。
                var rail = editorScreen.ChildrenOfType<EzSkinEditorSidebarGroupRail>().SingleOrDefault();
                return rail != null && rail.ScreenSpaceDrawQuad.Width > 0;
            });
            AddAssert("no size/colour scene tabs", () =>
                !EzSkinEditorSceneRegistry.All.Any(s => s.SceneType.ToString() is "Size" or "Colour"));
            AddAssert("hud layer present", () => editorScreen.ChildrenOfType<EzSkinEditorHudLayer>().Any());
            AddAssert("song select is first scene", () =>
                EzSkinEditorSceneRegistry.All[0].SceneType == EzSkinEditorSceneType.SongSelect);
        }

        [Test]
        public void TestSceneBarControlsTrailRightEdge()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Gameplay);

            var bar = (EzSkinEditorSceneBar)null!;
            var layer = (EzEditorLayerButton)null!;
            var picker = (EzToggleButton)null!;

            AddStep("capture scene bar controls", () =>
            {
                bar = editorScreen.ChildrenOfType<EzSkinEditorSceneBar>().Single();
                layer = editorScreen.ChildrenOfType<EzEditorLayerButton>().Single();
                picker = editorScreen.ChildrenOfType<EzToggleButton>().Single(b => b.Text.ToString() == EzEditorStrings.TOOLBAR_VISUAL_PICKER.ToString());
            });

            AddUntilStep("controls laid out", () => layer.ScreenSpaceDrawQuad.Width > 0 && picker.ScreenSpaceDrawQuad.Width > 0
                                                        && bar.ScreenSpaceDrawQuad.Width > 0);

            // The layer dropdown is the rightmost control; every later button is inserted to its left.
            AddAssert("layer dropdown hugs the right edge", () =>
                Math.Abs(bar.ScreenSpaceDrawQuad.TopRight.X - layer.ScreenSpaceDrawQuad.TopRight.X) <= 12);
            AddAssert("visual picker sits left of the layer dropdown", () =>
                picker.ScreenSpaceDrawQuad.TopRight.X <= layer.ScreenSpaceDrawQuad.TopLeft.X + 1);
        }

        [Test]
        public void TestAppearanceWorkspaceRails()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Appearance);

            selectSidebarGroup(AppearanceSceneStrategy.GROUP_ID_TEXTURE);
            AddUntilStep("all appearance groups visible", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() >= 5);
            AddAssert("texture group present", () => editorScreen.ChildrenOfType<Dropdown<string>>().Any());
            AddAssert("no skin-specific on appearance group", () =>
                !editorScreen.ChildrenOfType<EzBuiltInSkinScopedSettingsControls>().Any());

            selectSidebarGroup(AppearanceSceneStrategy.GROUP_ID_SIZE);
            AddUntilStep("size group remains visible", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() == 5);

            selectSidebarGroup(AppearanceSceneStrategy.GROUP_ID_COLUMN_COLOURS);
            AddUntilStep("all appearance groups visible", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() >= 5);
        }

        [Test]
        public void TestHudInteractionFollowsSceneAndLayer()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Gameplay);

            var sceneBar = (EzSkinEditorSceneBar)null!;
            var hud = (EzSkinEditorHudLayer)null!;

            AddStep("capture", () =>
            {
                sceneBar = editorScreen.ChildrenOfType<EzSkinEditorSceneBar>().Single();
                hud = editorScreen.ChildrenOfType<EzSkinEditorHudLayer>().Single();
            });

            AddStep("enable hud layer", () => sceneBar.ActiveLayer.Value = EzSkinEditorLayer.EzHud);
            AddUntilStep("gameplay interaction on", () => hud.InteractionEnabled.Value);
            AddUntilStep("hud workspace section", () => editorScreen.ChildrenOfType<EzSkinEditorHudWorkspaceSection>().Any());
            AddUntilStep("no-selection hint", () =>
                editorScreen.ChildrenOfType<OsuSpriteText>()
                            .Any(t => t.Text.ToString() == EzEditorStrings.SIDEBAR_HUD_NO_SELECTION.ToString()));

            switchScene(EzSkinEditorSceneType.Appearance);
            AddUntilStep("appearance interaction off", () => !hud.InteractionEnabled.Value);

            switchScene(EzSkinEditorSceneType.Gameplay);
            AddStep("hide hud layer", () => sceneBar.ActiveLayer.Value = EzSkinEditorLayer.HudOff);
            AddUntilStep("interaction off layer hidden", () => !hud.InteractionEnabled.Value);
            AddUntilStep("layer-off hint", () =>
                editorScreen.ChildrenOfType<OsuSpriteText>()
                            .Any(t => t.Text.ToString() == EzEditorStrings.SIDEBAR_HUD_LAYER_OFF.ToString()));
        }

        [Test]
        public void TestActiveLayerSelectsTargetWithoutChangingVisibility()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Gameplay);

            var sceneBar = (EzSkinEditorSceneBar)null!;
            var hud = (EzSkinEditorHudLayer)null!;

            AddStep("capture", () =>
            {
                sceneBar = editorScreen.ChildrenOfType<EzSkinEditorSceneBar>().Single();
                hud = editorScreen.ChildrenOfType<EzSkinEditorHudLayer>().Single();
            });

            AddStep("select global ez hud", () => sceneBar.ActiveLayer.Value = EzSkinEditorLayer.EzHud);
            AddUntilStep("ez hud resolves the global gameplay lookup", () =>
                EzSkinEditorLayerOptions.ResolveLookup(EzSkinEditorSceneType.Gameplay, EzSkinEditorLayer.EzHud, null) is { } globalLookup
                && globalLookup.Lookup == GlobalSkinnableContainers.MainHUDComponents
                && globalLookup.Ruleset == null);

            AddStep("select ruleset hud layer", () => sceneBar.ActiveLayer.Value = EzSkinEditorLayer.EzHudRuleset);
            AddUntilStep("ruleset layer stays visible but only edits its container", () =>
            {
                var ruleset = new ManiaRuleset().RulesetInfo;
                return hud.LayerVisible.Value
                       && EzSkinEditorLayerOptions.ResolveLookup(EzSkinEditorSceneType.Gameplay, EzSkinEditorLayer.EzHudRuleset, ruleset) is { } rulesetLookup
                       && rulesetLookup.Ruleset != null
                       && rulesetLookup.Lookup == GlobalSkinnableContainers.MainHUDComponents;
            });

            AddStep("select hud off", () => sceneBar.ActiveLayer.Value = EzSkinEditorLayer.HudOff);
            AddUntilStep("layer hidden", () => !hud.LayerVisible.Value);
            AddUntilStep("no editing lookup when hidden", () =>
                EzSkinEditorLayerOptions.ResolveLookup(EzSkinEditorSceneType.Gameplay, EzSkinEditorLayer.HudOff, null) == null);
        }

        [Test]
        public void TestSongSelectSceneResolvesItsOwnContainer()
        {
            AddAssert("song select layer resolves to the song select container", () =>
                EzSkinEditorLayerOptions.ResolveLookup(EzSkinEditorSceneType.SongSelect, EzSkinEditorLayer.EzHud, null) is { } lookup
                && lookup.Lookup == GlobalSkinnableContainers.SongSelect);

            AddAssert("song select hides everything on hud off", () =>
                EzSkinEditorLayerOptions.ResolveLookup(EzSkinEditorSceneType.SongSelect, EzSkinEditorLayer.HudOff, null) == null);

            AddAssert("appearance has no editable hud", () =>
                EzSkinEditorLayerOptions.ResolveLookup(EzSkinEditorSceneType.Appearance, EzSkinEditorLayer.EzHud, null) == null);
        }

        [Test]
        public void TestLayerDropdownClickAppliesLayer()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Gameplay);

            var sceneBar = (EzSkinEditorSceneBar)null!;
            var hud = (EzSkinEditorHudLayer)null!;

            AddStep("capture", () =>
            {
                sceneBar = editorScreen.ChildrenOfType<EzSkinEditorSceneBar>().Single();
                hud = editorScreen.ChildrenOfType<EzSkinEditorHudLayer>().Single();
            });

            AddStep("open layer dropdown", () =>
            {
                var button = editorScreen.ChildrenOfType<EzEditorLayerButton>().Single();
                InputManager.MoveMouseTo(button);
                InputManager.Click(MouseButton.Left);
            });

            AddUntilStep("popover shown", () => editorScreen.ChildrenOfType<PopoverContainer>().Any(p => p.CurrentPopover != null));

            AddStep("click ruleset layer option", () =>
            {
                var options = editorScreen.ChildrenOfType<EzEditorLayerButton.LayerListPopover.LayerSelectButton>().ToArray();
                TestContext.Progress.WriteLine("OPTIONS=" + string.Join(" | ", options.Select(o => o.Text.ToString())));

                string rulesetLayerLabel = EzSkinEditorLayerOptions.GetDisplayName(EzSkinEditorLayer.EzHudRuleset, sceneBar.Ruleset.Value).ToString();
                var option = options.SingleOrDefault(b => b.Text.ToString() == rulesetLayerLabel);
                Assert.That(option, Is.Not.Null, $"ruleset layer option ({rulesetLayerLabel}) not found in popover: " + string.Join(" | ", options.Select(o => o.Text.ToString())));

                InputManager.MoveMouseTo(option!);
                InputManager.PressButton(MouseButton.Left);
            });

            AddStep("release after hold", () => InputManager.ReleaseButton(MouseButton.Left));

            AddUntilStep("layer changed to ruleset", () => sceneBar.ActiveLayer.Value == EzSkinEditorLayer.EzHudRuleset);
            AddUntilStep("layer stays visible", () => hud.LayerVisible.Value);
            AddUntilStep("button text updated", () => editorScreen.ChildrenOfType<EzEditorLayerButton>().Single().Text.ToString()
                                                        == EzSkinEditorLayerOptions.GetDisplayName(EzSkinEditorLayer.EzHudRuleset, sceneBar.Ruleset.Value).ToString());
        }

        [Test]
        public void TestLayerNotForcedOffOnNonHudScene()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            var sceneBar = (EzSkinEditorSceneBar)null!;
            var hud = (EzSkinEditorHudLayer)null!;

            AddStep("capture", () =>
            {
                sceneBar = editorScreen.ChildrenOfType<EzSkinEditorSceneBar>().Single();
                hud = editorScreen.ChildrenOfType<EzSkinEditorHudLayer>().Single();
            });

            // The editor opens on Appearance by default, whose layer list is hidden-only. That must not
            // coerce the active layer to HudOff, or every later HUD scene would start hidden.
            AddAssert("layer not forced off on appearance", () => sceneBar.ActiveLayer.Value != EzSkinEditorLayer.HudOff);
            AddAssert("layer visible on appearance", () => hud.LayerVisible.Value);

            switchScene(EzSkinEditorSceneType.Gameplay);
            AddAssert("layer still visible on gameplay", () => hud.LayerVisible.Value);
            AddAssert("layer not hidden on gameplay", () => sceneBar.ActiveLayer.Value != EzSkinEditorLayer.HudOff);

            AddStep("select global ez hud", () => sceneBar.ActiveLayer.Value = EzSkinEditorLayer.EzHud);
            switchScene(EzSkinEditorSceneType.Appearance);
            switchScene(EzSkinEditorSceneType.Gameplay);
            AddAssert("layer choice preserved across scenes", () => sceneBar.ActiveLayer.Value == EzSkinEditorLayer.EzHud);
        }

        [Test]
        public void TestLibraryGroupsSplitOnModeLayer()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Gameplay);

            var sceneBar = (EzSkinEditorSceneBar)null!;

            AddStep("capture scene bar", () => sceneBar = editorScreen.ChildrenOfType<EzSkinEditorSceneBar>().Single());

            // Invisible sidebar groups create no content, so counting the library sections counts visible ones.
            AddStep("select global layer", () => sceneBar.ActiveLayer.Value = EzSkinEditorLayer.EzHud);
            AddUntilStep("only global library visible", () => visibleLibraryCount() == 1);

            AddStep("select mode layer", () => sceneBar.ActiveLayer.Value = EzSkinEditorLayer.EzHudRuleset);
            AddUntilStep("both libraries visible as separate groups", () => visibleLibraryCount() == 2);

            AddStep("hide hud layer", () => sceneBar.ActiveLayer.Value = EzSkinEditorLayer.HudOff);
            AddUntilStep("no library visible", () => visibleLibraryCount() == 0);
        }

        private int visibleLibraryCount() =>
            editorScreen.ChildrenOfType<EzSkinEditorComponentLibrarySection>().Count(s => s.Parent != null);

        [Test]
        public void TestComponentPickerOpenAndClamped()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Gameplay);

            var picker = (EzSkinEditorComponentPickerWindow)null!;

            AddStep("capture picker", () => picker = editorScreen.ChildrenOfType<EzSkinEditorComponentPickerWindow>().Single());

            AddStep("open picker", () => picker.Open.Value = true);
            AddUntilStep("picker visible", () => picker.Alpha == 1);
            AddUntilStep("picker inside gameplay hole", () =>
            {
                var parent = picker.Parent;
                return parent != null && picker.X >= 0 && picker.X + picker.DrawWidth <= parent.DrawWidth + 0.5f;
            });
            AddUntilStep("picker remembers position", () => ezConfig.Get<float>(Ez2Setting.SkinEditorComponentPickerX) >= 0);

            AddStep("close picker", () => picker.Open.Value = false);
            AddUntilStep("picker hidden", () => picker.Alpha == 0);
        }

        [Test]
        public void TestComponentPickerChromeMatchesSidebarHeightAndFolds()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Gameplay);

            var picker = (EzSkinEditorComponentPickerWindow)null!;

            AddStep("capture picker", () => picker = editorScreen.ChildrenOfType<EzSkinEditorComponentPickerWindow>().Single());
            AddStep("open picker", () => picker.Open.Value = true);
            AddUntilStep("picker visible", () => picker.Alpha == 1);

            AddUntilStep("top edge sits on the gameplay hole's top edge", () =>
            {
                var parent = picker.Parent;

                if (parent == null)
                    return false;

                return Math.Abs(picker.ScreenSpaceDrawQuad.TopLeft.Y - parent.ScreenSpaceDrawQuad.TopLeft.Y) <= 1;
            });

            // Expanded height matches the sidebar: both span the gameplay hole's full height.
            AddUntilStep("expanded fills the gameplay hole height", () =>
            {
                var parent = picker.Parent;
                return parent != null && Math.Abs(picker.DrawHeight - parent.DrawHeight) <= 2;
            });

            float expandedHeight = 0;
            float topEdge = 0;
            AddStep("record expanded height and top edge", () =>
            {
                expandedHeight = picker.DrawHeight;
                topEdge = picker.ScreenSpaceDrawQuad.TopLeft.Y;
            });

            AddStep("collapse", () => picker.Expanded.Value = false);

            AddUntilStep("collapsed to the title bar", () => picker.DrawHeight < expandedHeight);

            // Collapsing folds the body up: the header (and thus the top edge) must not move.
            AddAssert("top edge unchanged while collapsing", () => Math.Abs(picker.ScreenSpaceDrawQuad.TopLeft.Y - topEdge) <= 1);

            AddStep("expand again", () => picker.Expanded.Value = true);
            AddUntilStep("expands back to full height", () => Math.Abs(picker.DrawHeight - expandedHeight) <= 1);
            AddAssert("top edge unchanged while expanding", () => Math.Abs(picker.ScreenSpaceDrawQuad.TopLeft.Y - topEdge) <= 1);
        }

        [Test]
        public void TestComponentPickerRestoresRememberedPosition()
        {
            AddStep("seed remembered position at right edge", () =>
                ezConfig.GetBindable<float>(Ez2Setting.SkinEditorComponentPickerX).Value = 1f);

            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Gameplay);

            var picker = (EzSkinEditorComponentPickerWindow)null!;

            AddStep("capture picker", () => picker = editorScreen.ChildrenOfType<EzSkinEditorComponentPickerWindow>().Single());
            AddStep("open picker", () => picker.Open.Value = true);

            // Regression: the position was applied before the parent was sized, so free travel resolved
            // to 0 and the window stuck to the left instead of the remembered right edge.
            AddUntilStep("picker restored to remembered right edge", () =>
            {
                var parent = picker.Parent;

                if (parent == null || parent.DrawWidth <= 0)
                    return false;

                var sidebar = editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single();
                float expectedRight = parent.DrawWidth - sidebar.VisiblePanelReserve;
                return Math.Abs(picker.X + picker.DrawWidth - expectedRight) <= 1;
            });
        }

        [Test]
        public void TestComponentPickerTravelSpansScreenToSidebar()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Gameplay);

            var picker = (EzSkinEditorComponentPickerWindow)null!;

            AddStep("capture picker", () => picker = editorScreen.ChildrenOfType<EzSkinEditorComponentPickerWindow>().Single());
            AddStep("open picker", () => picker.Open.Value = true);
            AddUntilStep("picker visible", () => picker.Alpha == 1);

            // Left limit is the parent's left edge (no gap), right limit stops at the sidebar panel.
            AddStep("push hard left", () => picker.X = -50);
            AddStep("drag to flush left", () =>
            {
                InputManager.MoveMouseTo(picker.ScreenSpaceDrawQuad.TopLeft + new osuTK.Vector2(picker.DrawWidth / 2, 10));
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(picker.ScreenSpaceDrawQuad.TopLeft + new osuTK.Vector2(-10, 10));
                InputManager.ReleaseButton(MouseButton.Left);
            });

            AddUntilStep("left edge flushes the screen edge", () => Math.Abs(picker.X) <= 1);

            AddStep("push hard right", () => picker.X = 5000);
            AddUntilStep("right edge stops at the sidebar panel", () =>
            {
                var sidebar = editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single();
                var parent = picker.Parent!;
                return Math.Abs(picker.X - (parent.DrawWidth - sidebar.VisiblePanelReserve - picker.DrawWidth)) <= 1;
            });
        }

        [Test]
        public void TestComponentPickerDragsHorizontally()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Gameplay);

            var picker = (EzSkinEditorComponentPickerWindow)null!;

            AddStep("capture picker", () => picker = editorScreen.ChildrenOfType<EzSkinEditorComponentPickerWindow>().Single());
            AddStep("open picker", () => picker.Open.Value = true);
            AddUntilStep("picker visible", () => picker.Alpha == 1);

            float before = 0;

            AddStep("park picker at left edge", () => picker.X = before = 0);

            AddStep("grab header", () =>
            {
                // A drag starts on the header, so grab a point in the top strip.
                InputManager.MoveMouseTo(picker.ScreenSpaceDrawQuad.TopLeft + new osuTK.Vector2(picker.DrawWidth / 2, 10));
                InputManager.PressButton(MouseButton.Left);
            });

            AddStep("drag beyond the click threshold", () =>
            {
                InputManager.MoveMouseTo(picker.ScreenSpaceDrawQuad.TopLeft + new osuTK.Vector2(picker.DrawWidth / 2 + 120, 10));
            });

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));

            // Regression: the picker handled OnMouseDown/OnDrag but never OnDragStart, so the framework
            // never entered a drag and the window could not be moved horizontally at all.
            AddUntilStep("picker followed the pointer horizontally", () => picker.X > before + 20);
        }

        [Test]
        public void TestLibraryPlacementRequiresBoundTarget()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Gameplay);

            var hud = (EzSkinEditorHudLayer)null!;

            AddStep("capture hud", () => hud = editorScreen.ChildrenOfType<EzSkinEditorHudLayer>().Single());
            AddStep("select global layer", () => editorScreen.ChildrenOfType<EzSkinEditorSceneBar>().Single().ActiveLayer.Value = EzSkinEditorLayer.EzHud);

            // No real screen is presented in this bare test, so no Ez layout container is bound.
            AddAssert("no active target without bound layout", () => hud.ActiveTarget == null);
            AddAssert("placement gracefully refused", () =>
                !hud.TryPlaceComponent(SerialisedDrawableInfo.GetAllAvailableDrawables(null).First()));
        }

        [Test]
        public void TestSizeWorkspaceColumnWidthKeepsSnapshotBaseline()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Appearance);
            selectSidebarGroup(AppearanceSceneStrategy.GROUP_ID_SIZE);

            AddUntilStep("size settings visible", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Any());

            double baselineWidth = 0;
            AddStep("read snapshot baseline", () => baselineWidth = editorScreen.ComparisonSnapshotForTesting.NotePreviewWidth);

            double originalWidth = 0;
            double targetWidth = 0;

            AddStep("change column width", () =>
            {
                originalWidth = ezConfig.Get<double>(Ez2Setting.ColumnWidth);
                targetWidth = originalWidth + 25;
                ezConfig.GetBindable<double>(Ez2Setting.ColumnWidth).Value = targetWidth;
            });

            AddUntilStep("live value kept", () => ezConfig.Get<double>(Ez2Setting.ColumnWidth) == targetWidth);
            AddAssert("snapshot baseline unchanged", () => editorScreen.ComparisonSnapshotForTesting.NotePreviewWidth == baselineWidth);
            AddStep("restore column width", () => ezConfig.GetBindable<double>(Ez2Setting.ColumnWidth).Value = originalWidth);
        }

        [Test]
        public void TestSkinSpecificScenePlaceholder()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.SkinSpecific);

            AddUntilStep("placeholder text", () =>
                editorScreen.ChildrenOfType<OsuSpriteText>()
                            .Any(t => t.Text.ToString() == EzEditorStrings.SKIN_SPECIFIC_SCENE_PLACEHOLDER.ToString()));
            AddAssert("no scoped settings on skin-specific scene", () =>
                !editorScreen.ChildrenOfType<EzBuiltInSkinScopedSettingsControls>().Any());
        }

        [Test]
        public void TestSkinIniScenePlaybackAndSaveFooter()
        {
            importLegacySkin();
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.SkinIni);

            AddUntilStep("save footer button", () => editorScreen.ChildrenOfType<OsuButton>().Any(b => b.Text.ToString() == EzEditorStrings.SKIN_INI_SAVE_BUTTON.ToString()));
            AddUntilStep("skin.ini pages", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().PageCount == 3);
            AddUntilStep("all skin.ini groups visible", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() == 3);
            AddUntilStep("skin.ini form fields visible", () => editorScreen.ChildrenOfType<SettingsTextBox>().Any());
            AddAssert("no placeholder copy", () => editorScreen.ChildrenOfType<OsuSpriteText>().All(t => t.Text.ToString().Contains("M3 将在此") != true));
        }

        [Test]
        public void TestSkinIniFooterDoesNotOverlapLastField()
        {
            importLegacySkin();
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.SkinIni);

            AddUntilStep("all skin.ini groups loaded", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() == 3);

            AddAssert("footer reserve height applied", () =>
            {
                var sidebar = editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single();
                return sidebar.FooterReservedHeight == EzSkinEditorSidebar.FOOTER_HEIGHT;
            });

            AddAssert("scroll content bottom padding applied", () =>
            {
                var sidebar = editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single();
                return sidebar.ContentBottomPadding == EzSkinEditorSidebar.FOOTER_HEIGHT;
            });
        }

        [Test]
        public void TestSkinIniEditorUpdatesDraft()
        {
            importLegacySkin();
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.SkinIni);

            AddUntilStep("session loaded", () => editorScreen.SkinIniSession != null);

            AddStep("update name via document", () =>
            {
                var document = editorScreen.SkinIniSession!.ParseDraftDocument();
                document.SetValue(EzSkinIniDocument.GENERAL_SECTION, "Name", "M3 Draft Skin");
                editorScreen.SkinIniSession.ApplyDocument(document);
            });

            AddAssert("draft dirty", () => editorScreen.SkinIniSession!.IsDirty);
            AddAssert("draft contains updated name", () => editorScreen.SkinIniSession!.DraftText.Contains("Name: M3 Draft Skin", StringComparison.Ordinal));
        }

        [Test]
        public void TestApplySavesEzConfig()
        {
            const double target_alpha = 0.42;

            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            AddStep("change hit target alpha", () => ezConfig.GetBindable<double>(Ez2Setting.HitTargetAlpha).Value = target_alpha);
            AddStep("apply settings", () => editorScreen.ApplySettings());

            AddAssert("bindable retained", () => ezConfig.GetBindable<double>(Ez2Setting.HitTargetAlpha).Value, () => Is.EqualTo(target_alpha));
            AddAssert("EzSkinSettings.ini written", () => storage.Exists("EzSkinSettings.ini"));
        }

        [Test]
        public void TestSkinIniSessionLoadSave()
        {
            importLegacySkin();
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.SkinIni);

            AddUntilStep("session loaded", () => editorScreen.SkinIniSession != null);

            string savedText = null!;
            string dirtyText = null!;

            AddStep("capture saved text", () =>
            {
                savedText = editorScreen.SkinIniSession!.SavedText;
                dirtyText = savedText + "\n; m2 test marker";
            });

            AddStep("set dirty draft", () => editorScreen.SkinIniSession!.SetDraftText(dirtyText));
            AddAssert("session dirty", () => editorScreen.SkinIniSession!.IsDirty);

            AddStep("discard draft", () => editorScreen.SkinIniSession!.Discard());
            AddAssert("session clean after discard", () => !editorScreen.SkinIniSession!.IsDirty);
            AddAssert("draft restored", () => editorScreen.SkinIniSession!.DraftText, () => Is.EqualTo(savedText));

            AddUntilStep("session bound to imported skin", () => editorScreen.SkinIniSession!.SkinId == skinManager.CurrentSkinInfo.Value.ID);

            AddStep("set dirty draft again", () => editorScreen.SkinIniSession!.SetDraftText(dirtyText));
            AddStep("commit draft", () => editorScreen.SkinIniSession!.Commit());
            AddAssert("session clean after commit", () => !editorScreen.SkinIniSession!.IsDirty);
            AddAssert("saved text updated", () => editorScreen.SkinIniSession!.SavedText, () => Is.EqualTo(dirtyText));
            AddAssert("backup filename recorded", () => editorScreen.SkinIniSession!.LastBackupFilename!.StartsWith("Backup/skin.ini.", StringComparison.Ordinal));
            AddAssert("backup file stored on skin",
                () => skinManager.CurrentSkinInfo.Value.PerformRead(skin => skin.Files.Any(file => file.Filename.StartsWith("Backup/skin.ini.", StringComparison.Ordinal))));
        }

        [Test]
        public void TestSkinIniSessionPerSkin()
        {
            importLegacySkin();
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.SkinIni);

            AddUntilStep("session loaded", () => editorScreen.SkinIniSession != null);

            Guid originalSkinId = Guid.Empty;

            AddStep("capture skin id and dirty draft", () =>
            {
                originalSkinId = editorScreen.SkinIniSession!.SkinId;
                editorScreen.SkinIniSession.SetDraftText(editorScreen.SkinIniSession.SavedText + "\n; per-skin leak test");
            });

            AddStep("switch skin", () => skinManager.SetSkinFromConfiguration(SkinInfo.TRIANGLES_SKIN.ToString()));
            AddUntilStep("session cleared for built-in skin", () => editorScreen.SkinIniSession == null);
        }

        [Test]
        public void TestSkinIniUnavailableOnBuiltInSkin()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            AddAssert("no skin.ini session on built-in skin", () => editorScreen.SkinIniSession == null);

            AddStep("attempt skin.ini scene", () => editorScreen.CurrentScene.Value = EzSkinEditorSceneType.SkinIni);
            AddAssert("redirected away from skin.ini", () => editorScreen.CurrentScene.Value == EzSkinEditorSceneType.Appearance);
        }

        [Test]
        public void TestExitDialogSkinIniDirty()
        {
            importLegacySkin();
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.SkinIni);

            AddUntilStep("session loaded", () => editorScreen.SkinIniSession != null);

            AddStep("set dirty draft", () => editorScreen.SkinIniSession!.SetDraftText(editorScreen.SkinIniSession.SavedText + "\n; exit test"));
            AddStep("show exit dialog", () => editorScreen.ShowExitDialog());
            AddUntilStep("dialog visible", () => DialogOverlay.CurrentDialog is ConfirmDialog);

            AddStep("discard via cancel", () =>
            {
                InputManager.MoveMouseTo(DialogOverlay.CurrentDialog!.ChildrenOfType<PopupDialogButton>().Last());
                InputManager.Click(MouseButton.Left);
            });

            AddUntilStep("workspace closed", () => !editorScreen.IsAlive);
            AddAssert("session clean after discard exit", () => !editorScreen.SkinIniSession!.IsDirty);
        }

        [Test]
        public void TestExitDialogSkinIniApplyOnLegacySkin()
        {
            const string marker = "; exit apply test";

            importLegacySkin();
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.SkinIni);

            AddUntilStep("session loaded", () => editorScreen.SkinIniSession is { IsSupported: true });

            string dirtyText = null!;

            AddStep("set dirty draft", () =>
            {
                dirtyText = editorScreen.SkinIniSession!.SavedText + marker;
                editorScreen.SkinIniSession.SetDraftText(dirtyText);
            });

            AddStep("show exit dialog", () => editorScreen.ShowExitDialog());
            AddUntilStep("dialog visible", () => DialogOverlay.CurrentDialog is ConfirmDialog);

            AddStep("apply via confirm", () =>
            {
                InputManager.MoveMouseTo(DialogOverlay.CurrentDialog!.ChildrenOfType<PopupDialogButton>().First());
                InputManager.Click(MouseButton.Left);
            });

            AddUntilStep("workspace closed", () => !editorScreen.IsAlive);
            AddAssert("session clean after apply exit", () => !editorScreen.SkinIniSession!.IsDirty);
            AddAssert("saved text contains marker", () => editorScreen.SkinIniSession!.SavedText.Contains(marker, StringComparison.Ordinal));
        }

        [Test]
        public void TestSidebarPinSwitchVisibleAcrossScenes()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            AddUntilStep("pin switch shown on initial scene", () =>
                editorScreen.ChildrenOfType<SettingsCheckbox>()
                            .Any(c => c.LabelText.ToString() == EzEditorStrings.SIDEBAR_PIN_LABEL.ToString()));

            // 切场景会重建侧栏分组，固定开关不能随旧分组一起消失。
            switchScene(EzSkinEditorSceneType.Gameplay);
            AddUntilStep("pin switch still shown on gameplay", () =>
                editorScreen.ChildrenOfType<SettingsCheckbox>()
                            .Any(c => c.LabelText.ToString() == EzEditorStrings.SIDEBAR_PIN_LABEL.ToString()));

            switchScene(EzSkinEditorSceneType.Note);
            AddUntilStep("pin switch still shown on note", () =>
                editorScreen.ChildrenOfType<SettingsCheckbox>()
                            .Any(c => c.LabelText.ToString() == EzEditorStrings.SIDEBAR_PIN_LABEL.ToString()));
        }

        [Test]
        public void TestSidebarCollapse()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            // 取消固定后侧栏悬浮不占位，先落到这个状态再取基线。
            AddStep("unpin sidebar", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().Pinned.Value = false);

            float gameplayWidthBefore = 0;
            AddStep("capture gameplay width", () =>
                gameplayWidthBefore = editorScreen.ChildrenOfType<EzSkinEditorHudLayer>().Single().DrawWidth);

            AddStep("collapse sidebar", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().ExpandedState.Value = false);

            // 未固定时侧栏不占位：收起后设置面板隐藏，导航轨仍在，游戏区宽度不变。
            AddUntilStep("content panel hidden", () =>
            {
                var sidebar = editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single();
                return sidebar.ExpandedState.Value == false
                       && sidebar.DrawWidth <= EzSkinEditorSidebarGroupRail.WIDTH + 1;
            });
            AddAssert("rail still present", () =>
                editorScreen.ChildrenOfType<EzSkinEditorSidebarGroupRail>().Single().IsPresent);
            AddAssert("gameplay width unchanged", () =>
            {
                float after = editorScreen.ChildrenOfType<EzSkinEditorHudLayer>().Single().DrawWidth;
                return Math.Abs(after - gameplayWidthBefore) < 1;
            });
        }

        [Test]
        public void TestPinnedSidebarReservesSceneWidth()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Gameplay);

            var sidebar = (EzSkinEditorSidebar)null!;

            AddStep("capture sidebar", () => sidebar = editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single());

            // 固定开启：侧栏挤占场景可用空间，ReservedWidth 为面板+轨宽。
            AddUntilStep("pinned reserves panel width", () =>
                sidebar.Pinned.Value
                && Math.Abs(sidebar.ReservedWidth.Value - EzSkinEditorSidebar.ExpandedWidthWithRail) < 1);

            // 固定时设置面板必须让出导航轨位置，不能和轨叠在一起（也不越过屏幕右缘）。
            AddUntilStep("settings panel clears the rail", () =>
            {
                var rail = editorScreen.ChildrenOfType<EzSkinEditorSidebarGroupRail>().Single();
                var panel = editorScreen.ChildrenOfType<OsuScrollContainer>()
                                       .FirstOrDefault(s => s.Parent is Container c && c.Width == EzSkinEditorSidebar.CONTENT_WIDTH);

                return panel != null
                       && rail.ScreenSpaceDrawQuad.Width > 0
                       && panel.ScreenSpaceDrawQuad.TopRight.X <= rail.ScreenSpaceDrawQuad.TopLeft.X + 1;
            });

            // 取消固定：侧栏浮在场景之上，不再挤占空间。
            AddStep("unpin sidebar", () => sidebar.Pinned.Value = false);
            AddUntilStep("unpinned reserves nothing", () => sidebar.ReservedWidth.Value == 0);
        }

        [Test]
        public void TestSidebarHoverExpansionFollowsScene()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            var sidebar = (EzSkinEditorSidebar)null!;
            var rail = (EzSkinEditorSidebarGroupRail)null!;

            AddStep("capture sidebar", () =>
            {
                sidebar = editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single();
                rail = editorScreen.ChildrenOfType<EzSkinEditorSidebarGroupRail>().Single();
            });
            AddStep("unpin sidebar", () => sidebar.Pinned.Value = false);

            // 非 HUD 场景保持原样：收起后悬停导航轨唤出设置面板。
            switchScene(EzSkinEditorSceneType.Appearance);
            AddStep("collapse sidebar", () => sidebar.ExpandedState.Value = false);
            AddStep("hover rail", () => InputManager.MoveMouseTo(rail));
            AddUntilStep("appearance hover expands", () => sidebar.ExpandedState.Value);

            // HUD 场景悬停不唤出，避免面板盖住正在拖拽组件的游玩区。
            switchScene(EzSkinEditorSceneType.Gameplay);
            AddStep("collapse sidebar", () => sidebar.ExpandedState.Value = false);
            AddStep("hover rail", () => InputManager.MoveMouseTo(rail));
            AddWaitStep("wait for hover ticks", 5);
            AddAssert("hud hover does not expand", () => !sidebar.ExpandedState.Value);
        }

        [Test]
        public void TestTopToolbarControls()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            AddUntilStep("top toolbar visible", () => editorScreen.ChildrenOfType<EzSkinEditorTopToolbar>().Any());
            AddUntilStep("skin dropdown visible", () => editorScreen.ChildrenOfType<EzSkinEditorSkinDropdown>().Any());
            AddUntilStep("beatmap menu button visible", () => editorScreen.ChildrenOfType<EzSkinEditorBeatmapMenuButton>().Any());
            AddAssert("preview source resolved", () => editorScreen.PreviewSource.Value is EzSkinEditorPreviewSource.Static or EzSkinEditorPreviewSource.Beatmap);
        }

        [Test]
        public void TestConfigSnapshotRestore()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            double baseline = 0;
            AddStep("read baseline", () => baseline = ezConfig.Get<double>(Ez2Setting.ColumnWidth));

            AddStep("change column width", () => ezConfig.GetBindable<double>(Ez2Setting.ColumnWidth).Value = baseline + 30);
            AddAssert("width changed", () => ezConfig.Get<double>(Ez2Setting.ColumnWidth) == baseline + 30);

            AddStep("restore snapshot", () => editorScreen.RestoreConfigSnapshotForTesting());
            AddAssert("width restored", () => ezConfig.Get<double>(Ez2Setting.ColumnWidth) == baseline);
        }

        [Test]
        public void TestCreateConfigSnapshotUpdatesBaseline()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            double updated = 0;
            AddStep("change and capture", () =>
            {
                updated = ezConfig.Get<double>(Ez2Setting.ColumnWidth) + 15;
                ezConfig.GetBindable<double>(Ez2Setting.ColumnWidth).Value = updated;
                editorScreen.CreateConfigSnapshotForTesting();
            });

            AddStep("change again", () => ezConfig.GetBindable<double>(Ez2Setting.ColumnWidth).Value = updated + 20);
            AddStep("restore snapshot", () => editorScreen.RestoreConfigSnapshotForTesting());
            AddAssert("restored to captured value", () => ezConfig.Get<double>(Ez2Setting.ColumnWidth) == updated);
        }

        [Test]
        public void TestSkinIniConfigSnapshotRestore()
        {
            importLegacySkin();
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.SkinIni);

            AddUntilStep("session loaded", () => editorScreen.SkinIniSession != null);

            string baselineName = null!;
            AddStep("read baseline name", () =>
            {
                var document = editorScreen.SkinIniSession!.ParseDraftDocument();
                baselineName = document.GetValue(EzSkinIniDocument.GENERAL_SECTION, "Name") ?? string.Empty;
            });

            AddStep("change name", () =>
            {
                var document = editorScreen.SkinIniSession!.ParseDraftDocument();
                document.SetValue(EzSkinIniDocument.GENERAL_SECTION, "Name", "Snapshot Test Name");
                editorScreen.SkinIniSession.ApplyDocument(document);
            });

            AddAssert("name changed", () =>
            {
                var document = editorScreen.SkinIniSession!.ParseDraftDocument();
                return (document.GetValue(EzSkinIniDocument.GENERAL_SECTION, "Name") ?? string.Empty) == "Snapshot Test Name";
            });

            AddStep("restore snapshot", () => editorScreen.RestoreConfigSnapshotForTesting());
            AddAssert("name restored", () =>
            {
                var document = editorScreen.SkinIniSession!.ParseDraftDocument();
                return (document.GetValue(EzSkinIniDocument.GENERAL_SECTION, "Name") ?? string.Empty) == baselineName;
            });
        }

        [Test]
        public void TestNoteSceneWorkspace()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Note);

            // The Note preview host is hosted by the plugged screen, not the workspace tree.
            AddUntilStep("note sidebar pages", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().PageCount == 2);
            AddUntilStep("all note groups visible", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() == 2);
            AddAssert("mania note profile registered", () => EzSkinEditorNoteRulesetProfileRegistry.Get(3) != null);
        }

        [Test]
        public void TestNoteSnapshotRestore()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Note);

            AddStep("change variant", () => editorScreen.NoteSessionForTesting.VariantId.Value = "2");
            AddStep("create note snapshot", () => editorScreen.CreateNoteSnapshotForTesting());
            AddStep("change variant again", () => editorScreen.NoteSessionForTesting.VariantId.Value = "S");
            AddStep("restore note snapshot", () => editorScreen.RestoreNoteSnapshotForTesting());
            AddAssert("variant restored", () => editorScreen.NoteSessionForTesting.VariantId.Value == "2");
        }

        [Test]
        public void TestStaticPreviewAfterSkinSwitch()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            importLegacySkin();

            AddUntilStep("workspace still present", () => editorScreen.IsLoaded);
            AddAssert("still static preview", () => editorScreen.PreviewSource.Value == EzSkinEditorPreviewSource.Static);
        }

        [Test]
        public void TestConfigMenuPresent()
        {
            importLegacySkin();
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            AddUntilStep("config menu items", () =>
            {
                var menuBar = editorScreen.ChildrenOfType<EzSkinEditorMenuBar>().SingleOrDefault();
                return menuBar != null
                       && menuBar.Items.Any(m => m.Text.ToString() == EzEditorStrings.MENU_CONFIG.ToString());
            });
        }

        [Test]
        public void TestCreateEzSkinJsonWritesFile()
        {
            importLegacySkin();
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            AddUntilStep("json session ready", () => editorScreen.SkinJsonSession is { HasFile: false });

            AddStep("create EzSkin.json", () => editorScreen.CreateEzSkinJsonForTesting());
            AddAssert("has file", () => editorScreen.SkinJsonSession!.HasFile);
            AddAssert("json stored on skin",
                () => skinManager.CurrentSkinInfo.Value.PerformRead(skin => skin.Files.Any(f => f.Filename == EzSkinJsonDocument.FILENAME)));
        }

        [Test]
        public void TestApplyWithoutJsonFileDoesNotWriteSkinJson()
        {
            importLegacySkin();
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            AddUntilStep("no json file", () => editorScreen.SkinJsonSession is { HasFile: false });

            AddStep("change setting and apply", () =>
            {
                ezConfig.GetBindable<double>(Ez2Setting.HitTargetAlpha).Value = 0.33;
                editorScreen.ApplySettings();
            });

            AddAssert("still no skin json file",
                () => skinManager.CurrentSkinInfo.Value.PerformRead(skin => skin.Files.All(f => f.Filename != EzSkinJsonDocument.FILENAME)));
        }

        [Test]
        public void TestApplyDoesNotCommitSkinJsonSnapshot()
        {
            const double dirty_alpha = 0.37;

            importLegacySkin();
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            AddStep("create EzSkin.json", () => editorScreen.CreateEzSkinJsonForTesting());

            string snapshotBeforeApply = null!;

            AddStep("capture snapshot and dirty memory", () =>
            {
                snapshotBeforeApply = editorScreen.SkinJsonSession!.IsDirty(ezConfig)
                    ? string.Empty
                    : EzSkinJsonBridge.CreateNormalizedSnapshot(ezConfig);

                ezConfig.GetBindable<double>(Ez2Setting.HitTargetAlpha).Value = dirty_alpha;
            });

            AddAssert("memory dirty vs snapshot file", () => editorScreen.SkinJsonSession!.IsDirty(ezConfig));

            AddStep("apply settings", () => editorScreen.ApplySettings());

            AddAssert("bindable retained in memory", () => ezConfig.GetBindable<double>(Ez2Setting.HitTargetAlpha).Value, () => Is.EqualTo(dirty_alpha));
            AddAssert("skin json snapshot unchanged",
                () => skinManager.CurrentSkinInfo.Value.PerformRead(skin =>
                {
                    string? json = EzSkinJsonStorage.TryReadJson(skinManager, skin);
                    return json != null && !json.Contains(dirty_alpha.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
                }));
        }

        [Test]
        public void TestWriteSizesToSkinIniUpdatesDraft()
        {
            importLegacySkin();
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            AddUntilStep("skin.ini session loaded", () => editorScreen.SkinIniSession != null);

            AddStep("change column width", () => ezConfig.GetBindable<double>(Ez2Setting.ColumnWidth).Value = 99);
            AddStep("write sizes to skin.ini draft", () => editorScreen.WriteSizesToSkinIniForTesting());

            AddAssert("draft dirty", () => editorScreen.SkinIniSession!.IsDirty);
            AddAssert("draft contains column width", () => editorScreen.SkinIniSession!.DraftText.Contains("ColumnWidth:", StringComparison.Ordinal));
        }

        [Test]
        public void TestExportOskMenuPresent()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            AddAssert("export osk menu present", () =>
            {
                var menuBar = editorScreen.ChildrenOfType<EzSkinEditorMenuBar>().Single();
                var fileMenu = menuBar.Items.First(m => m.Text.ToString() == CommonStrings.MenuBarFile.ToString());
                return fileMenu.Items.OfType<EditorMenuItem>().Any(i => i.Text.ToString() == EzEditorStrings.MENU_EXPORT_OSK.ToString());
            });
            AddAssert("export disabled on built-in skin", () =>
            {
                var menuBar = editorScreen.ChildrenOfType<EzSkinEditorMenuBar>().Single();
                var fileMenu = menuBar.Items.First(m => m.Text.ToString() == CommonStrings.MenuBarFile.ToString());
                return fileMenu.Items.OfType<EditorMenuItem>().First(i => i.Text.ToString() == EzEditorStrings.MENU_EXPORT_OSK.ToString()).Action.Disabled;
            });
        }

        [Test]
        public void TestSkinScopedRailHiddenOnDefaultSkin()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Appearance);
            selectSidebarGroup(AppearanceSceneStrategy.GROUP_ID_TEXTURE);

            AddUntilStep("all appearance groups visible", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() >= 5);
            AddAssert("scoped group not selected", () =>
                editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().SelectedGroupId.Value
                != AppearanceSceneStrategy.GROUP_ID_SKIN_SCOPED);
            AddAssert("no skin-specific controls on appearance", () =>
                !editorScreen.ChildrenOfType<EzBuiltInSkinScopedSettingsControls>().Any());
        }

        [Test]
        public void TestSkinScopedRailEzStylePro() =>
            runSkinScopedRailTest(
                EzStyleProSkin.CreateInfo().ID,
                EzSkinEditorSkinKind.EzStylePro,
                expectHitTarget: true);

        [Test]
        public void TestSkinScopedRailEz2() =>
            runSkinScopedRailTest(
                Ez2Skin.CreateInfo().ID,
                EzSkinEditorSkinKind.Ez2,
                expectHitTarget: true);

        [Test]
        public void TestSkinScopedRailSbI() =>
            runSkinScopedRailTest(
                SbISkin.CreateInfo().ID,
                EzSkinEditorSkinKind.SbI,
                expectHitTarget: false);

        private void runSkinScopedRailTest(Guid skinId, EzSkinEditorSkinKind kind, bool expectHitTarget)
        {
            AddStep("select built-in skin", () =>
            {
                var live = skinManager.Query(s => s.ID == skinId);
                Assert.That(live, Is.Not.Null);
                skinManager.CurrentSkinInfo.Value = live!;
            });
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Appearance);
            selectSidebarGroup(AppearanceSceneStrategy.GROUP_ID_SKIN_SCOPED);

            AddUntilStep("scoped controls loaded", () =>
                editorScreen.ChildrenOfType<EzBuiltInSkinScopedSettingsControls>().Any(c => c.Kind == kind));
            AddAssert("hit-target controls", () =>
                editorScreen.ChildrenOfType<SettingsSlider<double>>()
                            .Any(s => s.LabelText.ToString() == EzSkinStrings.HIT_TARGET_ALPHA.ToString()) == expectHitTarget);

            selectSidebarGroup(AppearanceSceneStrategy.GROUP_ID_TEXTURE);
            AddAssert("texture group selected", () =>
                editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().SelectedGroupId.Value == AppearanceSceneStrategy.GROUP_ID_TEXTURE);
            AddAssert("scoped controls remain visible", () =>
                editorScreen.ChildrenOfType<EzBuiltInSkinScopedSettingsControls>().Any(c => c.Kind == kind));
        }
    }
}
