// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzNote : EzNoteBase
    {
        protected override bool ShowSeparators => true;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;
        }

        protected override void OnDrawableChanged()
        {
            var animation = Factory.CreateAnimation($"{ColorPrefix}note");

            if (animation is TextureAnimation textureAnimation && textureAnimation.FrameCount == 0)
            {
                animation.Dispose();
                Schedule(UpdateColor);
                return;
            }

            if (MainContainer != null)
            {
                MainContainer.Clear();
                MainContainer.Child = animation;
            }

            UpdateSize();
        }

        protected override void UpdateSize()
        {
            base.UpdateSize();
            float v = NoteSize.Value.Y;
            Height = v;
        }
    }
}
