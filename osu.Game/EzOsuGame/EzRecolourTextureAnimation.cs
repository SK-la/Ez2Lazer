// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Sprites;

namespace osu.Game.EzOsuGame
{
    public partial class EzRecolourTextureAnimation : TextureAnimation
    {
        public EzRecolourTextureAnimation(bool startAtCurrentTime = true)
            : base(startAtCurrentTime)
        {
        }

        protected override Sprite CreateSprite() => new ColorizeSprite
        {
            Algorithm = ColorizeAlgorithm.Multiply,
        };
    }
}
