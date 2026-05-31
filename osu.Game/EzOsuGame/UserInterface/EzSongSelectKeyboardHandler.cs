// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Mods;
using osuTK.Input;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// 选歌界面统一键盘处理：LAlt tag 高亮、LAlt+1 mod 清空/恢复。
    /// </summary>
    public partial class EzSongSelectKeyboardHandler : Component
    {
        private readonly EzModSelectionRestoreController restoreController;
        private readonly Bindable<IReadOnlyList<Mod>> mods;
        private readonly ModSelectOverlay modSelectOverlay;
        private readonly bool modRestoreOnOverlayOpen;
        private readonly bool handleTagAltHighlight;

        public static EzSongSelectKeyboardHandler ForSongSelect(
            EzModSelectionRestoreController restoreController,
            Bindable<IReadOnlyList<Mod>> mods,
            ModSelectOverlay modSelectOverlay)
            => new(restoreController, mods, modSelectOverlay, modRestoreOnOverlayOpen: false, handleTagAltHighlight: true);

        public static EzSongSelectKeyboardHandler ForModOverlay(
            EzModSelectionRestoreController restoreController,
            Bindable<IReadOnlyList<Mod>> mods,
            ModSelectOverlay modSelectOverlay)
            => new(restoreController, mods, modSelectOverlay, modRestoreOnOverlayOpen: true, handleTagAltHighlight: false);

        private EzSongSelectKeyboardHandler(
            EzModSelectionRestoreController restoreController,
            Bindable<IReadOnlyList<Mod>> mods,
            ModSelectOverlay modSelectOverlay,
            bool modRestoreOnOverlayOpen,
            bool handleTagAltHighlight)
        {
            this.restoreController = restoreController;
            this.mods = mods;
            this.modSelectOverlay = modSelectOverlay;
            this.modRestoreOnOverlayOpen = modRestoreOnOverlayOpen;
            this.handleTagAltHighlight = handleTagAltHighlight;
        }

        public override bool HandleNonPositionalInput => true;

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.LAlt && !e.Repeat && handleTagAltHighlight)
                EzDisplayTagAltHighlight.SetActive(true);

            if (!e.Repeat && e.Key == Key.Number1 && e.IsPressed(Key.LAlt) && canHandleModRestoreShortcut())
            {
                restoreController.ClearOrRestore(mods, modSelectOverlay);
                return true;
            }

            return false;
        }

        protected override void OnKeyUp(KeyUpEvent e)
        {
            if (e.Key == Key.LAlt && handleTagAltHighlight)
                EzDisplayTagAltHighlight.SetActive(false);
        }

        private bool canHandleModRestoreShortcut()
        {
            bool overlayVisible = modSelectOverlay.State.Value == Visibility.Visible;
            return modRestoreOnOverlayOpen ? overlayVisible : !overlayVisible;
        }
    }
}
