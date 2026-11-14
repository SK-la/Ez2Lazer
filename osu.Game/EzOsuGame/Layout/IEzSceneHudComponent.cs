// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// HUD components that belong to one <see cref="GlobalSkinnableContainers"/> scene.
    /// The official skin toolbox skips these unless the active target matches <see cref="LayoutTarget"/>.
    /// </summary>
    public interface IEzSceneHudComponent
    {
        GlobalSkinnableContainers LayoutTarget { get; }
    }
}
