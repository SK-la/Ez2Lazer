// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Edit;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Mania;

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

        private void loadScreen()
        {
            LoadScreen(editorScreen = new EzSkinEditorScreen());
        }

        private void switchScene(EzSkinEditorSceneType scene) =>
            AddStep($"switch to {scene}", () => editorScreen.CurrentScene.Value = scene);

        [Test]
        public void TestLayoutShell()
        {
            AddStep("load screen", loadScreen);

            AddUntilStep("menu bar visible", () => editorScreen.ChildrenOfType<EzSkinEditorMenuBar>().Any());
            AddUntilStep("scene bar visible", () => editorScreen.ChildrenOfType<EzSkinEditorSceneBar>().Any());
            AddUntilStep("sidebar visible", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Any());
            AddUntilStep("preview host visible", () => editorScreen.ChildrenOfType<EzSkinEditorPreviewHost>().Any());
        }

        [Test]
        public void TestAppearanceSceneGroups()
        {
            AddStep("load screen", loadScreen);
            switchScene(EzSkinEditorSceneType.Appearance);

            AddUntilStep("two sidebar groups", () => editorScreen.ChildrenOfType<EzSkinEditorCollapsibleSection>().Count() == 2);
            AddAssert("texture group present", () => editorScreen.ChildrenOfType<Dropdown<string>>().Any());
        }

        [Test]
        public void TestSizeSceneGroups()
        {
            AddStep("load screen", loadScreen);
            switchScene(EzSkinEditorSceneType.Size);

            AddUntilStep("at least one sidebar group", () => editorScreen.ChildrenOfType<EzSkinEditorCollapsibleSection>().Any());
        }

        [Test]
        public void TestColourSceneGroups()
        {
            AddStep("load screen", loadScreen);
            switchScene(EzSkinEditorSceneType.Colour);

            AddUntilStep("two sidebar groups", () => editorScreen.ChildrenOfType<EzSkinEditorCollapsibleSection>().Count() == 2);
        }

        [Test]
        public void TestSkinIniScenePlaybackAndSaveFooter()
        {
            AddStep("load screen", loadScreen);
            switchScene(EzSkinEditorSceneType.SkinIni);

            AddUntilStep("playback only host", () => editorScreen.ChildrenOfType<EzSkinEditorPreviewHost>().Any());
            AddUntilStep("save footer button", () => editorScreen.ChildrenOfType<OsuButton>().Any(b => b.Text.ToString() == "保存 Skin.ini"));
        }

        [Test]
        public void TestSidebarCollapse()
        {
            AddStep("load screen", loadScreen);

            AddStep("collapse sidebar", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().ExpandedState.Value = false);
            AddAssert("sidebar contracted", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().DrawWidth <= EzSkinEditorSidebar.CONTRACTED_WIDTH + 1);
        }
    }
}
