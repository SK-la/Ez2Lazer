// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzNote : EzNoteBase
    {
        protected override bool UseColorization => true;
        protected override bool ShowSeparators => true;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;
        }

        protected override void UpdateTexture()
        {
            var animation = Factory.CreateAnimation($"{ColorPrefix}note");

            if (animation is TextureAnimation textureAnimation && textureAnimation.FrameCount == 0)
            {
                animation.Dispose();
                return;
            }

            if (MainContainer != null)
            {
                MainContainer.Clear();
                MainContainer.Child = animation;
            }

            UpdateColor();
        }

        protected override void UpdateDrawable()
        {
            float v = NoteSizeBindable.Value.Y;
            Height = v;
        }
    }
}
