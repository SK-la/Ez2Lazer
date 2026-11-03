// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osuTK.Input;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneEzModSelectionRestore : SongSelectTestScene
    {
        [Test]
        public void TestClearRestoreViaShortcutOnSongSelect()
        {
            LoadSongSelect();

            var doubleTime = new OsuModDoubleTime();
            doubleTime.SpeedChange.Value = 1.2;

            AddStep("select DT", () => SelectedMods.Value = new[] { doubleTime });
            AddAssert("mod selected", () => SelectedMods.Value.Single(), () => Is.TypeOf<OsuModDoubleTime>());

            pressModClearRestoreShortcut();
            AddAssert("cleared", () => !SelectedMods.Value.Any());

            pressModClearRestoreShortcut();
            AddAssert("restored", () => SelectedMods.Value.Single(), () => Is.TypeOf<OsuModDoubleTime>());
            AddAssert("settings preserved", () => ((OsuModDoubleTime)SelectedMods.Value.Single()).SpeedChange.Value, () => Is.EqualTo(1.2).Within(0.005));
        }

        [Test]
        public void TestClearRestoreViaShortcutInModOverlay()
        {
            LoadSongSelect();

            var doubleTime = new OsuModDoubleTime();
            doubleTime.SpeedChange.Value = 1.15;

            AddStep("select DT", () => SelectedMods.Value = new[] { doubleTime });
            AddStep("open mod overlay", () => InputManager.Key(Key.F1));
            AddUntilStep("overlay visible", () => ModSelectOverlay.State.Value == Visibility.Visible);

            pressModClearRestoreShortcut();
            AddAssert("cleared in overlay", () => !SelectedMods.Value.Any());

            pressModClearRestoreShortcut();
            AddAssert("restored in overlay", () => SelectedMods.Value.Single(), () => Is.TypeOf<OsuModDoubleTime>());
            AddAssert("settings preserved in overlay", () => ((OsuModDoubleTime)SelectedMods.Value.Single()).SpeedChange.Value, () => Is.EqualTo(1.15).Within(0.005));
        }

        [Test]
        public void TestClearRestoreViaShortcutInModOverlayWithSearchFocused()
        {
            LoadSongSelect();

            AddStep("select HD", () => SelectedMods.Value = new[] { new OsuModHidden() });
            AddStep("open mod overlay", () => InputManager.Key(Key.F1));
            AddUntilStep("overlay visible", () => ModSelectOverlay.State.Value == Visibility.Visible);
            AddStep("focus search", () => ModSelectOverlay.SearchTextBox.TakeFocus());

            pressModClearRestoreShortcut();
            AddAssert("cleared with search focused", () => !SelectedMods.Value.Any());

            pressModClearRestoreShortcut();
            AddAssert("restored with search focused", () => SelectedMods.Value.Single(), () => Is.TypeOf<OsuModHidden>());
        }

        private ModSelectOverlay ModSelectOverlay => this.ChildrenOfType<EzUserModSelectOverlay>().Single();

        private void pressModClearRestoreShortcut() =>
            AddStep("press LAlt+1", () =>
            {
                InputManager.PressKey(Key.LAlt);
                InputManager.Key(Key.Number1);
                InputManager.ReleaseKey(Key.LAlt);
            });
    }
}
