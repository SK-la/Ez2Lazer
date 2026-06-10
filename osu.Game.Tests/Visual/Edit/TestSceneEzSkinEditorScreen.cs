// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.EzMania.Editor;
using osu.Game.Screens.Edit.Components.Menus;
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

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Ruleset.Value = new ManiaRuleset().RulesetInfo;
            SkinEditorProviderRegistry.Register(3, () => new EzSkinLNEditorProvider());
        }

        protected override void BackButtonPressed()
        {
            if (Stack.CurrentScreen is EzSkinEditorScreen ezScreen)
            {
                ezScreen.ShowExitDialog();
                return;
            }

            if (Stack.CurrentScreen != null)
                Stack.Exit();
        }

        private void loadScreen()
        {
            LoadScreen(editorScreen = new EzSkinEditorScreen());
        }

        private void waitForScreenLoaded() => AddUntilStep("wait for screen load", () => editorScreen.IsLoaded);

        private void switchScene(EzSkinEditorSceneType scene) => AddStep($"switch to {scene}", () => editorScreen.CurrentScene.Value = scene);

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

            AddUntilStep("menu bar visible", () => editorScreen.ChildrenOfType<EditorMenuBar>().Any());
            AddUntilStep("scene bar visible", () => editorScreen.ChildrenOfType<EzSkinEditorSceneBar>().Any());
            AddUntilStep("sidebar visible", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Any());
            AddUntilStep("preview host visible", () => editorScreen.ChildrenOfType<EzSkinEditorPreviewHost>().Any());
            AddUntilStep("scene and sidebar are horizontal", () =>
            {
                var sidebar = editorScreen.ChildrenOfType<EzSkinEditorSidebar>().SingleOrDefault();
                var preview = editorScreen.ChildrenOfType<EzSkinEditorPreviewHost>().SingleOrDefault();

                return sidebar != null && preview != null
                                       && sidebar.ScreenSpaceDrawQuad.Width > 0
                                       && sidebar.ScreenSpaceDrawQuad.TopLeft.X > preview.ScreenSpaceDrawQuad.TopLeft.X;
            });
        }

        [Test]
        public void TestAppearanceSceneGroups()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Appearance);

            AddUntilStep("two sidebar groups", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() == 2);
            AddAssert("texture group present", () => editorScreen.ChildrenOfType<Dropdown<string>>().Any());
            AddAssert("no comparison placeholder", () => editorScreen.ChildrenOfType<OsuSpriteText>().All(t => t.Text.ToString().Contains("对比区") != true));
        }

        [Test]
        public void TestSizeSceneComparisonDrawables()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            AddAssert("mania preview provider registered", () => SkinEditorProviderRegistry.Get(3) != null);
            switchScene(EzSkinEditorSceneType.Size);

            AddUntilStep("comparison grid visible", () => editorScreen.ChildrenOfType<EzSkinEditorPreviewHost>().Single().ChildrenOfType<GridContainer>().Any());
            AddUntilStep("at least one sidebar group", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Any());
            AddAssert("no comparison placeholder", () => editorScreen.ChildrenOfType<OsuSpriteText>().All(t => t.Text.ToString().Contains("对比区") != true));
            AddAssert("comparison preview supported", () => editorScreen.ChildrenOfType<OsuSpriteText>().All(t => t.Text.ToString().Contains("Comparison preview not supported") != true));
            AddUntilStep("static note label visible", () => editorScreen.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == "Note"));
            AddUntilStep("static ln label visible", () => editorScreen.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == "LN"));
            AddUntilStep("playback and comparison are horizontal", () =>
            {
                var preview = editorScreen.ChildrenOfType<EzSkinEditorPreviewHost>().SingleOrDefault();
                var comparisonLabel = editorScreen.ChildrenOfType<OsuSpriteText>().FirstOrDefault(t => t.Text.ToString() == "LN");

                return preview != null
                       && comparisonLabel != null
                       && comparisonLabel.ScreenSpaceDrawQuad.Centre.X > preview.ScreenSpaceDrawQuad.Centre.X;
            });
        }

        [Test]
        public void TestColourSceneGroups()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Colour);

            AddUntilStep("two sidebar groups", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() == 2);
        }

        [Test]
        public void TestSkinIniScenePlaybackAndSaveFooter()
        {
            importLegacySkin();
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.SkinIni);

            AddUntilStep("playback only host", () => editorScreen.ChildrenOfType<EzSkinEditorPreviewHost>().Any());
            AddUntilStep("save footer button", () => editorScreen.ChildrenOfType<OsuButton>().Any(b => b.Text.ToString() == "保存 Skin.ini"));
            AddUntilStep("three sidebar groups", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() == 3);
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

            AddUntilStep("sidebar groups loaded", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() == 3);

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

            AddUntilStep("screen exited", () => Stack.CurrentScreen == null);
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

            AddUntilStep("screen exited", () => Stack.CurrentScreen == null);
            AddAssert("session clean after apply exit", () => !editorScreen.SkinIniSession!.IsDirty);
            AddAssert("saved text contains marker", () => editorScreen.SkinIniSession!.SavedText.Contains(marker, StringComparison.Ordinal));
        }

        [Test]
        public void TestSidebarCollapse()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            AddStep("unpin sidebar", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().Pinned.Value = false);
            AddStep("collapse sidebar", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().ExpandedState.Value = false);
            AddUntilStep("sidebar contracted", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().DrawWidth <= EzSkinEditorSidebar.CONTRACTED_WIDTH + 1);
        }

        [Test]
        public void TestTopToolbarControls()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            AddUntilStep("top toolbar visible", () => editorScreen.ChildrenOfType<EzSkinEditorTopToolbar>().Any());
            AddUntilStep("skin dropdown visible", () => editorScreen.ChildrenOfType<EzSkinEditorSkinDropdown>().Any());
            AddUntilStep("beatmap menu button visible", () => editorScreen.ChildrenOfType<EzSkinEditorBeatmapMenuButton>().Any());
            AddAssert("default static preview", () => editorScreen.PreviewSource.Value == EzSkinEditorPreviewSource.Static);
        }

        [Test]
        public void TestStaticPreviewAfterSkinSwitch()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            importLegacySkin();

            AddUntilStep("preview host visible", () => editorScreen.ChildrenOfType<EzSkinEditorPreviewHost>().Any());
            AddAssert("still static preview", () => editorScreen.PreviewSource.Value == EzSkinEditorPreviewSource.Static);
        }

        [Test]
        public void TestConfigMenuPresent()
        {
            importLegacySkin();
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();

            AddUntilStep("config menu items", () => editorScreen.ChildrenOfType<EditorMenuBar>().Any()
                                                    && editorScreen.ChildrenOfType<EditorMenuItem>().Any(i => i.Text.ToString() == "配置"));
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
    }
}
