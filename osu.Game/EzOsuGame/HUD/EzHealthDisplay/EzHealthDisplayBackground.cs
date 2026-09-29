// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.EzOsuGame.HUD.EzHealthDisplay
{
    public partial class EzHealthDisplayBackground : Container
    {
        public EzHealthDisplayBackground(EzLocalTextureFactory textureFactory, string textureName)
        {
            Drawable? texture = textureFactory.CreateAnimation(textureName);

            if (texture != null)
                Add(texture);
        }
    }
}
