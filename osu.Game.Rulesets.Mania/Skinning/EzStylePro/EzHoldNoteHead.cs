// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    internal partial class EzHoldNoteHead : EzNoteBase
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
            animation = Factory.CreateAnimation(HeadName);

            if (animation.FrameCount == 0)
            {
                animation.Dispose();
                animation = Factory.CreateAnimation(NoteName);

                if (animation.FrameCount == 0)
                {
                    animation.Dispose();
                    return;
                }

                MainContainer.Anchor = Anchor.BottomCentre;
                MainContainer.Origin = Anchor.BottomCentre;
                MainContainer.RelativeSizeAxes = Axes.X;
                MainContainer.Masking = true;
                MainContainer.Child = new Container
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.X,
                    Masking = true,
                    Child = animation,
                };
            }
            else
            {
                MainContainer.Child = animation;
            }
        }

        protected override void UpdateDrawable()
        {
            Height = NoteHeight;

            if (MainContainer.Child is Container c)
            {
                MainContainer.Height = NoteHeight / 2;
                c.Height = NoteHeight;
            }
        }
    }
}
