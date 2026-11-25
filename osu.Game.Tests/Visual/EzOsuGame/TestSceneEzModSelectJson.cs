// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Mods;
using osu.Game.Tests.Visual.SongSelect;
using osuTK.Input;

namespace osu.Game.Tests.Visual.EzOsuGame
{
    public partial class TestSceneEzModSelectJson : SongSelectTestScene
    {
        [Test]
        public void TestEditModeShowsModLayoutControls()
        {
            LoadSongSelect();

            AddStep("open mod overlay", () => InputManager.Key(Key.F1));
            AddUntilStep("overlay visible", () => modOverlay.State.Value == Visibility.Visible);
            AddUntilStep("columns loaded", () => modOverlay.ChildrenOfType<ModColumn>().Any(c => c.IsLoaded && c.ItemsLoaded));

            enterEditMode();

            AddUntilStep("shear control visible", () => shearControl.Alpha > 0);
            AddUntilStep("hidden pool visible", () => hiddenPool.Alpha > 0.9f);
            AddUntilStep("new group visible", () => newGroupButton.Alpha > 0);
        }

        [Test]
        public void TestExitEditModeHidesModLayoutControls()
        {
            LoadSongSelect();

            AddStep("open mod overlay", () => InputManager.Key(Key.F1));
            AddUntilStep("overlay visible", () => modOverlay.State.Value == Visibility.Visible);
            AddUntilStep("columns loaded", () => modOverlay.ChildrenOfType<ModColumn>().Any(c => c.IsLoaded && c.ItemsLoaded));

            enterEditMode();
            AddUntilStep("editing", () => shearControl.Alpha > 0);

            AddStep("done editing", clickEditToggle);
            AddUntilStep("shear hidden", () => shearControl.Alpha == 0);
            AddUntilStep("new group hidden", () => newGroupButton.Alpha == 0);
        }

        private EzUserModSelectOverlay modOverlay => this.ChildrenOfType<EzUserModSelectOverlay>().Single();

        private EzLayoutShearControl shearControl => modOverlay.ChildrenOfType<EzLayoutShearControl>().Single();

        private EzModHiddenPool hiddenPool => modOverlay.ChildrenOfType<EzModHiddenPool>().Single();

        private ShearedButton newGroupButton => modOverlay.ChildrenOfType<ShearedButton>()
                                                          .First(b => b.Text.ToString() == EzModLayoutStrings.NEW_GROUP.ToString());

        private ShearedButton editToggleButton => modOverlay.ChildrenOfType<FillFlowContainer>()
                                                            .Single(flow => flow.ChildrenOfType<EzLayoutShearControl>().Any())
                                                            .ChildrenOfType<ShearedButton>().First();

        private void enterEditMode() => AddStep("enter edit mode", clickEditToggle);

        private void clickEditToggle()
        {
            InputManager.MoveMouseTo(editToggleButton);
            InputManager.Click(MouseButton.Left);
        }
    }
}
