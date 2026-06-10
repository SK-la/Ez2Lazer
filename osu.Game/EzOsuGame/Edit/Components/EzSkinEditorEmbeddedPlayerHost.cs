// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Hosts the appearance-scene embedded player inside the scene content region.
    /// </summary>
    public partial class EzSkinEditorEmbeddedPlayerHost : Container
    {
        public EzSkinEditorEmbeddedPlayerHost(EzSkinEditorEmbeddedPlayer player)
        {
            RelativeSizeAxes = Axes.Both;
            Masking = true;
            Child = player;
        }
    }
}
