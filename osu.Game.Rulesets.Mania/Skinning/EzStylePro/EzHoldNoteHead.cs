// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteHead : EzNoteBase
    {
        protected override bool UseColorization => true;
        protected override bool ShowSeparators => true;

        private Drawable? noteDrawable;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;
        }

        protected override void UpdateTexture()
        {
            noteDrawable = Factory.CreateAnimation(HeadName);

            if (noteDrawable == null)
            {
                noteDrawable = Factory.CreateAnimation(NoteName);

                if (noteDrawable == null)
                    return;

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
                    Child = noteDrawable,
                };
            }
            else
            {
                MainContainer.Child = noteDrawable;
            }
        }

        protected override void UpdateDrawable()
        {
            Height = NoteHeight;

            if (!MainContainer.Children.Any())
                return;

            if (MainContainer.Child is Container c)
            {
                MainContainer.Height = NoteHeight / 2;
                c.Height = NoteHeight;
            }
        }
    }
}
