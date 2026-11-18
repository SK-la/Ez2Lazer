// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Mods;
using osu.Game.Tests.Visual.SongSelect;
using osuTK.Input;

namespace osu.Game.Tests.Visual.EzOsuGame
{
    public partial class TestSceneEzModVerticalLayoutToggle : SongSelectTestScene
    {
        [Test]
        public void TestToggleVerticalLayoutInEditMode()
        {
            LoadSongSelect();

            AddStep("open mod overlay", () => InputManager.Key(Key.F1));
            AddUntilStep("overlay visible", () => modOverlay.State.Value == Visibility.Visible);
            AddUntilStep("columns loaded", () => modOverlay.ChildrenOfType<ModColumn>().Any(c => c.IsLoaded && c.ItemsLoaded));

            enterEditMode();
            AddUntilStep("shear control visible", () => modOverlay.ChildrenOfType<EzModLayoutShearControl>().Single().Alpha > 0);
            AddUntilStep("hidden pool visible", () => modOverlay.ChildrenOfType<EzModHiddenPool>().Single().Alpha > 0.9f);
        }

        [Test]
        public void TestToggleVerticalLayoutViaClick()
        {
            LoadSongSelect();

            AddStep("open mod overlay", () => InputManager.Key(Key.F1));
            AddUntilStep("overlay visible", () => modOverlay.State.Value == Visibility.Visible);
            AddUntilStep("columns loaded", () => modOverlay.ChildrenOfType<ModColumn>().Any(c => c.IsLoaded && c.ItemsLoaded));

            enterEditMode();
            AddUntilStep("shear control visible", () => modOverlay.ChildrenOfType<EzModLayoutShearControl>().Single().Alpha > 0);
            AddUntilStep("hidden pool visible", () => modOverlay.ChildrenOfType<EzModHiddenPool>().Single().Alpha > 0.9f);
        }

        private EzUserModSelectOverlay modOverlay => this.ChildrenOfType<EzUserModSelectOverlay>().Single();

        private void enterEditMode()
        {
            AddStep("enter edit mode", () =>
            {
                var editButton = modOverlay.ChildrenOfType<ShearedButton>()
                                           .First(b => b.GetType() == typeof(ShearedButton) && b.Alpha > 0);

                InputManager.MoveMouseTo(editButton);
                InputManager.Click(MouseButton.Left);
            });
        }
    }
}
