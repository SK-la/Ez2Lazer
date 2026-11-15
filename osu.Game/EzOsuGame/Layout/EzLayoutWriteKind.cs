// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Which persistence root a HUD apply actually wrote. Skin JSON, theme layout folders, and
    /// client <c>Config/</c> files are independent; the editor toast must name the ones that changed.
    /// </summary>
    [Flags]
    public enum EzLayoutWriteKind
    {
        None = 0,
        Skin = 1,
        Theme = 2,
        Client = 4,
    }
}
