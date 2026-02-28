// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Mania.Skinning.EzStylePro;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbINotePiece : EzNoteBase
    {
        public IBindable<double> NoteAccentRatio = new Bindable<double>(1f);
        public Bindable<double> NoteHeight = new Bindable<double>(8);
        public Bindable<double> CornerRadiusBindable = new Bindable<double>(0);

        private readonly IBindable<Colour4> columnColour = new Bindable<Colour4>();

        private Box colouredBox = null!;

        public SbINotePiece()
        {
            RelativeSizeAxes = Axes.X;

            // Masking = true;
        }

        // protected override void Update()
        // {
        //     base.Update();
        //
        //     // CreateIcon().Size = new Vector2(DrawWidth / 43 * 0.7f);
        // }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject? drawableObject)
        {
            CornerRadius = (float)CornerRadiusBindable.Value;

            if (MainContainer != null)
            {
                MainContainer.Children = new[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            // BorderColour = Color4.White.Opacity(1f),
                            // BorderColour = ColourInfo.GradientVertical(Color4.White.Opacity(0), Colour4.Black),
                        }
                    },
                    new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        // Masking = true,
                        // CornerRadius = CornerRadiusBindable,
                        Children = new Drawable[]
                        {
                            colouredBox = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                            }
                        }
                    },
                };
            }

            columnColour.BindTo(Column.EzColumnColourBindable);
            columnColour.BindValueChanged(onAccentChanged, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            NoteAccentRatio = Column.EzSkinInfo.NoteHeightScaleToWidth;
            UpdateSize();
        }

        protected override void UpdateSize()
        {
            base.UpdateSize();

            float fixedA = NoteAccentRatio.Value > 5
                ? (float)NoteAccentRatio.Value * 1.5f
                : NoteAccentRatio.Value > 2
                    ? (float)NoteAccentRatio.Value * 1.5f
                    : (float)NoteAccentRatio.Value;

            Height = (float)NoteHeight.Value * fixedA;
        }

        private void onAccentChanged(ValueChangedEvent<Colour4> accent)
        {
            var c = accent.NewValue;
            var color = new Color4(c.R, c.G, c.B, c.A);
            colouredBox.Colour = ColourInfo.GradientVertical(
                color.Lighten(0.1f),
                color
            );
        }
    }
}
