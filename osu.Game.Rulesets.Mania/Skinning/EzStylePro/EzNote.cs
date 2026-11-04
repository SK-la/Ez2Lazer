// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzNote : EzNoteBase
    {
        protected override ManiaNoteDisplayColourPart DisplayColourPart => ManiaNoteDisplayColourPart.Note;

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
            noteDrawable = Factory.CreateAnimation(ColorPrefix + "note");

            if (noteDrawable == null)
                return;

            MainContainer.Child = noteDrawable;
        }

        protected override void UpdateDrawable()
        {
            Height = NoteHeight;
        }
    }
}
