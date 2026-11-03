// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbINotePiece : FastNoteBase
    {
        protected Container Container = null!;
        protected Box Note = null!;

        public SbINotePiece()
        {
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            MainContainer.RelativeSizeAxes = Axes.X;
            MainContainer.Masking = true;
            MainContainer.Child = Container = new Container
            {
                RelativeSizeAxes = Axes.X,
                Masking = true,
                Child = Note = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                }
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
            float height = UnitHeight;

            Height = height;
            MainContainer.Height = height;
            Container.Height = height;
            Container.CornerRadius = radius;
        }

        protected override void UpdateColor()
        {
            Note.Colour = NoteColor;
        }
    }
}
