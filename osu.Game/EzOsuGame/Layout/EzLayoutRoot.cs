// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Which Game-cached layout directory owns a scene's HUD JSON files.
    /// </summary>
    public enum EzLayoutRoot
    {
        /// <summary>Switchable theme folder under <see cref="EzModifyPath.CONFIG_ROOT_PATH"/> (default <c>layout</c>).</summary>
        Theme,

        /// <summary>Fixed <see cref="EzModifyPath.CONFIG_ROOT_PATH"/>; not affected by theme <c>UseSet</c>.</summary>
        Client,
    }
}
