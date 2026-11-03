// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// 每列按键的按下状态。输入仍由列自己处理，舞台板上的按键贴图只读这份状态。
    /// </summary>
    public class EzStageKeyState
    {
        private readonly Bindable<bool>[] pressed;

        public EzStageKeyState(int columnCount)
        {
            pressed = new Bindable<bool>[columnCount < 0 ? 0 : columnCount];

            for (int i = 0; i < pressed.Length; i++)
                pressed[i] = new Bindable<bool>();
        }

        public IReadOnlyList<Bindable<bool>> Pressed => pressed;

        public IBindable<bool>? GetPressed(int index)
            => index >= 0 && index < pressed.Length ? pressed[index] : null;
    }
}
