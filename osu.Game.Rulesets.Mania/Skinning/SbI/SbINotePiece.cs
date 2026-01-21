// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.Skinning.EzStylePro;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbINotePiece : EzNoteBase
    {
        public Bindable<double> NoteAccentRatio = new Bindable<double>(1f);
        public Bindable<double> NoteHeight = new Bindable<double>(8);
        public Bindable<double> CORNER_RADIUS = new Bindable<double>(0);

        private readonly IBindable<Color4> accentColour = new Bindable<Color4>();

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
            CornerRadius = (float)CORNER_RADIUS.Value;

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
                        // CornerRadius = CORNER_RADIUS,
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

            if (drawableObject != null)
            {
                accentColour.BindTo(drawableObject.AccentColour);
                accentColour.BindValueChanged(onAccentChanged, true);
            }

            NoteAccentRatio = EzSkinConfig.GetBindable<double>(Ez2Setting.NoteHeightScaleToWidth);
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

        private void onAccentChanged(ValueChangedEvent<Color4> accent)
        {
            colouredBox.Colour = ColourInfo.GradientVertical(
                accent.NewValue.Lighten(0.1f),
                accent.NewValue
            );
        }
    }
}
