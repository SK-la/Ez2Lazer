// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteHead : EzNoteBase
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
            string newComponentName = $"{ColorPrefix}longnote/head";

            var animation = Factory.CreateAnimation(newComponentName);

            if (animation is TextureAnimation textureAnimation && textureAnimation.FrameCount == 0)
            {
                animation.Dispose();
                animation = Factory.CreateAnimation($"{ColorPrefix}note");

                if (animation is TextureAnimation newTexture && newTexture.FrameCount == 0)
                {
                    animation.Dispose();
                    return;
                }

                if (MainContainer != null)
                {
                    MainContainer.Clear();
                    MainContainer.RelativeSizeAxes = Axes.X;
                    MainContainer.Anchor = Anchor.BottomCentre;
                    MainContainer.Origin = Anchor.BottomCentre;
                    MainContainer.Masking = true;
                    MainContainer.Child = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Child = animation,
                    };
                }
            }
            else
            {
                if (MainContainer != null)
                {
                    MainContainer.Clear();
                    MainContainer.Child = animation;
                }
            }

            Schedule(UpdateSize);
        }

        protected override void UpdateSize()
        {
            base.UpdateSize();
            float v = NoteSize.Value.Y;
            Height = v;

            if (MainContainer?.Children.Count > 0 && MainContainer.Child is Container c)
            {
                MainContainer.Height = v / 2;
                c.Height = v;
            }
        }
    }
}
