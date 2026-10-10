// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Video;
using osu.Game.EzOsuGame.Screens.Menu;

namespace osu.Game.EzOsuGame.Background
{
    public abstract partial class EzVideoBackground : osu.Game.Graphics.Backgrounds.Background
    {
        private Video? video;

        [Resolved(canBeNull: true)]
        private EzLogoBackdropSampler? logoBackdropSampler { get; set; }

        protected void UseVideo(Video video) => this.video = video;

        protected override void Update()
        {
            base.Update();

            if (Alpha < 0.5f || video == null || logoBackdropSampler == null)
                return;

            if (video.TryGetBackdropSample(out Colour4 colour))
                logoBackdropSampler.Offer(colour);
        }
    }
}
