// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Skinning.EzStylePro;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbINotePiece : EzNoteBase
    {
        public Bindable<double> CornerRadiusBindable = new Bindable<double>();

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
        private void load(IEzSkinInfo ezSkinInfo)
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

            columnColour.BindTo(Column.EzNoteColourBindable);
            columnColour.BindValueChanged(onAccentChanged, true);
            UpdateDrawable();
        }

        // protected override void LoadComplete()
        // {
        //     base.LoadComplete();
        //
        //     NoteAccentRatio = Column.EzSkinInfo.NoteHeightScaleToWidth;
        //     UpdateSize();
        // }

        protected override void UpdateDrawable()
        {
            Height = NoteSizeBindable.Value.Y;
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
