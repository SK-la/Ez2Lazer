// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// 选歌界面 LAlt 按住时，由 <see cref="EzSongSelectKeyboardHandler"/> 更新；各 <see cref="EzDisplayTag"/> 订阅以切换高亮。
    /// </summary>
    internal static class EzDisplayTagAltHighlight
    {
        public static bool Active { get; private set; }

        public static event Action<bool>? ActiveChanged;

        public static void SetActive(bool active)
        {
            if (Active == active)
                return;

            Active = active;
            ActiveChanged?.Invoke(active);
        }

        public static void Reset() => SetActive(false);
    }
}
