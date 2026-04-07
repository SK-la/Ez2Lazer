// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Skinning.EzStylePro;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbIHoldNoteHeadPiece : EzNoteBase
    {
        protected override bool UseColorization => true;

        protected readonly IBindable<double> CornerRadiusBindable = new Bindable<double>();
        // private readonly IBindable<Colour4> columnColour = new Bindable<Colour4>();

        private const float corner = 5;

        private Container container = null!;

        public SbIHoldNoteHeadPiece()
        {
            RelativeSizeAxes = Axes.X;
            Masking = true;

            CornerRadius = corner;
        }

        [BackgroundDependencyLoader(true)]
        private void load(IEzSkinInfo ezSkinInfo)
        {
            UpdateTexture();
            CornerRadiusBindable.BindTo(ezSkinInfo.NoteCornerRadius);
            CornerRadiusBindable.BindValueChanged(_ => UpdateDrawable());
        }

        protected override void UpdateTexture()
        {
            if (MainContainer != null)
            {
                MainContainer.Clear();
                MainContainer.RelativeSizeAxes = Axes.X;
                MainContainer.Anchor = Anchor.BottomCentre;
                MainContainer.Origin = Anchor.BottomCentre;
                MainContainer.Masking = true;
                MainContainer.Child = container = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Masking = true,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                };
            }
        }

        protected override void UpdateDrawable()
        {
            float v = NoteSizeBindable.Value.Y;
            Height = v;

            if (MainContainer?.Children.Count > 0)
            {
                MainContainer.Height = v / 2;
                container.Height = v;
            }

            container.CornerRadius = (float)CornerRadiusBindable.Value;
        }
    }
}
