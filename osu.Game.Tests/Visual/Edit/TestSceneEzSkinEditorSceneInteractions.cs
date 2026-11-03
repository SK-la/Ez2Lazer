// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.EzMania.Editor;
using osu.Game.Skinning;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.Edit
{
    /// <summary>
    /// 按侧栏可见设置逐项改动，断言场景仍存活。
    /// </summary>
    public partial class TestSceneEzSkinEditorSceneInteractions : ScreenTestScene
    {
        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        private EzSkinEditorScreen editorScreen = null!;
        private EzSkinEditorSceneInteractionDriver? driver;

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("reset skin", () => skinManager.CurrentSkinInfo.SetDefault());
            AddStep("reset preview key mode", () => ezConfig.SetValue(Ez2Setting.ColumnTypeListSelect, 4));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Ruleset.Value = new ManiaRuleset().RulesetInfo;
            SkinEditorProviderRegistry.Register(3, () => new EzSkinLNEditorProvider());
        }

        [Test]
        public void TestAppearanceGroupInteractions()
        {
            runSidebarGroupInteraction<EzSkinEditorTextureSettingsSection>();
            AddAssert("workspace still present", () => editorScreen.IsLoaded);
            AddAssert("all appearance groups visible", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() >= 5);
        }

        [Test]
        public void TestSizeGroupInteractions()
        {
            runSidebarGroupInteraction<EzSkinEditorSizeSettingsSection>();
            AddAssert("size group present", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Any());
        }

        [Test]
        public void TestColourGroupInteractions()
        {
            runSidebarGroupInteraction<EzSkinEditorBaseColourSettingsSection>();
            AddAssert("all appearance groups visible", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() >= 5);
        }

        [Test]
        public void TestNoteSceneInteractions()
        {
            // Note 首页可改项较少，两页合计断言。
            runSceneInteraction(EzEditorSceneType.Note, minVisited: 1);
            AddStep("navigate to note edit group", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().SelectSidebarPage(1));
            cycleDriverSettings(minVisited: 1);
            AddAssert("workspace still present", () => editorScreen.IsLoaded);
            AddAssert("sidebar groups", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().PageCount == 2);
        }

        [Test]
        public void TestSkinIniSceneInteractions()
        {
            runSceneInteraction(EzEditorSceneType.SkinIni, importSkin: true);
            AddAssert("sidebar groups", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().PageCount == 3);
            AddAssert("workspace still present", () => editorScreen.IsLoaded);
            AddStep("discard draft", () => editorScreen.SkinIniSession.Discard());
        }

        private void loadWorkspace()
        {
            AddStep("load workspace", () =>
            {
                Child = editorScreen = new EzSkinEditorScreen
                {
                    RelativeSizeAxes = Framework.Graphics.Axes.Both,
                    RequestClose = () => editorScreen.Expire(),
                };
                editorScreen.Activate();
            });
            AddUntilStep("wait for workspace load", () => editorScreen.IsLoaded);
        }

        private void runSidebarGroupInteraction<T>() where T : EzSkinEditorSettingsGroup
        {
            driver = null;

            loadWorkspace();
            AddStep("switch to appearance", () => editorScreen.CurrentScene.Value = EzEditorSceneType.Appearance);
            AddStep($"select {typeof(T).Name}", () =>
            {
                var group = editorScreen.ChildrenOfType<T>().Single();
                editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().SelectSidebarGroup(group);
            });

            cycleDriverSettings();
        }

        private void runSceneInteraction(EzEditorSceneType scene, bool importSkin = false, int minVisited = 2)
        {
            if (importSkin)
                importLegacySkin();

            driver = null;

            loadWorkspace();
            AddStep($"switch to {scene}", () => editorScreen.CurrentScene.Value = scene);

            AddUntilStep("sidebar ready", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().PageCount > 0);
            AddStep("select first page", () => editorScreen.ChildrenOfType<EzSkinEditorSidebar>().Single().SelectSidebarPage(0));
            cycleDriverSettings(minVisited);
        }

        private void cycleDriverSettings(int minVisited = 2)
        {
            driver = null;

            AddUntilStep("driver has settings", () =>
            {
                driver ??= new EzSkinEditorSceneInteractionDriver(editorScreen);
                return driver.Count > 0;
            });

            AddUntilStep("cycle every setting", () =>
            {
                if (driver == null)
                    return false;

                if (driver.IsComplete)
                    return true;

                driver.CycleNext();
                return driver.IsComplete;
            });

            AddAssert("every setting was changed", () => driver!.Visited.Count, () => Is.GreaterThan(minVisited - 1));
            AddWaitStep("let preview settle", 2);
        }

        private void importLegacySkin() => AddStep("import legacy skin", () =>
        {
            var imported = skinManager.Import(new ImportTask(TestResources.OpenResource(@"Archives/modified-ezSkin.osk"), "modified-ezSkin.osk")).GetResultSafely();
            skinManager.CurrentSkinInfo.Value = imported;
        });
    }
}
