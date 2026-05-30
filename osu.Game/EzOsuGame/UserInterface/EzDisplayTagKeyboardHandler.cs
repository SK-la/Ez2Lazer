// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osuTK.Input;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// 在选歌界面统一监听 LAlt 按下/松开，避免每个难度卡 tag 组件轮询或注册按键输入。
    /// </summary>
    public partial class EzDisplayTagKeyboardHandler : Component
    {
        public override bool HandleNonPositionalInput => true;

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.LAlt && !e.Repeat)
                EzDisplayTagAltHighlight.SetActive(true);

            return false;
        }

        protected override void OnKeyUp(KeyUpEvent e)
        {
            if (e.Key == Key.LAlt)
                EzDisplayTagAltHighlight.SetActive(false);
        }
    }
}
