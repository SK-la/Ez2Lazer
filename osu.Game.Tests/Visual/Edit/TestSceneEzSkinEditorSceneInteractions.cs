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
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.EzMania.Editor;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.Edit
{
    /// <summary>
    /// Walks every setting of every skin editor scene in sidebar order and moves each one, asserting that the scene survives.
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
        public void TestAppearanceSceneInteractions()
        {
            runSceneInteraction(EzSkinEditorSceneType.Appearance);
            AddAssert("appearance scene still present", () => editorScreen.ChildrenOfType<EzSkinEditorAppearanceSceneContent>().Any());
            AddAssert("three sidebar groups", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() == 3);
        }

        [Test]
        public void TestSizeSceneInteractions()
        {
            runSceneInteraction(EzSkinEditorSceneType.Size);
            AddUntilStep("comparison panes still present", () => editorScreen.ChildrenOfType<EzSkinEditorNoteComparisonPane>().Count() == 2);
            AddAssert("sidebar group present", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Any());
        }

        [Test]
        public void TestColourSceneInteractions()
        {
            runSceneInteraction(EzSkinEditorSceneType.Colour);
            AddUntilStep("virtual playfield still present", () => editorScreen.ChildrenOfType<Column>().Any());
            AddAssert("two sidebar groups", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() == 2);
        }

        [Test]
        public void TestNoteSceneInteractions()
        {
            runSceneInteraction(EzSkinEditorSceneType.Note);
            AddUntilStep("note comparison host still present", () => editorScreen.ChildrenOfType<EzSkinEditorNoteComparisonHost>().Any());
            AddAssert("two sidebar groups", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() == 2);
        }

        [Test]
        public void TestSkinIniSceneInteractions()
        {
            runSceneInteraction(EzSkinEditorSceneType.SkinIni, importSkin: true);
            AddAssert("three sidebar groups", () => editorScreen.ChildrenOfType<EzSkinEditorSettingsGroup>().Count() == 3);
            AddAssert("preview host still present", () => editorScreen.ChildrenOfType<EzSkinEditorPreviewHost>().Any());
            AddStep("discard draft", () => editorScreen.SkinIniSession?.Discard());
        }

        private void runSceneInteraction(EzSkinEditorSceneType scene, bool importSkin = false)
        {
            if (importSkin)
                importLegacySkin();

            driver = null;

            AddStep("load screen", () => LoadScreen(editorScreen = new EzSkinEditorScreen()));
            AddUntilStep("wait for screen load", () => editorScreen.IsLoaded);
            AddStep($"switch to {scene}", () => editorScreen.CurrentScene.Value = scene);

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

            AddAssert("every setting was changed", () => driver!.Visited.Count, () => Is.GreaterThan(2));
            AddWaitStep("let preview settle", 2);
        }

        private void importLegacySkin() => AddStep("import legacy skin", () =>
        {
            var imported = skinManager.Import(new ImportTask(TestResources.OpenResource(@"Archives/modified-ezSkin.osk"), "modified-ezSkin.osk")).GetResultSafely();
            skinManager.CurrentSkinInfo.Value = imported;
        });
    }
}
