// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// 选歌界面 mod 清空/恢复切换：清空前保存快照（含 mod 配置），再次操作可恢复。
    /// </summary>
    public class EzModSelectionRestoreController
    {
        private Mod[]? savedMods;

        public event Action? StateChanged;

        public bool CanRestore => savedMods != null && savedMods.Length > 0;

        public bool CanRestoreNow(IReadOnlyList<Mod> currentMods) => CanRestore && !hasNonSystemMods(currentMods);

        public void Reset()
        {
            savedMods = null;
            StateChanged?.Invoke();
        }

        public void ClearOrRestore(Bindable<IReadOnlyList<Mod>> mods, ModSelectOverlay overlay)
        {
            if (CanRestoreNow(mods.Value))
            {
                mods.Value = savedMods!.Select(m => m.DeepClone()).ToArray();
                StateChanged?.Invoke();
                return;
            }

            saveCurrent(mods.Value);

            if (overlay.State.Value == Visibility.Visible)
                overlay.DeselectAll();
            else
                mods.Value = Array.Empty<Mod>();

            StateChanged?.Invoke();
        }

        private void saveCurrent(IReadOnlyList<Mod> currentMods)
        {
            var nonSystemMods = currentMods.Where(m => m.Type != ModType.System).ToArray();

            if (nonSystemMods.Length > 0)
                savedMods = nonSystemMods.Select(m => m.DeepClone()).ToArray();
        }

        private static bool hasNonSystemMods(IReadOnlyList<Mod> mods) => mods.Any(m => m.Type != ModType.System);
    }
}
