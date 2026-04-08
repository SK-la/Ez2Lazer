// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbINotePiece : FastNoteBase
    {
        public SbINotePiece()
        {
            RelativeSizeAxes = Axes.X;
        }

        protected override void UpdateLoad()
        {
            MainContainer.Clear();
            MainContainer.Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
            };
        }

        protected override void UpdateDrawable()
        {
            if (DrawWidth <= 1)
            {
                Schedule(UpdateDrawable);
                return;
            }

            float radius = (float)CornerRadiusBindable.Value;

            Height = UnitHeight;
            Masking = true;
            CornerRadius = radius;
        }

        protected override void UpdateColor()
        {
            MainContainer.Colour = NoteColor;
        }
    }
}
