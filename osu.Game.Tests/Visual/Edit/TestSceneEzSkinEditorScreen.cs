// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.EzOsuGame.Edit;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Mania;
using osu.Game.Screens.Edit.Components.Menus;

namespace osu.Game.Tests.Visual.Edit
{
    public partial class TestSceneEzSkinEditorScreen : ScreenTestScene
    {
        private EzSkinEditorScreen editorScreen = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Ruleset.Value = new ManiaRuleset().RulesetInfo;
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

        private void waitForScreenLoaded() =>
            AddUntilStep("wait for screen load", () => editorScreen.IsLoaded);

        private void switchScene(EzSkinEditorSceneType scene) =>
            AddStep($"switch to {scene}", () => editorScreen.CurrentScene.Value = scene);

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
        public void TestSizeSceneGroups()
        {
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.Size);

            AddUntilStep("at least one sidebar group", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Any());
            AddUntilStep("comparison area visible", () => editorScreen.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().Contains("对比区")));
            AddUntilStep("playback and comparison are horizontal", () =>
            {
                var preview = editorScreen.ChildrenOfType<EzSkinEditorPreviewHost>().SingleOrDefault();
                var comparisonText = editorScreen.ChildrenOfType<OsuSpriteText>()
                                                 .SingleOrDefault(t => t.Text.ToString().Contains("对比区"));

                return preview != null
                       && comparisonText != null
                       && comparisonText.ScreenSpaceDrawQuad.Centre.X > preview.ScreenSpaceDrawQuad.Centre.X;
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
            AddStep("load screen", loadScreen);
            waitForScreenLoaded();
            switchScene(EzSkinEditorSceneType.SkinIni);

            AddUntilStep("playback only host", () => editorScreen.ChildrenOfType<EzSkinEditorPreviewHost>().Any());
            AddUntilStep("save footer button", () => editorScreen.ChildrenOfType<OsuButton>().Any(b => b.Text.ToString() == "保存 Skin.ini"));
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
    }
}
