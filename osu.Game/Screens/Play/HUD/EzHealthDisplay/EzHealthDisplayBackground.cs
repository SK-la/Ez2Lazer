// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Containers;
using osu.Game.LAsEzExtensions;

namespace osu.Game.Screens.Play.HUD.EzHealthDisplay
{
    public partial class EzHealthDisplayBackground : Container
    {
        public EzHealthDisplayBackground(EzLocalTextureFactory textureFactory, string textureName)
        {
            var textureAnimation = textureFactory.CreateAnimation(textureName);

            Add(textureAnimation);
        }
    }
}
