// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    internal partial class EzNote : EzNoteBase
    {
        protected override bool UseColorization => true;
        protected override bool ShowSeparators => true;

        private TextureAnimation? animation;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;
        }

        protected override void UpdateTexture()
        {
            animation = Factory.CreateAnimation(ColorPrefix + "note");

            if (animation.FrameCount == 0)
            {
                animation = null;
                return;
            }

            MainContainer.Child = animation;
        }

        protected override void UpdateDrawable()
        {
            Height = NoteHeight;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            animation = null;
        }
    }
}
