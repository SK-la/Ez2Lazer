// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
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

            AddAssert("gameplay targets main hud", () =>
                EzSkinEditorSceneRegistry.ResolveLayoutTarget(EzSkinEditorSceneType.Gameplay) == GlobalSkinnableContainers.MainHUDComponents);
            AddAssert("song select targets song select layout", () =>
                EzSkinEditorSceneRegistry.ResolveLayoutTarget(EzSkinEditorSceneType.SongSelect) == GlobalSkinnableContainers.SongSelect);
            AddAssert("appearance has no layout target", () =>
                EzSkinEditorSceneRegistry.ResolveLayoutTarget(EzSkinEditorSceneType.Appearance) == null);
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
                var sidebar = editorScreen.ChildrenOfType<EzSkinEditorSidebar>().SingleOrDefault();
                return sidebar != null && sidebar.ScreenSpaceDrawQuad.Width > 0;
            });
            AddAssert("no size/colour scene tabs", () =>
                !EzSkinEditorSceneRegistry.All.Any(s => s.SceneType.ToString() is "Size" or "Colour"));
            AddAssert("hud layer present", () => editorScreen.ChildrenOfType<EzSkinEditorHudLayer>().Any());
            AddAssert("song select is first scene", () =>
                EzSkinEditorSceneRegistry.All[0].SceneType == EzSkinEditorSceneType.SongSelect);
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

            AddStep("enable hud layer", () => sceneBar.HudLayerVisible.Value = true);
            AddUntilStep("gameplay interaction on", () => hud.InteractionEnabled.Value);
            AddUntilStep("hud workspace section", () => editorScreen.ChildrenOfType<EzSkinEditorHudWorkspaceSection>().Any());
            AddUntilStep("no-selection hint", () =>
                editorScreen.ChildrenOfType<OsuSpriteText>()
                            .Any(t => t.Text.ToString() == EzEditorStrings.SIDEBAR_HUD_NO_SELECTION.ToString()));

            switchScene(EzSkinEditorSceneType.Appearance);
            AddUntilStep("appearance interaction off", () => !hud.InteractionEnabled.Value);

            switchScene(EzSkinEditorSceneType.Gameplay);
            AddStep("disable hud layer", () => sceneBar.HudLayerVisible.Value = false);
            AddUntilStep("interaction off layer hidden", () => !hud.InteractionEnabled.Value);
            AddUntilStep("layer-off hint", () =>
                editorScreen.ChildrenOfType<OsuSpriteText>()
                            .Any(t => t.Text.ToString() == EzEditorStrings.SIDEBAR_HUD_LAYER_OFF.ToString()));
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
        public void TestSidebarCollapse()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            float gameplayWidthBefore = 0;
            AddStep("capture gameplay width", () =>
                gameplayWidthBefore = editorScreen.ChildrenOfType<EzSkinEditorHudLayer>().Single().DrawWidth);

            AddStep("unpin sidebar", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().Pinned.Value = false);
            AddStep("collapse sidebar", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().ExpandedState.Value = false);

            // 布局只占轨宽；收起只藏设置面板，游戏区宽度与导航轨均不变。
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
        public void TestSidebarHoverExpansionFollowsScene()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            var sidebar = (EzSkinEditorSidebar)null!;

            AddStep("capture sidebar", () => sidebar = editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single());
            AddStep("unpin sidebar", () => sidebar.Pinned.Value = false);

            // 非 HUD 场景保持原样：收起后悬停唤出设置面板。
            switchScene(EzSkinEditorSceneType.Appearance);
            AddStep("collapse sidebar", () => sidebar.ExpandedState.Value = false);
            AddStep("hover sidebar", () => InputManager.MoveMouseTo(sidebar));
            AddUntilStep("appearance hover expands", () => sidebar.ExpandedState.Value);

            // HUD 场景悬停不唤出，避免面板盖住正在拖拽组件的游玩区。
            switchScene(EzSkinEditorSceneType.Gameplay);
            AddStep("collapse sidebar", () => sidebar.ExpandedState.Value = false);
            AddStep("hover sidebar", () => InputManager.MoveMouseTo(sidebar));
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
