// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Textures;
using osu.Game.EzOsuGame.Screens.Menu;

namespace osu.Game.Graphics.Backgrounds
{
    public partial class Background
    {
        [Resolved(canBeNull: true)]
        private EzLogoBackdropSampler? logoBackdropSampler { get; set; }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            offerLogoBackdropSample();
        }

        private void offerLogoBackdropSample()
        {
            Texture? texture = Sprite.Texture;

            if (texture == null || !texture.HasBackdropSample)
                return;

            logoBackdropSampler?.Offer(texture.BackdropSample);
        }
    }
}
